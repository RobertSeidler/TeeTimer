using System.IO;
using System.Media;

namespace TeeTimer;

public record TeaSound(string DisplayName, string? FileName)
{
    public static readonly TeaSound[] All =
    {
        new("Wecker", "Alarm05.wav"),
        new("Sanfter Hinweis", "Windows Notify System Generic.wav"),
        new("E-Mail-Pling", "Windows Notify Messaging.wav"),
        new("Glocke", "chimes.wav"),
        new("Tada", "tada.wav"),
        new("Systemton (immer verfügbar)", null),
    };

    /// <summary>Standardton fuer neue Presets/Timer.</summary>
    public static TeaSound Default => ByName("Wecker") ?? All[0];

    public static TeaSound? ByName(string? name) =>
        string.IsNullOrEmpty(name) ? null : All.FirstOrDefault(s => s.DisplayName == name);

    public static TeaSound Resolve(string? name) => ByName(name) ?? Default;
}

public static class TeaSoundPlayer
{
    /// <summary>
    /// Spielt den Signalton endlos im Loop, bis der Alarm quittiert wird.
    /// </summary>
    public static async Task PlayLoopAsync(TeaSound? sound, CancellationToken token)
    {
        sound ??= TeaSound.Default;

        var path = sound.FileName is null ? null
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", sound.FileName);
        if (path is not null && !File.Exists(path))
            path = null; // Fallback auf Systemton

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (path is null)
                    SystemSounds.Asterisk.Play();
                else
                    await Task.Run(
                        () => { using var p = new SoundPlayer(path); p.PlaySync(); }, token);

                await Task.Delay(400, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
