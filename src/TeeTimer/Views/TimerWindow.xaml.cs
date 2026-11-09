using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace TeeTimer;

public partial class TimerWindow
{
    private const double RingSize = 195;
    private const double RingStroke = 9;

    private static readonly Brush TrackBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x36));
    private static readonly Brush TeaGreen = new SolidColorBrush(Color.FromRgb(0x6F, 0xBF, 0x73));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0xE0, 0xA4, 0x58));
    private static readonly Brush FlashGreen = new SolidColorBrush(Color.FromArgb(0x4D, 0x6F, 0xBF, 0x73));

    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer _flash = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly Stopwatch _stopwatch = new();
    private readonly double _ringCircumference = Math.PI * (RingSize - RingStroke);

    private readonly string _name;
    private readonly TeaSound _sound;
    private TimerState _state = TimerState.Idle;
    private bool _closed;
    private TimeSpan _total;
    private TimeSpan _runStartRemaining;
    private TimeSpan _remaining;
    private System.Threading.CancellationTokenSource? _alarmCts;

    public TimerWindow(string name, TimeSpan duration, TeaSound sound)
    {
        InitializeComponent();

        _name = string.IsNullOrWhiteSpace(name) ? "Tee" : name.Trim();
        _total = duration;
        _runStartRemaining = duration;
        _remaining = duration;
        _sound = sound;

        _tick.Tick += OnTick;
        _flash.Tick += OnFlashTick;

        Title = _name;
        TitleBar.Title = _name;
        NameText.Text = _name;
        UpdateTimeUI();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RingTrack.Stroke = TrackBrush;
        UpdateRing(1);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Timer anhalten BEVOR quittiert wird: laeuft die Uhr noch fast ab,
        // wuerde ein bereits gequeueter Tick sonst nach dem Schliessen
        // Finish() ausloesen - ein unerreichbarer, endlos klingelnder Timer.
        _closed = true;
        _tick.Stop();
        _flash.Stop();
        Acknowledge(); // Sound/Alarm-Reste stoppen, Ressourcen freigeben
        App.Instance.RemoveTimer(this);
        base.OnClosed(e);
    }

    // ---------- Timer-Kern ----------

    private void OnTick(object? sender, EventArgs e)
    {
        if (_closed)
            return; // Fenster ist zu - hier darf nichts mehr passieren
        ComputeRemaining();
        UpdateTimeUI();
        if (_remaining <= TimeSpan.Zero)
            Finish();
    }

    private void ComputeRemaining()
    {
        var r = _runStartRemaining - _stopwatch.Elapsed;
        _remaining = r < TimeSpan.Zero ? TimeSpan.Zero : r;
    }

    private void OnStartPause(object sender, RoutedEventArgs e)
    {
        switch (_state)
        {
            case TimerState.Running:
                Pause();
                break;
            case TimerState.Paused:
                RunAgain();
                break;
            default:
                Acknowledge(); // Bedienung am Timer impliziert: Alarm abgehakt
                StartRun();
                break;
        }
    }

    /// <summary>Startet den Timer von vorne (auch fuer Auto-Start aus Presets/Hotkeys).</summary>
    public void StartRun()
    {
        _remaining = _total;
        RunAgain();
    }

    private void RunAgain()
    {
        _runStartRemaining = _remaining;
        _stopwatch.Restart();
        _state = TimerState.Running;
        _tick.Start();
        AckBtn.Visibility = Visibility.Collapsed;
        StartPauseBtn.Content = "⏸  Pause";
        UpdateTimeUI();
    }

    private void Pause()
    {
        ComputeRemaining();
        _tick.Stop();
        _stopwatch.Stop();
        _state = TimerState.Paused;
        StartPauseBtn.Content = "▶  Weiter";
        StatusText.Text = "Pausiert";
        UpdateTimeUI();
    }

    private void OnReset(object sender, RoutedEventArgs e) => Reset();

    private void Reset()
    {
        Acknowledge();
        _tick.Stop();
        _flash.Stop();
        _stopwatch.Reset();
        StopFlashing();
        _state = TimerState.Idle;
        _remaining = _total;
        StartPauseBtn.Content = "▶  Start";
        StatusText.Text = "Bereit";
        UpdateTimeUI();
    }

    private async void Finish()
    {
        if (_closed)
            return; // Race-Sicherung: Finish darf nie nach dem Schliessen laufen

        _tick.Stop();
        _state = TimerState.Finished;
        _remaining = TimeSpan.Zero;
        UpdateTimeUI();
        UpdateRing(1); // voller Ring in Troedelfarbe
        RingProgress.Stroke = TeaGreen;

        StartPauseBtn.Content = "▶  Start";
        Activate();
        StatusText.Text = "Fertig! 🍵";

        _flash.Start();
        AckBtn.Visibility = Visibility.Visible;
        // Höhe an neuen Inhalt (Quittieren-Button) anpassen; WPF kann
        // SizeToContent bei externen Groessenaenderungen auf Manual stellen.
        if (WindowState == WindowState.Normal)
            SizeToContent = SizeToContent.Height;
        App.Instance.ShowBalloon(_name);

        _alarmCts = new System.Threading.CancellationTokenSource();
        await TeaSoundPlayer.PlayLoopAsync(_sound, _alarmCts.Token);
    }

    // ---------- Alarm quittieren ----------

    private void OnAcknowledge(object sender, RoutedEventArgs e) => Acknowledge();

    public void Acknowledge()
    {
        if (_alarmCts is not null)
        {
            _alarmCts.Cancel();
            _alarmCts.Dispose();
            _alarmCts = null;
        }

        StopFlashing();
        AckBtn.Visibility = Visibility.Collapsed;
        if (WindowState == WindowState.Normal)
            SizeToContent = SizeToContent.Height;

        if (_state == TimerState.Finished)
        {
            _state = TimerState.Idle;
        }
    }

    // ---------- Blinken ----------

    private void OnFlashTick(object? sender, EventArgs e)
    {
        var on = FlashOverlay.Background is SolidColorBrush b && b.Color.A > 0;
        FlashOverlay.Background = on ? Brushes.Transparent : FlashGreen;
    }

    private void StopFlashing()
    {
        _flash.Stop();
        FlashOverlay.Background = Brushes.Transparent;
        RingProgress.Stroke = TeaGreen;
    }

    // ---------- UI-Helfer ----------

    private void UpdateTimeUI()
    {
        TimeText.Text = FormatSpan(_remaining);
        var frac = _total.TotalSeconds <= 0 ? 1.0 : _remaining.TotalSeconds / _total.TotalSeconds;
        UpdateRing(frac);
        RingProgress.Stroke = _state == TimerState.Running && _remaining.TotalSeconds <= 30
            ? Amber
            : TeaGreen;
    }

    private void UpdateRing(double frac)
    {
        frac = Math.Clamp(frac, 0, 1);
        RingProgress.StrokeDashArray = new DoubleCollection { _ringCircumference, _ringCircumference };
        RingProgress.StrokeDashOffset = _ringCircumference * (1 - frac);
    }

    private static string FormatSpan(TimeSpan t)
    {
        if (t.TotalHours >= 1)
            return t.ToString(@"h\:mm\:ss");
        return ((int)Math.Floor(t.TotalMinutes)).ToString("0") + ":" + t.Seconds.ToString("00");
    }

    public string TeaName => _name;
    public TimerState CurrentState => _state;
    public TimeSpan Remaining => _remaining;

    public void ShowActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
