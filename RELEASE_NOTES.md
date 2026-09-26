# Speedy Monitor 1.0.0

Erste öffentliche Version. Zeigt die aktuelle Upload- und Download-Geschwindigkeit direkt in der
Windows-11-Taskleiste an – so wie früher der NetSpeed Monitor unter Windows 10.

## Download

| Datei | Größe | Für wen |
|---|---|---|
| `SpeedyMonitor_Setup_1.0.0.exe` | ca. 2,4 MB | Wenn die [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0) schon installiert ist. Das Setup prüft das und bietet sonst den Download an. |
| `SpeedyMonitor_Setup_1.0.0_Standalone.exe` | ca. 47 MB | Funktioniert ohne weitere Voraussetzungen – im Zweifel diese nehmen. |

Installation ohne Administratorrechte, nur für den aktuellen Benutzer. Die Anleitung mit Screenshots
steht in [docs/ANLEITUNG.md](docs/ANLEITUNG.md).

> **Hinweis zur Windows-Warnung:** Das Setup ist nicht digital signiert. Windows SmartScreen zeigt
> deshalb „Der Computer wurde durch Windows geschützt“. Auf „Weitere Informationen“ und dann
> „Trotzdem ausführen“ klicken.

## Funktionen

- Upload/Download direkt auf der Taskleiste, farbcodiert, fest ausgerichtet – nichts springt
- Klick: Statistik mit Sitzung, Heute, Woche, Monat und Live-Graph
- Verlauf pro Tag oder Monat als Balkendiagramm, CSV-Export
- Rechtsklick-Menü: Einheit und Netzwerkadapter direkt umschalten
- Optional auf den Taskleisten aller Monitore
- Blendet sich bei Vollbild-Spielen und -Videos aus
- Hell/Dunkel passend zu Windows, scharf bei jeder Skalierung
- Zählt IPv4 und IPv6, ignoriert virtuelle Adapter (Hyper-V, WSL, …), damit nichts doppelt zählt
- Keine Daten verlassen den PC

## Bekannte Einschränkungen

- Windows 11 bietet keine offizielle Schnittstelle für Anzeigen in der Taskleiste. Speedy Monitor
  dockt sich deshalb als eigenes Fenster an. Größere Windows-Updates können Anpassungen nötig machen.
- Getestet unter Windows 11 mit Taskleiste oben und unten; Verhalten bei automatisch ausgeblendeter
  Taskleiste ist noch wenig erprobt.
