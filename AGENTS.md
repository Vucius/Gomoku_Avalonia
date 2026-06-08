# Developer System Environment

This repository contains the Gomoku Avalonia cross-platform game client.

## Runtime Targets

- Primary runtime: Android APK.
- Release-ready desktop target: Windows x64 desktop.
- Secondary/future targets: Linux desktop, macOS desktop, iOS, Browser/WASM.
- Language/runtime: C# / .NET 10.
- UI framework: Avalonia UI 12.0.4.
- State architecture: MVVM with CommunityToolkit.Mvvm.
- Package management: Central Package Management through `Directory.Packages.props`.

## Repository Layout

```text
Gomoku_Avalonia/
|-- Gomoku_Avalonia.slnx
|-- Directory.Packages.props
|-- Directory.Build.props
|-- AGENTS.md
|-- README.md
|-- packaging/
|   `-- publish-desktop.ps1
|-- Gomoku_Avalonia/
|   |-- App.axaml
|   |-- Assets/
|   |-- Models/
|   |-- Services/
|   |-- ViewModels/
|   `-- Views/
|-- Gomoku_Avalonia.Android/
|-- Gomoku_Avalonia.Desktop/
|-- Gomoku_Avalonia.Browser/
`-- Gomoku_Avalonia.iOS/
```

## Core Implementation Notes

- `Gomoku_Avalonia/Models/GomokuEngine.cs` owns the board state, move history,
  valid-move checks, and winner detection.
- `Gomoku_Avalonia/Services/GomokuApiClient.cs` sends inference requests to the
  direct Hugging Face endpoint by default and supports the Vercel proxy shape.
- `Gomoku_Avalonia/Services/SoundService.cs` generates PCM sounds in code. The
  current desktop playback implementation is Windows-only.
- `Gomoku_Avalonia/ViewModels/MainViewModel.cs` owns game flow, persistence,
  score state, model selection, skin selection, retry behavior, and settings.
- `Gomoku_Avalonia/Views/MainView.axaml` contains the shared responsive UI.
- `Gomoku_Avalonia/Views/MainWindow.axaml` is the desktop window shell.

## Build Commands

Run the desktop app:

```powershell
dotnet run --project Gomoku_Avalonia.Desktop/Gomoku_Avalonia.Desktop.csproj
```

Build desktop release:

```powershell
dotnet build Gomoku_Avalonia.Desktop/Gomoku_Avalonia.Desktop.csproj -c Release
```

Package Windows desktop release:

```powershell
powershell -ExecutionPolicy Bypass -File packaging\publish-desktop.ps1 -Runtime win-x64 -Version 1.0.0
```

Use `-NoRestore` only after packages have already been restored locally.

Build Android APK:

```powershell
dotnet publish Gomoku_Avalonia.Android/Gomoku_Avalonia.Android.csproj `
  -c Release `
  -f net10.0-android `
  -r android-arm64 `
  /p:AndroidPackageFormat=apk `
  /p:AcceptAndroidSDKLicenses=True
```

## Release Automation

The tag workflow is `.github/workflows/release-apps.yml`.

It builds:

- `Gomoku_Avalonia.Android` on Ubuntu and uploads the APK.
- `Gomoku_Avalonia.Desktop` on Windows and uploads the executable, portable zip,
  and SHA256 file.
- A GitHub Release containing all artifacts.

## Development Rules

1. Do not add `Version` attributes to `PackageReference` items. Use
   `Directory.Packages.props`.
2. Keep nullable reference types enabled.
3. Keep desktop packaging output under `artifacts/`, `bin/`, or `obj/`; these
   paths are ignored and should not be committed.
4. Do not treat Linux/macOS desktop builds as release-complete until audio and
   runtime smoke tests are verified on those operating systems.
5. Android debug builds intentionally route intermediate and output files to
   `%TEMP%\GomokuAvalonia\...` through `Directory.Build.props` to avoid long
   Xamarin.Android deployment paths on Windows.
