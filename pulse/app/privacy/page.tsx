import type { Metadata } from "next";
import Link from "next/link";

const REPO_URL = "https://github.com/swarajdhondge/digitalwellbeingpc";
const UPDATED = "July 5, 2026";

export const metadata: Metadata = {
  title: "Privacy Policy — Pulse",
  description:
    "Pulse is privacy-first: everything stays on your computer. No accounts, no cloud, no telemetry.",
};

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="mt-10">
      <h2 className="text-lg font-semibold text-ink">{title}</h2>
      <div className="mt-3 space-y-3 text-[15px] leading-relaxed text-muted">{children}</div>
    </section>
  );
}

export default function Privacy() {
  return (
    <div className="min-h-screen">
      <header className="sticky top-0 z-50 border-b border-line/60 bg-base/70 backdrop-blur-xl">
        <div className="mx-auto flex max-w-3xl items-center justify-between px-6 py-4">
          <Link href="/" className="flex items-center gap-2.5">
            <img src="/pulse-logo.png" alt="Pulse logo" width={32} height={32} className="h-8 w-8 rounded-xl" />
            <span className="font-semibold text-ink">Pulse</span>
          </Link>
          <Link href="/" className="text-sm text-muted transition-colors hover:text-ink">
            ← Back to home
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-6 py-16">
        <h1 className="text-3xl font-bold tracking-tight text-ink sm:text-4xl">Privacy Policy</h1>
        <p className="mt-2 text-sm text-faint">Last updated: {UPDATED}</p>

        <p className="mt-8 text-[15px] leading-relaxed text-muted">
          <span className="text-ink">Pulse — DigitalWellbeingPC</span> (&ldquo;Pulse&rdquo;, &ldquo;the app&rdquo;)
          is a privacy-first Windows application. This policy explains exactly what data the app
          handles. The short version:{" "}
          <span className="font-semibold text-ink">everything stays on your computer.</span>
        </p>

        <Section title="What data Pulse collects">
          <p>Pulse tracks, entirely on your local machine:</p>
          <ul className="ml-4 list-disc space-y-2 marker:text-accent">
            <li><span className="text-ink">Screen time</span> — active vs. idle time derived from mouse/keyboard activity.</li>
            <li><span className="text-ink">App usage</span> — which application window is in the foreground and for how long (process name, executable path, and a friendly display name).</li>
            <li><span className="text-ink">Audio exposure</span> — the system&rsquo;s audio <span className="text-ink">output</span> peak level, used only to warn you about prolonged loud listening. Pulse does <span className="text-ink">not</span> record, capture, or listen to any audio; it never accesses your microphone.</li>
            <li><span className="text-ink">Focus sessions, goals, break history, and settings</span> you configure.</li>
          </ul>
        </Section>

        <Section title="Where your data is stored">
          <p>
            All of the above is written to a <span className="text-ink">local SQLite database</span> on
            your own device, under your user profile at{" "}
            <code className="rounded bg-surface2 px-1.5 py-0.5 text-[13px] text-ink">%LocalAppData%\Pulse\</code>
            {" "}— the same location for both the GitHub and Microsoft Store builds.
          </p>
          <p>
            There are <span className="text-ink">no accounts, no cloud sync, no analytics, and no telemetry.</span>{" "}
            Your data is never transmitted to us or to any third party. We (the developer) never receive,
            see, or have access to any of it.
          </p>
        </Section>

        <Section title="Network usage">
          <p>Pulse makes <span className="text-ink">no network requests to collect or transmit your data.</span> The only network activity is:</p>
          <ul className="ml-4 list-disc space-y-2 marker:text-accent">
            <li><span className="text-ink">GitHub build only:</span> a periodic check to the public GitHub Releases API to see whether a newer version is available. This sends only a standard HTTP request for release metadata and contains none of your usage data.</li>
            <li><span className="text-ink">Microsoft Store build:</span> performs <span className="text-ink">no</span> update check of its own — updates are delivered by the Microsoft Store.</li>
          </ul>
        </Section>

        <Section title="Data sharing">
          <p>None. Because no data ever leaves your device, there is nothing to share, sell, or disclose.</p>
        </Section>

        <Section title="Your control over your data">
          <ul className="ml-4 list-disc space-y-2 marker:text-accent">
            <li><span className="text-ink">Export:</span> Settings → export screen time, app usage, and sound data to CSV.</li>
            <li><span className="text-ink">Delete:</span> Settings lets you delete all stored data (and, in v2.2+, delete a specific date range). Uninstalling the app also removes its local data store.</li>
          </ul>
        </Section>

        <Section title="Children's privacy">
          <p>Pulse is a general-purpose utility and does not knowingly collect data from anyone; all data is local and under the device owner&rsquo;s control.</p>
        </Section>

        <Section title="Changes to this policy">
          <p>Any changes will be published in this file in the project repository and, for Microsoft Store users, at the listing&rsquo;s privacy policy link.</p>
        </Section>

        <Section title="Contact">
          <p>
            Questions about privacy: open an issue at{" "}
            <a href={`${REPO_URL}/issues`} className="text-accent underline-offset-2 hover:underline">{REPO_URL.replace("https://", "")}/issues</a>{" "}
            or email{" "}
            <a href="mailto:swarajdhondge009@gmail.com" className="text-accent underline-offset-2 hover:underline">swarajdhondge009@gmail.com</a>.
          </p>
        </Section>

        <div className="mt-16 border-t border-line/60 pt-6">
          <Link href="/" className="text-sm text-muted transition-colors hover:text-ink">← Back to Pulse</Link>
        </div>
      </main>
    </div>
  );
}
