using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using MhTsManager.Models;

namespace MhTsManager.Services;

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
