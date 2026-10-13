using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ServerMonitor.Infrastructure.System;

/// <summary>
/// Centralized shell command execution
/// All shell commands in the application should go through this
/// </summary>
public class ShellExecutor
{
    private readonly ILogger<ShellExecutor> _logger;

    public ShellExecutor(ILogger<ShellExecutor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Execute a shell command and return the output.
    /// Cancellation (e.g. application shutdown) is propagated quietly as
    /// <see cref="OperationCanceledException"/> without error logging.
    /// Timeouts are reported as a <see cref="ShellResult"/> with
    /// <see cref="ShellResult.TimedOut"/> set, not as exceptions.
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 30s)</param>
    /// <param name="throwOnError">Whether to throw exception on non-zero exit code</param>
    public async Task<ShellResult> ExecuteAsync(
        string command,
        int timeoutMs = 30000,
        bool throwOnError = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        var output = new StringBuilder();
        var error = new StringBuilder();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) error.AppendLine(e.Data);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { }

                // Shutdown / caller cancellation: stay quiet so host
                // shutdown logs are not flooded with errors.
                if (cancellationToken.IsCancellationRequested)
                    throw;

                _logger.LogWarning("Command timed out after {Timeout}ms: {Command}",
                    timeoutMs, command);
                return new ShellResult
                {
                    ExitCode = -1,
                    StandardOutput = output.ToString(),
                    StandardError = $"Command timed out after {timeoutMs}ms",
                    Command = command,
                    TimedOut = true
                };
            }

            var result = new ShellResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = output.ToString(),
                StandardError = error.ToString(),
                Command = command
            };

            if (throwOnError && process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Command failed with exit code {process.ExitCode}: {error}");
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // Shutdown path - do not log as an error.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command: {Command}", command);
            return new ShellResult
            {
                ExitCode = -1,
                StandardOutput = output.ToString(),
                StandardError = ex.Message,
                Command = command,
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Check if a command/binary is available on the system
    /// </summary>
    public async Task<bool> IsCommandAvailableAsync(string commandName)
    {
        var result = await ExecuteAsync($"which {commandName}", timeoutMs: 5000);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }
}

/// <summary>
/// Result of shell command execution
/// </summary>
public class ShellResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public bool TimedOut { get; set; }
    public Exception? Exception { get; set; }
    
    public bool Success => ExitCode == 0 && !TimedOut && Exception == null;
}
