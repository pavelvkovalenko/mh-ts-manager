using System.Windows;
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

        // Инициализация логгера
        var debugMode = e.Args.Contains("--debug");
        Logger.Initialize(debugMode: debugMode);
        _logger.Info("Application starting. Version: {0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);

        // Парсинг аргументов
        var customConfigPath = ParseArgument(e.Args, "--config");
        var language = ParseArgument(e.Args, "--language") ?? "system";
        var theme = ParseArgument(e.Args, "--theme") ?? "system";

        if (e.Args.Contains("--help") || e.Args.Contains("-h"))
        {
            ShowHelp();
            Shutdown(0);
            return;
        }

        if (e.Args.Contains("--version") || e.Args.Contains("-v"))
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show($"mh-ts-manager v{version}", "Version", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        // Регистрация сервисов (ручной DI)
        var settingsService = new SettingsService(_logger, customConfigPath);
        var localizationService = new LocalizationService(_logger, language);
        var wtsService = new WtsSessionService(_logger);
        var windowEnumerator = new WindowEnumeratorService(_logger);
        var appStateService = new SessionAppStateService(wtsService, windowEnumerator, _logger);
        var commandExecutor = new CommandExecutor(_logger);

        // Загрузка настроек
        var settings = settingsService.LoadAsync().GetAwaiter().GetResult();

        // Применяем тему
        ApplyTheme(theme);

        // Загружаем Fluent стили
        var fluentStylesDict = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Views/Styles/FluentStyles.xaml")
        };
        Resources.MergedDictionaries.Add(fluentStylesDict);

        // Создаём MainViewModel
        var mainViewModel = new MainViewModel(
            wtsService,
            appStateService,
            commandExecutor,
            localizationService,
            settingsService,
            _logger);

        // Создаём и показываем главное окно
        var mainWindow = new MainWindow(mainViewModel, _logger);
        mainWindow.Show();

        // Запускаем автообновление
        mainWindow.Loaded += async (_, _) =>
        {
            await mainViewModel.StartRefreshLoopAsync();
        };

        // Остановка при закрытии
        mainWindow.Closed += (_, _) =>
        {
            mainViewModel.Unload();
            _logger.Info("Application shutting down");
        };
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
