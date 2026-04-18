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
        // 0. Консольный вывод ДО всего
        var debugMode = e.Args.Contains("--debug");
        if (debugMode)
        {
            AllocConsole();
            Console.WriteLine("[CONSOLE] === APP STARTING ===");
            Console.WriteLine($"[CONSOLE] Args: {string.Join(" ", e.Args)}");
            Console.WriteLine($"[CONSOLE] Debug: {debugMode}");
        }

        base.OnStartup(e);

        try
        {
            // Инициализация логгера
            _logger.Info("=== APPLICATION STARTUP BEGIN ===");
            _logger.Info("Arguments: {0}", string.Join(" ", e.Args));
            _logger.Info("Debug mode: {0}", debugMode);

            // Создаём консольное окно в режиме отладки
            if (debugMode)
            {
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
            _logger.Info("[STEP] Loading settings...");
            Console.WriteLine("[STEP] Loading settings...");
            var settings = settingsService.LoadAsync().GetAwaiter().GetResult();
            if (settings == null)
            {
                _logger.Info("[STEP] Settings file not found, creating defaults...");
                Console.WriteLine("[STEP] Settings file not found, creating defaults...");
                settings = new AppSettings();
                settingsService.CreateDefaultSettings();
                _logger.Info("[STEP] Default settings created");
                Console.WriteLine("[STEP] Default settings created");
            }
            else
            {
                _logger.Info("[STEP] Settings loaded successfully from: {0}", settingsService.SettingsFilePath);
                Console.WriteLine("[STEP] Settings loaded successfully");
            }

            // Применяем тему
            _logger.Info("[STEP] Applying theme: {0}", theme);
            Console.WriteLine($"[STEP] Applying theme: {theme}");
            ApplyTheme(theme);

            // Создаём MainViewModel
            _logger.Info("[STEP] Creating MainViewModel...");
            Console.WriteLine("[STEP] Creating MainViewModel...");
            MainViewModel? mainViewModel = null;
            try
            {
                mainViewModel = new MainViewModel(
                    wtsService,
                    appStateService,
                    commandExecutor,
                    windowEnumerator,
                    localizationService,
                    settingsService,
                    _logger);
                _logger.Info("[STEP] MainViewModel created successfully");
                Console.WriteLine("[STEP] MainViewModel created successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[STEP] FATAL: Failed to create MainViewModel");
                Console.WriteLine($"[STEP] FATAL: Failed to create MainViewModel: {ex.Message}");
                throw;
            }

            // Создаём MainWindow вручную с передачей ViewModel и Logger
            _logger.Info("[STEP] Creating MainWindow manually...");
            Console.WriteLine("[STEP] Creating MainWindow manually...");
            try
            {
                var mainWindow = new MainWindow(mainViewModel, _logger);
                _logger.Info("[STEP] MainWindow created. Handle: {0}", mainWindow != null ? "OK" : "NULL");
                Console.WriteLine($"[STEP] MainWindow created: {(mainWindow != null ? "OK" : "NULL")}");

                // Назначаем DataContext (уже установлен в конструкторе MainWindow, но для ясности оставим)
                _logger.Info("[STEP] Verifying DataContext on MainWindow...");
                Console.WriteLine("[STEP] Verifying DataContext on MainWindow...");
                _logger.Info("[STEP] DataContext type: {0}", mainWindow?.DataContext?.GetType().Name ?? "null");
                Console.WriteLine($"[STEP] DataContext type: {mainWindow?.DataContext?.GetType().Name ?? "null"}");

                // Подписываемся на Loaded для запуска фоновых задач
                _logger.Info("[STEP] Subscribing to MainWindow.Loaded event...");
                Console.WriteLine("[STEP] Subscribing to MainWindow.Loaded event...");
                if (mainWindow != null)
                {
                    mainWindow.Loaded += async (_, _) =>
                    {
                        _logger.Info("[STEP] MainWindow.Loaded event fired");
                        Console.WriteLine("[STEP] MainWindow.Loaded event fired");
                        _logger.Info("[STEP]   - IsVisible: {0}", mainWindow.IsVisible);
                        _logger.Info("[STEP]   - IsActive: {0}", mainWindow.IsActive);
                        Console.WriteLine($"[STEP]   - IsVisible: {mainWindow.IsVisible}");
                        Console.WriteLine($"[STEP]   - IsActive: {mainWindow.IsActive}");
                        
                        try
                        {
                            await mainViewModel.StartRefreshLoopAsync();
                            _logger.Info("[STEP] Refresh loop started successfully");
                            Console.WriteLine("[STEP] Refresh loop started successfully");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "[STEP] Failed to start refresh loop");
                            Console.WriteLine($"[STEP] Failed to start refresh loop: {ex.Message}");
                        }
                    };

                    mainWindow.Closed += (_, _) =>
                    {
                        _logger.Info("[STEP] MainWindow.Closed event fired");
                        Console.WriteLine("[STEP] MainWindow.Closed event fired");
                        mainViewModel.Unload();
                        _logger.Info("[STEP] Application shutting down");
                        Console.WriteLine("[STEP] Application shutting down");
                    };
                }
                else
                {
                    _logger.Error("[STEP] MainWindow is null, cannot subscribe to events!");
                    Console.WriteLine("[STEP] ERROR: MainWindow is null!");
                }

                // Показываем окно
                _logger.Info("[STEP] Calling MainWindow.Show()...");
                Console.WriteLine("[STEP] Calling MainWindow.Show()...");
                if (mainWindow != null)
                {
                    Console.WriteLine("[CONSOLE] About to call Show()");
                    mainWindow.Show();
                    Console.WriteLine("[CONSOLE] Show() completed");
                    _logger.Info("[STEP] MainWindow.Show() completed");
                    Console.WriteLine("[STEP] MainWindow.Show() completed");
                    _logger.Info("[STEP]   - IsVisible after Show: {0}", mainWindow.IsVisible);
                    _logger.Info("[STEP]   - IsActive after Show: {0}", mainWindow.IsActive);
                    _logger.Info("[STEP]   - Width: {0}, Height: {1}", mainWindow.Width, mainWindow.Height);
                    _logger.Info("[STEP]   - Left: {0}, Top: {1}", mainWindow.Left, mainWindow.Top);
                    _logger.Info("[STEP]   - WindowState: {0}", mainWindow.WindowState);
                    _logger.Info("[STEP]   - Visibility: {0}", mainWindow.Visibility);
                    Console.WriteLine($"[STEP]   - IsVisible: {mainWindow.IsVisible}");
                    Console.WriteLine($"[STEP]   - IsActive: {mainWindow.IsActive}");
                    Console.WriteLine($"[STEP]   - Width: {mainWindow.Width}, Height: {mainWindow.Height}");
                    Console.WriteLine($"[STEP]   - Visibility: {mainWindow.Visibility}");

                    // Проверка: если окно не видно, пробуем Activate
                    if (!mainWindow.IsVisible || mainWindow.Visibility != Visibility.Visible)
                    {
                        _logger.Warning("[STEP] MainWindow is not visible after Show(), trying to recover...");
                        Console.WriteLine("[STEP] WARNING: Window NOT visible! Attempting recovery...");
                        mainWindow.Visibility = Visibility.Visible;
                        mainWindow.Show();
                        Console.WriteLine("[STEP] Calling Activate()...");
                        mainWindow.Activate();
                        Console.WriteLine("[STEP] Activate() completed");
                        mainWindow.Focus();
                        _logger.Info("[STEP] Recovery attempted. IsVisible: {0}", mainWindow.IsVisible);
                        Console.WriteLine($"[STEP] Recovery result. IsVisible: {mainWindow.IsVisible}");
                    }

                    MainWindow = mainWindow;
                }
                else
                {
                    _logger.Error("[STEP] MainWindow is null, cannot show window!");
                    Console.WriteLine("[STEP] ERROR: MainWindow is null, cannot show!");
                    throw new InvalidOperationException("MainWindow creation failed!");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[STEP] FATAL: Failed to create/show MainWindow");
                Console.WriteLine($"[STEP] FATAL: Failed to create/show MainWindow: {ex.Message}");
                throw;
            }

            _logger.Info("[STEP] === APPLICATION STARTUP COMPLETE ===");
            Console.WriteLine("[STEP] === APPLICATION STARTUP COMPLETE ===");
            
            if (debugMode)
            {
                Console.WriteLine("[CONSOLE] === STARTUP COMPLETE ===");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Fatal error during startup: {0}", ex.Message);
            _logger.Error("Stack trace: {0}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                _logger.Error("Inner exception: {0}", ex.InnerException);
            }
            
            Console.WriteLine($"[CONSOLE] FATAL ERROR: {ex.Message}");
            Console.WriteLine($"[CONSOLE] Stack: {ex.StackTrace}");
            
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
