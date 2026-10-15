# ⚡ AeroFence

<div align="center">

![AeroFence Banner](src/AeroFence/Assets/app.png)

**A lightweight, modern Windows 11 desktop organization tool inspired by Stardock Fences.**  
*Crafted with .NET 9, C# 13, and WPF — featuring native frosted Aero Acrylic glass, tabbed containers, folder portals, and ultra-low memory footprint.*

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square&logo=githubactions)](https://github.com)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-blue?style=flat-square&logo=windows)](https://microsoft.com/windows)
[![Framework](https://img.shields.io/badge/.NET-9.0%20WPF-purple?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![RAM Usage](https://img.shields.io/badge/RAM-~15%20MB-success?style=flat-square&logo=speedtest)](https://github.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](CONTRIBUTING.md)

[Features](#-key-features) • [Installation](#-quick-start) • [Building](#-building-from-source) • [Architecture](#-architecture) • [Shortcuts](#-interaction-cheat-sheet) • [Contributing](#-contributing)

</div>

---

## ✨ Key Features

- 🎨 **Fluent Frosted Aero Glass**: Native Windows DWM backdrop composition with real-time blur, customizable tint color, and opacity sliders.
- 📌 **Desktop Layer Anchoring (`HWND_BOTTOM`)**: Fences sit directly on your wallpaper below regular windows. Fully resistant to `Win + D` (Show Desktop) and `Win + M` minimizing.
- 🗂️ **Tabbed Fences**: Group shortcuts into separate categories (e.g., *Games*, *Work*, *Tools*) with custom category icons (Rocket, Gamepad, Code, Star, Heart, etc.).
- 📁 **Folder Portals**: Mirror any local directory directly onto your desktop with real-time two-way filesystem synchronization.
- 🖱️ **Desktop Marquee Selection**: Drag a rectangle on your desktop with **Left Click** or **Right Click** to summon the instant Action Pill (`+ Create Fence` / `📁 Folder Portal`).
- 🔄 **Smart Auto-Rollup**: Double-click title bar to collapse into a slim bar. Hovering or dragging items over the bar smoothly expands it with cubic easing.
- ⚡ **Native Drag & Drop**: Drag shortcuts and files into fences seamlessly. Drag an item out to return it to the desktop.
- 🔍 **Quick-Type Search**: Type anywhere inside a fence to instantly filter apps and shortcuts.
- 🚀 **High Performance & Low Memory**: Consumes only **~10–25 MB RAM** and **0.0% CPU** during idle state.
- 🛡️ **Modern Installer & Uninstaller**: Fluent single-file setup (`AeroFence_Setup.exe`) with complete Windows Settings / Control Panel uninstallation support.

---

## 🚀 Quick Start

### Option 1: Download Pre-built Installer
1. Grab the latest `AeroFence_Setup.exe` from the [Releases](https://github.com) page.
2. Run the installer and click **Install Now**.
3. AeroFence will launch automatically with a clean default *Workspace* fence on your desktop!

### Option 2: Build From Source
Ensure you have the [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) installed on Windows 10/11.

```powershell
# Clone the repository
git clone https://github.com/<username>/AeroFence.git
cd AeroFence

# 1-Click Automated Build & Package
./build.ps1
```
The output installer will be generated in `dist/AeroFence_Setup.exe`.

---

## 🏗️ Architecture

AeroFence is built using modern MVVM principles with clean separation of UI, business logic, and low-level Win32 interop:

```
AeroFence/
├── .github/                       # CI/CD workflows, issue templates, & PR guidelines
├── src/
│   ├── AeroFence/                 # Core WPF desktop application
│   │   ├── Models/                # Fence, Tab, Item, & AppConfig data models
│   │   ├── ViewModels/            # MVVM MainViewModel & RelayCommands
│   │   ├── Views/                 # Main Window, ItemCard, Overlay Pill, & Settings
│   │   ├── Services/
│   │   │   └── Native/            # Win32 DWM Acrylic, Low-Level Hooks, & Desktop Pinning
│   │   └── Assets/                # Icons & Fluent Design resources
│   └── AeroFence.Installer/       # Self-contained single-file setup & uninstaller
├── AeroFence.sln                  # Visual Studio Solution
└── build.ps1                      # Automated build & packaging script
```

### Key Technical Highlights:
- **`DesktopSelectionService`**: Low-level global mouse hook (`WH_MOUSE_LL`) tracking native Explorer marquee box creation without injecting DLLs.
- **`DesktopManagerService`**: Places and locks fence windows at `HWND_BOTTOM` while preventing minimize signals from `WM_SYSCOMMAND / SC_MINIMIZE`.
- **`DwmService`**: Direct access to `DwmSetWindowAttribute` (`DWMWA_SYSTEMBACKDROP_TYPE`) for native Windows 11 Acrylic materials.

---

## ⌨️ Interaction Cheat Sheet

| Action | How to Trigger |
| :--- | :--- |
| **Create New Fence** | Drag a selection rectangle on empty desktop space with Left or Right Mouse Button |
| **Roll-Up / Collapse** | Double-click the fence title bar |
| **Expand Collapsed Fence**| Hover mouse over the title bar or drag an item over it |
| **Rename Fence Title** | Double-click title text or click settings gear ⚙️ → *Rename Fence* |
| **Add / Switch Tabs** | Click `+` button in tab bar, or drag items directly onto tab headers |
| **Quick Search** | Click anywhere inside a fence and start typing |
| **Move Fence** | Click and drag the fence title bar |
| **Resize Fence** | Drag any window border or corner |
| **Open Settings** | Click settings gear ⚙️ in title bar or right-click System Tray icon |

---

## 🇮🇩 Ringkasan Bahasa Indonesia

**AeroFence** adalah aplikasi pengorganisir desktop modern untuk Windows 11 & 10 yang terinspirasi dari Stardock Fences.
- **Desain Modern**: Efek kaca frosted Aero Acrylic yang elegan dengan transparansi dinamis.
- **Ringan & Hemat Daya**: Penggunaan RAM hanya ~15 MB dan 0% CPU saat idle.
- **Fitur Lengkap**: Dukungan Tab, Folder Portal, Auto-Rollup, Search instan, dan pembuatan fence langsung dengan menggambar kotak di desktop.
- **Installer & Uninstaller Mandiri**: Dilengkapi installer modern dan uninstaller bersih yang terintegrasi dengan Windows Settings.

---

## 🤝 Contributing

Contributions are what make the open-source community an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

Please see [CONTRIBUTING.md](CONTRIBUTING.md) for detailed guidelines, commit conventions, and development setup.

---

## 📄 License

Distributed under the **MIT License**. See [LICENSE](LICENSE) for more information.

---

<div align="center">
  <sub>Crafted with passion for a clutter-free Windows desktop experience.</sub>
</div>
