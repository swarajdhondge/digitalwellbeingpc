# Security Policy

## Data safety statement

Pulse — DigitalWellbeingPC is designed to be private by construction. **All data
stays local** on your device in a SQLite database under your user profile. There
are no accounts, no cloud sync, no telemetry, and no remote data collection. The
only outbound network call is an optional update check against the public GitHub
Releases API (GitHub build only; the Microsoft Store build updates via the
Store). See [PRIVACY.md](PRIVACY.md) for details.

## Supported versions

Security fixes are applied to the latest released version. Please update to the
newest release before reporting an issue.

| Version | Supported |
|---|---|
| 2.2.x (latest) | ✅ |
| < 2.2 | ❌ |

## Reporting a vulnerability

Please report security vulnerabilities **privately** — do not open a public
issue for anything exploitable.

- Preferred: use GitHub's **[Private vulnerability reporting](https://github.com/swarajdhondge/digitalwellbeingpc/security/advisories/new)**
  (Security → Report a vulnerability).
- Alternatively, email **tosdhondge@gmail.com** with the subject
  `SECURITY: Pulse`.

Please include:

- A description of the vulnerability and its impact.
- Steps to reproduce (proof-of-concept if possible).
- Affected version and your environment (Windows version, build channel).

### What to expect

- Acknowledgement within **7 days**.
- An assessment and, if confirmed, a fix timeline.
- Credit in the release notes once a fix ships, unless you prefer to remain
  anonymous.

Thank you for helping keep Pulse and its users safe.
