param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$packageVersion = $Version -replace '^[vV]', ''
$fileVersion = if ($packageVersion -match '^\d+\.\d+\.\d+$') { "$packageVersion.0" } else { "1.0.0.0" }
$projectPath = Join-Path $repoRoot "Gomoku_Avalonia.Desktop\Gomoku_Avalonia.Desktop.csproj"
$publishDir = Join-Path $repoRoot "Gomoku_Avalonia.Desktop\bin\$Configuration\net10.0\$Runtime\publish"
$artifactRoot = Join-Path $repoRoot "artifacts\desktop"
$stagingRoot = Join-Path $artifactRoot "Gomoku-Avalonia-$Runtime"
$appStageDir = Join-Path $stagingRoot "Gomoku-Avalonia"
$zipPath = Join-Path $artifactRoot "Gomoku-Avalonia-$Runtime-$packageVersion-portable.zip"
$exeAssetPath = Join-Path $artifactRoot "Gomoku-Avalonia-$Runtime-$packageVersion.exe"
$checksumPath = Join-Path $artifactRoot "Gomoku-Avalonia-$Runtime-$packageVersion-portable.sha256.txt"

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

Assert-UnderRoot -Path $artifactRoot -Root $repoRoot
Assert-UnderRoot -Path $stagingRoot -Root $repoRoot
Assert-UnderRoot -Path $zipPath -Root $repoRoot
Assert-UnderRoot -Path $exeAssetPath -Root $repoRoot

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path -LiteralPath $exeAssetPath) {
    Remove-Item -LiteralPath $exeAssetPath -Force
}

if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

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

Copy-Item -LiteralPath $stagedExePath -Destination $exeAssetPath -Force

$readmePath = Join-Path $appStageDir "README.txt"
@"
Gomoku Avalonia Desktop

Run GomokuAvalonia.exe to start the game.

This is a self-contained Windows desktop build. It does not require a separate
.NET runtime installation.

Default AI endpoint:
https://mitsutake-model-space.hf.space
"@ | Set-Content -Path $readmePath -Encoding UTF8

Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $zipPath -Force

$hash = Get-FileHash -Path $zipPath -Algorithm SHA256
$exeHash = Get-FileHash -Path $exeAssetPath -Algorithm SHA256
@(
    "$($hash.Hash)  $(Split-Path -Leaf $zipPath)",
    "$($exeHash.Hash)  $(Split-Path -Leaf $exeAssetPath)"
) | Set-Content -Path $checksumPath -Encoding ASCII

Write-Host "Desktop package: $zipPath"
Write-Host "Desktop executable: $exeAssetPath"
Write-Host "SHA256: $($hash.Hash)"
