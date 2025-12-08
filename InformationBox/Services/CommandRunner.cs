using System;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InformationBox.Services;

// ============================================================================
// COMMAND RUNNER - TROUBLESHOOTING ACTION EXECUTION ENGINE
// ============================================================================
//
// PURPOSE:
//   Executes PowerShell commands for the Troubleshoot tab and captures output
//   in real-time for display in the application UI.
//
// EXECUTION MODES:
//   1. Standard execution (RunAsync)
//      - Runs PowerShell as the current user
//      - Captures stdout and stderr
//      - Supports real-time output streaming via callback
//      - Supports cancellation and timeout
//
//   2. Elevated execution (RunAsAdminAsync)
//      - Runs PowerShell with "runas" verb (triggers UAC prompt)
//      - Limited output capture (elevated process can't redirect to our streams)
//      - Uses temp file to capture output
//
// SECURITY CONSIDERATIONS:
//   - Commands are executed with current user privileges (or elevated if requested)
//   - No shell injection protection - commands come from trusted config only
//   - ExecutionPolicy is set to Bypass for script execution
//   - Commands are logged for audit purposes
//
// PROCESS MANAGEMENT:
//   - Uses async WaitForExitAsync for non-blocking execution
//   - Implements proper cancellation with process tree termination
//   - 5-minute default timeout prevents hung processes
//   - Output streams are read asynchronously to prevent deadlocks
//
// COMMAND EXECUTION FLOW (Standard):
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  1. Create ProcessStartInfo for PowerShell                      │
//   │     - FileName: powershell.exe                                  │
//   │     - Arguments: -NoLogo -NoProfile -ExecutionPolicy Bypass     │
//   │     - Redirect stdout/stderr, create no window                  │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  2. Start process and begin async output reading                │
//   │     - BeginOutputReadLine() for stdout                          │
//   │     - BeginErrorReadLine() for stderr                           │
//   │     - Output callback invoked for each line (real-time UI)      │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  3. Wait for process exit with timeout/cancellation             │
//   │     - Combined CancellationTokenSource for timeout + user cancel│
//   │     - WaitForExitAsync with cancellation support                │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  4. Wait for output streams to complete                         │
//   │     - TaskCompletionSource signals when stdout/stderr are done  │
//   │     - Short timeout ensures we don't hang                       │
//   └─────────────────────────────────────────────────────────────────┘
//                                  │
//                                  ▼
//   ┌─────────────────────────────────────────────────────────────────┐
//   │  5. Return CommandResult with output and exit code              │
//   │     - Success = ExitCode == 0                                   │
//   │     - Duration measured for display                             │
//   └─────────────────────────────────────────────────────────────────┘
//
// ============================================================================

/// <summary>
/// Result from executing a troubleshooting command.
/// </summary>
/// <param name="Success">True if command completed with exit code 0.</param>
/// <param name="ExitCode">Process exit code (-1 for errors/cancellation).</param>
/// <param name="Output">Standard output captured from the process.</param>
/// <param name="Error">Standard error or error message if execution failed.</param>
/// <param name="Duration">Time taken for the command to execute.</param>
public sealed record CommandResult(
    bool Success,
    int ExitCode,
    string Output,
    string Error,
    TimeSpan Duration);

/// <summary>
    /// Executes PowerShell commands and captures output for the Troubleshoot tab.
    /// </summary>
    /// <remarks>
    /// <para><b>Security:</b></para>
    /// Uses <c>-EncodedCommand</c> with UTF-16 Base64 plus a trusted environment preamble to neutralize PowerShell metacharacters and hostile env overrides.
    /// Temp files created for elevated runs are ACL-locked to the current user and deleted on completion.
    ///
    /// <para><b>Entry points:</b></para>
    /// <list type="bullet">
    ///   <item><see cref="RunAsync"/> - Execute with current user privileges</item>
    ///   <item><see cref="RunAsAdminAsync"/> - Execute with elevation (UAC prompt)</item>
    /// </list>
