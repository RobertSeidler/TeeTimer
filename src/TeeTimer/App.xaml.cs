using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace TeeTimer;

public partial class App
{
    // ---------- App-Zustand ----------

    public static App Instance => (App)Current;

    public List<Preset> Presets { get; } = new();
    public List<TimerWindow> Timers { get; } = new();
    public AppSettings Settings { get; private set; } = new();
    public bool Quitting { get; private set; }

    private SettingsWindow? _settings;
    private TaskbarIcon? _tray;

    public TaskbarIcon Tray => _tray ??= (TaskbarIcon)FindResource("TrayIcon");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "TeeTimer-crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {args.Exception}\n\n");

        // Dark-Theme explizit anwenden. Das ThemesDictionary reicht in
        // Wpf.Ui 4.3 offensichtlich nicht, um die Dynamischen Theme-Ressourcen
        // der Eingabefelder App-weit auf Dark zu stellen.
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);

        Presets.AddRange(PresetStore.Load());
        Settings = SettingsStore.LoadSettings();

        // TaskbarIcon als App-Ressource wird nie "Loaded" (kein visueller Baum),
        // H.NotifyIcon erzeugt das Tray-Icon deshalb erst auf Anforderung -> Force.
        Tray.ForceCreate();
        RefreshTrayMenu();
        _llProc = KeyboardHookProc;
        _llHook = SetWindowsHookEx(WH_KEYBOARD_LL, _llProc, GetModuleHandle(null), 0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_llHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_llHook);
            _llHook = IntPtr.Zero;
        }
        _tray?.Dispose();
        base.OnExit(e);
    }

    // ---------- Fenster-Verwaltung ----------

    public void OpenSettings()
    {
        if (_settings is null)
            _settings = new SettingsWindow();

        _settings.Show();
        _settings.WindowState = WindowState.Normal;
        // Die laufenden Uhren sind Topmost - ein einfaches Activate wuerde
        // hinter ihnen landen. Kurzer Topmost-Tanz hebt die Einstellungen
        // ueber alles, danach wieder normale Ebene.
        _settings.Topmost = true;
        _settings.Activate();
        _settings.Topmost = false;
    }

    public TimerWindow CreateTimer(Preset preset, bool autoStart) =>
        CreateTimer(preset.Name, preset.Duration, TeaSound.Resolve(preset.SoundName), autoStart);

    public TimerWindow CreateTimer(string name, TimeSpan duration, TeaSound sound, bool autoStart)
    {
        var window = new TimerWindow(name, duration, sound);

        // Neu geoeffnete Timer leicht versetzt anordnen, uebereinander mit Cascadierung
        var n = Timers.Count;
        var wa = SystemParameters.WorkArea;
        window.Left = wa.Left + (wa.Width - window.Width) / 2 + 28 * (n % 7);
        window.Top = wa.Top + (wa.Height / 2) - 80 + 26 * (n % 7);

        Timers.Add(window);
        window.ShowActivate();
        if (autoStart)
            window.StartRun();
        return window;
    }

    internal void RemoveTimer(TimerWindow window) => Timers.Remove(window);

    public void ShowAllTimers()
    {
        foreach (var timer in Timers)
            timer.ShowActivate();
    }

    /// <summary>Quittiert alle faelligen Alarme (Box-Klick auf Tray/Benachrichtigung).</summary>
    public void AcknowledgeAll()
    {
        foreach (var timer in Timers)
            timer.Acknowledge();
    }

    public void ShowBalloon(string timerName)
    {
        // Ein Balloon-Fehler darf niemals laufende Timer mitreissen
        try
        {
            Tray.ShowNotification("Tee Timer", $"{timerName} ist fertig! 🍵", NotificationIcon.Info);
        }
        catch
        {
            // Tray ggf. nicht (mehr) erreichbar - Alarm laeuft ohnehin im Fenster
        }
    }

    public void SaveSoundChoice(string displayName)
    {
        Settings.LastSoundName = displayName;
        SettingsStore.SaveSettings(Settings);
    }

    public void Quit()
    {
        Quitting = true;
        foreach (var timer in Timers.ToList())
            timer.Close();
        _settings?.Close();
        Shutdown();
    }

    // ---------- Tray ----------

    public void RefreshTrayMenu()
    {
        var menu = new ContextMenu();
        foreach (var preset in Presets)
        {
            var item = new MenuItem { Header = $"{preset.Name} starten" };
            var captured = preset;
            item.Click += (_, _) => CreateTimer(captured, autoStart: true);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());

        var settings = new MenuItem { Header = "Einstellungen" };
        settings.Click += (_, _) => OpenSettings();
        menu.Items.Add(settings);

        var showTimers = new MenuItem { Header = "Alle Timer anzeigen" };
        showTimers.Click += (_, _) => ShowAllTimers();
        menu.Items.Add(showTimers);

        var quit = new MenuItem { Header = "Beenden" };
        quit.Click += (_, _) => Quit();
        menu.Items.Add(quit);

        Tray.ContextMenu = menu;
        Tray.ToolTipText = Timers.Count > 0
            ? $"Tee Timer – {Timers.Count} Timer aktiv"
            : "Tee Timer";
    }

    private void OnTrayClick(object sender, RoutedEventArgs e)
    {
        OpenSettings();
        AcknowledgeAll();
    }

    private void OnBalloonClicked(object sender, RoutedEventArgs e)
    {
        ShowAllTimers();
        AcknowledgeAll();
    }

    // ---------- Globale Hotkeys (systemweit, auch im Tray) ----------
    // Strg+Alt+1..9 startet Preset n als neuen Timer, Strg+Alt+T oeffnet die Einstellungen.
    // Implementiert als Low-Level-Keyboard-Hook statt RegisterHotKey,
    // weil AltGr fuer Windows "Strg+Alt" (links) ist: RegisterHotKey wuerde
    // Zeichen wie [ ] { } @ € systemweit schlucken. Der Hook loest NUR bei
    // linkem Strg + linkem Alt aus, echtes AltGr bleibt unberuehrt.

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4, VK_RMENU = 0xA5; // Alt / AltGr
    private const int HotkeySettingsIndex = -2;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode, ScanCode, Flags, Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string? name);

    private IntPtr _llHook;
    private LowLevelKeyboardProc? _llProc; // Feld haelt den Delegate am Leben (GC)

    private static bool CtrlAndLeftAltDown() =>
        (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0 &&
        (GetAsyncKeyState(VK_LMENU) & 0x8000) != 0 &&
        (GetAsyncKeyState(VK_RMENU) & 0x8000) == 0; // AltGr (rechtes Alt) nicht!

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == 0)
        {
            var msg = wParam.ToInt64();
            var isDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            var isUp = msg is WM_KEYUP or WM_SYSKEYUP;
            if (isDown || isUp)
            {
                var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                // Index des Presets (0-basiert) bzw. Einstellungs-Shortcut
                var index = info.VkCode switch
                {
                    >= 0x31 and <= 0x39 => (int)info.VkCode - 0x31,          // '1'..'9'
                    >= 0x61 and <= 0x69 => (int)info.VkCode - 0x61,          // NumPad '1'..'9'
                    0x54 => HotkeySettingsIndex,                              // 'T'
                    _ => -1,
                };

                if (index != -1 && CtrlAndLeftAltDown() &&
                    (index == HotkeySettingsIndex || index < Presets.Count))
                {
                    if (isDown)
                        HandleHotkey(index);
                    return (IntPtr)1; // Down UND Up schlucken
                }
            }
        }

        return CallNextHookEx(_llHook, nCode, wParam, lParam);
    }

    private void HandleHotkey(int index)
    {
        if (index == HotkeySettingsIndex)
        {
            OpenSettings();
            AcknowledgeAll();
            return;
        }

        if (index < 0 || index >= Presets.Count)
            return; // kein Preset mit dieser Nummer -> Taste durchreichen

        CreateTimer(Presets[index], autoStart: true);
    }
}
