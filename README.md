# Gomoku Avalonia (五子棋 Avalonia)

A modern, cross-platform Gomoku (五子棋) client game built with **Avalonia UI** and **.NET 10 / C# 14**. It supports playing against a cloud-based AI model, features customizable UI skins (Classic Wood & Cyberpunk), and is optimized for cross-platform deployment, particularly for **Android** and **Desktop**.

---

## 🌟 Key Features

- **Cross-Platform Compatibility**: Targets Android APK as the primary runtime environment and Windows/macOS/Linux Desktop as the secondary environment for development and debugging. Also supports iOS and WebAssembly (Browser).
- **Smart AI Opponent**:
  - Connects to remote AI inference servers via a resilient API client.
  - Supports dual backends: **Direct Hugging Face Space** (default) or **Vercel Proxy**.
  - Offers selection between multiple pre-trained AI models (`best_model`, `Medium-ppo`, `easy-iter`, `Third-dqn`, `12.13best`).
  - **AI Hint**: Ask the AI for a move recommendation complete with confidence rating.
- **Rich Theme Skins**:
  - **Classic Wood**: Elegant traditional wooden board and pieces.
  - **Cyberpunk**: Vibrant, glowing neon grid designed for a futuristic look.
  - Custom animations including scale transitions for placed stones and continuous breathing pulses on the last move.
- **Dynamic Wave Sound Synthesis**: Audio effects are generated programmatically on the fly (generating Triangle/Sine wave PCM streams) rather than using heavy asset files, ensuring a lightweight package.
- **Resilient Networking**: Automatically monitors network status. Displays reconnection overlays, retries API requests every 2 seconds during disruptions, and manages task cancellation cleanly.
- **User Settings & Persistence**: Persists scores (wins/losses/draws), active skins, selected model, and custom API endpoints locally across sessions.

---

## 🛠️ Technology Stack

- **Framework**: [Avalonia UI v12.0.4](https://avaloniaui.net/)
- **Runtime**: .NET 10.0 / C# 14
- **Architecture**: MVVM using [CommunityToolkit.Mvvm v8.4.0](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- **Dependency Management**: Central Package Management (CPM) enabled via `Directory.Packages.props`.

---

## 📂 Project Structure

```
Gomoku_Avalonia/                          <-- Solution Root
├── Gomoku_Avalonia.slnx                  <-- Modern Visual Studio Solution File
├── Directory.Packages.props              <-- Centralized NuGet Package Versioning
├── AGENTS.md                             <-- Environment and developer specifications
├── Gomoku_Avalonia/                      <-- Core MVVM & Shared Game Logic (Shared Library)
│   ├── Models/                           <-- Core game logic engines and data structures
│   │   └── GomokuEngine.cs               <-- Board representation, history stack, win check logic
│   ├── Services/                         <-- Supporting services
│   │   ├── GomokuApiClient.cs            <-- Hugging Face/Vercel API integration client
│   │   └── SoundService.cs               <-- Procedural PCM audio generator
│   ├── ViewModels/                       <-- MVVM ViewModels
│   │   ├── ViewModelBase.cs              <-- Base viewmodel setup
│   │   └── MainViewModel.cs              <-- Core state, theme styling, network status handlers
│   └── Views/                            <-- MVVM Views (XAML files & customized drawing views)
│       ├── GomokuBoardView.cs            <-- Custom rendering logic for the 15x15 board
│       ├── MainView.axaml                <-- Main game control board layout
│       └── MainWindow.axaml              <-- Desktop window wrapper
├── Gomoku_Avalonia.Desktop/              <-- Desktop Host Project (Windows/macOS/Linux)
├── Gomoku_Avalonia.Android/              <-- Android Host Project (targets net10.0-android)
├── Gomoku_Avalonia.iOS/                  <-- iOS Host Project
└── Gomoku_Avalonia.Browser/              <-- WebAssembly Host Project
```

---

## 🚀 Getting Started

### Prerequisites

- Install [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Install [.NET Multi-platform App UI workload](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation) (for Android/iOS support)

### Run the Desktop Client (Windows, macOS, Linux)

For rapid development and local testing, run the Desktop project:

```bash
dotnet run --project Gomoku_Avalonia.Desktop/Gomoku_Avalonia.Desktop.csproj
```

### Build & Package Android APK

To publish a release build for Android:

```bash
dotnet publish Gomoku_Avalonia.Android/Gomoku_Avalonia.Android.csproj -c Release -f net10.0-android /p:AndroidPackageFormat=apk
```

The compiled APK will be located under:  
`Gomoku_Avalonia.Android/bin/Release/net10.0-android/publish/`

---

## 📝 Development Guidelines

1. **Central Package Management**: Do **NOT** specify `Version` attributes directly inside `<PackageReference>` nodes in individual `.csproj` files. Manage and update all package versions centrally in [Directory.Packages.props](file:///C:/AAAAAAAAAAA_temp/desktop/Hephaestus_Repository/Colosseum/Gomoku_Avalonia/Directory.Packages.props).
2. **Nullable Safety**: Keep `#nullable enable` active across all C# source files. Code should compile without nullable references warning.
3. **Audio Generation**: Keep audio assets procedural inside `SoundService` to prevent deployment bloating.
