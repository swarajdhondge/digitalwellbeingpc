# Third-Party Notices

Pulse — DigitalWellbeingPC is licensed under the GNU General Public License v3.0
(see [LICENSE](LICENSE)). It bundles or depends on the third-party components
listed below. Each is used under its own license, all of which are compatible
with GPL-3.0. The full license text for each component ships with the
corresponding NuGet/npm package and is available at the linked source.

## Desktop application (.NET / WPF)

| Component | Version | License | Project |
|---|---|---|---|
| MaterialDesignThemes | 5.2.1 | MIT | https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit |
| MaterialDesignColors | 5.2.1 | MIT | https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit |
| LiveChartsCore.SkiaSharpView.WPF | 2.0.0-rc3.3 | MIT | https://github.com/beto-rodriguez/LiveCharts2 |
| sqlite-net-pcl | 1.9.172 | MIT | https://github.com/praeclarum/sqlite-net |
| NAudio | 2.1.0 | MIT | https://github.com/naudio/NAudio |
| Velopack | 0.0.* | MIT | https://github.com/velopack/velopack |
| System.Drawing.Common | 6.0.0 | MIT | https://github.com/dotnet/runtime |
| System.IO.FileSystem.AccessControl | 5.0.0 | MIT | https://github.com/dotnet/runtime |

## Tests

| Component | License | Project |
|---|---|---|
| xUnit | Apache-2.0 | https://github.com/xunit/xunit |
| xunit.runner.visualstudio | Apache-2.0 | https://github.com/xunit/visualstudio.xunit |
| Microsoft.NET.Test.Sdk | MIT | https://github.com/microsoft/vstest |
| FlaUI (UI tests) | MIT | https://github.com/FlaUI/FlaUI |

## Marketing site (`pulse/`)

The Next.js site depends on a number of MIT- and Apache-2.0-licensed npm
packages. The authoritative, per-package list (with resolved versions and SPDX
license identifiers) is `pulse/package-lock.json`. Notable direct dependencies:

| Component | License | Project |
|---|---|---|
| Next.js | MIT | https://github.com/vercel/next.js |
| React / React DOM | MIT | https://github.com/facebook/react |
| Tailwind CSS | MIT | https://github.com/tailwindlabs/tailwindcss |

MIT and Apache-2.0 are both permissive licenses compatible with GPL-3.0 as used
here (Pulse is the combined/derivative work distributed under GPL-3.0; the
components retain their own permissive licenses).
