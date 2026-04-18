=== /workspace/src/Services/Logger.cs ===
using System.Text;
using System.IO;

namespace MhTsManager.Services
{

/// <summary>
/// Уровень логирования.
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
/// Сервис логирования с ротацией по дате и размеру файла.
/// Путь: %APPDATA%\mh-ts-manager\logs\mh-ts-manager-YYYYMMDD.log
/// Кодировка: UTF-8 без BOM.
/// </summary>
public sealed class Logger : IDisposable
{
    /// <summary>Максимальный размер файла лога (5 МБ).</summary>
    private const long MaxFileSize = 5L * 1024 * 1024;

    private static readonly object LockObj = new();
    private static Logger? _instance;
    private static bool _isDebug;

    private readonly string _logDirectory;
    private readonly object _writeLock = new();
    private string? _currentLogFile;
    private long _currentFileSize;

    /// <summary>
    ///Singleton-экземпляр логгера.
    /// </summary>
    public static Logger Instance
    {
        get
        {
            if (_instance == null)
            {
                Initialize(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "mh-ts-manager",
                    "logs"));
            }
            return _instance!;
        }
    }

    /// <summary>
    /// Приватный конструктор для создания экземпляра логгера.
    /// </summary>
    /// <param name="logDirectory">Директория для логов.</param>
    internal Logger(string logDirectory)
    {
        _logDirectory = logDirectory;
        InitializeDirectory();
    }

    /// <summary>
    /// Инициализация логгера.
    /// </summary>
    /// <param name="logDirectory">Директория для логов.</param>
    /// <param name="debugMode">Включить отладочное логирование.</param>
    public static void Initialize(string? logDirectory = null, bool debugMode = false)
    {
        if (_instance != null) return;

        var dir = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "mh-ts-manager",
            "logs");

        _isDebug = debugMode;
        _instance = new Logger(dir);
        _instance.Info("Logger initialized. Directory: {0}, Debug: {1}", dir, debugMode);
    }

    /// <summary>
    /// Запись сообщения уровня DEBUG.
    /// </summary>
    public void Debug(string message, params object?[] args)
    {
        if (!_isDebug) return;
        Write(LogLevel.Debug, message, args);
    }

    /// <summary>
    /// Запись сообщения уровня INFO.
    /// </summary>
    public void Info(string message, params object?[] args)
    {
        Write(LogLevel.Info, message, args);
    }

    /// <summary>
    /// Запись сообщения уровня WARNING.
    /// </summary>
    public void Warning(string message, params object?[] args)
    {
        Write(LogLevel.Warning, message, args);
    }

    /// <summary>
    /// Запись сообщения уровня ERROR.
    /// </summary>
    public void Error(string message, params object?[] args)
    {
        Write(LogLevel.Error, message, args);
    }

    /// <summary>
    /// Запись сообщения уровня ERROR с исключением.
    /// </summary>
    public void Error(Exception ex, string message, params object?[] args)
    {
        var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
        Write(LogLevel.Error, $"{formattedMessage} | Exception: {ex.GetType().Name}: {ex.Message}");
    }

    /// <summary>
    /// Статический метод для удобства.
    /// </summary>
    public static void DebugStatic(string message, params object?[] args) => Instance.Debug(message, args);

    /// <summary>
    /// Статический метод для удобства.
    /// </summary>
    public static void InfoStatic(string message, params object?[] args) => Instance.Info(message, args);

    /// <summary>
    /// Статический метод для удобства.
    /// </summary>
    public static void WarningStatic(string message, params object?[] args) => Instance.Warning(message, args);

    /// <summary>
    /// Статический метод для удобства.
    /// </summary>
    public static void ErrorStatic(string message, params object?[] args) => Instance.Error(message, args);

    /// <summary>
    /// Запись лога с блокировкой.
    /// </summary>
    private void Write(LogLevel level, string message, object?[]? args = null)
    {
        lock (_writeLock)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var levelStr = level.ToString().ToUpperInvariant();
                var formattedMessage = args != null && args.Length > 0
                    ? string.Format(message, args)
                    : message;

                var logLine = $"[{timestamp}] [{levelStr}] {formattedMessage}";

                // Вывод в консоль в режиме отладки
                if (_isDebug)
                {
                    Console.WriteLine(logLine);
                }

                // Запись в файл
                var logFile = GetOrCreateLogFile();
                if (logFile == null) return;

                // Проверка размера — ротация
                if (_currentFileSize > MaxFileSize)
                {
                    RotateLogs();
                    logFile = GetOrCreateLogFile();
                    if (logFile == null) return;
                }

                // UTF-8 без BOM
                File.AppendAllText(logFile, logLine + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                // Не допускаем падения приложения из-за ошибок логирования
                if (_isDebug)
                {
                    Console.WriteLine($"[LOGGER ERROR] {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Получить или создать текущий файл лога (на сегодня).
    /// </summary>
    private string? GetOrCreateLogFile()
    {
        if (!string.IsNullOrEmpty(_currentLogFile) && File.Exists(_currentLogFile))
            return _currentLogFile;

        var today = DateTime.Now.ToString("yyyyMMdd");
        var newFile = Path.Combine(_logDirectory, $"mh-ts-manager-{today}.log");

        if (File.Exists(newFile))
        {
            _currentLogFile = newFile;
            _currentFileSize = new FileInfo(newFile).Length;
            return newFile;
        }

        try
        {
            // Создание нового файла без BOM
            using var fs = new FileStream(newFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Flush();
            _currentFileSize = 0;
            _currentLogFile = newFile;

            // Удаление старых логов
            CleanOldLogs();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create log file: {ex.Message}");
            return null;
        }

        return _currentLogFile;
    }

    /// <summary>
    /// Ротация логов — переименование текущего файла.
    /// </summary>
    private void RotateLogs()
    {
        if (string.IsNullOrEmpty(_currentLogFile) || !File.Exists(_currentLogFile))
            return;

        try
        {
            var rotatedName = _currentLogFile + ".old";
            if (File.Exists(rotatedName))
                File.Delete(rotatedName);
            File.Move(_currentLogFile, rotatedName);
            _currentLogFile = null;
            _currentFileSize = 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to rotate log: {ex.Message}");
        }
    }

    /// <summary>
    /// Удаление старых файлов логов.
    /// </summary>
    private void CleanOldLogs()
    {
        try
        {
            var retentionDays = 7; // По умолчанию
            var cutoffDate = DateTime.Now.AddDays(-retentionDays);
            var pattern = "mh-ts-manager-*.log";

            var files = Directory.GetFiles(_logDirectory, pattern);
            foreach (var file in files)
            {
                if (File.GetCreationTime(file) < cutoffDate)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Игнорируем ошибки очистки
        }
    }

    /// <summary>
    /// Создание директории логов.
    /// </summary>
    private void InitializeDirectory()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create log directory: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _instance = null;
    }
}
}
=== /workspace/src/Services/LocalizationService.cs ===
namespace MhTsManager.Services
{

/// <summary>
/// Интерфейс сервиса локализации.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Получить локализованную строку по ключу.
    /// </summary>
    string GetString(string key, params object[] args);

    /// <summary>
    /// Текущий язык интерфейса.
    /// </summary>
    string CurrentLanguage { get; }
}

/// <summary>
/// Сервис локализации на основе встроенных словарей.
/// Автоматический fallback: текущий → ru-RU → en-US → ключ.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly Logger _logger;
    private readonly Dictionary<string, Dictionary<string, string>> _resources = new();
    private string _currentLanguage;

    public string CurrentLanguage => _currentLanguage;

    public LocalizationService(Logger logger, string language = "system")
    {
        _logger = logger;

        // Определяем текущий язык
        _currentLanguage = language == "system"
            ? System.Globalization.CultureInfo.CurrentUICulture.Name
            : language;

        if (string.IsNullOrEmpty(_currentLanguage))
            _currentLanguage = "en-US";

        // Загружаем встроенные ресурсы
        LoadBuiltInResources();

        _logger.Info("LocalizationService initialized. Language: {0}", _currentLanguage);
    }

    /// <summary>
    /// Получить локализованную строку с fallback-цепочкой.
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        // 1. Текущий язык
        if (_resources.TryGetValue(_currentLanguage, out var currentLang) &&
            currentLang.TryGetValue(key, out var value))
        {
            return args.Length > 0 ? string.Format(value, args) : value;
        }

        // 2. Fallback: ru-RU
        if (_currentLanguage != "ru-RU" &&
            _resources.TryGetValue("ru-RU", out var ruLang) &&
            ruLang.TryGetValue(key, out var ruValue))
        {
            return args.Length > 0 ? string.Format(ruValue, args) : ruValue;
        }

        // 3. Fallback: en-US
        if (_currentLanguage != "en-US" &&
            _resources.TryGetValue("en-US", out var enLang) &&
            enLang.TryGetValue(key, out var enValue))
        {
            return args.Length > 0 ? string.Format(enValue, args) : enValue;
        }

        // 4. Возвращаем ключ как заглушку
        _logger.Debug("Missing localization key: {0} (language: {1})", key, _currentLanguage);
        return args.Length > 0 ? string.Format(key, args) : key;
    }

    /// <summary>
    /// Загрузить встроенные ресурсы для основных языков.
    /// </summary>
    private void LoadBuiltInResources()
    {
        // Русский язык
        _resources["ru-RU"] = new Dictionary<string, string>
        {
            ["App.WindowTitle"] = "Диспетчер пользовательских сессий",
            ["App.About.Title"] = "О приложении",
            ["Toolbar.UsersTitle"] = "Пользователи",
            ["Toolbar.Connect"] = "Подключить",
            ["Toolbar.SendMessage"] = "Отправить сообщение",
            ["Toolbar.AdminMode.Locked"] = "Разблокировать режим системного администратора?",
            ["Toolbar.AdminMode.Active"] = "Режим системного администратора",
            ["Toolbar.AdminMode.ElevateConfirm"] = "Для выполнения операции требуются права администратора. Перезапустить приложение с повышенными привилегиями?",
            ["Status.SessionActive"] = "Активна",
            ["Status.SessionDisconnected"] = "Отключена",
            ["Status.SessionIdle"] = "Бездействие",
            ["Status.SessionLocked"] = "Заблокирована",
            ["Status.SessionUnavailable"] = "Недоступна",
            ["Column.User"] = "Пользователь",
            ["Column.Status"] = "Состояние",
            ["Column.CPU"] = "ЦП",
            ["Column.Memory"] = "Память",
            ["Column.Disk"] = "Диск",
            ["Column.Network"] = "Сеть",
            ["Column.GPU"] = "GPU",
            ["Column.GPUEngine"] = "Движок GPU",
            ["Column.SessionID"] = "ID",
            ["Column.SessionType"] = "Сеанс",
            ["Column.ClientName"] = "Имя клиента",
            ["Column.NPU"] = "NPU",
            ["Column.NPUEngine"] = "Движок NPU",
            ["UserCell.Format"] = "{0} — {1} ({2})",
            ["UserCell.AppsCount.Tooltip"] = "Приложений в сессии: {0}",
            ["UserCell.Locked.Tooltip"] = "Сессия заблокирована",
            ["UserCell.Screensaver.Tooltip"] = "Активен хранитель экрана",
            ["Table.Columns.Select"] = "Выбрать столбцы…",
            ["Table.Columns.Reset"] = "Сбросить по умолчанию",
            ["ContextMenu.Expand"] = "Развернуть",
            ["ContextMenu.Collapse"] = "Свернуть",
            ["ContextMenu.Connect"] = "Подключить",
            ["ContextMenu.Switch"] = "Переключиться",
            ["ContextMenu.Disconnect"] = "Отключить",
            ["ContextMenu.Logoff"] = "Выйти",
            ["ContextMenu.SendMessage"] = "Отправить сообщение...",
        };

        // English
        _resources["en-US"] = new Dictionary<string, string>
        {
            ["App.WindowTitle"] = "User Session Manager",
            ["App.About.Title"] = "About",
            ["Toolbar.UsersTitle"] = "Users",
            ["Toolbar.Connect"] = "Connect",
            ["Toolbar.SendMessage"] = "Send Message",
            ["Toolbar.AdminMode.Locked"] = "Unlock System Administrator mode?",
            ["Toolbar.AdminMode.Active"] = "System Administrator mode",
            ["Toolbar.AdminMode.ElevateConfirm"] = "Administrator privileges are required for this operation. Restart with elevated privileges?",
            ["Status.SessionActive"] = "Active",
            ["Status.SessionDisconnected"] = "Disconnected",
            ["Status.SessionIdle"] = "Idle",
            ["Status.SessionLocked"] = "Locked",
            ["Status.SessionUnavailable"] = "Unavailable",
            ["Column.User"] = "User",
            ["Column.Status"] = "Status",
            ["Column.CPU"] = "CPU",
            ["Column.Memory"] = "Memory",
            ["Column.Disk"] = "Disk",
            ["Column.Network"] = "Network",
            ["Column.GPU"] = "GPU",
            ["Column.GPUEngine"] = "GPU Engine",
            ["Column.SessionID"] = "ID",
            ["Column.SessionType"] = "Session",
            ["Column.ClientName"] = "Client Name",
            ["Column.NPU"] = "NPU",
            ["Column.NPUEngine"] = "NPU Engine",
            ["UserCell.Format"] = "{0} — {1} ({2})",
            ["UserCell.AppsCount.Tooltip"] = "Applications in session: {0}",
            ["UserCell.Locked.Tooltip"] = "Session is locked",
            ["UserCell.Screensaver.Tooltip"] = "Screensaver is active",
            ["Table.Columns.Select"] = "Select Columns…",
            ["Table.Columns.Reset"] = "Reset to defaults",
            ["ContextMenu.Expand"] = "Expand",
            ["ContextMenu.Collapse"] = "Collapse",
            ["ContextMenu.Connect"] = "Connect",
            ["ContextMenu.Switch"] = "Switch To",
            ["ContextMenu.Disconnect"] = "Disconnect",
            ["ContextMenu.Logoff"] = "Log Off",
            ["ContextMenu.SendMessage"] = "Send Message...",
        };
    }
}
}
=== /workspace/src/Services/SessionAppStateService.cs ===
using MhTsManager.Models;