///
/// <para><b>Output streaming:</b></para>
/// The <c>onOutput</c> callback enables real-time display of command output
/// in the UI. Each line is delivered as it's produced by the command.
///
/// <para><b>Cancellation:</b></para>
/// Pass a <see cref="CancellationToken"/> to allow users to cancel long-running
/// commands. The entire process tree is terminated on cancellation.
/// </remarks>
public static class CommandRunner
{
    /// <summary>
    /// Default timeout for command execution (5 minutes).
    /// </summary>
    private static readonly TimeSpan DefaultTimeout = ExecutionTimeouts.CommandDefault;

    /// <summary>
    /// Timeout for waiting on stdout/stderr completion after process exit.
    /// </summary>
    private static readonly TimeSpan StreamReadTimeout = ExecutionTimeouts.StreamRead;

    /// <summary>
    /// Runs a PowerShell command asynchronously with output capture.
    /// </summary>
    /// <remarks>
    /// <para><b>Execution details:</b></para>
    /// <list type="bullet">
    ///   <item>Uses <c>powershell.exe</c> with <c>-NoLogo -NoProfile -ExecutionPolicy Bypass</c></item>
    ///   <item>Output is captured in real-time via async event handlers</item>
    ///   <item>Process runs with current user privileges (no elevation)</item>
    /// </list>
    ///
    /// <para><b>Output callback:</b></para>
    /// The <paramref name="onOutput"/> callback is invoked on each line of output,
    /// enabling real-time display in the UI. Errors are prefixed with "[ERROR] ".
    ///
    /// <para><b>Process completion detection:</b></para>
    /// Uses <see cref="TaskCompletionSource{TResult}"/> to detect when output
    /// streams are complete (not just when the process exits). This ensures all
    /// output is captured before returning.
    ///
    /// <para><b>Security:</b></para>
    /// The command text is passed via <c>-EncodedCommand</c> to avoid injection by PowerShell metacharacters. Environment variables are normalized to trusted values ahead of execution.
    /// </remarks>
    /// <param name="command">The PowerShell command or script to execute.</param>
    /// <param name="onOutput">
    /// Optional callback invoked for each line of output (stdout and stderr).
    /// Use this to update UI in real-time.
    /// </param>
    /// <param name="cancellation">
    /// Cancellation token to allow user-initiated cancellation.
    /// The process tree is killed if cancelled.
    /// </param>
    /// <returns>
    /// <see cref="CommandResult"/> containing success status, output, and duration.
    /// </returns>
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
            // -----------------------------------------------------------------
            // STEP 1: Configure process start info
            // -----------------------------------------------------------------
            // PowerShell arguments:
            //   -NoLogo: Skip PowerShell logo/banner
            //   -NoProfile: Don't load user profile (faster, more consistent)
            //   -ExecutionPolicy Bypass: Allow script execution without prompts
            //   -Command: Execute the provided command string
            //
            var normalizedCommand = AddSafeEnvPreamble(command);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = BuildEncodedArguments(normalizedCommand),
                UseShellExecute = false,          // Required for output redirection
                RedirectStandardOutput = true,    // Capture stdout
                RedirectStandardError = true,     // Capture stderr
                CreateNoWindow = true,            // Run silently (no console window)
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            // -----------------------------------------------------------------
            // STEP 2: Set up output stream completion detection
            // -----------------------------------------------------------------
            // We use TaskCompletionSource to know when all output has been read.
            // This is important because WaitForExitAsync can return before all
            // output is flushed to our event handlers.
            // -----------------------------------------------------------------
            var outputComplete = new TaskCompletionSource<bool>();
            var errorComplete = new TaskCompletionSource<bool>();

