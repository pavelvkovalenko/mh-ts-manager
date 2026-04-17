using System.Text.RegularExpressions;

namespace MhTsManager.Services;

/// <summary>
/// Интерфейс безопасного исполнителя внешних команд (mstsc, msg, tscon).
/// </summary>
public interface ICommandExecutor
{
    /// <summary>
    /// Удалённое подключение через mstsc /shadow.
    /// </summary>
    Task<bool> ShadowSessionAsync(int sessionId, CancellationToken ct);

    /// <summary>
    /// Отправить сообщение в сессию через msg.exe.
    /// </summary>
    Task<bool> SendMessageAsync(int sessionId, string message, CancellationToken ct);

    /// <summary>
    /// Открыть оснастку управления пользователями (lusrmgr.msc).
    /// </summary>
    Task<bool> OpenUserManagement(CancellationToken ct);
}

/// <summary>
/// Безопасный исполнитель внешних команд.
/// Все аргументы валидируются, запускаются только разрешённые исполняемые файлы.
/// </summary>
public sealed class CommandExecutor : ICommandExecutor
{
    private readonly Logger _logger;

    public CommandExecutor(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Удалённое подключение через mstsc /shadow:<SessionId> /control /noConsentPrompt.
    /// </summary>
    public async Task<bool> ShadowSessionAsync(int sessionId, CancellationToken ct)
    {
        _logger.Info("Initiating shadow session (RDP shadow) for session {0}", sessionId);

        // Валидация: SessionId должен быть положительным числом
        if (sessionId <= 0)
        {
            _logger.Warning("Invalid session ID for shadow: {0}", sessionId);
            return false;
        }

        // Формируем аргументы — только число, без инъекций
        var arguments = $"/shadow:{sessionId} /control /noConsentPrompt";

        var psi = new ProcessStartInfo
        {
            FileName = "mstsc.exe",
            Arguments = arguments,
            UseShellExecute = true, // true для GUI-приложений
            CreateNoWindow = false,
        };

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = psi.FileName,
                Arguments = psi.Arguments,
                UseShellExecute = psi.UseShellExecute
            });

            if (process == null)
            {
                _logger.Error("Failed to start mstsc.exe for session {0}", sessionId);
                return false;
            }

            _logger.Info("mstsc.exe started for session {0}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to launch mstsc.exe for session {0}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Отправить сообщение в сессию через msg.exe.
    /// </summary>
    public async Task<bool> SendMessageAsync(int sessionId, string message, CancellationToken ct)
    {
        _logger.Info("Sending message to session {0}", sessionId);

        // Валидация SessionId
        if (sessionId < 0)
        {
            _logger.Warning("Invalid session ID for message: {0}", sessionId);
            return false;
        }

        // Валидация сообщения: не пустое, макс. 1024 символа
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.Warning("Empty message for session {0}", sessionId);
            return false;
        }

        if (message.Length > 1024)
        {
            _logger.Warning("Message too long ({0} chars) for session {1}, truncating", message.Length, sessionId);
            message = message.Substring(0, 1024);
        }

        // Санитизация: экранирование специальных символов для командной строки
        var sanitizedMessage = message.Replace("\"", "\"\"\"").Replace("&", "^&").Replace("|", "^|");

        var psi = new ProcessStartInfo
        {
            FileName = "msg.exe",
            Arguments = $"/server:localhost {sessionId} \"{sanitizedMessage}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.Error("Failed to start msg.exe for session {0}", sessionId);
                return false;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("msg.exe timed out for session {0}", sessionId);
                process.Kill();
                return false;
            }

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.Warning("msg.exe exited with code {0}: {1}", process.ExitCode, stderr);
                return false;
            }

            _logger.Info("Message sent to session {0} successfully", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to launch msg.exe for session {0}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Открыть оснастку управления пользователями (lusrmgr.msc).
    /// </summary>
    public async Task<bool> OpenUserManagement(CancellationToken ct)
    {
        _logger.Info("Opening User Management console (lusrmgr.msc)");

        var psi = new ProcessStartInfo
        {
            FileName = "lusrmgr.msc",
            UseShellExecute = true,
            CreateNoWindow = false,
        };

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = psi.FileName,
                UseShellExecute = psi.UseShellExecute
            });

            if (process == null)
            {
                _logger.Error("Failed to start lusrmgr.msc");
                return false;
            }

            _logger.Info("lusrmgr.msc started");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to launch lusrmgr.msc");
            return false;
        }
    }
}
