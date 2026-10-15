# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-10-15

### Added
- **Core Desktop Fence Containers**:
  - Resizable, movable floating containers with direct mouse coordinate tracking.
  - Windows 11 Acrylic / DWM Blur backdrop with live tint & opacity adjustments.
  - Desktop Layer Anchoring (`HWND_BOTTOM`) with seamless `Win + D` (Show Desktop) resistance.
  - Prevention of unwanted Windows 11 Snap Layouts flyout when moving near top edge.
- **Tabbed Fences & Folder Portals**:
  - Multiple tab navigation with customizable tab names and icons (Rocket, Gamepad, Code, Star, Heart, etc.).
  - Automatic tab switching on drag-hover.
  - Direct folder browsing with two-way file system sync and recursive folder navigation.
- **Marquee Desktop Selection Service**:
  - Draw a selection box on the desktop with either **Left Click** or **Right Click**.
  - Interactive Action Pill (`+ Create Fence` / `📁 Folder Portal`) placed directly where the cursor is released.
  - Complete OLE drag-and-drop collision protection to prevent unwanted popups when transferring icons.
- **Auto-Rollup on Inactivity**:
  - Double-click title bar to collapse into a sleek minimal title bar.
  - Hover or drag items over collapsed bar to smoothly expand with cubic easing animation.
- **Quick-Type Search & Launch**:
  - Instant in-fence keyboard filtering with visual highlight badge.
  - Enter to launch matching apps, Escape or Backspace to clear query.
- **Modern Fluent Installer & Uninstaller**:
  - Self-contained single-file setup executable with live progress indicator.
  - Clean uninstaller (`AeroFence_Uninstall.exe`) with options to keep or delete fences configuration.
  - Windows Start Menu and Desktop shortcut registration.
  - Windows Settings / Control Panel *Installed Apps* integration.

### Performance
- Ultra-low memory footprint (~10-25 MB RAM).
- Zero background CPU usage during idle state.
