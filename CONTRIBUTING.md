# Contributing to Pulse

Thanks for helping improve Pulse. Bug reports, fixes, docs and design ideas are all welcome.

By contributing you agree your work is licensed under [GPL-3.0](LICENSE) and you follow the
[Code of Conduct](CODE_OF_CONDUCT.md).

## Setup

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) and Windows 10 (17763+) or 11.
Node.js 20+ is only needed for the website in `pulse/`.

```powershell
dotnet run --project digital-wellbeing-app               # run the app
dotnet test digital-wellbeing-app/Tests/Tests.csproj     # unit tests (must pass)
```

`UITests/` holds FlaUI tests that drive the real app and need an interactive desktop.

If your antivirus quarantines the freshly built, unsigned exe, run it from your IDE or exclude
the build folder.

## Layout

| Path | What it is |
|---|---|
| `digital-wellbeing-app/` | WPF app (MVVM: `Views/`, `ViewModels/`, `Models/`; tracking in `CoreLogic/`; data and logic in `Services/`; Win32 interop in `Platform/Windows/`) |
| `digital-wellbeing-app/Tests/` | xUnit tests, one file per class under test |
| `UITests/` | FlaUI UI tests and screenshot capture |
| `pulse/` | Website (Next.js, deployed to Vercel) |

## Pull requests

1. Open or find an issue first for anything bigger than a small fix.
2. Keep each PR to **one** change. Unrelated refactors, renames, formatting sweeps and features go in separate PRs.
3. Add or update a test that fails without your change.
4. **Test it yourself and show it.** Run the unit tests and use the change in the running app. In the PR,
   list what you ran and what you saw, with a screenshot for UI changes. PRs without this are closed.
5. CI must be green.
6. Use a [Conventional Commits](https://www.conventionalcommits.org/) title, e.g. `fix: stop counting the desktop as File Explorer`.
7. Don't commit build output, installers, archives or personal data.

## AI-assisted contributions

Using AI tools is fine. You are still the author, so:

- **Understand every line** you submit and be ready to explain it in review.
- **Run it.** Test the change in the app yourself. Tests written by the same tool that wrote the code are
  not enough on their own.
- **Keep it minimal.** No generated boilerplate, speculative abstractions, unused code or restated comments.
- **Say so** in the PR description when a tool wrote a significant part of the change.

PRs that are large, unfocused or clearly unreviewed may be closed with a request to split or trim them.

## Translations

All UI text lives in `digital-wellbeing-app/Properties/Strings.resx` (English). To add a language:

1. Copy it to `Strings.<culture>.resx` in the same folder, using a .NET culture name such as `zh-Hans`, `es`, `pt-BR` or `ja`.
2. Translate only the `<value>` text. Keep the keys and any `{0}` placeholders. Remove keys you haven't translated; they fall back to English.
3. Run the app, pick the language in **Settings → Appearance → Language**, restart Pulse, and check every page for cut-off or overlapping text. Add screenshots to the PR.

`dotnet test` checks that translations use known keys and keep every placeholder. New text in code goes into
`Strings.resx` and is read with `{l:Loc Key}` in XAML or `Loc.Get("Key")` / `Loc.Format("Key", ...)` in C#.

## Bugs and security

Use the issue templates for bugs and feature requests. Report security issues privately as described in
[SECURITY.md](SECURITY.md).
