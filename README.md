# Speedy Monitor

**Your network speed right in the Windows 11 taskbar – just like the classic NetSpeed Monitor.**

[Deutsch](README.de.md) · English

![Speedy Monitor in the taskbar](docs/images/taskleiste-zoom.png)

## Features

- **Live display in the taskbar**, right next to the tray icons: ▲ upload (orange) and
  ▼ download (green) – no box around it, matching the light or dark Windows theme
- **Click** for statistics: current rates, data volume for session / today / week / month,
  live graph of the last 60 measurements
- **History** as a bar chart (30 days, 90 days, 12 months) with totals, daily average and
  CSV export
- **Double-click** opens the settings, **right-click** a menu with all functions
- Selectable unit (automatic, KB/s, MB/s, kbit/s, Mbit/s) and network adapter (or automatic –
  virtual adapters from Hyper-V, WSL, VirtualBox and VMware are ignored)
- Optionally **on all monitors** – every taskbar gets its own display
- Hides automatically for **fullscreen apps** (games, videos)
- Taskbar at the top or bottom – both work
- User interface in **English and German** (follows the Windows language, can be changed in
  the settings)
- Installs **without admin rights**, optionally starts with Windows
- **No telemetry**, no internet connection – all data stays on your PC

## Download

On the **[Releases](../../releases)** page there are two setup variants:

| File | For whom? |
|---|---|
| `SpeedyMonitor_Setup_<version>.exe` | Small. Requires the [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0) – the setup checks for it and helps you install it. |
| `SpeedyMonitor_Setup_<version>_Standalone.exe` | Larger, but with the runtime built in – **works everywhere without extra installs**. |

If in doubt, take the **Standalone** variant.

The setup is not digitally signed, so Windows SmartScreen shows "Windows protected your PC"
on first launch. Click **"More info"** → **"Run anyway"**.

Usage: **click** the display for statistics, **double-click** for settings, **right-click**
for the menu. Settings and statistics are stored in `%AppData%\SpeedyMonitor`. The detailed
guide with screenshots is currently available in German: [docs/ANLEITUNG.md](docs/ANLEITUNG.md).

## How it came about

On Windows 10 I always had NetSpeed Monitor in my taskbar – one quick glance and I knew what
was going on in my network. With Windows 11 that was over: the technology behind it no longer
exists, and none of the alternatives really convinced me. So I thought: then I'll just build
one myself – exactly the way I want it.

## Note

Speedy Monitor is a hobby project. Windows 11 no longer offers an official interface for
custom displays in the taskbar, so Speedy Monitor docks itself precisely to the taskbar as its
own window. This works reliably but may need adjustments after major Windows updates. Feel
free to report problems as an [issue](../../issues).

## Credits & license

Developer: **breiti35** · Code contribution: **Claude (Anthropic)**

License: [MIT](LICENSE)
