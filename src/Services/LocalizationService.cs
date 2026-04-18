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
        _logger.Debug("[LocalizationService] Constructor started");
        Console.WriteLine("[CONSOLE] [LocalizationService] Constructor started");

        // Определяем текущий язык
        _logger.Debug("[LocalizationService] Determining current language");
        _currentLanguage = language == "system"
            ? System.Globalization.CultureInfo.CurrentUICulture.Name
            : language;
        _logger.Debug("[LocalizationService] Language parameter: {0}", language);
        _logger.Debug("[LocalizationService] CurrentUICulture.Name: {0}", System.Globalization.CultureInfo.CurrentUICulture.Name);
        _logger.Debug("[LocalizationService] Resolved current language: {0}", _currentLanguage);
        Console.WriteLine($"[CONSOLE] [LocalizationService] Language parameter: {language}");
        Console.WriteLine($"[CONSOLE] [LocalizationService] CurrentUICulture.Name: {System.Globalization.CultureInfo.CurrentUICulture.Name}");
        Console.WriteLine($"[CONSOLE] [LocalizationService] Resolved current language: {_currentLanguage}");

        if (string.IsNullOrEmpty(_currentLanguage))
        {
            _logger.Debug("[LocalizationService] Current language is null or empty, defaulting to en-US");
            _currentLanguage = "en-US";
            Console.WriteLine("[CONSOLE] [LocalizationService] Defaulted to en-US");
        }

        // Загружаем встроенные ресурсы
        _logger.Debug("[LocalizationService] Loading built-in resources");
        Console.WriteLine("[CONSOLE] [LocalizationService] Loading built-in resources");
        LoadBuiltInResources();
        _logger.Debug("[LocalizationService] Built-in resources loaded");
        Console.WriteLine("[CONSOLE] [LocalizationService] Built-in resources loaded");

        _logger.Info("LocalizationService initialized. Language: {0}", _currentLanguage);
        Console.WriteLine($"[CONSOLE] [LocalizationService] Initialization complete. Language: {_currentLanguage}");
    }

    /// <summary>
    /// Получить локализованную строку с fallback-цепочкой.
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        _logger.Debug("[LocalizationService.GetString] Requested key: {0}, args count: {1}", key, args.Length);
        Console.WriteLine($"[CONSOLE] [LocalizationService.GetString] Requested key: {key}, args count: {args.Length}");

        // 1. Текущий язык
        _logger.Debug("[LocalizationService.GetString] Checking current language: {0}", _currentLanguage);
        if (_resources.TryGetValue(_currentLanguage, out var currentLang) &&
            currentLang.TryGetValue(key, out var value))
        {
            var result = args.Length > 0 ? string.Format(value, args) : value;
            _logger.Debug("[LocalizationService.GetString] Found in current language: {0}", result);
            Console.WriteLine($"[CONSOLE] [LocalizationService.GetString] Found in current language: {result}");
            return result;
        }
        _logger.Debug("[LocalizationService.GetString] Not found in current language");
        Console.WriteLine("[CONSOLE] [LocalizationService.GetString] Not found in current language");

        // 2. Fallback: ru-RU
        if (_currentLanguage != "ru-RU" &&
            _resources.TryGetValue("ru-RU", out var ruLang) &&
            ruLang.TryGetValue(key, out var ruValue))
        {
            var result = args.Length > 0 ? string.Format(ruValue, args) : ruValue;
            _logger.Debug("[LocalizationService.GetString] Found in ru-RU fallback: {0}", result);
            Console.WriteLine($"[CONSOLE] [LocalizationService.GetString] Found in ru-RU fallback: {result}");
            return result;
        }
        _logger.Debug("[LocalizationService.GetString] Not found in ru-RU fallback");
        Console.WriteLine("[CONSOLE] [LocalizationService.GetString] Not found in ru-RU fallback");

        // 3. Fallback: en-US
        if (_currentLanguage != "en-US" &&
            _resources.TryGetValue("en-US", out var enLang) &&
            enLang.TryGetValue(key, out var enValue))
        {
            var result = args.Length > 0 ? string.Format(enValue, args) : enValue;
            _logger.Debug("[LocalizationService.GetString] Found in en-US fallback: {0}", result);
            Console.WriteLine($"[CONSOLE] [LocalizationService.GetString] Found in en-US fallback: {result}");
            return result;
        }
        _logger.Debug("[LocalizationService.GetString] Not found in en-US fallback");
        Console.WriteLine("[CONSOLE] [LocalizationService.GetString] Not found in en-US fallback");

        // 4. Возвращаем ключ как заглушку
        _logger.Debug("[LocalizationService.GetString] Missing localization key: {0} (language: {1})", key, _currentLanguage);
        Console.WriteLine($"[CONSOLE] [LocalizationService.GetString] Missing localization key: {key} (language: {_currentLanguage})");
        return args.Length > 0 ? string.Format(key, args) : key;
    }

    /// <summary>
    /// Загрузить встроенные ресурсы для основных языков.
    /// </summary>
    private void LoadBuiltInResources()
    {
        _logger.Debug("[LocalizationService.LoadBuiltInResources] START");
        Console.WriteLine("[CONSOLE] [LocalizationService.LoadBuiltInResources] START");

        // Русский язык
        _logger.Debug("[LocalizationService.LoadBuiltInResources] Loading ru-RU resources");
        Console.WriteLine("[CONSOLE] [LocalizationService.LoadBuiltInResources] Loading ru-RU resources");
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
        _logger.Debug("[LocalizationService.LoadBuiltInResources] ru-RU resources loaded: {0} keys", _resources["ru-RU"].Count);
        Console.WriteLine($"[CONSOLE] [LocalizationService.LoadBuiltInResources] ru-RU resources loaded: {_resources["ru-RU"].Count} keys");

        // English
        _logger.Debug("[LocalizationService.LoadBuiltInResources] Loading en-US resources");
        Console.WriteLine("[CONSOLE] [LocalizationService.LoadBuiltInResources] Loading en-US resources");
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
        _logger.Debug("[LocalizationService.LoadBuiltInResources] en-US resources loaded: {0} keys", _resources["en-US"].Count);
        Console.WriteLine($"[CONSOLE] [LocalizationService.LoadBuiltInResources] en-US resources loaded: {_resources["en-US"].Count} keys");

        _logger.Debug("[LocalizationService.LoadBuiltInResources] END");
        Console.WriteLine("[CONSOLE] [LocalizationService.LoadBuiltInResources] END");
    }
}
}
