# Privacy Policy

_Last updated: 2026-07-05_

**Pulse — DigitalWellbeingPC** ("Pulse", "the app") is a privacy-first Windows
application. This policy explains exactly what data the app handles. The short
version: **everything stays on your computer.**

## What data Pulse collects

Pulse tracks, entirely on your local machine:

- **Screen time** — active vs. idle time derived from mouse/keyboard activity.
- **App usage** — which application window is in the foreground and for how long
  (process name, executable path, and a friendly display name).
- **Audio exposure** — the system's audio **output** peak level, used only to
  warn you about prolonged loud listening. Pulse does **not** record, capture, or
  listen to any audio; it never accesses your microphone.
- **Focus sessions, goals, break history, and settings** you configure.

## Where your data is stored

All of the above is written to a **local SQLite database** on your own device,
under your user profile, at `%LocalAppData%\Pulse\` — the same location for both
the GitHub and Microsoft Store builds.

There are **no accounts, no cloud sync, no analytics, and no telemetry.** Your
data is never transmitted to us or to any third party. We (the developer) never
receive, see, or have access to any of it.

## Network usage

Pulse makes **no network requests to collect or transmit your data.** The only
network activity is:

- **GitHub build only:** a periodic check to the public GitHub Releases API to
  see whether a newer version is available. This sends only a standard HTTP
  request for release metadata and contains none of your usage data.
- **Microsoft Store build:** performs **no** update check of its own — updates
  are delivered by the Microsoft Store.

## Data sharing

None. Because no data ever leaves your device, there is nothing to share, sell,
or disclose.

## Your control over your data

- **Export:** Settings → export screen time, app usage, and sound data to CSV.
- **Delete:** Settings lets you delete all stored data (and, in v2.2+, delete a
  specific date range). Uninstalling the app also removes its local data store.

## Children's privacy

Pulse is a general-purpose utility and does not knowingly collect data from
anyone; all data is local and under the device owner's control.

## Changes to this policy

Any changes will be published in this file in the project repository and, for
Microsoft Store users, at the listing's privacy policy link.

## Contact

Questions about privacy: open an issue at
<https://github.com/swarajdhondge/digitalwellbeingpc/issues> or email
swarajdhondge009@gmail.com.