namespace MhTsManager.Services
{

/// <summary>
/// Интерфейс сервиса состояния приложений сессии.
/// Объединяет подсчёт окон (Alt+Tab), проверку блокировки и хранителя экрана.
/// </summary>
public interface ISessionAppStateService
{
    /// <summary>
    /// Обновить состояние приложения для сессии (подсчёт окон, блокировка, скринсейвер).
    /// </summary>
    Task<SessionInfo> UpdateSessionAppStateAsync(SessionInfo session, CancellationToken ct);

    /// <summary>
    /// Подсчитать окна Alt+Tab для сессии.
    /// </summary>
    Task<int> GetApplicationCountAsync(int sessionId, CancellationToken ct);

    /// <summary>
    /// Проверить, заблокирована ли сессия.
    /// </summary>
    bool IsSessionLocked(int sessionId);

    /// <summary>
    /// Проверить, активен ли хранитель экрана в сессии.
    /// (Заглушка для v1.3 — будет реализовано в v1.4 через WTSQueryUserToken + ImpersonateLoggedOnUser)
    /// </summary>
    bool IsScreensaverActive(int sessionId);
}

/// <summary>
/// Сервис состояния приложений сессии.
/// Объединяет данные от WtsSessionService и WindowEnumeratorService.
/// </summary>
public sealed class SessionAppStateService : ISessionAppStateService
{
    private readonly IWtsSessionService _wtsService;
    private readonly IWindowEnumeratorService _windowEnumerator;
    private readonly Logger _logger;

    public SessionAppStateService(
        IWtsSessionService wtsService,
        IWindowEnumeratorService windowEnumerator,
        Logger logger)
    {
        _wtsService = wtsService;
        _windowEnumerator = windowEnumerator;
        _logger = logger;
    }

    /// <summary>
    /// Обновить состояние приложения для сессии.
    /// </summary>
    public async Task<SessionInfo> UpdateSessionAppStateAsync(SessionInfo session, CancellationToken ct)
    {
        var appCount = await GetApplicationCountAsync(session.Id, ct);
        var isLocked = IsSessionLocked(session.Id);
        var screensaver = IsScreensaverActive(session.Id);

        _logger.Debug(
            "Session {0} ({1}) state: apps={2}, locked={3}, screensaver={4}",
            session.Id, session.UserName, appCount, isLocked, screensaver);

        return session with
        {
            ApplicationCount = appCount,
            IsLocked = isLocked,
            IsScreensaverActive = screensaver
        };
    }

    /// <summary>
    /// Подсчитать окна Alt+Tab для сессии.
    /// </summary>
    public async Task<int> GetApplicationCountAsync(int sessionId, CancellationToken ct)
    {
        try
        {
            return await _windowEnumerator.CountAltTabWindowsAsync(sessionId, ct);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to count Alt+Tab windows for session {0}", sessionId);
            return 0;
        }
    }

    /// <summary>
    /// Проверить, заблокирована ли сессия.
    /// </summary>
    public bool IsSessionLocked(int sessionId)
    {
        try
        {
            return _wtsService.IsSessionLocked(sessionId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check lock state for session {0}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Проверить, активен ли хранитель экрана.
    /// Заглушка: требует выполнение в контексте сессии через WTSQueryUserToken.
    /// </summary>
    public bool IsScreensaverActive(int sessionId)
    {
        // TODO: v1.4 — реализовать через WTSQueryUserToken + ImpersonateLoggedOnUser + SystemParametersInfo
        // SPI_GETSCREENSAVERRUNNING возвращает TRUE, если скринсейвер активен
        _logger.Debug("Screensaver check not implemented for session {0}", sessionId);
        return false;
    }
}
}
=== /workspace/src/Services/SettingsService.cs ===
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace MhTsManager.Services
{

/// <summary>
/// Конфигурация приложения (сериализуемая модель).
/// </summary>
public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
}

public sealed class GeneralSettings
{
    public string Language { get; set; } = "system";
    public string Theme { get; set; } = "system";
    public int AutoRefreshInterval { get; set; } = 5;
    public bool ExpandAppsByDefault { get; set; } = false;
}

public sealed class UiSettings
{
    public SessionsUiSettings Sessions { get; set; } = new();
}

public sealed class SessionsUiSettings
{
    public ColumnsSettings Columns { get; set; } = new();
}

public sealed class ColumnsSettings
{
    public List<string> Visible { get; set; } = new()
    {
        "User", "Status", "CPU", "Memory", "Disk", "Network", "GPU", "GPUEngine"
    };

    public List<string> Order { get; set; } = new()
    {
        "User", "Status", "CPU", "Memory", "Disk", "Network", "GPU", "GPUEngine",
        "SessionID", "SessionType", "ClientName", "NPU", "NPUEngine"
    };

    public Dictionary<string, double> Widths { get; set; } = new();
}

public sealed class AdvancedSettings
{
    public bool EnableDebugLogging { get; set; } = false;
    public int LogRetentionDays { get; set; } = 7;
    public bool ConfirmDangerousActions { get; set; } = true;
}

/// <summary>
/// Интерфейс сервиса настроек.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Загрузить настройки из файла.
    /// </summary>
    Task<AppSettings> LoadAsync();

    /// <summary>
    /// Сохранить настройки в файл (атомарная запись).
    /// </summary>
    Task SaveAsync(AppSettings settings);

    /// <summary>
    /// Путь к файлу настроек.
    /// </summary>
    string SettingsFilePath { get; }
}

/// <summary>
/// Сервис настроек с атомарной записью через временный файл.
/// Путь: %APPDATA%\mh-ts-manager\settings.json
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly Logger _logger;
    private readonly string _settingsDirectory;
    private readonly string _settingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public SettingsService(Logger logger, string? customPath = null)
    {
        _logger = logger;

        if (!string.IsNullOrEmpty(customPath))
        {
            _settingsFilePath = Path.GetFullPath(customPath);
            _settingsDirectory = Path.GetDirectoryName(_settingsFilePath)
                                 ?? throw new InvalidOperationException("Invalid custom settings path");
        }
        else
        {
            _settingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "mh-ts-manager");
            _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");
        }
    }

    public string SettingsFilePath => _settingsFilePath;

