# Pulse — DigitalWellbeingPC · Website

The marketing / download site for **Pulse — DigitalWellbeingPC**, built with Next.js + Tailwind. It's the GitHub "front" for the app: a Pulse-themed landing page whose **Download** section reads the **latest GitHub release** live — showing the current version and a download button for each installer asset.

## Develop

```bash
npm install
npm run dev        # http://localhost:3000
npm run build      # production build
```

## Deploy to Vercel

This site lives in the **`pulse/`** subdirectory of the repo, so:

1. Import the repo `swarajdhondge/digitalwellbeingpc` on Vercel.
2. Set **Root Directory** = `pulse`.
3. Framework preset **Next.js** is auto-detected — the defaults build it as-is.

No environment variables needed: the download data is fetched from the public GitHub API in the browser.

## How the live download works

`app/components/DownloadButtons.tsx` fetches
`https://api.github.com/repos/swarajdhondge/digitalwellbeingpc/releases/latest`
client-side and renders a button per `.exe` installer asset (plus version + date).
If there's no release yet, or the API is unreachable, it links to the GitHub releases page.

## Screenshots

App screenshots live in `public/screenshots/`. Regenerate them from the desktop app when the UI changes.
