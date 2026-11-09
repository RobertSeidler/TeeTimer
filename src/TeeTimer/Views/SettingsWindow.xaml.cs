using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;
using WpfButton = Wpf.Ui.Controls.Button;

namespace TeeTimer;

public partial class SettingsWindow
{
    private readonly List<Preset> _presets;
    private bool _soundReady;

    public SettingsWindow()
    {
        InitializeComponent();
        _presets = App.Instance.Presets;

        SoundBox.ItemsSource = TeaSound.All;
        SoundBox.SelectedItem = TeaSound.Resolve(App.Instance.Settings.LastSoundName);
        _soundReady = true;

        RenderPresets();

        // Laufende-Timer-Liste periodisch aktualisieren
        var refresh = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        refresh.Tick += (_, _) => RenderTimers();
        refresh.Start();
        RenderTimers();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (App.Instance.Quitting)
            return;
        // Ins Tray minimieren statt beenden
        e.Cancel = true;
        Hide();
    }

    // ---------- Signalton ----------

    private void OnSoundChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_soundReady || SoundBox.SelectedItem is not TeaSound sound)
            return;
        App.Instance.SaveSoundChoice(sound.DisplayName);
    }

    // ---------- Presets ----------

    private void OnAddPreset(object sender, RoutedEventArgs e)
    {
        var name = NewNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            NewNameBox.Focus();
            return;
        }

        int.TryParse(NewMinBox.Text?.Trim(), out var min);
        int.TryParse(NewSecBox.Text?.Trim(), out var sec);
        min = Math.Clamp(min, 0, 99);
        sec = Math.Clamp(sec, 0, 59);
        if (min * 60 + sec < 1)
            return;

        var preset = new Preset
        {
            Name = name!,
            Minutes = min,
            Seconds = sec,
            SoundName = (SoundBox.SelectedItem as TeaSound)?.DisplayName,
        };
        _presets.Add(preset);
        PresetStore.Save(_presets);

        NewNameBox.Text = "";
        RenderPresets();
    }

    private void RemovePreset(Preset preset)
    {
        _presets.Remove(preset);
        PresetStore.Save(_presets);
        RenderPresets();
    }

    private void RenderPresets()
    {
        PresetPanel.Children.Clear();
        foreach (var preset in _presets)
        {
            var button = new WpfButton
            {
                Content = $"{preset.Name}  ·  {FormatSpan(preset.Duration)}",
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 5, 8, 5),
                ToolTip = $"Signalton: {TeaSound.Resolve(preset.SoundName).DisplayName}",
            };

            var captured = preset;
            // Ein Klick startet den Timer sofort
            button.Click += (_, _) => App.Instance.CreateTimer(captured, autoStart: true);
            button.MouseRightButtonUp += (_, _) => RemovePreset(captured);
            PresetPanel.Children.Add(button);
        }

        RefitHeight();
        App.Instance.RefreshTrayMenu();
    }

    // ---------- Laufende Timer ----------

    private void RenderTimers()
    {
        var timers = App.Instance.Timers;
        TimersPanel.Children.Clear();
        TimersHeader.Visibility = timers.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        foreach (var timer in timers)
        {
            var captured = timer;
            var button = new WpfButton
            {
                Content = $"{captured.TeaName}  ·  {FormatSpan(captured.Remaining)}",
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 5, 8, 5),
                ToolTip = captured.CurrentState switch
                {
                    TimerState.Running => "Zieht… – Klick: Fenster öffnen",
                    TimerState.Paused => "Pausiert – Klick: Fenster öffnen",
                    TimerState.Finished => "Fertig! – Klick: Fenster öffnen",
                    _ => "Bereit – Klick: Fenster öffnen",
                },
            };
            if (captured.CurrentState == TimerState.Finished)
                button.Appearance = ControlAppearance.Danger;

            button.Click += (_, _) => captured.ShowActivate();
            // Rechtsklick verwirft den Timer (Fenster schliessen stoppt Ton/Alarm)
            button.MouseRightButtonUp += (_, _) => captured.Close();
            TimersPanel.Children.Add(button);
        }

        RefitHeight();
    }

    private void RefitHeight()
    {
        // WPF stellt SizeToContent automatisch auf Manual (u.a. wenn Windows
        // die Fenstergroesse von aussen anfasst). Damit die Hoehe dauerhaft
        // zum Inhalt passt, bei jeder Inhaltsaenderung neu beauftragen.
        if (WindowState == WindowState.Normal)
            SizeToContent = SizeToContent.Height;
    }

    // ---------- Einmal-Timer ----------

    private void OnCustomStart(object sender, RoutedEventArgs e)
    {
        int.TryParse(CustomMinBox.Text?.Trim(), out var min);
        int.TryParse(CustomSecBox.Text?.Trim(), out var sec);
        min = Math.Clamp(min, 0, 99);
        sec = Math.Clamp(sec, 0, 59);
        var duration = TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec);
        if (duration.TotalSeconds < 1)
            return;

        App.Instance.CreateTimer("Tee", duration,
            SoundBox.SelectedItem as TeaSound ?? TeaSound.Default, autoStart: true);
    }

    private static string FormatSpan(TimeSpan t)
    {
        if (t.TotalHours >= 1)
            return t.ToString(@"h\:mm\:ss");
        return ((int)Math.Floor(t.TotalMinutes)).ToString("0") + ":" + t.Seconds.ToString("00");
    }
}
