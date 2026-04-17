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
                var logFile = GetOrCreateLogFile();
                if (logFile == null) return;

                // Проверка размера — ротация
                if (_currentFileSize > MaxFileSize)
                {
                    RotateLogs();
                    logFile = GetOrCreateLogFile();
                    if (logFile == null) return;
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var levelStr = level.ToString().ToUpperInvariant();
                var formattedMessage = args != null && args.Length > 0
                    ? string.Format(message, args)
                    : message;

                var logLine = $"[{timestamp}] [{levelStr}] {formattedMessage}{Environment.NewLine}";

                // UTF-8 без BOM
                File.AppendAllText(logFile, logLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch
            {
                // Не допускаем падения приложения из-за ошибок логирования
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