    /// <summary>
    /// Загрузить настройки из файла.
    /// Возвращает настройки по умолчанию, если файл отсутствует или повреждён.
    /// </summary>
    public async Task<AppSettings> LoadAsync()
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger.Info("Settings file not found, returning defaults: {0}", _settingsFilePath);
                return CreateDefaultSettings();
            }

            try
            {
                // Проверка на symlink-атаку
                var fileInfo = new FileInfo(_settingsFilePath);
                if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    _logger.Warning("Settings file is a reparse point (symlink), using defaults: {0}", _settingsFilePath);
                    return CreateDefaultSettings();
                }

                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

                if (settings == null)
                {
                    _logger.Warning("Settings file deserialized to null, returning defaults");
                    return CreateDefaultSettings();
                }

                _logger.Info("Settings loaded from: {0}", _settingsFilePath);
                return settings;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to parse settings file, returning defaults");
                return CreateDefaultSettings();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load settings file, returning defaults");
                return CreateDefaultSettings();
            }
        });
    }

    /// <summary>
    /// Сохранить настройки в файл (атомарная запись через временный файл + переименование).
    /// </summary>
    public async Task SaveAsync(AppSettings settings)
    {
        await Task.Run(() =>
        {
            try
            {
                // Создание директории
                if (!Directory.Exists(_settingsDirectory))
                {
                    Directory.CreateDirectory(_settingsDirectory);
                }

                // Проверка на symlink
                if (Directory.Exists(_settingsDirectory))
                {
                    var dirInfo = new DirectoryInfo(_settingsDirectory);
                    if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        _logger.Warning("Settings directory is a reparse point, refusing to write: {0}", _settingsDirectory);
                        return;
                    }
                }

                // Атомарная запись: пишем во временный файл, затем переименовываем
                var tempFile = _settingsFilePath + ".tmp";
                var json = JsonSerializer.Serialize(settings, JsonOptions);

                File.WriteAllText(tempFile, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                // Атомарное переименование
                if (File.Exists(_settingsFilePath))
                {
                    File.Replace(tempFile, _settingsFilePath, null);
                }
                else
                {
                    File.Move(tempFile, _settingsFilePath);
                }

                _logger.Info("Settings saved to: {0}", _settingsFilePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save settings file");
                throw;
            }
        });
    }

    /// <summary>
    /// Создать настройки по умолчанию и сохранить их в файл.
    /// </summary>
    public static AppSettings CreateDefaultSettings()
    {
        try
        {
            var logger = Logger.Instance;
            Console.WriteLine("[CONSOLE] [SettingsService] CreateDefaultSettings: START");
            logger.Debug("[SettingsService] CreateDefaultSettings: Creating default settings...");
            
            var defaultSettings = new AppSettings
            {
                General = new GeneralSettings
                {
                    Language = "system",
                    Theme = "system",
                    AutoRefreshInterval = 5,
                    ExpandAppsByDefault = false,
                },
                Ui = new UiSettings
                {
                    Sessions = new SessionsUiSettings
                    {
                        Columns = new ColumnsSettings()
                    }
                },
                Advanced = new AdvancedSettings
                {
                    EnableDebugLogging = false,
                    LogRetentionDays = 7,
                    ConfirmDangerousActions = true,
                }
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(defaultSettings);
            logger.Debug($"[SettingsService] CreateDefaultSettings: Default settings created. JSON: {json}");
            Console.WriteLine($"[CONSOLE] [SettingsService] JSON serialized successfully, length: {json.Length}");
            
            // Попытка записать файл немедленно для диагностики
            var settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "mh-ts-manager");
            var settingsFile = Path.Combine(settingsDir, "settings.json");
            
            Console.WriteLine($"[CONSOLE] [SettingsService] Attempting to write file: {settingsFile}");
            logger.Debug($"[SettingsService] Attempting to write settings file: {settingsFile}");
            
            if (!Directory.Exists(settingsDir))
            {
                Console.WriteLine($"[CONSOLE] [SettingsService] Creating directory: {settingsDir}");
                Directory.CreateDirectory(settingsDir);
            }
            
            File.WriteAllText(settingsFile, json);
            Console.WriteLine($"[CONSOLE] [SettingsService] File written successfully!");
            logger.Debug("[SettingsService] Settings file written successfully");
            
            Console.WriteLine("[CONSOLE] [SettingsService] CreateDefaultSettings: END");
            return defaultSettings;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CONSOLE] [SettingsService] FATAL ERROR in CreateDefaultSettings: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[CONSOLE] [SettingsService] StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[CONSOLE] [SettingsService] InnerException: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
            throw;
        }
    }
}
}
=== /workspace/src/Services/WindowEnumeratorService.cs ===
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MhTsManager.Models;

namespace MhTsManager.Services
{

/// <summary>
/// Интерфейс сервиса перечисления окон, видимых в Alt+Tab, для заданной сессии.
/// </summary>
public interface IWindowEnumeratorService
{
    /// <summary>
    /// Перечислить окна, видимые в Alt+Tab, для заданной сессии.
    /// </summary>
    Task<IReadOnlyList<AppWindowInfo>> EnumerateAltTabWindowsAsync(int sessionId, CancellationToken ct);

    /// <summary>
    /// Подсчитать количество окон, видимых в Alt+Tab, для заданной сессии.
    /// </summary>
    Task<int> CountAltTabWindowsAsync(int sessionId, CancellationToken ct);
}

/// <summary>
/// Native-обёртка над User32.dll для работы с окнами.
/// </summary>
internal static class User32Native
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr ExtractAssociatedIcon(
        IntPtr hInst,
        StringBuilder lpIconPath,
        ref ushort lpiIcon);
}

/// <summary>
/// Сервис перечисления окон, видимых в Alt+Tab.
/// Критерии фильтрации (раздел 5.7 ТЗ):
/// 1. Окно принадлежит целевой сессии
/// 2. IsWindowVisible == TRUE
/// 3. Не WS_EX_TOOLWINDOW
/// 4. GetWindowTextLength > 0
/// 5. Не системное окно (по классу)
/// </summary>
public sealed class WindowEnumeratorService : IWindowEnumeratorService
{
    private readonly Logger _logger;

    /// <summary>
    /// Список классов системных окон, исключаемых из Alt+Tab.
    /// </summary>
    private static readonly HashSet<string> ExcludedClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "#32770",         // Dialog
        "Shell_TrayWnd",  // Taskbar
        "Shell_SecondaryTrayWnd",
        "TaskManagerWindow",
        "WorkerW",        // Desktop background
        "Progman",        // Program Manager
        "SysListView32",
        "SysTreeView32",
        "CiceroUIWndFrame",
        "TF_FloatingLangBar_WndTitle",
        "MSCTFIME UI",
        "OskWindow",      // On-Screen Keyboard
        "Ghost",          // Hung window ghost
    };

    public WindowEnumeratorService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Перечислить окна Alt+Tab для заданной сессии.
    /// </summary>
    public async Task<IReadOnlyList<AppWindowInfo>> EnumerateAltTabWindowsAsync(int sessionId, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var windows = new List<AppWindowInfo>();

            User32Native.EnumWindows((hWnd, _) =>
            {
                if (ct.IsCancellationRequested) return false;

                try
                {
                    if (!IsAltTabWindow(hWnd, sessionId))
                        return true;

                    var windowInfo = BuildWindowInfo(hWnd);
                    if (windowInfo != null)
                    {
                        windows.Add(windowInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug("Error enumerating window {0}: {1}", hWnd, ex.Message);
                }

                return true;
            }, IntPtr.Zero);

            _logger.Debug("Enumerated {0} Alt+Tab windows for session {1}", windows.Count, sessionId);
            return (IReadOnlyList<AppWindowInfo>)windows;
        }, ct);
    }

    /// <summary>
    /// Подсчитать количество окон Alt+Tab для заданной сессии.
    /// </summary>
    public async Task<int> CountAltTabWindowsAsync(int sessionId, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            int count = 0;

            User32Native.EnumWindows((hWnd, _) =>
            {
                if (ct.IsCancellationRequested) return false;

                try
                {
                    if (IsAltTabWindow(hWnd, sessionId))
                        count++;
                }
                catch
                {
                    // Игнорируем ошибки при подсчёте
                }

                return true;
            }, IntPtr.Zero);

            return count;
        }, ct);
    }

    /// <summary>
    /// Проверить, является ли окно видимым в Alt+Tab для заданной сессии.
    /// </summary>
    private bool IsAltTabWindow(IntPtr hWnd, int targetSessionId)
    {
        // 1. Принадлежит ли окно целевой сессии?
        var threadId = User32Native.GetWindowThreadProcessId(hWnd, out var pid);
        if (threadId == 0) return false;

        if (!User32Native.ProcessIdToSessionId(pid, out var windowSessionId))
            return false;

        if (windowSessionId != targetSessionId) return false;

        // 2. Видимо ли окно?
        if (!User32Native.IsWindowVisible(hWnd)) return false;

        // 3. Исключаем инструментальные окна (не показываются в Alt+Tab)
        var exStyle = User32Native.GetWindowLong(hWnd, User32Native.GWL_EXSTYLE);
        if ((exStyle & User32Native.WS_EX_TOOLWINDOW) != 0) return false;

        // 4. Есть ли заголовок?
        if (User32Native.GetWindowTextLength(hWnd) == 0) return false;

        // 5. Исключаем системные окна по классу
        var className = GetWindowClassName(hWnd);
        if (!string.IsNullOrEmpty(className) && ExcludedClasses.Contains(className))
            return false;

        return true;
    }

    /// <summary>
    /// Построить AppWindowInfo для окна.
    /// </summary>
    private static AppWindowInfo? BuildWindowInfo(IntPtr hWnd)
    {
        // Получаем заголовок
        var titleLength = User32Native.GetWindowTextLength(hWnd);
        if (titleLength == 0) return null;

        var sb = new StringBuilder(titleLength + 1);
        User32Native.GetWindowText(hWnd, sb, sb.Capacity);
        var title = sb.ToString();

        if (string.IsNullOrWhiteSpace(title))
            return null;

        // Получаем PID
        User32Native.GetWindowThreadProcessId(hWnd, out var pid);

        // Получаем имя процесса
        string? processName = null;
        string? processPath = null;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            processName = process.ProcessName;
            processPath = process.MainModule?.FileName;
        }
        catch
        {
            // Нет доступа к процессу — пропускаем путь
        }

        return new AppWindowInfo
        {
            Handle = hWnd,
            ProcessId = (int)pid,
            Title = title,
            ProcessName = processName,
            ProcessPath = processPath,
        };
    }

    /// <summary>
    /// Получить класс окна (имя класса окна).
    /// </summary>
    private static string? GetWindowClassName(IntPtr hWnd)
    {
        try
        {
            var sb = new StringBuilder(256);
            User32Native.GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch
        {
            return null;
        }
    }
}
}
=== /workspace/src/Services/CommandExecutor.cs ===
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MhTsManager.Services
{

/// <summary>
/// Интерфейс безопасного исполнителя внешних команд (mstsc, msg, tscon).
/// </summary>
public interface ICommandExecutor
{
    /// <summary>
    /// Удалённое подключение через mstsc /shadow.
    /// </summary>
    Task<bool> ShadowSessionAsync(int sessionId, CancellationToken ct);

    /// <summary>
    /// Отправить сообщение в сессию через msg.exe.
    /// </summary>
    Task<bool> SendMessageAsync(int sessionId, string message, CancellationToken ct);

    /// <summary>
    /// Открыть оснастку управления пользователями (lusrmgr.msc).
    /// </summary>
    Task<bool> OpenUserManagement(CancellationToken ct);
}

/// <summary>
/// Безопасный исполнитель внешних команд.
/// Все аргументы валидируются, запускаются только разрешённые исполняемые файлы.
/// </summary>
public sealed class CommandExecutor : ICommandExecutor
{
    private readonly Logger _logger;

    public CommandExecutor(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Удалённое подключение через mstsc /shadow:<SessionId> /control /noConsentPrompt.
    /// </summary>
    public async Task<bool> ShadowSessionAsync(int sessionId, CancellationToken ct)
    {
        _logger.Info("Initiating shadow session (RDP shadow) for session {0}", sessionId);

        // Валидация: SessionId должен быть положительным числом
        if (sessionId <= 0)
        {
            _logger.Warning("Invalid session ID for shadow: {0}", sessionId);
            return false;
        }

        // Формируем аргументы — только число, без инъекций
        var arguments = $"/shadow:{sessionId} /control /noConsentPrompt";

        var psi = new ProcessStartInfo
        {
            FileName = "mstsc.exe",
            Arguments = arguments,
            UseShellExecute = true, // true для GUI-приложений
            CreateNoWindow = false,
        };

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = psi.FileName,
                Arguments = psi.Arguments,
                UseShellExecute = psi.UseShellExecute
            });

            if (process == null)
            {
                _logger.Error("Failed to start mstsc.exe for session {0}", sessionId);
                return false;
            }

            _logger.Info("mstsc.exe started for session {0}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to launch mstsc.exe for session {0}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Отправить сообщение в сессию через msg.exe.
    /// </summary>
    public async Task<bool> SendMessageAsync(int sessionId, string message, CancellationToken ct)
    {
        _logger.Info("Sending message to session {0}", sessionId);

        // Валидация SessionId
        if (sessionId < 0)
        {
            _logger.Warning("Invalid session ID for message: {0}", sessionId);
            return false;
        }

        // Валидация сообщения: не пустое, макс. 1024 символа
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.Warning("Empty message for session {0}", sessionId);
            return false;
        }

        if (message.Length > 1024)
        {
            _logger.Warning("Message too long ({0} chars) for session {1}, truncating", message.Length, sessionId);
            message = message.Substring(0, 1024);
        }

        // Санитизация: экранирование специальных символов для командной строки
        var sanitizedMessage = message.Replace("\"", "\"\"\"").Replace("&", "^&").Replace("|", "^|");

        var psi = new ProcessStartInfo
        {
            FileName = "msg.exe",
            Arguments = $"/server:localhost {sessionId} \"{sanitizedMessage}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.Error("Failed to start msg.exe for session {0}", sessionId);
                return false;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("msg.exe timed out for session {0}", sessionId);
                process.Kill();
                return false;
            }

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.Warning("msg.exe exited with code {0}: {1}", process.ExitCode, stderr);
                return false;
            }

            _logger.Info("Message sent to session {0} successfully", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to launch msg.exe for session {0}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Открыть оснастку управления пользователями (lusrmgr.msc).
    /// </summary>
    public async Task<bool> OpenUserManagement(CancellationToken ct)
    {
        _logger.Info("Opening User Management console (lusrmgr.msc)");

        var psi = new ProcessStartInfo
        {
            FileName = "lusrmgr.msc",
            UseShellExecute = true,
            CreateNoWindow = false,
        };

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = psi.FileName,
                UseShellExecute = psi.UseShellExecute
            });

            if (process == null)
            {
                _logger.Error("Failed to start lusrmgr.msc");
                return false;
            }

            _logger.Info("lusrmgr.msc started");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to launch lusrmgr.msc");
            return false;
        }
    }
}
}
=== /workspace/src/Services/WtsSessionService.cs ===
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using MhTsManager.Models;

