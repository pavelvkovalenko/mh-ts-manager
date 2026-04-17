namespace MhTsManager.Services;

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
