param(
    [ValidateSet("win-x64", "win-x86")]
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$packageVersion = $Version -replace '^[vV]', ''
$assetVersion = $packageVersion -replace '[^0-9A-Za-z._-]', '-'
$fileVersion = if ($packageVersion -match '^\d+\.\d+\.\d+$') { "$packageVersion.0" } else { "1.0.0.0" }
$projectPath = Join-Path $repoRoot "Gomoku_Avalonia.Desktop\Gomoku_Avalonia.Desktop.csproj"
$publishDir = Join-Path $repoRoot "Gomoku_Avalonia.Desktop\bin\$Configuration\net10.0\$Runtime\publish"
$artifactRoot = Join-Path $repoRoot "artifacts\desktop"
$stagingRoot = Join-Path $artifactRoot "Gomoku-Avalonia-$Runtime"
$appStageDir = Join-Path $stagingRoot "app"
$installerScriptPath = Join-Path $stagingRoot "installer.iss"
$installerBaseName = "Gomoku-Avalonia-$Runtime-$assetVersion-setup"
$installerPath = Join-Path $artifactRoot "$installerBaseName.exe"
$iconPath = Join-Path $repoRoot "Gomoku_Avalonia\Assets\avalonia-logo.ico"
$appId = if ($Runtime -eq "win-x64") {
    "{{E8DF90EA-0E42-4DEB-84D9-238B9B89A301}"
} else {
    "{{26D874B7-6835-4508-9210-C84B4C66E207}"
}
$architectureSetupLines = if ($Runtime -eq "win-x64") {
    @(
        "ArchitecturesAllowed=x64compatible",
        "ArchitecturesInstallIn64BitMode=x64compatible"
    )
} else {
    @()
}

function Assert-UnderRoot {
    param(
        [string]$Path,
        [string]$Root
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($Root)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside repository root: $resolvedPath"
    }
}

function Get-InnoSetupCompiler {
    $fromPath = Get-Command ISCC -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Inno Setup compiler was not found. Install it with: choco install innosetup -y"
}

Assert-UnderRoot -Path $artifactRoot -Root $repoRoot
Assert-UnderRoot -Path $stagingRoot -Root $repoRoot
Assert-UnderRoot -Path $installerPath -Root $repoRoot

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

Get-ChildItem -LiteralPath $artifactRoot -File -Filter "Gomoku-Avalonia-$Runtime-*" |
    Remove-Item -Force

$publishArgs = @(
    "publish",
    $projectPath,
    "-c",
    $Configuration,
    "-r",
    $Runtime,
    "--self-contained",
    "true",
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:DebugType=none",
    "/p:DebugSymbols=false",
    "/p:Version=$packageVersion",
    "/p:FileVersion=$fileVersion",
    "/p:AssemblyVersion=$fileVersion"
)

if ($NoRestore) {
    $publishArgs += "--no-restore"
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $publishDir)) {
    throw "Publish directory was not created: $publishDir"
}

New-Item -ItemType Directory -Force -Path $appStageDir | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $appStageDir -Recurse -Force
Get-ChildItem -LiteralPath $appStageDir -Recurse -Filter "*.pdb" | Remove-Item -Force

$stagedExePath = Join-Path $appStageDir "GomokuAvalonia.exe"
if (-not (Test-Path -LiteralPath $stagedExePath)) {
    throw "Desktop executable was not created: $stagedExePath"
}

$readmePath = Join-Path $appStageDir "README.txt"
@"
Gomoku Avalonia Desktop

Run Gomoku Avalonia from the Start menu after installation.

This is a self-contained Windows desktop build. It does not require a separate
.NET runtime installation.

Default AI endpoint:
https://mitsutake-model-space.hf.space
"@ | Set-Content -Path $readmePath -Encoding UTF8

$architectureSetupText = ($architectureSetupLines -join [Environment]::NewLine)
$installerScript = @"
#define MyAppName "Gomoku Avalonia"
#define MyAppVersion "$packageVersion"
#define MyAppPublisher "Vucius"
#define MyAppExeName "GomokuAvalonia.exe"

[Setup]
AppId=$appId
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Gomoku Avalonia
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=$artifactRoot
OutputBaseFilename=$installerBaseName
SetupIconFile=$iconPath
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
$architectureSetupText

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "$appStageDir\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
"@

New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
$installerScript | Set-Content -Path $installerScriptPath -Encoding UTF8

$isccPath = Get-InnoSetupCompiler
& $isccPath $installerScriptPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer was not created: $installerPath"
}

Write-Host "Desktop installer: $installerPath"
