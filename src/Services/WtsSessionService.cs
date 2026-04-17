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
        _serverHandle = new SafeWtsServerHandle();
        // Копируем хендл в SafeHandle
        _serverHandle.SetHandle(handle);
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
