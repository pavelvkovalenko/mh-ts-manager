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

            // Загружаем Fluent стили ДО создания окна
            _logger.Info("Loading Fluent styles...");
            try
            {
                var fluentStylesDict = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Views/Styles/FluentStyles.xaml")
                };
                Resources.MergedDictionaries.Add(fluentStylesDict);
                _logger.Info("Fluent styles loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load Fluent styles!");
                throw;
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

            // Создаём и показываем главное окно
            _logger.Info("Creating MainWindow...");
            MainWindow? mainWindow = null;
            try
            {
                mainWindow = new MainWindow(mainViewModel, _logger);
                _logger.Info("MainWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MainWindow constructor threw exception!");
                MessageBox.Show($"Failed to create main window: {ex.Message}\n\nSee logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            _logger.Info("MainWindow created. Setting properties...");
            _logger.Info("  - ShowActivated: {0}", mainWindow.ShowActivated);
            _logger.Info("  - Visibility: {0}", mainWindow.Visibility);
            _logger.Info("  - Width: {0}, Height: {1}", mainWindow.Width, mainWindow.Height);
            _logger.Info("  - MinWidth: {0}, MinHeight: {1}", mainWindow.MinWidth, mainWindow.MinHeight);

            // Принудительно делаем окно видимым и активным (защита от невидимости)
            mainWindow.ShowActivated = true;
            mainWindow.Topmost = false;
            mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Гарантируем корректные размеры перед показом
            if (mainWindow.Width < mainWindow.MinWidth)
                mainWindow.Width = mainWindow.MinWidth;
            if (mainWindow.Height < mainWindow.MinHeight)
                mainWindow.Height = mainWindow.MinHeight;

            _logger.Info("Calling mainWindow.Show()...");
            mainWindow.Visibility = Visibility.Visible;
            mainWindow.Show();

            _logger.Info("Show() completed. IsVisible: {0}, IsActive: {1}", mainWindow.IsVisible, mainWindow.IsActive);

            _logger.Info("Calling mainWindow.Activate() and Focus()...");
            mainWindow.Activate();
            mainWindow.Focus();

            _logger.Info("Activate/Focus completed. IsVisible: {0}, IsActive: {1}", mainWindow.IsVisible, mainWindow.IsActive);

            // Дополнительная проверка: если окно всё ещё не видно, перемещаем его в центр
            var helper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
            _logger.Info("MainWindow shown. Handle: {0}, Visible: {1}, Width: {2}, Height: {3}, IsVisible: {4}",
                helper.Handle, mainWindow.IsVisible, mainWindow.Width, mainWindow.Height, mainWindow.IsVisible);

            if (!mainWindow.IsVisible)
            {
                _logger.Warning("Window is not visible after Show()! Attempting recovery...");
                mainWindow.Show();
                mainWindow.Activate();
                _logger.Info("Recovery attempt completed. IsVisible: {0}", mainWindow.IsVisible);
            }

            // Блокировка завершения приложения до закрытия окна
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            _logger.Info("ShutdownMode set to OnMainWindowClose");

            // Запускаем автообновление ПОСЛЕ того как окно показано
            _logger.Info("Subscribing to Loaded event for refresh loop...");
            mainWindow.Loaded += async (_, _) =>
            {
                _logger.Info("MainWindow.Loaded event fired. Starting refresh loop...");
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

            // Остановка при закрытии
            mainWindow.Closed += (_, _) =>
            {
                _logger.Info("MainWindow.Closed event fired");
                mainViewModel.Unload();
                _logger.Info("Application shutting down");
            };

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