namespace MhTsManager.Services
{

/// <summary>
/// Интерфейс сервиса работы с пользовательскими сессиями через WTS API.
/// </summary>
public interface IWtsSessionService
{
    /// <summary>
    /// Перечислить все пользовательские сессии.
    /// </summary>
    Task<IReadOnlyList<SessionInfo>> EnumerateSessionsAsync(CancellationToken ct);

    /// <summary>
    /// Получить состояние конкретной сессии.
    /// </summary>
    Task<SessionState> GetSessionStateAsync(int sessionId, CancellationToken ct);

    /// <summary>
    /// Отключить сессию.
    /// </summary>
    Task<bool> DisconnectSessionAsync(int sessionId, CancellationToken ct);

    /// <summary>
    /// Завершить сессию (logoff).
    /// </summary>
    Task<bool> LogoffSessionAsync(int sessionId, CancellationToken ct);

    /// <summary>
    /// Переключиться на сессию (tscon).
    /// </summary>
    Task<bool> SwitchToSessionAsync(int sessionId, CancellationToken ct);

    /// <summary>
    /// Проверить, заблокирована ли сессия.
    /// </summary>
    bool IsSessionLocked(int sessionId);

    /// <summary>
    /// Проверить, запущено ли приложение с правами администратора.
    /// </summary>
    bool IsElevated { get; }
}

/// <summary>
/// SafeHandle для сервера WTS.
/// </summary>
internal sealed class SafeWtsServerHandle : SafeHandle
{
    public SafeWtsServerHandle(IntPtr handle) : base(handle, true) { }
    public SafeWtsServerHandle() : base(IntPtr.Zero, true) { }

    public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);

    protected override bool ReleaseHandle()
    {
        WtsNative.WTSCloseServer(handle);
        return true;
    }
}

/// <summary>
/// SafeHandle для памяти, выделенной WTS API.
/// </summary>
internal sealed class SafeWtsMemoryHandle : SafeHandle
{
    private SafeWtsMemoryHandle() : base(IntPtr.Zero, true) { }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        WtsNative.WTSFreeMemory(handle);
        return true;
    }
}

/// <summary>
/// Native-обёртка над WTS API (wtsapi32.dll).
/// </summary>
internal static class WtsNative
{
    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern IntPtr WTSOpenServer([MarshalAs(UnmanagedType.LPStr)] string pServerName);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern void WTSCloseServer(IntPtr hServer);

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool WTSEnumerateSessions(
        IntPtr hServer,
        int Reserved,
        int Version,
        out IntPtr ppSessionInfo,
        out int pCount);

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool WTSQuerySessionInformation(
        IntPtr hServer,
        int sessionId,
        WTS_INFO_CLASS wtsInfoClass,
        out IntPtr ppBuffer,
        out int pBytesReturned);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSDisconnectSession(
        IntPtr hServer,
        int sessionId,
        bool bWait);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSLogoffSession(
        IntPtr hServer,
        int sessionId,
        bool bWait);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DllImport("user32.dll")]
    public static extern bool GetThreadDesktop(uint dwThreadId, out IntPtr hDesktop);
}

/// <summary>
/// WTS_INFO_CLASS — типы информации о сессии.
/// </summary>
internal enum WTS_INFO_CLASS
{
    SessionId = 4,
    UserName = 5,
    SessionName = 6,
    DomainName = 7,
    ConnectState = 8,
    ClientBuildNumber = 9,
    ClientName = 10,
    ClientDirectory = 11,
    ClientProtocolType = 12,
    IsSessionLocked = 35,
    InitialProgram = 36
}

/// <summary>
/// WTS_CONNECTSTATE_CLASS — состояния сессии.
/// </summary>
internal enum WTS_CONNECTSTATE_CLASS
{
    Active,
    Connected,
    ConnectQuery,
    Shadow,
    Disconnected,
    Idle,
    Listen,
    Reset,
    Down,
    Init
}

/// <summary>
/// Структура WTS_SESSION_INFO (нативная).
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WTS_SESSION_INFO
{
    public int SessionId;
    [MarshalAs(UnmanagedType.LPStr)]
    public string? pWinStationName;
    public WTS_CONNECTSTATE_CLASS State;
}

/// <summary>
/// Сервис работы с пользовательскими сессиями через WTS API.
/// Все P/Invoke вызовы обёрнуты в SafeHandle для автоматической очистки ресурсов.
/// </summary>
public sealed class WtsSessionService : IWtsSessionService
{
    private readonly Logger _logger;
    private readonly object _lock = new();
    private SafeWtsServerHandle? _serverHandle;
    private readonly Dictionary<int, SessionInfo> _sessionCache = new();
    private DateTime _lastRefresh = DateTime.MinValue;

    public WtsSessionService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Запущено ли приложение с правами администратора.
    /// </summary>
    public bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>
    /// Получить или создать хендл сервера WTS.
    /// </summary>
    private SafeWtsServerHandle GetServerHandle()
    {
        if (_serverHandle != null && !_serverHandle.IsInvalid)
            return _serverHandle;

        var handle = WtsNative.WTSOpenServer(Environment.MachineName);
        _serverHandle = new SafeWtsServerHandle(handle);
        return _serverHandle;
    }

    /// <summary>
    /// Перечислить все пользовательские сессии.
    /// </summary>
    public async Task<IReadOnlyList<SessionInfo>> EnumerateSessionsAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var sessions = new List<SessionInfo>();
                var server = GetServerHandle();

                // WTSEnumerateSessions
                var result = WtsNative.WTSEnumerateSessions(
                    server.DangerousGetHandle(),
                    0,
                    1,
                    out var pSessionInfo,
                    out var count);

                if (!result || pSessionInfo == IntPtr.Zero)
                {
                    var ex = new Win32Exception(Marshal.GetLastWin32Error());
                    _logger.Error("WTSEnumerateSessions failed: {0}", ex.Message);
                    return (IReadOnlyList<SessionInfo>)sessions;
                }

