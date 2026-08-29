internal static class AppLogger
{
    private const long MaxLogFileBytes = 256 * 1024;

    private static readonly object LogLock = new();
    private static readonly Dictionary<string, DateTimeOffset> LastThrottledWrites = new();

    public static string LogDirectory { get; } = AppPaths.AppDataDirectory;

    public static string LogPath { get; } = Path.Combine(
        LogDirectory,
        "LegionGoAutoRotate.log");

    private static string OldLogPath { get; } = Path.Combine(
        LogDirectory,
        "LegionGoAutoRotate.old.log");

    public static void Info(string message)
    {
        Write("INFO", message, null);
    }

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    public static void ThrottledError(
        string key,
        TimeSpan interval,
        string message,
        Exception? exception = null)
    {
        var now = DateTimeOffset.Now;

        lock (LogLock)
        {
            if (LastThrottledWrites.TryGetValue(key, out var lastWrite) &&
                now - lastWrite < interval)
            {
                return;
            }

            LastThrottledWrites[key] = now;
            WriteCore(now, "ERROR", message, exception);
        }
    }

    private static void Write(
        string level,
        string message,
        Exception? exception)
    {
        lock (LogLock)
        {
            WriteCore(DateTimeOffset.Now, level, message, exception);
        }
    }

    private static void WriteCore(
        DateTimeOffset timestamp,
        string level,
        string message,
        Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            RotateLogIfNeeded();

            using var writer = File.AppendText(LogPath);

            writer.Write(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            writer.Write(" [");
            writer.Write(level);
            writer.Write("] ");
            writer.WriteLine(message);

            if (exception is not null)
            {
                writer.WriteLine(exception);
            }
        }
        catch
        {
            // Logging must never disturb the tray application.
        }
    }

    private static void RotateLogIfNeeded()
    {
        var logFile = new FileInfo(LogPath);

        if (!logFile.Exists || logFile.Length < MaxLogFileBytes)
            return;

        File.Delete(OldLogPath);
        File.Move(LogPath, OldLogPath);
    }
}
