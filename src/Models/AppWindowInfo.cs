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
