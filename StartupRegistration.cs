using Microsoft.Win32;

internal static class StartupRegistration
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ValueName = "LegionGoAutoRotate";

    public static bool IsEnabled()
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);

            return IsCurrentExecutable((string?)runKey?.GetValue(ValueName));
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to read Windows startup registration.", ex);
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ??
                Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (enabled)
            {
                runKey.SetValue(ValueName, Quote(Application.ExecutablePath));
            }
            else
            {
                runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to update Windows startup registration.", ex);
            throw;
        }
    }

    private static bool IsCurrentExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        return string.Equals(
            ExtractExecutablePath(command),
            Application.ExecutablePath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractExecutablePath(string command)
    {
        command = command.Trim();

        if (command.StartsWith('"'))
        {
            var closingQuote = command.IndexOf('"', startIndex: 1);

            if (closingQuote > 1)
                return command.Substring(1, closingQuote - 1);
        }

        var firstSpace = command.IndexOf(' ');

        return firstSpace > 0
            ? command.Substring(0, firstSpace)
            : command;
    }

    private static string Quote(string path)
    {
        return $"\"{path}\"";
    }
}
