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
        // НЕ вызываем base.OnStartup() чтобы избежать дублирования с StartupUri
        // base.OnStartup(e);

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

            // Получаем главное окно из Current.MainWindow (создаётся через StartupUri)
            _logger.Info("Waiting for MainWindow to be created via StartupUri...");
            
            // Создаём MainViewModel и назначаем его DataContext окна
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

            // Назначаем DataContext после создания окна
            _logger.Info("Assigning DataContext to MainWindow...");
            if (MainWindow != null)
            {
                MainWindow.DataContext = mainViewModel;
                _logger.Info("DataContext assigned successfully. MainWindow type: {0}", MainWindow.GetType().Name);
                _logger.Info("MainWindow.IsVisible: {0}, MainWindow.IsActive: {1}", MainWindow.IsVisible, MainWindow.IsActive);
                
                // Подписываемся на Loaded для запуска фоновых задач
                _logger.Info("Subscribing to MainWindow.Loaded event...");
                MainWindow.Loaded += async (_, _) =>
                {
                    _logger.Info("MainWindow.Loaded event fired");
                    _logger.Info("  - IsVisible: {0}", MainWindow.IsVisible);
                    _logger.Info("  - IsActive: {0}", MainWindow.IsActive);
                    
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

                MainWindow.Closed += (_, _) =>
                {
                    _logger.Info("MainWindow.Closed event fired");
                    mainViewModel.Unload();
                    _logger.Info("Application shutting down");
                };
            }
            else
            {
                _logger.Error("MainWindow is NULL after StartupUri!");
                throw new InvalidOperationException("MainWindow is null after StartupUri");
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
