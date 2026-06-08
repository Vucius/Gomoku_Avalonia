# Gomoku Avalonia

Cross-platform Gomoku client built with Avalonia UI and .NET 10. The app can run
as an Android APK and as a Windows desktop application. The shared project owns
the game logic, MVVM state, AI API client, board renderer, skins, and persistence.

## Features

- Play Gomoku against a remote AI inference service.
- Default backend: `https://mitsutake-model-space.hf.space/gomoku/predict`.
- Optional Vercel proxy endpoint support: `https://vukservices.vercel.app/api/gomoku/move`.
- AI model selection, AI hint requests, undo, score tracking, and local settings.
- Classic wood and cyberpunk board skins.
- Procedural sound effects without bundled audio assets.
- Responsive Avalonia layout for portrait mobile and landscape desktop windows.

## Project Layout

```text
Gomoku_Avalonia/
|-- Gomoku_Avalonia.slnx
|-- Directory.Packages.props
|-- Directory.Build.props
|-- Gomoku_Avalonia/              Shared Avalonia UI, MVVM, models, services
|-- Gomoku_Avalonia.Android/      Android host project
|-- Gomoku_Avalonia.Desktop/      Windows/macOS/Linux desktop host project
|-- Gomoku_Avalonia.Browser/      WebAssembly host project
|-- Gomoku_Avalonia.iOS/          iOS host project
|-- packaging/                    Release packaging scripts
`-- .github/workflows/            CI and GitHub Release automation
```

## Requirements

- .NET 10 SDK
- Android workload when building the APK:

```powershell
dotnet workload install android
```

## Run Desktop Locally

```powershell
dotnet run --project Gomoku_Avalonia.Desktop/Gomoku_Avalonia.Desktop.csproj
```

## Build Android APK

```powershell
dotnet publish Gomoku_Avalonia.Android/Gomoku_Avalonia.Android.csproj `
  -c Release `
  -f net10.0-android `
  -r android-arm64 `
  /p:AndroidPackageFormat=apk `
  /p:AcceptAndroidSDKLicenses=True
```

APK output:

```text
Gomoku_Avalonia.Android/bin/Release/net10.0-android/android-arm64/publish/
```

## Build Windows Desktop Installers

Install Inno Setup first, then use the release script to create a self-contained
Windows installer:

```powershell
powershell -ExecutionPolicy Bypass -File packaging\publish-desktop.ps1 -Runtime win-x64 -Version 1.0.0
powershell -ExecutionPolicy Bypass -File packaging\publish-desktop.ps1 -Runtime win-x86 -Version 1.0.0
```

If NuGet packages are already restored and the local shell has no network
access, add `-NoRestore`.

Desktop installer output:

```text
artifacts/desktop/Gomoku-Avalonia-win-x64-1.0.0-setup.exe
artifacts/desktop/Gomoku-Avalonia-win-x86-1.0.0-setup.exe
```

The installers are self-contained and do not require users to install a separate
.NET runtime.

## GitHub Release

Pushing a tag triggers `.github/workflows/release-apps.yml`.

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The workflow builds and uploads:

- Android APK
- Windows x64 desktop installer
- Windows x86 desktop installer

## Development Notes

- Central Package Management is enabled. Do not put package versions in project
  files; update `Directory.Packages.props` instead.
- Keep nullable reference types enabled.
- Release desktop builds use the `GomokuAvalonia` executable name.
- Desktop sound playback currently targets Windows. Linux/macOS desktop builds
  should be treated as future work until cross-platform audio is implemented or
  sound behavior is explicitly accepted as silent.
