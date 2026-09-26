# Speedy Monitor

**Die Netzwerk-Geschwindigkeit direkt in der Windows-11-Taskleiste – wie früher der NetSpeed Monitor.**

![Speedy Monitor in der Taskleiste](docs/images/taskleiste-zoom.png)

## Funktionen

- **Live-Anzeige in der Taskleiste**, direkt neben den Tray-Symbolen:
  ▲ Upload (orange) und ▼ Download (grün) – ohne Kasten, passend zum hellen
  oder dunklen Windows-Design
- **Klick** öffnet die Statistik: aktuelle Raten, Datenmengen für
  Sitzung / Heute / Woche / Monat, Live-Graph der letzten 60 Messungen
- **Verlauf** als Balkendiagramm (30 Tage, 90 Tage, 12 Monate) mit Summen,
  Tagesdurchschnitt und CSV-Export für Excel
- **Doppelklick** öffnet die Einstellungen, **Rechtsklick** ein Menü mit allen
  Funktionen
- Einheit wählbar (automatisch, KB/s, MB/s, kbit/s, Mbit/s), Adapter wählbar
  oder automatisch (virtuelle Adapter von Hyper-V, WSL, VirtualBox, VMware
  werden ignoriert)
- Optional **auf allen Monitoren** – jede Taskleiste bekommt ihre eigene Anzeige
- Blendet sich bei **Vollbild-Apps** (Spiele, Videos) automatisch aus
- Taskleiste oben oder unten – beides funktioniert
- Installation **ohne Administratorrechte**, optional mit Autostart
- **Keine Telemetrie**, keine Internetverbindung – alle Daten bleiben auf deinem PC

| Statistik | Verlauf |
|---|---|
| ![Statistik](docs/images/statistik.png) | ![Verlauf](docs/images/verlauf.png) |

## Download

Unter **[Releases](../../releases)** gibt es zwei Varianten des Setups:

| Datei | Für wen? |
|---|---|
| `SpeedyMonitor_Setup_1.0.0.exe` | Klein. Benötigt die [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0) – das Setup prüft das und hilft beim Nachinstallieren. |
| `SpeedyMonitor_Setup_1.0.0_Standalone.exe` | Größer, aber mit eingebauter Laufzeitumgebung – **funktioniert überall ohne Zusatzinstallation**. |

Im Zweifel die **Standalone-Variante** nehmen.

Das Setup ist nicht digital signiert, deshalb meldet sich beim ersten Start
Windows SmartScreen („Der Computer wurde durch Windows geschützt“). Einfach
auf **„Weitere Informationen“** → **„Trotzdem ausführen“** klicken.

## Anleitung

Die ausführliche Anleitung mit Screenshots – Installation, Bedienung,
Einstellungen, Update, Deinstallation und häufige Fragen – findest du hier:

**➜ [docs/ANLEITUNG.md](docs/ANLEITUNG.md)**

## Wie es dazu kam

Unter Windows 10 hatte ich immer den NetSpeed Monitor unten in der Taskleiste –
ein kurzer Blick genügte, und ich wusste, was im Netzwerk los ist. Mit Windows 11
war damit Schluss: Die Technik dahinter gibt es nicht mehr, und keine der
Alternativen hat mich wirklich überzeugt. Also habe ich mir gedacht: Dann baue
ich mir eben selbst eins – genau so, wie ich es haben will.

## Hinweis

Speedy Monitor ist ein Hobbyprojekt. Windows 11 bietet keine offizielle
Schnittstelle mehr für eigene Anzeigen in der Taskleiste; Speedy Monitor
dockt sich deshalb als eigenes Fenster passgenau an die Taskleiste an. Das
funktioniert zuverlässig, kann aber nach größeren Windows-Updates eine
Anpassung erfordern. Probleme gern als [Issue](../../issues) melden.

## Credits & Lizenz

Entwickler: **breiti35** · Code-Mitarbeit: **Claude (Anthropic)**

Lizenz: [MIT](LICENSE)

---

## Für Entwickler

### Technischer Hintergrund

