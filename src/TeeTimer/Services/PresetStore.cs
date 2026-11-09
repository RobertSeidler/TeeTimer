using System.IO;
using System.Text.Json;

namespace TeeTimer;

public static class PresetStore
{
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeeTimer");

    private static string FilePath => Path.Combine(Dir, "presets.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static List<Preset> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<List<Preset>>(File.ReadAllText(FilePath));
                if (loaded is { Count: > 0 })
                    return loaded;
            }
        }
        catch
        {
            // Kaputte Datei -> Defaults wiederherstellen
        }

        var presets = DefaultPresets();
        Save(presets);
        return presets;
    }

    public static void Save(List<Preset> presets)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(presets, JsonOpts));
        }
        catch
        {
            // Speichern ist für den Prototyp optional; der Timer läuft weiter
        }
    }

    private static List<Preset> DefaultPresets() => new()
    {
        new() { Name = "Darjeeling", Minutes = 2, Seconds = 0 },
        new() { Name = "Earl Grey", Minutes = 3, Seconds = 0 },
        new() { Name = "English Breakfast", Minutes = 3, Seconds = 30 },
    };
}
