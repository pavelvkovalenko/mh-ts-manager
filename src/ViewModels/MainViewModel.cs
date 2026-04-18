using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MhTsManager.Models;
using MhTsManager.Services;

namespace MhTsManager.ViewModels
{

/// <summary>
/// Главный ViewModel приложения.
/// Содержит коллекцию сессий, автообновление и команды toolbar/меню.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IWtsSessionService _wtsService;
    private readonly ISessionAppStateService _appStateService;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IWindowEnumeratorService _windowEnumerator;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly Logger _logger;

    private CancellationTokenSource? _refreshCts;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(5);

    [ObservableProperty]
    private ObservableCollection<SessionViewModel> _sessions = new();

    [ObservableProperty]
    private SessionViewModel? _selectedSession;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private string _usersTitle = string.Empty;

    [ObservableProperty]
    private string _adminModeIcon = "\uD83D\uDD12"; // 🔒

    [ObservableProperty]
    private string _adminModeTooltip = string.Empty;

    [ObservableProperty]
    private bool _isElevated;

    public MainViewModel(
        IWtsSessionService wtsService,
        ISessionAppStateService appStateService,
        ICommandExecutor commandExecutor,
        IWindowEnumeratorService windowEnumerator,
        ILocalizationService localizationService,
        ISettingsService settingsService,
        Logger logger)
    {
        _wtsService = wtsService;
        _appStateService = appStateService;
        _commandExecutor = commandExecutor;
        _windowEnumerator = windowEnumerator;
        _localizationService = localizationService;
        _settingsService = settingsService;
        _logger = logger;

        _logger.Info("=== MAINVIEWMODEL CONSTRUCTOR BEGIN ===");
        _logger.Info("Services injected successfully");
        
        InitializeDisplayValues();
        _logger.Info("InitializeDisplayValues completed");
        _logger.Info("MainViewModel constructor completed successfully");
    }

    /// <summary>
    /// Инициализация отображаемых значений (заголовки, локализация).
    /// </summary>
    private void InitializeDisplayValues()
    {
        _logger.Info("InitializeDisplayValues: Starting...");
        WindowTitle = _localizationService.GetString("App.WindowTitle");
        _logger.Info("WindowTitle set to: {0}", WindowTitle);
        UsersTitle = _localizationService.GetString("Toolbar.UsersTitle");
        _logger.Info("UsersTitle set to: {0}", UsersTitle);
        AdminModeTooltip = _localizationService.GetString("Toolbar.AdminMode.Locked");
        _logger.Info("AdminModeTooltip (initial) set to: {0}", AdminModeTooltip);
        IsElevated = _wtsService.IsElevated;
        _logger.Info("IsElevated: {0}", IsElevated);
        AdminModeIcon = IsElevated ? "\uD83D\uDD13" : "\uD83D\uDD12"; // 🔓 или 🔒
        _logger.Info("AdminModeIcon set to: {0}", AdminModeIcon);
        AdminModeTooltip = IsElevated
            ? _localizationService.GetString("Toolbar.AdminMode.Active")
            : _localizationService.GetString("Toolbar.AdminMode.Locked");
        _logger.Info("AdminModeTooltip (final) set to: {0}", AdminModeTooltip);
        _logger.Info("InitializeDisplayValues: Complete");
    }