                try
                {
                    var structSize = Marshal.SizeOf<WTS_SESSION_INFO>();
                    for (var i = 0; i < count; i++)
                    {
                        if (ct.IsCancellationRequested) break;

                        var currentPtr = IntPtr.Add(pSessionInfo, i * structSize);
                        var info = Marshal.PtrToStructure<WTS_SESSION_INFO>(currentPtr);

                        // Пропускаем сессии без имени
                        if (string.IsNullOrEmpty(info.pWinStationName))
                            continue;

                        var sessionInfo = BuildSessionInfo(server, info);
                        if (sessionInfo != null)
                        {
                            sessions.Add(sessionInfo);
                            _sessionCache[sessionInfo.Id] = sessionInfo;
                        }
                    }
                }
                finally
                {
                    WtsNative.WTSFreeMemory(pSessionInfo);
                }

                _lastRefresh = DateTime.Now;
                _logger.Debug("Enumerated {0} sessions", sessions.Count);
                return sessions;
            }
        }, ct);
    }

    /// <summary>
    /// Построить объект SessionInfo из нативной структуры.
    /// </summary>
    private SessionInfo? BuildSessionInfo(SafeWtsServerHandle server, WTS_SESSION_INFO nativeInfo)
    {
        try
        {
            var userName = QuerySessionString(server, nativeInfo.SessionId, WTS_INFO_CLASS.UserName);
            var domainName = QuerySessionString(server, nativeInfo.SessionId, WTS_INFO_CLASS.DomainName);
            var clientName = QuerySessionString(server, nativeInfo.SessionId, WTS_INFO_CLASS.ClientName);

            if (string.IsNullOrEmpty(userName))
                return null; // Системная сессия, пропускаем

            var state = MapSessionState(nativeInfo.State);
            var isLocked = IsSessionLocked(nativeInfo.SessionId);

            return new SessionInfo
            {
                Id = nativeInfo.SessionId,
                UserName = string.IsNullOrEmpty(domainName)
                    ? userName
                    : $"{domainName}\\{userName}",
                FullName = null, // Заполняется отдельно через Win32 NetApi или SAM
                ClientName = string.IsNullOrEmpty(clientName) ? null : clientName,
                State = state,
                Type = nativeInfo.SessionId == 0 ? SessionType.Console : SessionType.Remote,
                ApplicationCount = 0, // Заполняется SessionAppStateService
                IsLocked = isLocked,
                IsScreensaverActive = false, // Заполняется SessionAppStateService
                CpuPercent = 0,
                MemoryBytes = 0,
                DiskBytesPerSec = 0,
                NetworkBytesPerSec = 0,
                GpuPercent = 0,
                StateChangedTime = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to build session info for session {0}", nativeInfo.SessionId);
            return null;
        }
    }

    /// <summary>
    /// Запросить строковую информацию о сессии.
    /// </summary>
    private static string? QuerySessionString(SafeWtsServerHandle server, int sessionId, WTS_INFO_CLASS infoClass)
    {
        var result = WtsNative.WTSQuerySessionInformation(
            server.DangerousGetHandle(),
            sessionId,
            infoClass,
            out var pBuffer,
            out var bytesReturned);

        if (!result || pBuffer == IntPtr.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUni(pBuffer);
        }
        finally
        {
            WtsNative.WTSFreeMemory(pBuffer);
        }
    }

    /// <summary>
    /// Преобразование нативного состояния сессии в наше перечисление.
    /// </summary>
    private static SessionState MapSessionState(WTS_CONNECTSTATE_CLASS nativeState)
    {
        return nativeState switch
        {
            WTS_CONNECTSTATE_CLASS.Active => SessionState.Active,
            WTS_CONNECTSTATE_CLASS.Connected => SessionState.Active,
            WTS_CONNECTSTATE_CLASS.ConnectQuery => SessionState.Active,
            WTS_CONNECTSTATE_CLASS.Shadow => SessionState.Active,
            WTS_CONNECTSTATE_CLASS.Disconnected => SessionState.Disconnected,
            WTS_CONNECTSTATE_CLASS.Idle => SessionState.Idle,
            WTS_CONNECTSTATE_CLASS.Listen or
            WTS_CONNECTSTATE_CLASS.Reset or
            WTS_CONNECTSTATE_CLASS.Down or
            WTS_CONNECTSTATE_CLASS.Init => SessionState.Unavailable,
            _ => SessionState.Unavailable
        };
    }

    /// <summary>
    /// Получить состояние конкретной сессии.
    /// </summary>
    public async Task<SessionState> GetSessionStateAsync(int sessionId, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var server = GetServerHandle();
                var stateStr = QuerySessionString(server, sessionId, WTS_INFO_CLASS.ConnectState);

                if (!string.IsNullOrEmpty(stateStr) && Enum.TryParse<WTS_CONNECTSTATE_CLASS>(stateStr, out var nativeState))
                    return MapSessionState(nativeState);

                return SessionState.Unavailable;
            }
        }, ct);
    }

    /// <summary>
    /// Отключить сессию.
    /// </summary>
    public async Task<bool> DisconnectSessionAsync(int sessionId, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            _logger.Info("Attempting to disconnect session {0}", sessionId);

            // Идемпотентность: проверяем текущее состояние
            if (_sessionCache.TryGetValue(sessionId, out var cached) && cached.State == SessionState.Disconnected)
            {
                _logger.Info("Session {0} already disconnected, skipping", sessionId);
                return true;
            }

            var server = GetServerHandle();
            var result = WtsNative.WTSDisconnectSession(
                server.DangerousGetHandle(),
                sessionId,
                bWait: false);

            if (!result)
            {
                var ex = new Win32Exception(Marshal.GetLastWin32Error());
                _logger.Warning("WTSDisconnectSession failed for session {0}: {1}", sessionId, ex.Message);
                return false;
            }

            _logger.Info("Session {0} disconnected successfully", sessionId);
            return true;
        }, ct);
    }

    /// <summary>
    /// Завершить сессию (logoff).
    /// </summary>
    public async Task<bool> LogoffSessionAsync(int sessionId, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            _logger.Info("Attempting to logoff session {0}", sessionId);

            var server = GetServerHandle();
            var result = WtsNative.WTSLogoffSession(
                server.DangerousGetHandle(),
                sessionId,
                bWait: false);

            if (!result)
            {
                var ex = new Win32Exception(Marshal.GetLastWin32Error());
                _logger.Warning("WTSLogoffSession failed for session {0}: {1}", sessionId, ex.Message);
                return false;
            }

            _logger.Info("Session {0} logged off successfully", sessionId);
            return true;
        }, ct);
    }

    /// <summary>
    /// Переключиться на сессию (через tscon).
    /// </summary>
    public async Task<bool> SwitchToSessionAsync(int sessionId, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            _logger.Info("Attempting to switch to session {0}", sessionId);

            var psi = new ProcessStartInfo
            {
                FileName = "tscon.exe",
                Arguments = $"{sessionId} /dest:console",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            try
            {
                using var process = Process.Start(psi);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to switch to session {0}", sessionId);
                return false;
            }
        }, ct);
    }

    /// <summary>
    /// Проверить, заблокирована ли сессия (через WTSIsSessionLocked).
    /// </summary>
    public bool IsSessionLocked(int sessionId)
    {
        try
        {
            var server = GetServerHandle();
            var result = WtsNative.WTSQuerySessionInformation(
                server.DangerousGetHandle(),
                sessionId,
                WTS_INFO_CLASS.IsSessionLocked,
                out var pBuffer,
                out _);

            if (!result || pBuffer == IntPtr.Zero)
                return false;

            try
            {
                return Marshal.ReadByte(pBuffer) != 0;
            }
            finally
            {
                WtsNative.WTSFreeMemory(pBuffer);
            }
        }
        catch
        {
            return false;
        }
    }
}
}
=== /workspace/src/mh-ts-manager.csproj ===
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    
    <!-- Assembly info -->
    <AssemblyName>mh-ts-manager</AssemblyName>
    <RootNamespace>MhTsManager</RootNamespace>
    <Version>1.4.0.0</Version>
    <Product>Диспетчер пользовательских сессий</Product>
    <Copyright>© 2026 Коваленко Павел</Copyright>

    <!-- Build settings -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>14</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Microsoft-WindowsAPICodePack-Shell" Version="1.1.5" />
  </ItemGroup>

</Project>
=== /workspace/src/App.xaml.cs ===
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
                SettingsService.CreateDefaultSettings();
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
=== /workspace/src/Converters/StringToColorConverter.cs ===
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MhTsManager.Converters
{
    /// <summary>
    /// Конвертер текста статуса в цвет индикатора.
    /// </summary>
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                "Активна" or "Active" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),     // 🟢
                "Бездействие" or "Idle" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // 🟡
                "Отключена" or "Disconnected" => new SolidColorBrush(Color.FromRgb(107, 114, 128)), // ⚪
                "Заблокирована" or "Locked" => new SolidColorBrush(Color.FromRgb(99, 102, 241)),   // 🔵
                "Недоступна" or "Unavailable" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),   // 🔴
                _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
