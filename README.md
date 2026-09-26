# NetSpeed Monitor

Schlanker Nachbau des klassischen Windows-NetSpeedMonitor: eine Textanzeige
("↑ ... / ↓ ...") direkt in der Taskleiste, neben Netzwerk/Lautstärke/Uhr.
Das Original lief als Taskbar-Deskband – eine Technik, die Windows 11
komplett entfernt hat. Diese Version erreicht optisch fast dasselbe Ergebnis
über ein eigenes, randloses Overlay-Fenster, das sich automatisch an die
Taskleiste andockt (siehe Abschnitt "Technischer Hintergrund").

## Funktionen

- Textanzeige direkt in der Taskleiste: `↑ 2,5 KB/s` / `↓ 107,2 KB/s`,
  fett, farbcodiert (orange = Upload, grün = Download), fest angedockt
  links neben Netzwerk/Lautstärke/Uhr
- Feste Panelgröße – wechselt die Ziffernanzahl (z. B. `9,9 KB/s` →
  `123,4 KB/s`), bewegt oder verschiebt sich das Panel nicht
- Bleibt zuverlässig über der Taskleiste sichtbar (reasserted sich aktiv
  als Topmost-Fenster, da Windows 11 die Taskleiste selbst bevorzugt)
- Passt sich automatisch an hellen/dunklen Windows-Anzeigemodus an
- Tooltip beim Hovern mit exakten Werten sowie Tages- und Wochen-Traffic
- Rechtsklick auf das Panel → Einstellungen/Beenden; Doppelklick öffnet
  direkt die Einstellungen
- Netzwerkadapter wählbar (Einstellungen), Standard: alle aktiven Adapter kombiniert
- Einheit umschaltbar: Automatisch, KB/s, MB/s, kbit/s, Mbit/s
- Aktualisierungsintervall einstellbar (250–5000 ms)
- Autostart mit Windows
- Panel bei Bedarf komplett abschaltbar (Einstellungen)

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

- `TrayApplicationContext.cs` – Einstiegspunkt der Anwendung, verbindet Overlay, Monitor und Menü
- `NetworkMonitor.cs` – pollt `NetworkInterface`-Zähler und berechnet die Transferrate
- `TaskbarOverlayWindow.cs` – randloses Panel mit der Live-Textanzeige, feste Größe, haelt sich selbst im Vordergrund
- `TaskbarLayoutHelper.cs` – ermittelt Taskleisten- und Tray-Cluster-Position für die Andockung
- `Formatting.cs` – Zahlenformatierung für Panel und Tooltip
- `StatsStore.cs` – Tages-/Wochen-Traffic-Persistenz (`%AppData%\NetSpeedMonitor\stats.json`)
- `AppSettings.cs` – Einstellungen-Modell und Persistenz (`%AppData%\NetSpeedMonitor\settings.json`)
- `SettingsForm.cs` – Einstellungsdialog
- `AutostartHelper.cs` – Autostart-Registry-Eintrag