    /// <summary>
    /// Запустить цикл автообновления сессий.
    /// </summary>
    public async Task StartRefreshLoopAsync()
    {
        _logger.Info("StartRefreshLoopAsync: Starting...");
        _refreshCts = new CancellationTokenSource();
        _logger.Info("StartRefreshLoopAsync: Calling initial RefreshSessionsAsync...");
        await RefreshSessionsAsync(); // Первоначальная загрузка

        _logger.Info("StartRefreshLoopAsync: Starting background refresh task...");
        _ = Task.Run(async () =>
        {
            while (!_refreshCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_refreshInterval, _refreshCts.Token);
                    await RefreshSessionsAsync();
                }
                catch (OperationCanceledException)
                {
                    _logger.Info("StartRefreshLoopAsync: Operation cancelled, exiting loop");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in refresh loop");
                }
            }
            _logger.Info("StartRefreshLoopAsync: Background task completed");
        }, _refreshCts.Token);
        _logger.Info("StartRefreshLoopAsync: Completed");
    }

    /// <summary>
    /// Остановить цикл автообновления.
    /// </summary>
    public void StopRefreshLoop()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    /// <summary>
    /// Обновить список сессий.
    /// </summary>
    [RelayCommand]
    public async Task RefreshSessionsAsync()
    {
        if (IsLoading) 
        {
            _logger.Debug("RefreshSessionsAsync: Already loading, skipping");
            return;
        }

        try
        {
            IsLoading = true;
            _logger.Debug("RefreshSessionsAsync: Starting refresh...");

            var newSessions = await _wtsService.EnumerateSessionsAsync(_refreshCts?.Token ?? CancellationToken.None);
            _logger.Debug("RefreshSessionsAsync: Enumerated {0} sessions", newSessions.Count);

            // Обновляем AppState для каждой сессии
            var updatedSessions = new List<SessionInfo>();
            foreach (var s in newSessions)
            {
                var updated = await _appStateService.UpdateSessionAppStateAsync(s, _refreshCts?.Token ?? CancellationToken.None);
                updatedSessions.Add(updated);
            }
            _logger.Debug("RefreshSessionsAsync: Updated {0} sessions with app state", updatedSessions.Count);

            // Синхронизируем коллекцию ViewModel'ов
            UpdateSessionCollection(updatedSessions);

            _logger.Debug("RefreshSessionsAsync: Completed. Total sessions: {0}", updatedSessions.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "RefreshSessionsAsync: Failed to refresh sessions");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Синхронизировать коллекцию SessionViewModel с новыми данными.
    /// Обновляет существующие, добавляет новые, удаляет завершённые.
    /// </summary>
    private void UpdateSessionCollection(IReadOnlyList<SessionInfo> newSessions)
    {
        var newIds = new HashSet<int>(newSessions.Select(s => s.Id));
        var existingIds = new HashSet<int>(Sessions.Select(s => s.SessionInfo.Id));

        // Удаляем завершённые сессии
        var toRemove = Sessions.Where(s => !newIds.Contains(s.SessionInfo.Id)).ToList();
        foreach (var vm in toRemove)
        {
            Sessions.Remove(vm);
        }

        // Обновляем существующие и добавляем новые
        foreach (var newInfo in newSessions)
        {
            var existingVm = Sessions.FirstOrDefault(s => s.SessionInfo.Id == newInfo.Id);
            if (existingVm != null)
            {
                existingVm.UpdateFrom(newInfo);
            }
            else
            {
                var newVm = new SessionViewModel(
                    newInfo, _wtsService, _commandExecutor, _windowEnumerator, _logger);

                // Сортируем по ID
                var insertIndex = Sessions.TakeWhile(s => s.SessionInfo.Id < newInfo.Id).Count();
                Sessions.Insert(insertIndex, newVm);
            }
        }
    }

    /// <summary>
    /// Тоггл привилегий (клик по замку).
    /// </summary>
    [RelayCommand]
    public async Task ToggleAdminModeAsync()
    {
        if (IsElevated) return; // Уже админ

        _logger.Info("Requesting elevation to admin mode");
        await ElevateAsync();
    }

    /// <summary>
    /// Перезапуск с правами администратора (через UAC runas).
    /// </summary>
    public async Task ElevateAsync()
    {
        // Сохраняем состояние
        var settings = await _settingsService.LoadAsync();
        var stateFile = Path.Combine(Path.GetTempPath(), "mh-ts-manager-state.json");
        var stateJson = System.Text.Json.JsonSerializer.Serialize(settings);
        await File.WriteAllTextAsync(stateFile, stateJson);

        // Перезапуск с runas
        var psi = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath ?? Environment.GetCommandLineArgs().First(),
            Arguments = $"--elevated --state-file \"{stateFile}\"",
            Verb = "runas",
            UseShellExecute = true
        };

        try
        {
            Process.Start(psi);
            _logger.Info("Elevated process started");
            // Завершаем текущий процесс
            Environment.Exit(0);
        }
        catch (Exception ex) when ((uint)ex.HResult == 0x800704C7)
        {
            // Пользователь отменил UAC
            _logger.Info("UAC elevation cancelled by user");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to elevate");
        }
    }

    /// <summary>
    /// Подключить к выбранной сессии (toolbar кнопка).
    /// </summary>
    [RelayCommand]
    public async Task ConnectSelectedAsync(CancellationToken ct)
    {
        if (SelectedSession == null) return;
        await SelectedSession.ConnectCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Открыть диалог отправки сообщения (toolbar кнопка).
    /// </summary>
    [RelayCommand]
    public void OpenSendMessageDialog()
    {
        if (SelectedSession == null) return;
        // TODO: открыть ContentDialog с TextBox
        _logger.Info("Open send message dialog for session {0}", SelectedSession.SessionInfo.Id);
    }

    /// <summary>
    /// Открыть оснастку управления пользователями.
    /// </summary>
    [RelayCommand]
    public async Task OpenUserManagementAsync(CancellationToken ct)
    {
        await _commandExecutor.OpenUserManagement(ct);
    }

    /// <summary>
    /// Открыть диалог "О программе".
    /// </summary>
    [RelayCommand]
    public void OpenAboutDialog()
    {
        _logger.Info("Open about dialog");
        // TODO: открыть ContentDialog
    }

    /// <summary>
    /// Выгрузить ViewModel (остановка фоновых задач).
    /// </summary>
    public void Unload()
    {
        StopRefreshLoop();
    }
}
}
