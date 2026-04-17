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
