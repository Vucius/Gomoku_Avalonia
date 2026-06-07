# Developer System Environment (AGENTS.md)

This document provides details on the development environment, project folder structure, and technical requirements for the **Gomoku_Avalonia** cross-platform game client application.

---

## 1. Development & Runtime Environment

- **OS Target**: Android APK (Primary), Windows Desktop (Secondary / Local debugging).
- **Language / Runtime**: C# 14 / .NET 10.0 (as specified in the `.csproj` files).
- **UI Framework**: **Avalonia UI v12.0.4** (with central package management enabled).
- **State & Architecture**: MVVM architecture utilizing **CommunityToolkit.Mvvm v8.4.0**.
- **Package Management**: Central Package Management (CPM) is enabled. All package versions are managed inside [Directory.Packages.props](file:///C:/AAAAAAAAAAA_temp/desktop/Hephaestus_Repository/Colosseum/Gomoku_Avalonia/Directory.Packages.props).

---

## 2. Directory Structure Analysis

The solution follows the standard Avalonia Multi-platform template:

```
Gomoku_Avalonia/                          <-- Solution Root
├── Gomoku_Avalonia.slnx                  <-- VS 2022 Solution File
├── Directory.Packages.props              <-- Centralized Package Versioning
├── AGENTS.md                             <-- Developer/Agent environment spec (this file)
├── Gomoku_Avalonia/                      <-- Shared Library Project (Core MVVM & logic)
│   ├── Gomoku_Avalonia.csproj            <-- Targets net10.0
│   ├── App.axaml / App.axaml.cs          <-- Application entry and resources
│   ├── ViewLocator.cs                    <-- View-ViewModel mapper
│   ├── Assets/                           <-- Static assets (logos, icons)
│   ├── Models/                           <-- Game logic & data models (to be created)
│   │   └── GomokuEngine.cs               <-- Core game logic
│   ├── Services/                         <-- Services (to be created)
│   │   ├── GomokuApiClient.cs            <-- API Client for cloud inference
│   │   └── SoundService.cs               <-- Synthesizer for sound effects
│   ├── ViewModels/                       <-- MVVM ViewModels
│   │   ├── ViewModelBase.cs              <-- Base viewmodel implementation
│   │   └── MainViewModel.cs              <-- Game flow, state, skin themes, network handler
│   └── Views/                            <-- MVVM Views
│       ├── MainView.axaml / .cs          <-- Main UI View (Common on all platforms)
│       └── MainWindow.axaml / .cs        <-- Desktop Window container
├── Gomoku_Avalonia.Android/              <-- Android Platform Host Project
│   ├── Gomoku_Avalonia.Android.csproj    <-- Targets net10.0-android (SupportedOSVersion=23)
│   ├── MainActivity.cs                   <-- Android Activity entry point
│   └── Application.cs                    <-- Android Application class
├── Gomoku_Avalonia.Desktop/              <-- Desktop Platform Host Project
│   ├── Gomoku_Avalonia.Desktop.csproj    <-- Targets net10.0
│   └── Program.cs                        <-- Desktop CLI entry point
├── Gomoku_Avalonia.Browser/              <-- WebAssembly Platform Host Project
└── Gomoku_Avalonia.iOS/                  <-- iOS Platform Host Project
```

---

## 3. Core Classes Implementation Details

### Models
- **`GomokuEngine.cs`**:
  - Contains board representation: `int[,] Board` (15x15 grid, 0=Empty, 1=Black/Player/AI, -1=White/Player/AI).
  - Tracks move history: `Stack<(int row, int col, int player)> History` for Undo.
  - Implements winner scanning: Checks for five-in-a-row in 8 directions, returns matching coordinates if a win condition is met.
  - Handles turn alternation and valid moves check.

### Services
- **`GomokuApiClient.cs`**:
  - Uses `HttpClient` to communicate with the remote API. Android defaults to direct Hugging Face Space access (`https://mitsutake-model-space.hf.space/gomoku/predict`) and still supports the Vercel proxy (`https://vukservices.vercel.app/api/gomoku/move`) when that base URL is configured.
  - Timeout configured at 30 seconds.
  - Does not run a separate health check before inference; request failures are handled by the ViewModel retry flow.
- **`SoundService.cs`**:
  - Generates sound synthesized waveforms dynamically to avoid packing audio assets.
  - **Android**: Uses `Android.Media.AudioTrack` to stream dynamically generated raw PCM data (e.g. Triangle/Sine waves) simulating timber clicks.
  - **Desktop / Windows**: Implements Windows/Desktop specific wave audio generation or wrapper calls.

### MVVM (ViewModels & Views)
- **`MainViewModel.cs`**:
  - Manages UI states: `IsBusy` (busy thinking overlay indicator), `Score` card (Player wins vs. AI wins), history logs.
  - Manages configuration settings: custom API Base URL.
  - Implements game flow: Skin switching (Wood vs. Cyberpunk), First-move selection, "AI Hint" querying, and undo step triggers.
  - Retries failed AI requests twice with lightweight status text, then shows a network wait overlay and keeps retrying without exiting the app.
- **`MainView.axaml`**:
  - Uses a custom canvas/layout for rendering the 15x15 board.
  - Employs smooth animations for dropping pieces (scale transitions) and last move indicator (continuous breathing pulse).
  - Renders cyberpunk neon grids or classic amber wood boards depending on the theme state.

---

## 4. Key Development & Build Commands

Ensure you have the .NET 10.0 SDK and the Android workloads installed.

### Run Local Desktop App (for rapid testing)
```bash
dotnet run --project Gomoku_Avalonia.Desktop/Gomoku_Avalonia.Desktop.csproj
```

### Debug Android from Visual Studio
Debug builds for `Gomoku_Avalonia.Android` intentionally use `Directory.Build.props` to place Android intermediate/output files under `%TEMP%\GomokuAvalonia\...` and set `EmbedAssembliesIntoApk=true`. This avoids `XARDF7024` failures from Xamarin.Android deployment cleanup tasks on long generated paths. Release publish output remains under the project directory.

`Gomoku_Avalonia.Browser` is kept in the repository but omitted from the default `.slnx` build. Building it in the same solution emits a WebAssembly native-reference warning unless `wasm-tools` is installed and native WASM linking is enabled, neither of which is needed for Android debugging.

### Build & Package Android APK
```bash
dotnet publish Gomoku_Avalonia.Android/Gomoku_Avalonia.Android.csproj -c Release -f net10.0-android /p:AndroidPackageFormat=apk
```
*Output APK path:* `Gomoku_Avalonia.Android/bin/Release/net10.0-android/publish/`

---

## 5. Development Guidelines
1. **Central Package Management**: Do **NOT** add version attributes directly inside `PackageReference` nodes in individual project files. Instead, add or update versions inside `Directory.Packages.props`.
2. **Nullable Reference Types**: Keep `#nullable enable` active across all files. Ensure warning-free code.
3. **Android Lifecycle Constraints**: For the "auto-exit" requirement, ensure platform-specific exit methods (e.g., `Activity.FinishAndRemoveTask` or `Process.KillProcess`) are triggered cleanly via platform-specific service interfaces or dependency injection.
