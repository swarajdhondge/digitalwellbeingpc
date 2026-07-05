# Pulse — Microsoft Store (MSIX) packaging

This project (`digital-wellbeing-app.Package.wapproj`) is a **Windows Application
Packaging Project** that wraps the existing WPF + WinForms .NET 9 app
(`..\digital-wellbeing-app\digital-wellbeing-app.csproj`, `AssemblyName=DigitalWellbeing`)
into an **MSIX** for Microsoft Store submission.

## Why a separate .wapproj (and not single-project MSIX)

Single-project MSIX packs the manifest/assets directly into the app project with
no separate packaging project. **It is a WinUI 3 / Windows App SDK feature and is
not safe for a WPF + WinForms host** — the `WindowsPackageType`/`EnableMsixTooling`
single-project path assumes a WinUI 3 SDK project. Pulse is classic WPF (+WinForms),
so we use the classic packaging-project (`.wapproj`) approach: the app csproj is
untouched and referenced by the `.wapproj` as the package entry point.

## Store identity (final)

| Field | Value |
| --- | --- |
| Identity/Name | `SwarajDhondge.DigitalWellbeingPC` |
| Identity/Publisher | `CN=516C9AB9-2BCA-4787-9354-4A2FFCDBED01` |
| PublisherDisplayName | `Swaraj Dhondge` |
| DisplayName | `Pulse - Digital Wellbeing PC` |
| Package Family Name | `SwarajDhondge.DigitalWellbeingPC_h295br46hdej4` |
| Store product id | `9ND5GNMBQVQ7` |

## Prerequisites

- Visual Studio 2022 with the **.NET desktop** and **Universal Windows Platform /
  MSIX packaging** components (the `Microsoft.DesktopBridge.*` MSBuild targets).
- Windows 10 SDK 10.0.19041.0+ (matches `TargetPlatformVersion`).
- The tile assets generated (see below) — **the build/package will fail without them.**

## 1. Generate tile assets (required first)

The largest existing icon is 256px, too small for the 310px tile. Create a
**≥512px (ideally 1024px) square, transparent** master at
`..\digital-wellbeing-app\Resources\Icons\digital-balance-icon-512.png`, then:

```powershell
pwsh .\scripts\generate-store-assets.ps1
```

This writes `Images\*.png` (uses ImageMagick if on PATH, else GDI+ fallback).
See `Images\README.txt` for the full asset list.

## 2. Build the MSIX

From Visual Studio: set the `.Package` project as **Startup Project**, pick
platform **x64** (or **arm64**), then **Build**. To produce a Store upload bundle:
right-click the `.Package` project → **Publish → Create App Packages…** →
**Microsoft Store** → sign in / pick the app → build the **x64 + arm64** bundle,
producing a `.msixupload`.

Command line equivalent:

```powershell
msbuild digital-wellbeing-app.Package.wapproj `
  /p:Configuration=Release `
  /p:AppxBundlePlatforms="x64|arm64" `
  /p:UapAppxPackageBuildMode=StoreUpload `
  /p:AppxBundle=Always
```

The `.msixupload` under `AppPackages\` is what you upload in Partner Center.

## 3. WACK (Windows App Certification Kit)

Before submitting, run WACK to catch certification failures early. From the
**Create App Packages** wizard choose **"Yes, run Windows App Certification Kit"**
after building a **sideload/test** package, or run it standalone:

```powershell
& "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" `
  test -appxpackagepath .\AppPackages\<pkg>.msix -reportoutputpath .\wack-report.xml
```

Fix any failures (common: missing/soft tile assets, missing capabilities) and rebuild.

## 4. Data path / VFS note (IMPORTANT for the Store build)

The unpackaged app stores its DB, logs and settings under
`%LocalAppData%\Pulse\` (via `Environment.SpecialFolder.LocalApplicationData`).

Under MSIX, `LocalApplicationData` is **redirected** by the runtime into the
package container:

```
%LocalAppData%\Packages\SwarajDhondge.DigitalWellbeingPC_h295br46hdej4\LocalCache\Local\Pulse\
```

Implications:
- The Store build gets its **own private database** at that VFS-redirected path —
  it does **not** share data with an existing unpackaged/Velopack install.
- First run on the Store build starts fresh; if data migration from the classic
  install is desired, it must be handled explicitly by reading the real
  `%LocalAppData%\Pulse\` (not the redirected path).
- Registry writes (e.g. `HKCU\...\Run` used by `StartupService`) are likewise
  virtualized — use the manifest `windows.startupTask` (declared here as
  `PulseStartupId`) instead of the Run key in packaged mode.

## What is declared in `Package.appxmanifest`

- Real Store identity (table above).
- `TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0"`.
- Capability **`runFullTrust` only** (no `broadFileSystemAccess`).
- `Application Executable="DigitalWellbeing.exe" EntryPoint="Windows.FullTrustApplication"`.
- `windows.startupTask` extension: `TaskId="PulseStartupId"`, `Enabled="false"`,
  `Executable="DigitalWellbeing.exe"`, `EntryPoint="Windows.FullTrustApplication"`.
- VisualElements `DisplayName="Pulse - Digital Wellbeing PC"`.

> Tile asset paths in the manifest are **placeholders** until step 1 is run.
