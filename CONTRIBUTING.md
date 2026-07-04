# Contributing to Pulse

Thanks for your interest in improving **Pulse — DigitalWellbeingPC**!
Contributions of all kinds are welcome: bug reports, feature ideas, code, docs,
and design.

By contributing you agree that your contributions will be licensed under the
project's [GNU General Public License v3.0](LICENSE).

Please also read our [Code of Conduct](CODE_OF_CONDUCT.md).

## Getting started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10 (build 17763+) or Windows 11
- (Optional, for the marketing site) Node.js 20+ for `pulse/`

### Build & run

```powershell
# from the repo root
dotnet build
dotnet run --project digital-wellbeing-app
```

> **Antivirus note:** Some AV products (e.g. Quick Heal) quarantine freshly
> built, unsigned executables when they are launched from a shell. If this
> happens, run the app from Visual Studio / VS Code, or add the build output
> directory to your AV exclusions while developing.

### Run the tests

```powershell
dotnet test digital-wellbeing-app/Tests/Tests.csproj
```

UI tests live in `UITests/` (FlaUI) and require an interactive desktop session.

## Project layout

| Path | What it is |
|---|---|
| `digital-wellbeing-app/` | The WPF desktop app (.NET 9) |
| `digital-wellbeing-app/Tests/` | xUnit unit tests |
| `UITests/` | FlaUI UI automation & screenshot capture |
| `pulse/` | Next.js marketing site (deployed to Vercel) |
| `scripts/` | Build / release / screenshot helper scripts |
| `.github/` | CI workflows, issue/PR templates, README screenshots |

Architecture: MVVM (`Views/`, `ViewModels/`, `Models/`), a `Services/` layer for
business logic and data access, core tracking in `CoreLogic/`, and
platform-specific interop under `Platform/Windows/`.

## Coding conventions

- PascalCase for public members, `_camelCase` for private fields.
- Interfaces prefixed with `I`.
- `async`/`await` for all I/O — **never block the UI thread**; background work via
  `Task.Run`, UI-thread delays via `Task.Delay` (never `Thread.Sleep`).
- MVVM: bind UI state via `INotifyPropertyChanged`; avoid direct UI manipulation
  from code-behind.
- Nullable reference types are enabled — keep the build warning-clean.
- Run `dotnet format` before committing.

## Pull request process

1. Fork the repo and create a topic branch (`feat/…`, `fix/…`, `docs/…`).
2. Make your change, add or update tests, and keep the build warning-clean.
3. Run `dotnet build` and `dotnet test digital-wellbeing-app/Tests/Tests.csproj` —
   both must pass.
4. Use [Conventional Commits](https://www.conventionalcommits.org/) for commit
   messages (e.g. `fix: correct category attribution in reports`).
5. Open a PR against `main`, fill in the PR template, and link any related issue.

CI will build the app, run the unit tests, and build the `pulse/` site on every
PR. All checks must be green before merge.

## Regenerating screenshots

README, website, and Microsoft Store screenshots are **generated**, never hand
cropped. See [`scripts/capture-screenshots.ps1`](scripts/capture-screenshots.ps1)
and the [Screenshot pipeline](#screenshot-pipeline) section below.

### Screenshot pipeline

> Documented in full in Phase 3 of the v2.2 release. In short: the FlaUI capture
> suite launches the built app at a fixed window size, seeds a fixture database
> so views show believable data, navigates each section via the nav-rail
> automation IDs, captures every section in both Light and Dark themes, and
> writes canonical PNGs to `.github/screenshots/` (with the site subset copied to
> `pulse/public/screenshots/`). Regenerate instead of editing images by hand.

## Reporting bugs & requesting features

Use the issue templates under **Issues → New issue**. For security issues, follow
[SECURITY.md](SECURITY.md) and report privately.
