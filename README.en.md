[Türkçe](README.md) | [English](README.en.md)
# GoodbyeDPI Tray
[![Version](https://img.shields.io/github/v/release/trfurkan/GoodbyeDPI-Tray?label=version)](https://github.com/trfurkan/GoodbyeDPI-Tray/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/trfurkan/GoodbyeDPI-Tray/total?label=downloads&color=2ea44f)](https://github.com/trfurkan/GoodbyeDPI-Tray/releases)
[![License](https://img.shields.io/github/license/trfurkan/GoodbyeDPI-Tray?label=license)](LICENSE)
![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows)
![.NET Framework 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4)
A Windows application for [GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI). Start and stop [GoodbyeDPI-Turkey](https://github.com/cagritaskn/GoodbyeDPI-Turkey) profiles and run them at Windows startup without a command line or CMD files.
**[⬇️ Download the latest version](https://github.com/trfurkan/GoodbyeDPI-Tray/releases/latest)** · [Website (English)](https://trfurkan.github.io/GoodbyeDPI-Tray/en/) · [User guide (Turkish)](docs/KULLANIM.md)
<img src="docs/images/main-dark.png" width="640" alt="GoodbyeDPI Tray main window (Turkish interface)">
## Features
- Runs GoodbyeDPI as a Windows service; starting, stopping and switching profiles **do not require administrator permission**.
- All seven GoodbyeDPI-Turkey profiles, with the same parameters.
- System tray icon, taskbar jump list and automatic startup with Windows.
- **Access test:** opens real HTTPS connections to blocked and regular websites and displays the results.
- Console window showing live engine output.
- Turkish, English and Russian interface; seven themes.
- Single-file installer with clean uninstallation.
> [!NOTE]
> GoodbyeDPI Tray is not a VPN. Traffic is not routed through another server; it works locally to bypass your internet provider’s DPI and DNS restrictions. It cannot unblock websites blocked directly by IP address.
## Installation
1. Download `GoodbyeDPI-Installer-<version>.exe` from [Releases](https://github.com/trfurkan/GoodbyeDPI-Tray/releases/latest).
2. Run it. If Windows SmartScreen displays a warning, choose **More info → Run anyway**. This warning appears because the installer is unsigned.
3. Acknowledge the notice and click **Install**. Administrator permission is required only for installation and uninstallation.
Requirements: Windows 10 / 11 (x86 or x64), .NET Framework 4.8.
If you previously used the GoodbyeDPI-Turkey service script, the installer detects the existing service and asks for permission to take it over. To uninstall, use **Uninstall application** in the app or Windows Settings > Apps.
## How it works
| Component | What it does |
| --- | --- |
| **Service** (`GoodbyeDPI`) | Runs as SYSTEM using `GoodbyeDPI.Tray.exe --engine-service <profile>`, starts `goodbyedpi.exe` and writes its output to `engine-console.log`. |
| **Tray application** | Runs with normal user privileges. Starts/stops the service and sends profile and startup requests as service-specific commands; the service applies the changes itself. |
| **Permissions** | The installer grants signed-in users only permission to start, stop and send requests to the service. Changing service settings remains restricted to administrators. |
| **Installation folder** | When installed outside `Program Files`, only administrators and SYSTEM can modify the folder. |
| **Update check** | Queries the GitHub API for the latest release once a day; sends no other information. Can be disabled in the About window. |
## Building from source
Visual Studio is not required; the .NET Framework compiler (`csc.exe`) included with Windows is used.
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1
```
| Output | Description |
| --- | --- |
| `build\app\GoodbyeDPI.Tray.exe` | Application, service and uninstaller in one file |
| `dist\GoodbyeDPI-Installer.exe` | Single-file installer containing all required files |
### Tests
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify.ps1
```
Other scripts under `tests\` check windows, installation flows, tray behavior and translations. Scripts under `tests\system\` modify the actual installation and service; run them only on a test machine.
### Project structure
```text
src/                 C# source (WinForms, .NET Framework 4.8)
  Program.cs         Entry point, service management
  MainForm.cs        Main window and system tray
  EngineService.cs   Windows service and engine console
  Setup.cs           Installer and uninstaller
  Lang*.cs           Turkish / English / Russian strings
scripts/             Build scripts
tests/               Test scripts
vendor/goodbyedpi/   GoodbyeDPI-Turkey 0.2.3rc3 binaries and licenses
docs/                User guide, release notes, website (GitHub Pages)
```
## Contributions and feedback
[Open an issue](https://github.com/trfurkan/GoodbyeDPI-Tray/issues/new) to report a bug or suggest an improvement. Including the application log (`%LOCALAPPDATA%\GoodbyeDPI Tray\Logs\application.log`) helps diagnose bugs. If you find the project useful, you can support it with a ⭐.
## About me
[trfurkan](https://github.com/trfurkan)
Hi, I’m Furkan. In my spare time, I build simple, user-friendly projects as a hobby to make everyday life easier. I make no claim to professional expertise; I’m happy if something I’ve built is useful to you. Thank you for your interest and support!
## License and acknowledgments
The application code is licensed under [Apache 2.0](LICENSE). Bundled third-party components retain their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for details.
- [GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI) — ValdikSS
- [GoodbyeDPI-Turkey](https://github.com/cagritaskn/GoodbyeDPI-Turkey) — cagritaskn
- [WinDivert](https://github.com/basil00/WinDivert) — basil00
> [!WARNING]
> This application is entirely experimental and comes with no warranty. It may not work on every network and may affect your connection. You use it at your own discretion and assume all responsibility. GoodbyeDPI Tray is an independent project, not an official release of ValdikSS/GoodbyeDPI or cagritaskn/GoodbyeDPI-Turkey.
