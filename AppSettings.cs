using System.Text.Json;

internal sealed class AppSettings
{
    public bool RotateWithControllersAttached { get; set; }
}

internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(AppPaths.SettingsPath);

            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ??
                new AppSettings();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to load application settings.", ex);
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(AppPaths.SettingsPath, json);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to save application settings.", ex);
        }
    }
}
