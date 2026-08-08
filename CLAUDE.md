# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Windows system tray application (.NET 10 / WPF, [H.NotifyIcon.Wpf](https://github.com/HardcodetNet/H.NotifyIcon))
that imports photos from a Canon camera card and from Dropbox Camera Uploads into a dated
photo library, converting CR3 raw files to DNG via Adobe DNG Converter and re-encoding MP4
videos with ffmpeg.

It is a port of the earlier console script at `~/Code/TestProjects/ProcessRawPhotos/`. The
output-folder convention is reproduced exactly; the script's hardcoded constants became the
default preferences.

**There is no .NET SDK on the Linux dev box.** CI on `windows-latest` is the only compiler.
Push to a branch and let `.github/workflows/build.yml` verify the build and tests.

## Build & Test Commands

```bash
# Build - never build the .sln, the .NET SDK cannot load the WiX .wixproj
dotnet build ImportCanonPhotos.csproj -c Release

# Run tests (xUnit)
dotnet test ImportCanonPhotos.Tests/ImportCanonPhotos.Tests.csproj

# Build the MSI (Windows, needs WiX v3.11)
msbuild ImportCanonPhotos.Installer/ImportCanonPhotos.Installer.wixproj /p:Configuration=Release /p:Platform=x64

# Create a release (increments patch version, tags, and pushes)
./scripts/make-release
```

## Architecture

- **ImportCanonPhotos** (`ImportCanonPhotos.csproj`) - .NET 10 WPF WinExe.
  `App.xaml` sets `ShutdownMode="OnExplicitShutdown"`; `MainWindow.xaml` is a zero-size
  invisible window hosting the `TaskbarIcon`. Tray menu: Import from Camera / Import from
  Dropbox / Preferences / Show Logs / About / Exit.
- **ImportCanonPhotos.Tests** - xUnit, targets `net10.0-windows` and project-references the
  app. Covers the pure logic: path building, EXIF date parsing, DNG argument strings, settings.
- **ImportCanonPhotos.Installer** - WiX v3 MSI. Installs to Program Files, creates
  Start Menu/Desktop shortcuts, and registers auto-start via `HKCU\...\Run`.

### Code layout

| Path | Responsibility |
|---|---|
| `Core/AppPaths.cs` | Everything the app writes lives under `%LOCALAPPDATA%\ImportCanonPhotos` |
| `Core/AppSettings.cs` | Settings model + JSON persistence (source-generated serializer) |
| `Core/AppLog.cs` | Thread-safe file log with size rollover |
| `Core/OutputPathBuilder.cs` | The dated output-folder convention |
| `Core/PhotoDate.cs` | EXIF date reading via MetadataExtractor |
| `Core/DngConverter.cs` | Adobe DNG Converter process wrapper |
| `Core/VideoEncoder.cs` | NReco/ffmpeg MP4 re-encode |
| `Core/ImportEngine.cs` | Orchestrates both import legs, reports `IProgress<ImportProgress>` |
| `Ui/ProgressWindow.xaml` | Phase + overall progress bars, Cancel |
| `Ui/PreferencesWindow.xaml` | Paths and behaviour toggles |

### Output convention (load-bearing)

```
<OutputFolder>\<yyyy>\<MMMM>\<yyyy-MM-dd>\<jpg|dng|dng-lossless|mp4|re-encoded-mp4>
```

e.g. `...\Photos\2026\August\2026-08-08\dng`. The month folder is the **current-culture**
full month name. Filenames are preserved verbatim; files are copied, never moved.
`SearchOption.TopDirectoryOnly` throughout — but unlike the original, every subfolder of the
configured DCIM root is scanned, so `101CANON` onwards are not missed.

### Runtime locations

| Item | Path |
|---|---|
| Settings | `%LOCALAPPDATA%\ImportCanonPhotos\settings.json` |
| Log | `%LOCALAPPDATA%\ImportCanonPhotos\logs\import.log` (rolls to `import.1.log`) |
| ffmpeg extraction | `%LOCALAPPDATA%\ImportCanonPhotos\ffmpeg` |
| Card staging | `%TEMP%\ImportCanonPhotos\<cardFolder>` |

Nothing user-writable goes in Program Files: the MSI installs `perMachine` but
`app.manifest` requests `asInvoker`, so the install directory is read-only at runtime.

## Gotchas

- **`Directory.Build.props` is scoped to `.csproj`.** It sets `TreatWarningsAsErrors` with
  `AnalysisLevel=latest-All`; unscoped, the WiX project inherits it and candle/light ICE
  *warnings* become release-pipeline *errors*. Analyser exclusions live in the root
  `.editorconfig`.
- **The WiX file list is hand-maintained** (`DoNotHarvest=True`). Every new NuGet DLL needs a
  `<Component>` with a fresh GUID plus a `<ComponentRef>` in `ProductFeature`. A forgotten
  entry builds green and fails at runtime — the "Verify installer payload" step in
  `build.yml` catches it.
- **Do not reformat `Product.wxs` line 3.** `release.yml` stamps the version with
  `(?<=<\?define ProductVersion=")[\d\.]*(?=" \?>)`; the single space before `?>` is required.
- **`MetadataExtractor.Directory` collides with `System.IO.Directory`** under
  `ImplicitUsings`. Never `using MetadataExtractor;` — fully qualify instead.
- **Adobe DNG Converter needs `-c`** or it shows its GUI on Windows and never exits. It can
  also return exit code 0 on failure, so the output-file existence check is kept as well.
- **The MSI ships from `bin\Release\net10.0-windows\`** (the build directory), not `publish\`,
  despite the release workflow's `dotnet publish` step.
- Auto-start uses `HKCU\...\Run` from a `perMachine` MSI, so it registers only for the
  account that ran the installer.

## Release Process

Releases are triggered by pushing a `v*.*.*` tag (or manual workflow_dispatch).
`.github/workflows/release.yml` builds the app, runs tests, builds the WiX MSI, and creates a
GitHub Release with the MSI attached. Ordinary pushes and PRs run `build.yml` instead, which
also uploads the MSI as an artifact for smoke-testing.
