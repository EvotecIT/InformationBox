using System;
using System.IO;

namespace InformationBox.Services;

/// <summary>
/// Minimal file logger for troubleshooting.
/// </summary>
public static class Logger
{
    private static readonly object Sync = new();
    private static readonly string LogFilePath;

    static Logger()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InformationBox", "logs");
            Directory.CreateDirectory(folder);
            LogFilePath = Path.Combine(folder, "log.txt");
        }
        catch
        {
            LogFilePath = Path.GetTempFileName();
        }
    }

    /// <summary>
    /// Gets the path of the log file on disk.
    /// </summary>
    public static string LogFile => LogFilePath;

    /// <summary>
    /// Writes an informational message to the log.
    /// </summary>
    public static void Info(string message) => Write("INFO", message);

    /// <summary>
    /// Writes an error message (and optional exception) to the log.
    /// </summary>
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message + (ex is null ? string.Empty : $" :: {ex}"));

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTime.UtcNow:O} [{level}] {message}";
            lock (Sync)
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // swallow logging failures
        }
    }
}
