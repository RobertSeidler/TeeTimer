namespace TeeTimer;

public enum TimerState { Idle, Running, Paused, Finished }

public class Preset
{
    public string Name { get; set; } = "";
    public int Minutes { get; set; }
    public int Seconds { get; set; }

    /// <summary>
    /// Display-Name des Signaltons (siehe TeaSound.All).
    /// Null oder leer -> Standardton ("Wecker").
    /// </summary>
    public string? SoundName { get; set; }

    public TimeSpan Duration => TimeSpan.FromMinutes(Minutes) + TimeSpan.FromSeconds(Seconds);
}
