# NetSpeed Monitor

Schlanker Nachbau des klassischen Windows-NetSpeedMonitor: eine Textanzeige
("↑ ... / ↓ ...") direkt in der Taskleiste, neben Netzwerk/Lautstärke/Uhr.
Das Original lief als Taskbar-Deskband – eine Technik, die Windows 11
komplett entfernt hat. Diese Version erreicht optisch fast dasselbe Ergebnis
über ein eigenes, randloses Overlay-Fenster, das sich automatisch an die
Taskleiste andockt (siehe Abschnitt "Technischer Hintergrund").

## Funktionen

- Textanzeige direkt auf der Taskleiste (transparentes Layered Window, kein
  Kasten): ▲/▼ farbcodiert (orange = Upload, grün = Download), Zahlen und
  Einheiten in festen Spalten ausgerichtet – das Panel bewegt sich nie
- Bleibt zuverlässig über der Taskleiste sichtbar, blendet sich aber bei
  Vollbild-Apps (Spiele, Videos) und automatisch ausgeblendeter Taskleiste aus
- Passt sich live an hellen/dunklen Windows-Modus an
- **Klick** aufs Panel: Statistik-Popup wie beim Original – aktuelle Raten,
  Sitzung/Heute/Woche/Monat, Live-Graph der letzten 60 Messungen, Adapter
- **Doppelklick**: Einstellungen (Dark Mode, gruppiert, "Standard"-Button)
- **Rechtsklick**: Menü mit Live-Werten, Einheit und Adapter direkt
  umschaltbar, Autostart, Statistik zurücksetzen, Über, Beenden
- Zählt IPv4 + IPv6; im Automatik-Modus werden virtuelle Switches
  (Hyper-V, WSL, VirtualBox, VMware) ignoriert, damit VM-Traffic nicht doppelt zählt
- Nur eine Instanz; Fehler landen in `%AppData%\NetSpeedMonitor\error.log`,
  Einstellungen/Statistik werden absturzsicher (atomar) gespeichert

Kein Tray-/Systray-Icon mehr – die komplette Bedienung läuft über das
Taskleisten-Panel selbst.

## Starten

Fertig gebaute Version liegt unter `publish\NetSpeedMonitor.exe` – einfach
doppelklicken. Das Panel erscheint automatisch links neben den System-Tray-
Icons.

## Technischer Hintergrund

Windows 11 hat die alte Deskband-Technik entfernt, mit der NetSpeedMonitor
unter Windows 10 direkt in die Taskleistenfläche eingebettet war – es gibt
keine offizielle API mehr, um dort eigene Inhalte zu zeichnen. Diese Version
behilft sich mit einem eigenen, nicht aktivierbaren Fenster (`WS_EX_TOOLWINDOW`
+ `WS_EX_NOACTIVATE`), das per UI Automation die Position des System-Tray-
Clusters ermittelt (`TaskbarLayoutHelper.cs`) und sich direkt links daneben
andockt (`TaskbarOverlayWindow.cs`). Da Windows 11 die Taskleiste selbst mit
erhöhter Shell-Priorität rendert und sie sich bei eigenen Ereignissen
(Hover, Klicks, Icon-Updates) immer wieder über normale Topmost-Fenster legt,
holt sich das Panel bei jeder Werteaktualisierung aktiv per `SetWindowPos`
zurück nach vorne. Zusätzlich reagiert es über einen `SetWinEventHook` auf
jeden Vordergrundwechsel im System (Start-Menü, Suche, Schnelleinstellungen,
Alt+Tab, ...) mit einer kurzen Salve an Reassert-Versuchen, damit es sofort
zurückkehrt, sobald ein Flyout schließt, statt bis zum nächsten Timer-Tick
verdeckt zu bleiben. Während ein Shell-Flyout selbst offen ist (z. B.
Start-Menü), liegt kurz legitim die Taskleiste obenauf – das wäre beim
alten Deskband nicht anders gewesen.

Das ist kein offiziell unterstützter Mechanismus, sondern der bestmögliche
Ersatz ohne Systemeingriff (kein Taskleisten-Mod wie ExplorerPatcher nötig).
Funktioniert zuverlässig, kann aber bei künftigen größeren Windows-Updates
angepasst werden müssen, falls sich die interne Taskleisten-Struktur ändert.

## Entwickeln / neu bauen

```
dotnet build                    # Debug-Build unter src\NetSpeedMonitor\bin\Debug\...
dotnet publish src\NetSpeedMonitor\NetSpeedMonitor.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true -o publish
```

Benötigt das .NET 8 SDK (bzw. zur Laufzeit die .NET 8 Desktop Runtime,
die mit dem SDK mitkommt).

## Projektstruktur

- `Program.cs` – Einstieg, Einzelinstanz-Mutex, globale Fehlerbehandlung
- `TrayApplicationContext.cs` – verbindet Overlay, Monitor, Kontextmenü, Flyout und Dialoge
- `NetworkMonitor.cs` – pollt `NetworkInterface`-Zähler und berechnet die Transferrate
- `TaskbarOverlayWindow.cs`, `Overlay*.cs` – Layered-Window-Panel, Rendering, Topmost-/Vollbild-Logik
- `TaskbarLayoutHelper.cs` – Taskleisten- und Tray-Cluster-Position, Auto-Hide-Erkennung
- `StatsFlyout.cs` – Statistik-Popup mit Live-Graph
- `MenuRenderer.cs` – Kontextmenü im Windows-11-Stil
- `SettingsForm.cs`, `AboutDialog.cs`, `ConfirmDialog.cs` – Dialoge
- `ThemeHelper.cs` – Hell/Dunkel-Paletten, dunkle Titelleisten, abgerundete Ecken
- `StatsStore.cs` – Sitzungs-/Tages-/Wochen-/Monats-Traffic (`%AppData%\NetSpeedMonitor\stats.json`)
- `AppSettings.cs` – Einstellungen (`%AppData%\NetSpeedMonitor\settings.json`)
- `AppLog.cs`, `AppIconProvider.cs`, `AutostartHelper.cs`, `Formatting.cs`, `WindowActivation.cs` – Hilfsklassen