Das Original lief als Taskbar-Deskband – eine Technik, die Windows 11
komplett entfernt hat. Speedy Monitor verwendet stattdessen ein eigenes,
nicht aktivierbares Layered Window (`WS_EX_TOOLWINDOW` + `WS_EX_NOACTIVATE`),
das per UI Automation die Position des System-Tray-Clusters ermittelt
(`TaskbarLayoutHelper.cs`) und sich direkt links daneben andockt
(`TaskbarOverlayWindow.cs`).

Da Windows 11 die Taskleiste mit erhöhter Shell-Priorität rendert und sie
sich bei eigenen Ereignissen (Hover, Klicks, Icon-Updates) immer wieder über
normale Topmost-Fenster legt, holt sich das Panel bei jeder
Werteaktualisierung per `SetWindowPos` zurück nach vorne. Zusätzlich
reagiert es über einen `SetWinEventHook` auf Vordergrundwechsel (Start-Menü,
Suche, Schnelleinstellungen, Alt+Tab …) mit einer kurzen Salve an
Reassert-Versuchen. Während ein Shell-Flyout offen ist, liegt kurz legitim
die Taskleiste obenauf – wie beim alten Deskband auch. Vollbild-Apps und eine
automatisch ausgeblendete Taskleiste werden erkannt; dann blendet sich das
Panel aus. Die Taskleisten werden alle 5 s abgeglichen (Auflösungs- und
Monitorwechsel, Explorer-Neustart).

Kein offiziell unterstützter Mechanismus, aber der bestmögliche Ersatz ohne
Systemeingriff (kein Taskleisten-Mod wie ExplorerPatcher nötig).

Weitere Details: zählt IPv4 + IPv6, nur eine Instanz (Mutex), Einstellungen
und Statistik werden atomar gespeichert (`%AppData%\SpeedyMonitor\settings.json`
bzw. `stats.json`, Tageswerte ca. 400 Tage), Fehler landen in
`%AppData%\SpeedyMonitor\error.log`.

### Bauen

Benötigt das .NET 8 SDK.

```
dotnet build                    # Debug-Build unter src\NetSpeedMonitor\bin\Debug\...
dotnet publish src\NetSpeedMonitor\NetSpeedMonitor.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true -o publish
```

Setup bauen (benötigt zusätzlich Inno Setup 6.7+; Ergebnis unter
`installer\Output\`):

```
powershell -ExecutionPolicy Bypass -File build-installer.ps1
```

Das Setup installiert pro Benutzer ohne Admin-Rechte nach
`%LocalAppData%\Programs\Speedy Monitor` (Startmenü-Eintrag, optional
Desktop-Icon und Autostart über `HKCU\...\Run`). Die Deinstallation fragt, ob
Einstellungen und Statistik gelöscht werden sollen.

### Projektstruktur (`src/NetSpeedMonitor`)

- `Program.cs` – Einstieg, Einzelinstanz-Mutex, globale Fehlerbehandlung
- `TrayApplicationContext.cs` – verbindet Overlay, Monitor, Kontextmenü, Flyout und Dialoge
- `NetworkMonitor.cs` – pollt die `NetworkInterface`-Zähler und berechnet die Transferrate
- `TaskbarOverlayWindow.cs`, `Overlay*.cs` – Layered-Window-Panel, Rendering, Topmost-/Vollbild-Logik
- `TaskbarLayoutHelper.cs` – Taskleisten- und Tray-Cluster-Position, Auto-Hide-Erkennung
- `StatsFlyout.cs` – Statistik-Popup mit Live-Graph
- `HistoryWindow.cs`, `HistoryChart.cs` – Verlaufsfenster mit Balkendiagramm und CSV-Export
- `MenuRenderer.cs` – Kontextmenü im Windows-11-Stil
- `SettingsForm.cs`, `AboutDialog.cs`, `ConfirmDialog.cs` – Dialoge
- `ThemeHelper.cs` – Hell/Dunkel-Paletten, dunkle Titelleisten, abgerundete Ecken
- `StatsStore.cs` – Sitzungs-/Tages-/Wochen-/Monats-Traffic
- `AppSettings.cs` – Einstellungen
- `AppPaths.cs`, `AppLog.cs`, `AppIconProvider.cs`, `AutostartHelper.cs`, `Formatting.cs`, `WindowActivation.cs` – Hilfsklassen