=== /workspace/src/ViewModels/SessionViewModel.cs ===
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
=== /workspace/src/ViewModels/MainViewModel.cs ===
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
=== /workspace/src/Views/MainWindow.xaml ===
<Window x:Class="MhTsManager.Views.MainWindow"
        Loaded="Window_Loaded"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:MhTsManager.ViewModels"
        xmlns:converters="clr-namespace:MhTsManager.Converters"
        Title="Диспетчер пользовательских сессий"
        Width="1024" Height="680"
        MinWidth="800" MinHeight="500"
        WindowStartupLocation="CenterScreen"
        Background="{StaticResource BackgroundBrush}">

    <Window.Resources>
        <!-- Конвертер для цвета статуса -->
        <converters:StringToColorConverter x:Key="StatusToColorConverter"/>
        <!-- Конвертер Boolean to Visibility -->
        <BooleanToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Toolbar -->
        <Border Grid.Row="0" Background="{StaticResource CardBackgroundBrush}"
                BorderBrush="{StaticResource BorderBrush}" BorderThickness="0,0,0,1" Padding="16,12">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Левая часть: заголовок -->
                <StackPanel Grid.Column="0" Orientation="Horizontal" VerticalAlignment="Center">
                    <TextBlock Text="{Binding UsersTitle}" Style="{StaticResource TitleTextStyle}"
                               Margin="0,0,8,0"/>
                    <TextBlock x:Name="LoadingIndicator" Text="⟳" Foreground="{StaticResource AccentBrush}"
                               FontSize="16" 
                               Visibility="{Binding IsLoading, Converter={StaticResource BoolToVisibilityConverter}}"
                               Margin="8,0,0,0">
                        <TextBlock.RenderTransform>
                            <RotateTransform x:Name="LoadingRotation" Angle="0" CenterX="8" CenterY="8"/>
                        </TextBlock.RenderTransform>
                    </TextBlock>
                </StackPanel>

                <!-- Правая часть: кнопки -->
                <StackPanel Grid.Column="1" Orientation="Horizontal">
                    <!-- Кнопка-замок -->
                    <Button Style="{StaticResource FluentButtonStyle}" Margin="4,0"
                            Command="{Binding ToggleAdminModeCommand}"
                            ToolTip="{Binding AdminModeTooltip}">
                        <TextBlock Text="{Binding AdminModeIcon}" FontSize="14"/>
                    </Button>

                    <!-- Подключить -->
                    <Button Style="{StaticResource FluentButtonStyle}" Margin="4,0"
                            Command="{Binding ConnectSelectedCommand}"
                            ToolTip="Подключить к выбранной сессии">
                        <TextBlock Text="🔗" FontSize="14"/>
                    </Button>

                    <!-- Отправить сообщение -->
                    <Button Style="{StaticResource FluentButtonStyle}" Margin="4,0"
                            Command="{Binding OpenSendMessageDialogCommand}"
                            ToolTip="Отправить сообщение">
                        <TextBlock Text="✉" FontSize="14"/>
                    </Button>

                    <!-- Меню ⋮ -->
                    <Menu VerticalAlignment="Center">
                        <MenuItem Style="{DynamicResource FluentMenuItemStyle}">
                            <MenuItem.Header>
                                <TextBlock Text="⋮" FontSize="16"/>
                            </MenuItem.Header>
                            <MenuItem Header="Развернуть все" Click="ExpandAll_Click"/>
                            <MenuItem Header="Свернуть все" Click="CollapseAll_Click"/>
                            <Separator/>
                            <MenuItem Header="Управление пользователями"
                                      Command="{Binding OpenUserManagementCommand}"/>
                            <Separator/>
                            <MenuItem Header="О программе"
                                      Command="{Binding OpenAboutDialogCommand}"/>
                        </MenuItem>
                    </Menu>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Таблица сессий -->
        <ListView Grid.Row="1" x:Name="SessionsListView"
                  ItemsSource="{Binding Sessions}"
                  SelectedItem="{Binding SelectedSession}"
                  ItemContainerStyle="{StaticResource SessionListViewItemStyle}"
                  BorderThickness="0"
                  Background="Transparent"
                  VirtualizingStackPanel.IsVirtualizing="True"
                  VirtualizingStackPanel.VirtualizationMode="Recycling">

            <ListView.View>
                <GridView>
                    <GridViewColumn Header="Пользователь" Width="280">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="4,2">
                                    <!-- Композитная ячейка пользователя -->
                                    <TextBlock Text="{Binding SessionInfo.FormattedUserString}"
                                               Style="{StaticResource TableTextStyle}"
                                               TextTrimming="CharacterEllipsis"/>
                                </StackPanel>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>

                    <GridViewColumn Header="Состояние" Width="100">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                                    <!-- Индикатор статуса -->
                                    <Ellipse Width="8" Height="8" Margin="0,0,6,0"
                                             Fill="{Binding StatusText, Converter={StaticResource StatusToColorConverter}}"/>
                                    <TextBlock Text="{Binding StatusText}" Style="{StaticResource TableTextStyle}"/>
                                </StackPanel>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>

                    <GridViewColumn Header="ЦП" Width="60">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding CpuText}" Style="{StaticResource TableTextStyle}"
                                           HorizontalAlignment="Right"/>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>

                    <GridViewColumn Header="Память" Width="80">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding MemoryText}" Style="{StaticResource TableTextStyle}"
                                           HorizontalAlignment="Right"/>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>

                    <GridViewColumn Header="Диск" Width="80">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding DiskText}" Style="{StaticResource TableTextStyle}"
                                           HorizontalAlignment="Right"/>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>

                    <GridViewColumn Header="Сеть" Width="80">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding NetworkText}" Style="{StaticResource TableTextStyle}"
                                           HorizontalAlignment="Right"/>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>

                    <GridViewColumn Header="GPU" Width="60">
                        <GridViewColumn.CellTemplate>
                            <DataTemplate>
                                <TextBlock Text="{Binding SessionInfo.GpuPercent, StringFormat={}{0:F1}%}"
                                           Style="{StaticResource TableTextStyle}"
                                           HorizontalAlignment="Right"/>
                            </DataTemplate>
                        </GridViewColumn.CellTemplate>
                    </GridViewColumn>
                </GridView>
            </ListView.View>

            <!-- Контекстное меню сессии (правый клик) -->
            <ListView.ContextMenu>
                <ContextMenu>
                    <MenuItem Header="Развернуть" Click="ExpandSession_Click"/>
                    <MenuItem Header="Свернуть" Click="CollapseSession_Click"/>
                    <Separator/>
                    <MenuItem Header="🔗 Подключить" Command="{Binding ConnectCommand}"/>
                    <MenuItem Header="🔄 Переключиться" Command="{Binding SwitchCommand}"/>
                    <MenuItem Header="🔌 Отключить" Command="{Binding DisconnectCommand}"/>
                    <MenuItem Header="🚪 Выйти" Command="{Binding LogoffCommand}"/>
                    <Separator/>
                    <MenuItem Header="✉ Отправить сообщение..."/>
                </ContextMenu>
            </ListView.ContextMenu>

            <!-- Двойной клик → подключиться -->
            <ListView.InputBindings>
                <MouseBinding Gesture="LeftDoubleClick" Command="{Binding ConnectSelectedCommand}"/>
            </ListView.InputBindings>
        </ListView>

        <!-- Status Bar -->
        <Border Grid.Row="2" Background="{StaticResource CardBackgroundBrush}"
                BorderBrush="{StaticResource BorderBrush}" BorderThickness="0,1,0,0" Padding="16,6">
            <TextBlock x:Name="StatusBarText" Text="Готово"
                       Style="{StaticResource SubtitleTextStyle}"/>
        </Border>
    </Grid>
</Window>
=== /workspace/src/Views/Styles/FluentStyles.xaml ===
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Шрифты -->
    <FontFamily x:Key="DefaultFont">Segoe UI, Segoe UI Variable</FontFamily>

    <!-- Цвета Fluent Design -->
    <Color x:Key="AccentColor">#0078D4</Color>
    <Color x:Key="BackgroundColor">#F3F3F3</Color>
    <Color x:Key="CardBackgroundColor">#FFFFFF</Color>
    <Color x:Key="TextPrimaryColor">#1A1A1A</Color>
    <Color x:Key="TextSecondaryColor">#616161</Color>
    <Color x:Key="BorderBrushColor">#E5E5E5</Color>

    <!-- Кисти -->
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}"/>
    <SolidColorBrush x:Key="BackgroundBrush" Color="{StaticResource BackgroundColor}"/>
    <SolidColorBrush x:Key="CardBackgroundBrush" Color="{StaticResource CardBackgroundColor}"/>
    <SolidColorBrush x:Key="TextPrimaryBrush" Color="{StaticResource TextPrimaryColor}"/>
    <SolidColorBrush x:Key="TextSecondaryBrush" Color="{StaticResource TextSecondaryColor}"/>
    <SolidColorBrush x:Key="BorderBrush" Color="{StaticResource BorderBrushColor}"/>

    <!-- Скругления -->
    <CornerRadius x:Key="ControlCornerRadius">4,4,4,4</CornerRadius>
    <CornerRadius x:Key="LargeCornerRadius">8,8,8,8</CornerRadius>

    <!-- Стиль заголовка окна -->
    <Style x:Key="TitleTextStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource DefaultFont}"/>
        <Setter Property="FontSize" Value="20"/>
        <Setter Property="FontWeight" Value="Semibold"/>
        <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    </Style>

    <!-- Стиль подзаголовка -->
    <Style x:Key="SubtitleTextStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource DefaultFont}"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="FontWeight" Value="Normal"/>
        <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
    </Style>

    <!-- Стиль текста таблицы -->
    <Style x:Key="TableTextStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource DefaultFont}"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
    </Style>

    <!-- Стиль заголовка столбца -->
    <Style x:Key="ColumnHeaderStyle" TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{StaticResource DefaultFont}"/>
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="FontWeight" Value="Semibold"/>
        <Setter Property="Foreground" Value="{StaticResource TextSecondaryBrush}"/>
    </Style>

    <!-- Стиль кнопки Fluent -->
    <Style x:Key="FluentButtonStyle" TargetType="Button">
        <Setter Property="FontFamily" Value="{StaticResource DefaultFont}"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="Padding" Value="12,6"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
        <Setter Property="Background" Value="{StaticResource CardBackgroundBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="{StaticResource ControlCornerRadius}"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="#E9ECEF"/>
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- Стиль ListViewItem -->
    <Style x:Key="SessionListViewItemStyle" TargetType="ListViewItem">
        <Setter Property="Padding" Value="0"/>
        <Setter Property="Margin" Value="0"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
    </Style>

    <!-- Стиль MenuItem Fluent -->
    <Style x:Key="FluentMenuItemStyle" TargetType="MenuItem">
        <Setter Property="FontFamily" Value="{StaticResource DefaultFont}"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="Padding" Value="12,8"/>
        <Setter Property="Background" Value="{StaticResource CardBackgroundBrush}"/>
        <Setter Property="Foreground" Value="{StaticResource TextPrimaryBrush}"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="MenuItem">
                    <Border Background="{TemplateBinding Background}"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter Content="{TemplateBinding Header}"/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="#E9ECEF"/>
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- Анимация вращения для индикатора загрузки -->
    <Storyboard x:Key="LoadingAnimation" RepeatBehavior="Forever">
        <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(RotateTransform.Angle)"
                         From="0" To="360" Duration="0:0:1"/>
    </Storyboard>

</ResourceDictionary>
=== /workspace/src/Views/MainWindow.xaml.cs ===
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MhTsManager.Services;
using MhTsManager.ViewModels;

namespace MhTsManager.Views
{
    /// <summary>
    /// Главное окно приложения.
    /// </summary>
    public partial class MainWindow : Window
    {
        // Инициализируется в конструкторе, оператор ! подавляет ложное предупреждение CS8602
        private readonly MainViewModel _viewModel = null!;
        // Инициализируется в конструкторе, оператор ! подавляет ложное предупреждение CS8602
        private readonly Logger _logger = null!;