            // Handle stdout - invoked for each line of output
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stdout.AppendLine(e.Data);
                    onOutput?.Invoke(e.Data); // Real-time callback for UI
                }
                else
                {
                    // e.Data is null when stream is closed
                    outputComplete.TrySetResult(true);
                }
            };

            // Handle stderr - invoked for each line of error output
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stderr.AppendLine(e.Data);
                    onOutput?.Invoke($"[ERROR] {e.Data}"); // Prefix errors for UI
                }
                else
                {
                    // e.Data is null when stream is closed
                    errorComplete.TrySetResult(true);
                }
            };

            // -----------------------------------------------------------------
            // STEP 3: Start process and begin async output reading
            // -----------------------------------------------------------------
            process.Start();
            process.BeginOutputReadLine(); // Start async stdout reading
            process.BeginErrorReadLine();  // Start async stderr reading

            // -----------------------------------------------------------------
            // STEP 4: Wait for process exit with timeout and cancellation
            // -----------------------------------------------------------------
            // Create linked cancellation token that fires on:
            //   - User cancellation (cancellation parameter)
            //   - Timeout (DefaultTimeout)
            // -----------------------------------------------------------------
            using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeoutCts.Token);

            try
            {
                // Wait for process to exit
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);

                // -----------------------------------------------------------------
                // STEP 5: Wait for output streams to complete
                // -----------------------------------------------------------------
                // Even after process exits, there may be buffered output.
                // Wait a short time for streams to flush completely.
                // -----------------------------------------------------------------
                var streamTimeout = Task.Delay(StreamReadTimeout, CancellationToken.None);
                await Task.WhenAny(
                    Task.WhenAll(outputComplete.Task, errorComplete.Task),
                    streamTimeout).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // -----------------------------------------------------------------
                // Handle cancellation or timeout
                // -----------------------------------------------------------------
                // Kill the entire process tree to clean up child processes
                // (e.g., if PowerShell spawned other processes)
                // -----------------------------------------------------------------
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Process already exited
                }
                catch
                {
                    // Ignore other kill errors
                }

                var reason = cancellation.IsCancellationRequested ? "Cancelled" : "Timed out";
                return new CommandResult(false, -1, stdout.ToString().Trim(), reason, DateTime.UtcNow - startTime);
            }

            // -----------------------------------------------------------------
            // STEP 6: Return result
            // -----------------------------------------------------------------
            var duration = DateTime.UtcNow - startTime;
            var exitCode = process.ExitCode;
            var success = exitCode == 0;

            var result = new CommandResult(success, exitCode, stdout.ToString().Trim(), stderr.ToString().Trim(), duration);
            Logger.Info($"CommandRunner (user): exit={exitCode} success={success} durationMs={duration.TotalMilliseconds:F0}");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error($"Command execution failed: {ex.Message}");
            return new CommandResult(false, -1, stdout.ToString(), ex.Message, DateTime.UtcNow - startTime);
        }
    }

    /// <summary>
    /// Runs a command with administrator elevation (triggers UAC prompt).
    /// </summary>
    /// <remarks>
    /// <para><b>Elevation mechanism:</b></para>
    /// Uses <c>Verb = "runas"</c> to trigger Windows UAC elevation prompt.
    /// User must approve elevation for the command to execute.
    ///
    /// <para><b>Security:</b></para>
    /// The wrapped command is Base64-encoded and temp output is written to a per-user ACL-protected file that is cleaned up after execution.
    ///
    /// <para><b>Output capture limitation:</b></para>
    /// When running elevated, we cannot directly redirect stdout/stderr because
    /// the elevated process runs in a different security context.
    ///
    /// <b>Workaround:</b> We wrap the command in a script that writes output
    /// to a temporary file, then read the file after execution.
    ///
    /// <para><b>UAC cancellation:</b></para>
    /// If the user cancels the UAC prompt, a <see cref="System.ComponentModel.Win32Exception"/>
    /// with error code 1223 is thrown. This is handled gracefully.
    /// </remarks>
    /// <param name="command">The PowerShell command to execute with elevation.</param>
    /// <returns>
    /// <see cref="CommandResult"/> containing success status, captured output, and duration.
    /// Note: Output may be limited compared to non-elevated execution.
    /// </returns>
    public static async Task<CommandResult> RunAsAdminAsync(string command)
    {
        var startTime = DateTime.UtcNow;
        var outputFile = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"InfoBox_Output_{Guid.NewGuid():N}.txt");

        try
        {
            // -----------------------------------------------------------------
            // Create temp file for output capture
            // -----------------------------------------------------------------
            // Because elevated processes can't redirect to our streams,
            // we use a temp file as an intermediary.
            // -----------------------------------------------------------------
            SecureTempFile(outputFile);

            // Wrap the command to capture output to temp file
            var normalizedCommand = AddSafeEnvPreamble(command);

            var wrappedCommand = $@"
$ErrorActionPreference = 'Continue'
try {{
    {normalizedCommand} 2>&1 | Out-File -FilePath '{outputFile}' -Encoding UTF8
    exit $LASTEXITCODE
}} catch {{
    $_.Exception.Message | Out-File -FilePath '{outputFile}' -Encoding UTF8
    exit 1
}}
";

            // -----------------------------------------------------------------
            // Configure elevated process
            // -----------------------------------------------------------------
            // UseShellExecute = true is required for Verb = "runas"
            // This triggers the UAC elevation prompt
            // -----------------------------------------------------------------
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = BuildEncodedArguments(wrappedCommand),
                UseShellExecute = true,  // Required for elevation
                Verb = "runas"           // Request elevation
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return new CommandResult(false, -1, "", "Failed to start elevated process", DateTime.UtcNow - startTime);
            }

            await process.WaitForExitAsync().ConfigureAwait(false);

            var duration = DateTime.UtcNow - startTime;
            var output = "";

            // -----------------------------------------------------------------
            // Read output from temp file
            // -----------------------------------------------------------------
            if (System.IO.File.Exists(outputFile))
            {
                try
                {
                    // Small delay to ensure file is fully written
                    await Task.Delay(100).ConfigureAwait(false);
                    output = await System.IO.File.ReadAllTextAsync(outputFile).ConfigureAwait(false);
                    System.IO.File.Delete(outputFile); // Clean up
                }
                catch { /* Ignore file read errors */ }
            }

            var result = new CommandResult(process.ExitCode == 0, process.ExitCode, output.Trim(), "", duration);
            Logger.Info($"CommandRunner (admin): exit={process.ExitCode} success={process.ExitCode == 0} durationMs={duration.TotalMilliseconds:F0}");
            return result;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // -----------------------------------------------------------------
            // Handle UAC cancellation
            // -----------------------------------------------------------------
            // Error 1223 = "The operation was canceled by the user"
            // This happens when user clicks "No" on UAC prompt
            // -----------------------------------------------------------------
            Logger.Info("Admin command cancelled by user (UAC declined)");
            return new CommandResult(false, -1, "", "User cancelled elevation prompt", DateTime.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            Logger.Error($"Admin command execution failed: {ex.Message}");
            return new CommandResult(false, -1, "", ex.Message, DateTime.UtcNow - startTime);
        }
        finally
        {
            // Best-effort temp file cleanup
            try
            {
                if (System.IO.File.Exists(outputFile))
                {
                    System.IO.File.Delete(outputFile);
                }
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    // Builds PowerShell arguments using -EncodedCommand to avoid injection via special characters.
    private static string BuildEncodedArguments(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return $"-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}";
    }

    // Creates the temp file with ACL restricted to current user so elevated runs cannot be read by others.
    private static void SecureTempFile(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            var security = fs.GetAccessControl();
            var sid = WindowsIdentity.GetCurrent().User;
            if (sid != null)
            {
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.SetAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
                fs.SetAccessControl(security);
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"Failed to harden temp file ACLs: {ex.Message}");
        }
    }

    // Normalizes environment variables to trusted values before executing user-provided script fragments.
    private static string AddSafeEnvPreamble(string script)
    {
        static string Sq(string value) => value.Replace("'", "''");

        var localAppData = Sq(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var appData = Sq(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        var temp = Sq(System.IO.Path.GetTempPath());
        var systemRoot = Sq(Environment.GetFolderPath(Environment.SpecialFolder.Windows) ??
                           Environment.GetEnvironmentVariable("SystemRoot") ??
                           "C:\\Windows");

        return $"$env:LOCALAPPDATA='{localAppData}';$env:APPDATA='{appData}';$env:TEMP='{temp}';$env:SystemRoot='{systemRoot}';{script}";
    }
}
