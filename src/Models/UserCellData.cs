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
