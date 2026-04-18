using System.Windows;
using System.IO;
using System.Runtime.InteropServices;
using MhTsManager.Services;
using MhTsManager.ViewModels;
using MhTsManager.Views;

namespace MhTsManager
{

/// <summary>
/// Точка входа приложения. Настраивает DI, сервисы, запускает главное окно.
/// </summary>
public partial class App : Application
{
    // P/Invoke для создания консольного окна в режиме отладки
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    private readonly Logger _logger = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "mh-ts-manager",
        "logs"));

    /// <summary>
    /// Обработка аргументов командной строки.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Инициализация логгера
            var debugMode = e.Args.Contains("--debug");

            _logger.Info("=== APPLICATION STARTUP BEGIN ===");
            _logger.Info("Arguments: {0}", string.Join(" ", e.Args));
            _logger.Info("Debug mode: {0}", debugMode);

            // Создаём консольное окно в режиме отладки
            if (debugMode)
            {
                AllocConsole();
                _logger.Info("Debug console attached successfully");
                Console.WriteLine("[DEBUG] Console allocated");
            }

            Logger.Initialize(debugMode: debugMode);
            _logger.Info("Logger initialized");
            _logger.Info("Application starting. Version: {0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            _logger.Info("OS Version: {0}", Environment.OSVersion.VersionString);
            _logger.Info("Process Path: {0}", Environment.ProcessPath ?? "N/A");
            _logger.Info("Current Directory: {0}", Environment.CurrentDirectory);

            // Парсинг аргументов
            var customConfigPath = ParseArgument(e.Args, "--config");
            var language = ParseArgument(e.Args, "--language") ?? "system";
            var theme = ParseArgument(e.Args, "--theme") ?? "system";

            _logger.Info("Config path: {0}", customConfigPath ?? "(default)");
            _logger.Info("Language: {0}", language);
            _logger.Info("Theme: {0}", theme);

            if (e.Args.Contains("--help") || e.Args.Contains("-h"))
            {
                _logger.Info("Help requested, showing and exiting");
                ShowHelp();
                Shutdown(0);
                return;
            }

            if (e.Args.Contains("--version") || e.Args.Contains("-v"))
            {
                _logger.Info("Version requested, showing and exiting");
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                MessageBox.Show($"mh-ts-manager v{version}", "Version", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            // Регистрация сервисов (ручной DI)
            _logger.Info("Creating services...");
            var settingsService = new SettingsService(_logger, customConfigPath);
            _logger.Info("SettingsService created");
            var localizationService = new LocalizationService(_logger, language);
            _logger.Info("LocalizationService created");
            var wtsService = new WtsSessionService(_logger);
            _logger.Info("WtsSessionService created. IsElevated: {0}", wtsService.IsElevated);
            var windowEnumerator = new WindowEnumeratorService(_logger);
            _logger.Info("WindowEnumeratorService created");
            var appStateService = new SessionAppStateService(wtsService, windowEnumerator, _logger);
            _logger.Info("SessionAppStateService created");
            var commandExecutor = new CommandExecutor(_logger);
            _logger.Info("CommandExecutor created");

            // Загрузка настроек (без прерывания, если файл отсутствует)
            _logger.Info("Loading settings...");
            var settings = settingsService.LoadAsync().GetAwaiter().GetResult();
            if (settings == null)
            {
                _logger.Info("Settings file not found or invalid, using defaults.");
                settings = new AppSettings();
            }
            else
            {
                _logger.Info("Settings loaded successfully from: {0}", settingsService.SettingsFilePath);
            }

            // Применяем тему
            _logger.Info("Applying theme: {0}", theme);
            ApplyTheme(theme);

            // Создаём MainViewModel
            _logger.Info("Creating MainViewModel...");
            var mainViewModel = new MainViewModel(
                wtsService,
                appStateService,
                commandExecutor,
                windowEnumerator,
                localizationService,
                settingsService,
                _logger);
            _logger.Info("MainViewModel created successfully");

            // Создаём MainWindow вручную с передачей ViewModel и Logger
            _logger.Info("Creating MainWindow manually with ViewModel and Logger...");
            var mainWindow = new MainWindow(mainViewModel, _logger);
            _logger.Info("MainWindow created. Handle: {0}", mainWindow != null ? "OK" : "NULL");

            // Назначаем DataContext (уже установлен в конструкторе MainWindow, но для ясности оставим)
            _logger.Info("Verifying DataContext on MainWindow...");
            _logger.Info("DataContext type: {0}", mainWindow?.DataContext?.GetType().Name ?? "null");

            // Подписываемся на Loaded для запуска фоновых задач
            _logger.Info("Subscribing to MainWindow.Loaded event...");
            if (mainWindow != null)
            {
                mainWindow.Loaded += async (_, _) =>
                {
                    _logger.Info("MainWindow.Loaded event fired");
                    _logger.Info("  - IsVisible: {0}", mainWindow.IsVisible);
                    _logger.Info("  - IsActive: {0}", mainWindow.IsActive);
                    
                    try
                    {
                        await mainViewModel.StartRefreshLoopAsync();
                        _logger.Info("Refresh loop started successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to start refresh loop");
                    }
                };

                mainWindow.Closed += (_, _) =>
                {
                    _logger.Info("MainWindow.Closed event fired");
                    mainViewModel.Unload();
                    _logger.Info("Application shutting down");
                };
            }
            else
            {
                _logger.Error("MainWindow is null, cannot subscribe to events!");
            }

            // Показываем окно
            _logger.Info("Calling MainWindow.Show()...");
            if (mainWindow != null)
            {
                mainWindow.Show();
                _logger.Info("MainWindow.Show() completed");
                _logger.Info("  - IsVisible after Show: {0}", mainWindow.IsVisible);
                _logger.Info("  - IsActive after Show: {0}", mainWindow.IsActive);
                _logger.Info("  - Width: {0}, Height: {1}", mainWindow.Width, mainWindow.Height);
                _logger.Info("  - Left: {0}, Top: {1}", mainWindow.Left, mainWindow.Top);
                _logger.Info("  - WindowState: {0}", mainWindow.WindowState);
                _logger.Info("  - Visibility: {0}", mainWindow.Visibility);

                // Проверка: если окно не видно, пробуем Activate
                if (!mainWindow.IsVisible || mainWindow.Visibility != Visibility.Visible)
                {
                    _logger.Warning("MainWindow is not visible after Show(), trying to recover...");
                    mainWindow.Visibility = Visibility.Visible;
                    mainWindow.Show();
                    mainWindow.Activate();
                    _logger.Info("Recovery attempted. IsVisible: {0}", mainWindow.IsVisible);
                }

                MainWindow = mainWindow;
            }
            else
            {
                _logger.Error("MainWindow is null, cannot show window!");
                throw new InvalidOperationException("MainWindow creation failed!");
            }

            _logger.Info("=== APPLICATION STARTUP COMPLETE ===");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Fatal error during startup: {0}", ex.Message);
            _logger.Error("Stack trace: {0}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                _logger.Error("Inner exception: {0}", ex.InnerException);
            }
            MessageBox.Show($"Critical Error: {ex.Message}\n\nSee logs for details.", "Startup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Применить тему (light/dark/system).
    /// </summary>
    private static void ApplyTheme(string theme)
    {
        // WPF не имеет встроенной системной темы — используем ресурсы
        // TODO: реализовать переключение темы через ResourceDictionary
    }

    /// <summary>
    /// Распарсить аргумент командной строки.
    /// </summary>
    private static string? ParseArgument(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag)
                return args[i + 1];
        }
        return null;
    }

    /// <summary>
    /// Показать справку по аргументам.
    /// </summary>
    private static void ShowHelp()
    {
        var help = @"mh-ts-manager — Диспетчер пользовательских сессий

Использование: mh-ts-manager.exe [ОПЦИИ]

ОПЦИИ:
  --config <path>    Путь к альтернативному файлу настроек
  --language <code>  Принудительный язык интерфейса (ru-RU, en-US)
  --theme <mode>     Принудительная тема (light, dark, system)
  --debug            Включить отладочное логирование
  --help, -h         Вывод справки
  --version, -v      Вывод версии";

        MessageBox.Show(help, "Справка", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
}
