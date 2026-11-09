using System.IO;
using System.Text.Json;

namespace TeeTimer;

public class AppSettings
{
    /// <summary>Zuletzt gewaehlter Signalton (wird pro Sitzung ueberlebt).</summary>
    public string LastSoundName { get; set; } = TeaSound.Default.DisplayName;
}

public static class SettingsStore
{
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeeTimer");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            // Kaputte Datei -> Default-Einstellungen
        }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOpts));
        }
        catch
        {
            // Speichern optional; App laeuft weiter
        }
    }
}