        public MainWindow(MainViewModel viewModel, Logger logger)
        {
            _logger = logger;
            _logger.Info("=== MAINWINDOW CONSTRUCTOR BEGIN ===");
            _logger.Info("Constructor called with ViewModel: {0}, Logger: {1}", 
                viewModel?.GetType().Name ?? "null", 
                logger?.GetType().Name ?? "null");

            try
            {
                _logger.Info("Step 1: Calling InitializeComponent()...");
                _logger.Info("  - Current thread: {0}", System.Threading.Thread.CurrentThread.ManagedThreadId);
                _logger.Info("  - Thread is STA: {0}", System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA);
                
                InitializeComponent();
                _logger.Info("Step 1 COMPLETE: InitializeComponent() completed successfully");
                _logger.Info("  - Window Handle (after InitializeComponent): {0}", 
                    new System.Windows.Interop.WindowInteropHelper(this).Handle);

                _viewModel = viewModel!;
                _logger.Info("Step 2: Setting DataContext to MainViewModel...");
                DataContext = _viewModel;
                _logger.Info("Step 2 COMPLETE: DataContext set successfully. DataContext type: {0}", DataContext?.GetType().Name ?? "null");

                // Привязка Title к ViewModel
                _logger.Info("Step 3: Setting up Title binding...");
                SetBinding(TitleProperty, new Binding("WindowTitle"));
                _logger.Info("Step 3 COMPLETE: Title binding established. Current Title: {0}", Title);

                // Проверка ресурсов после InitializeComponent
                _logger.Info("Step 4: Checking resources...");
                _logger.Info("  - Resources count: {0}", Resources.Count);
                _logger.Info("  - MergedDictionaries count: {0}", 
                    Application.Current?.Resources.MergedDictionaries.Count ?? -1);
                
                foreach (var key in Resources.Keys)
                {
                    _logger.Info("  - Resource key: {0}, Type: {1}", 
                        key?.ToString() ?? "(null)", 
                        Resources[key]?.GetType().Name ?? "(null)");
                }

                // Запуск анимации загрузки
                _logger.Info("Step 5: Looking for LoadingAnimation resource...");
                var loadingAnimation = TryFindResource("LoadingAnimation") as Storyboard;
                _logger.Info("  - LoadingAnimation found: {0}", loadingAnimation != null ? "YES" : "NO");
                _logger.Info("  - LoadingIndicator control: {0}", LoadingIndicator != null ? "FOUND" : "NULL");
                
                if (loadingAnimation != null && LoadingIndicator != null)
                {
                    _logger.Info("Step 5a: Setting up rotation animation...");
                    var rotateTransform = LoadingIndicator.RenderTransform as RotateTransform;
                    if (rotateTransform == null)
                    {
                        _logger.Info("  - Creating new RotateTransform...");
                        rotateTransform = new RotateTransform(0);
                        LoadingIndicator.RenderTransform = rotateTransform;
                    }
                    Storyboard.SetTarget(loadingAnimation, LoadingIndicator);
                    _logger.Info("  - Starting animation...");
                    loadingAnimation.Begin();
                    _logger.Info("Step 5a COMPLETE: Loading animation started successfully");
                }
                else
                {
                    _logger.Warning("Step 5 SKIPPED: LoadingAnimation resource not found or LoadingIndicator is null. Animation: {0}, Indicator: {1}",
                        loadingAnimation == null ? "null" : "found",
                        LoadingIndicator == null ? "null" : "found");
                }

                // Убеждаемся, что окно видимо и активно
                _logger.Info("Step 6: Setting ShowActivated = true and Visibility = Visible");
                this.ShowActivated = true;
                this.Visibility = Visibility.Visible;
                
                _logger.Info("Step 6 COMPLETE: Properties set");
                _logger.Info("  - ShowActivated: {0}", this.ShowActivated);
                _logger.Info("  - Visibility: {0}", this.Visibility);
                _logger.Info("  - IsVisible: {0}", this.IsVisible);
                _logger.Info("  - IsActive: {0}", this.IsActive);
                _logger.Info("  - Width: {0}, Height: {1}", this.Width, this.Height);
                _logger.Info("  - MinWidth: {0}, MinHeight: {1}", this.MinWidth, this.MinHeight);
                _logger.Info("  - WindowStyle: {0}", this.WindowStyle);
                _logger.Info("  - AllowsTransparency: {0}", this.AllowsTransparency);
                _logger.Info("  - Background: {0}", this.Background?.ToString() ?? "null");
                
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                _logger.Info("  - Window Handle: {0}", helper.Handle);
                
                _logger.Info("=== MAINWINDOW CONSTRUCTOR COMPLETE ===");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "❌ ERROR during MainWindow initialization");
                _logger.Error("Exception type: {0}", ex.GetType().FullName);
                _logger.Error("Message: {0}", ex.Message);
                _logger.Error("Stack trace: {0}", ex.StackTrace);
                if (ex.InnerException != null)
                {
                    _logger.Error("Inner exception: {0}", ex.InnerException);
                    _logger.Error("Inner exception stack trace: {0}", ex.InnerException.StackTrace);
                }
                
                // Логируем все детали ресурсов для диагностики
                try
                {
                    _logger.Info("Diagnostic info at failure point:");
                    _logger.Info("  - Application.Current: {0}", Application.Current != null ? "YES" : "NO");
                    if (Application.Current != null)
                    {
                        _logger.Info("  - Application.Resources count: {0}", Application.Current.Resources.Count);
                        _logger.Info("  - Application.MergedDictionaries count: {0}", Application.Current.Resources.MergedDictionaries.Count);
                    }
                }
                catch (Exception diagEx)
                {
                    _logger.Error(diagEx, "Failed to collect diagnostic info");
                }
                
                throw;
            }
        }

        /// <summary>
        /// Развернуть все сессии.
        /// </summary>
        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            _logger.Info("ExpandAll_Click triggered");
            foreach (var vm in _viewModel.Sessions)
                vm.IsExpanded = true;
        }

        /// <summary>
        /// Свернуть все сессии.
        /// </summary>
        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            _logger.Info("CollapseAll_Click triggered");
            foreach (var vm in _viewModel.Sessions)
                vm.IsExpanded = false;
        }

        /// <summary>
        /// Развернуть выбранную сессию.
        /// </summary>
        private void ExpandSession_Click(object sender, RoutedEventArgs e)
        {
            _logger.Info("ExpandSession_Click triggered");
            if (_viewModel.SelectedSession != null)
                _viewModel.SelectedSession.IsExpanded = true;
        }

        /// <summary>
        /// Свернуть выбранную сессию.
        /// </summary>
        private void CollapseSession_Click(object sender, RoutedEventArgs e)
        {
            _logger.Info("CollapseSession_Click triggered");
            if (_viewModel.SelectedSession != null)
                _viewModel.SelectedSession.IsExpanded = false;
        }
        
        /// <summary>
        /// Обработчик события Loaded для дополнительной диагностики
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _logger.Info("=== WINDOW LOADED EVENT ===");
            _logger.Info("  - IsVisible: {0}", this.IsVisible);
            _logger.Info("  - IsActive: {0}", this.IsActive);
            _logger.Info("  - ActualWidth: {0}, ActualHeight: {1}", this.ActualWidth, this.ActualHeight);
            _logger.Info("  - RenderSize: {0}x{1}", this.RenderSize.Width, this.RenderSize.Height);
            
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            _logger.Info("  - Handle: {0}", helper.Handle);
            
            // Проверяем, есть ли дочерние элементы
            if (this.Content is Grid grid)
            {
                _logger.Info("  - Content is Grid with {0} children", grid.Children.Count);
            }
            else
            {
                _logger.Info("  - Content type: {0}", this.Content?.GetType().Name ?? "null");
            }
        }
    }
}
=== /workspace/src/App.xaml ===
<Application x:Class="MhTsManager.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Views/Styles/FluentStyles.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
=== /workspace/src/mh-ts-manager.sln ===

Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.8.0.0
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "mh-ts-manager", "mh-ts-manager.csproj", "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
=== /workspace/src/Models/AppWindowInfo.cs ===
namespace MhTsManager.Models
{

/// <summary>
/// DTO информации об окне приложения (видимом в Alt+Tab).
/// </summary>
public sealed record AppWindowInfo
{
    /// <summary>Хендл окна.</summary>
    public required IntPtr Handle { get; init; }

    /// <summary>ID процесса, которому принадлежит окно.</summary>
    public required int ProcessId { get; init; }

    /// <summary>Заголовок окна.</summary>
    public required string Title { get; init; }

    /// <summary>Полный путь к исполняемому файлу процесса.</summary>
    public string? ProcessPath { get; init; }

    /// <summary>Имя процесса (без .exe).</summary>
    public string? ProcessName { get; init; }

    /// <summary>Иконка приложения (извлекается лениво).</summary>
    public System.Drawing.Icon? Icon { get; init; }

    /// <summary>
    /// Краткое отображение: "🪟 Заголовок" или "🪟 ProcessName — Заголовок".
    /// </summary>
    public string DisplayString
    {
        get
        {
            if (!string.IsNullOrEmpty(ProcessName) && ProcessName != Title)
                return $"\uD83E\uDE9F {ProcessName} \u2014 {Title}";
            return $"\uD83E\uDE9F {Title}";
        }
    }
}
}
=== /workspace/src/Models/UserCellData.cs ===
namespace MhTsManager.Models
{

/// <summary>
/// Данные для композитной ячейки «Пользователь» в таблице сессий.
/// Используется для отображения в UserCellControl.
/// </summary>
public sealed record UserCellData
{
    /// <summary>Имя учётной записи (логин).</summary>
    public required string Login { get; init; }

    /// <summary>Полное имя пользователя.</summary>
    public string? FullName { get; init; }

    /// <summary>Количество приложений в сессии (Alt+Tab).</summary>
    public int AppCount { get; init; }

    /// <summary>Сессия заблокирована.</summary>
    public bool IsLocked { get; init; }

    /// <summary>Хранитель экрана активен.</summary>
    public bool IsScreensaverActive { get; init; }

    /// <summary>
    /// Основная строка: "Login — FullName (AppCount)".
    /// Если FullName отсутствует: "Login — (AppCount)".
    /// </summary>
    public string MainText
    {
        get
        {
            var name = string.IsNullOrEmpty(FullName) ? Login : FullName;
            return $"{Login} \u2014 {name} ({AppCount})";
        }
    }

    /// <summary>
    /// Строка иконок: "🔒", "🖥️", "🔒🖥️" или пустая.
    /// </summary>
    public string IconsText
    {
        get
        {
            var icons = "";
            if (IsLocked) icons += "\uD83D\uDD12";
            if (IsScreensaverActive) icons += "\uD83D\uDDA5\uFE0F";
            return icons;
        }
    }

