# Tee Timer ☕

Ein kleiner, nativer Windows-Tee-Timer in WPF (.NET 10, [WPF-UI](https://wpfui.lepo.co/), Dark Design):
Fortschrittsring-Uhr mit Always-on-Top, beliebig viele parallele Timer, globalen Hotkeys,
Tray-Icon, Signalton und speicherbaren Tee-Presets.

## Features

- **Timer-Fenster**: Uhr mit Fortschrittsring, Start/Pause/Reset, permanent *Always on Top*
- **Einstellungs-Fenster**: Preset-Verwaltung, Signalton-Auswahl, Einmal-Timer
- **Mehrere Timer parallel**: Jeder Klick auf ein Preset startet einen eigenen Timer mit eigenem Fenster
- **Laufende-Timer-Liste** im Einstellungsfenster (Klick: Fenster öffnen, Rechtsklick: Timer verwerfen)
- **Alarm**: Signalton in Endlosschleife + blinkendes Fenster + Tray-Benachrichtigung,
  quittierbar per Button, Tray-Klick oder Benachrichtigungs-Klick
- **Global Hotkeys** (funktionieren auch im Tray):
  - `Strg + Alt + 1 … 9` → Preset *n* starten
  - `Strg + Alt + T` → Einstellungsfenster öffnen
  - AltGr bleibt frei (Low-Level-Hook löst nur bei *linkem* Alt aus)
- **Tray-Icon** mit Preset-Menü; Fenster-✕ minimiert in den Tray, Timer laufen weiter

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (nur zum Bauen – die fertige EXE braucht es nicht)

## Bauen & Starten (Entwicklung)

```powershell
dotnet build src/TeeTimer/TeeTimer.csproj
dotnet run --project src/TeeTimer/TeeTimer.csproj
```

## Komplette EXE erstellen (Single-File, all inclusive)

Eine einzige, komplett eigenständige `TeeTimer.exe` (~65 MB, enthält die .NET-Runtime
und alle Bibliotheken – läuft auf jedem 64-bit-Windows ohne installiertes .NET):

```powershell
dotnet publish src/TeeTimer/TeeTimer.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none
```

**Ergebnis:** `src\TeeTimer\bin\Release\net10.0-windows\win-x64\publish\TeeTimer.exe`

> Wichtig: Immer über `dotnet publish` bauen – die `TeeTimer.exe` aus `bin\Debug`
> ist nicht eigenständig und braucht das SDK auf dem Zielrechner.

## Daten (werden lokal gespeichert)

| Datei | Inhalt |
|---|---|
| `%APPDATA%\TeeTimer\presets.json` | Tee-Presets mit Name, Ziehzeit und Signalton |
| `%APPDATA%\TeeTimer\settings.json` | Zuletzt gewählter Standard-Signalton |

## Projektstruktur

```
TeeTimer/
├── TeeTimer.slnx
└── src/TeeTimer/
    ├── App.xaml(.cs)           # App-Start, Tray-Icon, globale Hotkeys
    ├── Views/
    │   ├── TimerWindow        # Uhren-Fenster (pro Timer eine Instanz)
    │   └── SettingsWindow      # Presets, Signalton, Timer-Liste
    ├── Models/Preset.cs
    ├── Services/               # Persistenz, Sounds
    └── Assets/TeaTimer.ico
```
