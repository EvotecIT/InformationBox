using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InformationBox.Services;

/// <summary>
/// Result from running a command.
/// </summary>
public sealed record CommandResult(
    bool Success,
    int ExitCode,
    string Output,
    string Error,
    TimeSpan Duration);

/// <summary>
/// Executes PowerShell commands and captures output.
/// </summary>
public static class CommandRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Runs a PowerShell command asynchronously and captures output.
    /// </summary>
    /// <param name="command">The PowerShell command to run.</param>
    /// <param name="onOutput">Callback for streaming output.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>Command execution result.</returns>
    public static async Task<CommandResult> RunAsync(
        string command,
        Action<string>? onOutput = null,
        CancellationToken cancellation = default)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var startTime = DateTime.UtcNow;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var outputComplete = new TaskCompletionSource<bool>();
            var errorComplete = new TaskCompletionSource<bool>();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stdout.AppendLine(e.Data);
                    onOutput?.Invoke(e.Data);
                }
                else
                {
                    outputComplete.TrySetResult(true);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stderr.AppendLine(e.Data);
                    onOutput?.Invoke($"[ERROR] {e.Data}");
                }
                else
                {
                    errorComplete.TrySetResult(true);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Create a combined cancellation with timeout
            using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeoutCts.Token);

            try
            {
                // Wait for process to exit
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);

                // Wait for output streams to complete (with a short timeout)
                var streamTimeout = Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);
                await Task.WhenAny(
                    Task.WhenAll(outputComplete.Task, errorComplete.Task),
                    streamTimeout).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Kill the process if cancelled or timed out
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch { /* ignore */ }

                var reason = cancellation.IsCancellationRequested ? "Cancelled" : "Timed out";
                return new CommandResult(false, -1, stdout.ToString().Trim(), reason, DateTime.UtcNow - startTime);
            }

            var duration = DateTime.UtcNow - startTime;
            var exitCode = process.ExitCode;
            var success = exitCode == 0;

            return new CommandResult(success, exitCode, stdout.ToString().Trim(), stderr.ToString().Trim(), duration);
        }
        catch (Exception ex)
        {
            Logger.Error($"Command execution failed: {ex.Message}");
            return new CommandResult(false, -1, stdout.ToString(), ex.Message, DateTime.UtcNow - startTime);
        }
    }

    /// <summary>
    /// Runs a command with admin elevation (UAC). Output capture is limited for elevated processes.
    /// </summary>
    /// <param name="command">The PowerShell command to run.</param>
    /// <returns>Command result.</returns>
    public static async Task<CommandResult> RunAsAdminAsync(string command)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // For admin commands, we run a script that captures output to a temp file
            var outputFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"InfoBox_Output_{Guid.NewGuid():N}.txt");
            var wrappedCommand = $@"
$ErrorActionPreference = 'Continue'
try {{
    {command} 2>&1 | Out-File -FilePath '{outputFile}' -Encoding UTF8
    exit $LASTEXITCODE
}} catch {{
    $_.Exception.Message | Out-File -FilePath '{outputFile}' -Encoding UTF8
    exit 1
}}
";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"{wrappedCommand.Replace("\"", "\\\"")}\"",
                UseShellExecute = true,
                Verb = "runas"
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return new CommandResult(false, -1, "", "Failed to start elevated process", DateTime.UtcNow - startTime);
            }

            await process.WaitForExitAsync().ConfigureAwait(false);

            var duration = DateTime.UtcNow - startTime;
            var output = "";

            // Try to read the output file
            if (System.IO.File.Exists(outputFile))
            {
                try
                {
                    // Small delay to ensure file is fully written
                    await Task.Delay(100).ConfigureAwait(false);
                    output = await System.IO.File.ReadAllTextAsync(outputFile).ConfigureAwait(false);
                    System.IO.File.Delete(outputFile);
                }
                catch { /* ignore */ }
            }

            return new CommandResult(process.ExitCode == 0, process.ExitCode, output.Trim(), "", duration);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled UAC prompt
            Logger.Info("Admin command cancelled by user (UAC declined)");
            return new CommandResult(false, -1, "", "User cancelled elevation prompt", DateTime.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            Logger.Error($"Admin command execution failed: {ex.Message}");
            return new CommandResult(false, -1, "", ex.Message, DateTime.UtcNow - startTime);
        }
    }
}
