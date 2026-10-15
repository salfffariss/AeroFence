# Contributing to AeroFence

Thank you for your interest in contributing to **AeroFence**! We welcome contributions from developers, designers, and enthusiasts of modern Windows desktop customization.

---

## 🛠️ Development Setup

### Prerequisites
- **Windows 10 (Build 19041+) or Windows 11 (Recommended)**
- **.NET 9.0 SDK** (or later): [Download .NET 9](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Visual Studio 2022** (v17.12+ with *.NET Desktop Development* workload) or **Visual Studio Code** / **JetBrains Rider**.

### Getting the Code
```bash
git clone https://github.com/<username>/AeroFence.git
cd AeroFence
```

### Building the Project
You can build the entire solution via the .NET CLI:
```bash
dotnet build AeroFence.sln -c Release
```
Or run the complete packaging script:
```powershell
./build.ps1
```

---

## 📐 Project Architecture

```
AeroFence/
├── .github/                 # CI/CD Workflows & Issue/PR templates
├── src/
│   ├── AeroFence/           # Core WPF .NET 9 desktop application
│   │   ├── Models/          # Fence, Tab, Item, & Config data entities
│   │   ├── ViewModels/      # MVVM MainViewModel & RelayCommands
│   │   ├── Views/           # Windows, UserControls, & Overlay Callouts
│   │   ├── Services/
│   │   │   └── Native/      # DWM Acrylic, Win32 Hooks, & Desktop Pinning
│   │   └── Assets/          # Icons, vectors, & visual resources
│   └── AeroFence.Installer/ # Modern Fluent self-extracting single-file installer
├── AeroFence.sln            # Root Visual Studio Solution
└── build.ps1                # Automated packaging & build script
```

---

## 📝 Commit Conventions

We follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

- `feat:` Adds a new feature or capability (e.g., `feat(tabs): add drag-to-reorder tabs`)
- `fix:` Fixes a bug (e.g., `fix(desktop): restore left-click marquee box drawing`)
- `docs:` Documentation only changes (e.g., `docs: update build instructions in README`)
- `style:` Changes that do not affect the meaning of the code (formatting, whitespaces)
- `refactor:` Code changes that neither fix a bug nor add a feature
- `perf:` A code change that improves performance or reduces memory usage
- `chore:` Maintenance tasks, dependency updates, build scripts

---

## 🚀 Pull Request Workflow

1. **Fork the repository** on GitHub.
2. **Create a descriptive feature branch**:
   ```bash
   git checkout -b feat/my-awesome-feature
   ```
3. **Commit your changes** following the commit message conventions.
4. **Ensure clean compilation**:
   ```bash
   dotnet build AeroFence.sln -c Release
   ```
5. **Push to your fork** and submit a Pull Request targeting the `main` branch.
6. Provide a concise summary of changes and attach screenshots or screen recordings where applicable.

---

## 💡 Code Quality & Guidelines

- Maintain the ultra-low memory footprint (~10-25 MB RAM).
- Zero background CPU usage during idle state.
- Ensure all Win32 native handles (`HwndSource`, Hooks, Icons) are properly disposed.
- Adhere to the existing `.editorconfig` rules (4 spaces, UTF-8, PascalCase for classes/methods).
