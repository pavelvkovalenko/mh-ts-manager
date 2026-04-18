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
    /// Создать настройки по умолчанию.
    /// </summary>
    private static AppSettings CreateDefaultSettings()
    {
        // Добавлено подробное логирование для диагностики отсутствия окна
        Logger.DebugStatic("[SettingsService] CreateDefaultSettings: Creating default settings...");
        
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
        
        Logger.DebugStatic($"[SettingsService] CreateDefaultSettings: Default settings created. JSON: {System.Text.Json.JsonSerializer.Serialize(defaultSettings)}");
        
        return defaultSettings;
    }
}
}
