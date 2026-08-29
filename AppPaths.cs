internal static class AppPaths
{
    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LegionGoAutoRotate");

    public static string SettingsPath { get; } = Path.Combine(
        AppDataDirectory,
        "settings.json");
}
