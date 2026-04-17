using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MhTsManager.Models;
using MhTsManager.Services;

namespace MhTsManager.ViewModels
{

/// <summary>
/// ViewModel одной пользовательской сессии.
/// Содержит данные сессии, список приложений и команды управления.
/// </summary>
public sealed partial class SessionViewModel : ObservableObject
{
    private readonly IWtsSessionService _wtsService;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IWindowEnumeratorService _windowEnumerator;
    private readonly Logger _logger;

    [ObservableProperty]
    private SessionInfo _sessionInfo;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private ObservableCollection<AppWindowInfo> _applications = new();

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _memoryText = string.Empty;

    [ObservableProperty]
    private string _cpuText = string.Empty;

    [ObservableProperty]
    private string _diskText = string.Empty;

    [ObservableProperty]
    private string _networkText = string.Empty;

    public SessionViewModel(
        SessionInfo sessionInfo,
        IWtsSessionService wtsService,
        ICommandExecutor commandExecutor,
        IWindowEnumeratorService windowEnumerator,
        Logger logger)
    {
        _sessionInfo = sessionInfo;
        _wtsService = wtsService;
        _commandExecutor = commandExecutor;
        _windowEnumerator = windowEnumerator;
        _logger = logger;

        UpdateDisplayValues();
    }

    /// <summary>
    /// Обновить отображаемые значения из SessionInfo.
    /// </summary>
    partial void OnSessionInfoChanged(SessionInfo value)
    {
        UpdateDisplayValues();
    }

    private void UpdateDisplayValues()
    {
        StatusText = MapStatusText(SessionInfo.State);
        CpuText = SessionInfo.CpuPercent > 0 ? $"{SessionInfo.CpuPercent:F1}%" : "0%";
        MemoryText = FormatBytes(SessionInfo.MemoryBytes);
        DiskText = FormatBytesPerSec(SessionInfo.DiskBytesPerSec);
        NetworkText = FormatBytesPerSec(SessionInfo.NetworkBytesPerSec);
    }

    private static string MapStatusText(SessionState state)
    {
        return state switch
        {
            SessionState.Active => "Активна",
            SessionState.Idle => "Бездействие",
            SessionState.Disconnected => "Отключена",
            SessionState.Locked => "Заблокирована",
            SessionState.Unavailable => "Недоступна",
            _ => "Неизвестно"
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 МБ";
        if (bytes < 1024 * 1024) return $"{bytes / 1024} КБ";
        return $"{bytes / (1024.0 * 1024.0):F0} МБ";
    }

    private static string FormatBytesPerSec(long bytesPerSec)
    {
        if (bytesPerSec == 0) return "0 КБ/с";
        if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024} КБ/с";
        return $"{bytesPerSec / (1024.0 * 1024.0):F1} МБ/с";
    }

    /// <summary>
    /// Переключить раскрытие сессии (загрузка списка приложений).
    /// </summary>
    [RelayCommand]
    public async Task ToggleExpandedAsync(CancellationToken ct)
    {
        if (!IsExpanded) return;

        _logger.Debug("Loading applications for session {0}", SessionInfo.Id);
        try
        {
            var windows = await _windowEnumerator.EnumerateAltTabWindowsAsync(SessionInfo.Id, ct);
            Applications.Clear();
            foreach (var w in windows)
                Applications.Add(w);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to enumerate windows for session {0}", SessionInfo.Id);
        }
    }

    /// <summary>
    /// Удалённое подключение к сессии (RDP shadow).
    /// </summary>
    [RelayCommand]
    public async Task ConnectAsync(CancellationToken ct)
    {
        _logger.Info("Connecting to session {0}", SessionInfo.Id);
        await _commandExecutor.ShadowSessionAsync(SessionInfo.Id, ct);
    }

    /// <summary>
    /// Переключиться на сессию.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanManageSession))]
    public async Task SwitchAsync(CancellationToken ct)
    {
        _logger.Info("Switching to session {0}", SessionInfo.Id);
        await _wtsService.SwitchToSessionAsync(SessionInfo.Id, ct);
    }

    /// <summary>
    /// Отключить сессию.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanManageSession))]
    public async Task DisconnectAsync(CancellationToken ct)
    {
        _logger.Info("Disconnecting session {0}", SessionInfo.Id);
        await _wtsService.DisconnectSessionAsync(SessionInfo.Id, ct);
    }

    /// <summary>
    /// Завершить сессию (logoff).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanManageSession))]
    public async Task LogoffAsync(CancellationToken ct)
    {
        _logger.Info("Logging off session {0}", SessionInfo.Id);
        await _wtsService.LogoffSessionAsync(SessionInfo.Id, ct);
    }

    /// <summary>
    /// Может ли текущий пользователь управлять сессией.
    /// </summary>
    private bool CanManageSession()
    {
        return _wtsService.IsElevated;
    }

    /// <summary>
    /// Обновить данные сессии из внешнего источника.
    /// </summary>
    public void UpdateFrom(SessionInfo newInfo)
    {
        SessionInfo = newInfo;
    }
}
}
