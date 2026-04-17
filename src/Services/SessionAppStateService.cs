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