    /// <summary>
    /// Tooltip при наведении на иконку замка.
    /// </summary>
    public string? LockTooltip => IsLocked ? "Сессия заблокирована" : null;

    /// <summary>
    /// Tooltip при наведении на иконку хранителя экрана.
    /// </summary>
    public string? ScreensaverTooltip => IsScreensaverActive ? "Активен хранитель экрана" : null;

    /// <summary>
    /// Tooltip при наведении на счётчик приложений.
    /// </summary>
    public string AppCountTooltip => $"Приложений в сессии: {AppCount}";
}
}
=== /workspace/src/Models/SessionInfo.cs ===
namespace MhTsManager.Models
{

/// <summary>
/// Состояние пользовательской сессии (аналог WTS_CONNECTSTATE_CLASS).
/// </summary>
public enum SessionState
{
    /// <summary>Активная сессия (пользователь работает).</summary>
    Active,

    /// <summary>Сессия в режиме бездействия.</summary>
    Idle,

    /// <summary>Отключённая сессия (RDP disconnected).</summary>
    Disconnected,

    /// <summary>Заблокированная сессия.</summary>
    Locked,

    /// <summary>Сессия недоступна.</summary>
    Unavailable
}

/// <summary>
/// Тип сессии.
/// </summary>
public enum SessionType
{
    /// <summary>Локальная консольная сессия.</summary>
    Console,

    /// <summary>Удалённая сессия (RDP).</summary>
    Remote,

    /// <summary>Неизвестный тип.</summary>
    Unknown
}

/// <summary>
/// DTO информации о пользовательской сессии (immutable record).
/// </summary>
public sealed record SessionInfo
{
    /// <summary>ID сессии (SessionId из WTS API).</summary>
    public required int Id { get; init; }

    /// <summary>Имя пользователя (SAM-имя или UPN).</summary>
    public required string UserName { get; init; }

    /// <summary>Полное имя пользователя (FullName / DisplayName).</summary>
    public string? FullName { get; init; }

    /// <summary>Имя удалённого клиента (для RDP-сессий).</summary>
    public string? ClientName { get; init; }

    /// <summary>Состояние сессии.</summary>
    public required SessionState State { get; init; }

    /// <summary>Тип сессии (Console / Remote).</summary>
    public SessionType Type { get; init; }

    /// <summary>Количество приложений (окон Alt+Tab) в сессии.</summary>
    public int ApplicationCount { get; init; }

    /// <summary>Признак заблокированной сессии.</summary>
    public bool IsLocked { get; init; }

    /// <summary>Признак активного хранителя экрана.</summary>
    public bool IsScreensaverActive { get; init; }

    /// <summary>Использование CPU (%).</summary>
    public double CpuPercent { get; init; }

    /// <summary>Использование памяти (байты).</summary>
    public long MemoryBytes { get; init; }

    /// <summary>Дисковая активность (байты/сек).</summary>
    public long DiskBytesPerSec { get; init; }

    /// <summary>Сетевая активность (байты/сек).</summary>
    public long NetworkBytesPerSec { get; init; }

    /// <summary>Использование GPU (%).</summary>
    public double GpuPercent { get; init; }

    /// <summary>Имя движка GPU.</summary>
    public string? GpuEngineName { get; init; }

    /// <summary>Время последнего изменения состояния.</summary>
    public DateTime StateChangedTime { get; init; }

    /// <summary>
    /// Форматированная строка пользователя: "Login — FullName (N) 🔒🖥️"
    /// </summary>
    public string FormattedUserString
    {
        get
        {
            var name = string.IsNullOrEmpty(FullName) ? UserName : FullName;
            var icons = "";
            if (IsLocked) icons += "\uD83D\uDD12"; // 🔒
            if (IsScreensaverActive) icons += "\uD83D\uDDA5\uFE0F"; // 🖥️

            var separator = !string.IsNullOrEmpty(icons) ? " " : "";
            return $"{UserName} \u2014 {name} ({ApplicationCount}){separator}{icons}";
        }
    }
}
}
=== /workspace/Strings/en-US/Resources.resw ===
<?xml version="1.0" encoding="utf-8"?>
<root>
  <!-- === Application === -->
  <data name="App.WindowTitle" xml:space="preserve">
    <value>User Session Manager</value>
  </data>
  <data name="App.About.Title" xml:space="preserve">
    <value>About</value>
  </data>

  <!-- === Toolbar === -->
  <data name="Toolbar.UsersTitle" xml:space="preserve">
    <value>Users</value>
  </data>
  <data name="Toolbar.Connect" xml:space="preserve">
    <value>Connect</value>
  </data>
  <data name="Toolbar.SendMessage" xml:space="preserve">
    <value>Send Message</value>
  </data>
  <data name="Toolbar.AdminMode.Locked" xml:space="preserve">
    <value>Unlock System Administrator mode?</value>
  </data>
  <data name="Toolbar.AdminMode.Active" xml:space="preserve">
    <value>System Administrator mode</value>
  </data>

  <!-- === Session Statuses === -->
  <data name="Status.SessionActive" xml:space="preserve">
    <value>Active</value>
  </data>
  <data name="Status.SessionDisconnected" xml:space="preserve">
    <value>Disconnected</value>
  </data>
  <data name="Status.SessionIdle" xml:space="preserve">
    <value>Idle</value>
  </data>
  <data name="Status.SessionLocked" xml:space="preserve">
    <value>Locked</value>
  </data>
  <data name="Status.SessionUnavailable" xml:space="preserve">
    <value>Unavailable</value>
  </data>

  <!-- === Column Headers === -->
  <data name="Column.User" xml:space="preserve">
    <value>User</value>
  </data>
  <data name="Column.Status" xml:space="preserve">
    <value>Status</value>
  </data>
  <data name="Column.CPU" xml:space="preserve">
    <value>CPU</value>
  </data>
  <data name="Column.Memory" xml:space="preserve">
    <value>Memory</value>
  </data>
  <data name="Column.Disk" xml:space="preserve">
    <value>Disk</value>
  </data>
  <data name="Column.Network" xml:space="preserve">
    <value>Network</value>
  </data>
  <data name="Column.GPU" xml:space="preserve">
    <value>GPU</value>
  </data>
  <data name="Column.GPUEngine" xml:space="preserve">
    <value>GPU Engine</value>
  </data>

  <!-- === Context Menu === -->
  <data name="ContextMenu.Expand" xml:space="preserve">
    <value>Expand</value>
  </data>
  <data name="ContextMenu.Collapse" xml:space="preserve">
    <value>Collapse</value>
  </data>
  <data name="ContextMenu.Connect" xml:space="preserve">
    <value>Connect</value>
  </data>
  <data name="ContextMenu.Switch" xml:space="preserve">
    <value>Switch To</value>
  </data>
  <data name="ContextMenu.Disconnect" xml:space="preserve">
    <value>Disconnect</value>
  </data>
  <data name="ContextMenu.Logoff" xml:space="preserve">
    <value>Log Off</value>
  </data>
  <data name="ContextMenu.SendMessage" xml:space="preserve">
    <value>Send Message...</value>
  </data>
</root>
=== /workspace/Strings/ru-RU/Resources.resw ===
<?xml version="1.0" encoding="utf-8"?>
<root>
  <!-- === Приложение === -->
  <data name="App.WindowTitle" xml:space="preserve">
    <value>Диспетчер пользовательских сессий</value>
  </data>
  <data name="App.About.Title" xml:space="preserve">
    <value>О приложении</value>
  </data>

  <!-- === Toolbar === -->
  <data name="Toolbar.UsersTitle" xml:space="preserve">
    <value>Пользователи</value>
  </data>
  <data name="Toolbar.Connect" xml:space="preserve">
    <value>Подключить</value>
  </data>
  <data name="Toolbar.SendMessage" xml:space="preserve">
    <value>Отправить сообщение</value>
  </data>
  <data name="Toolbar.AdminMode.Locked" xml:space="preserve">
    <value>Разблокировать режим системного администратора?</value>
  </data>
  <data name="Toolbar.AdminMode.Active" xml:space="preserve">
    <value>Режим системного администратора</value>
  </data>

  <!-- === Статусы сессий === -->
  <data name="Status.SessionActive" xml:space="preserve">
    <value>Активна</value>
  </data>
  <data name="Status.SessionDisconnected" xml:space="preserve">
    <value>Отключена</value>
  </data>
  <data name="Status.SessionIdle" xml:space="preserve">
    <value>Бездействие</value>
  </data>
  <data name="Status.SessionLocked" xml:space="preserve">
    <value>Заблокирована</value>
  </data>
  <data name="Status.SessionUnavailable" xml:space="preserve">
    <value>Недоступна</value>
  </data>

  <!-- === Заголовки столбцов === -->
  <data name="Column.User" xml:space="preserve">
    <value>Пользователь</value>
  </data>
  <data name="Column.Status" xml:space="preserve">
    <value>Состояние</value>
  </data>
  <data name="Column.CPU" xml:space="preserve">
    <value>ЦП</value>
  </data>
  <data name="Column.Memory" xml:space="preserve">
    <value>Память</value>
  </data>
  <data name="Column.Disk" xml:space="preserve">
    <value>Диск</value>
  </data>
  <data name="Column.Network" xml:space="preserve">
    <value>Сеть</value>
  </data>
  <data name="Column.GPU" xml:space="preserve">
    <value>GPU</value>
  </data>
  <data name="Column.GPUEngine" xml:space="preserve">
    <value>Движок GPU</value>
  </data>

  <!-- === Контекстное меню сессии === -->
  <data name="ContextMenu.Expand" xml:space="preserve">
    <value>Развернуть</value>
  </data>
  <data name="ContextMenu.Collapse" xml:space="preserve">
    <value>Свернуть</value>
  </data>
  <data name="ContextMenu.Connect" xml:space="preserve">
    <value>Подключить</value>
  </data>
  <data name="ContextMenu.Switch" xml:space="preserve">
    <value>Переключиться</value>
  </data>
  <data name="ContextMenu.Disconnect" xml:space="preserve">
    <value>Отключить</value>
  </data>
  <data name="ContextMenu.Logoff" xml:space="preserve">
    <value>Выйти</value>
  </data>
  <data name="ContextMenu.SendMessage" xml:space="preserve">
    <value>Отправить сообщение...</value>
  </data>
</root>
