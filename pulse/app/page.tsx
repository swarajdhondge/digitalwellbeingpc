import DownloadButtons from "./components/DownloadButtons";

const REPO_URL = "https://github.com/swarajdhondge/digitalwellbeingpc";

const features = [
  {
    title: "Screen time",
    accent: "#6c6fe8",
    desc: "Active vs. idle time at a glance, with today and weekly views and your longest stretches.",
    icon: <path d="M3 4h18v12H3zM8 20h8M12 16v4" />,
  },
  {
    title: "App usage",
    accent: "#a867d0",
    desc: "See which apps take your time, ranked with friendly names, switches, and average focus.",
    icon: <path d="M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z" />,
  },
  {
    title: "Focus sessions",
    accent: "#e0892c",
    desc: "Pick a duration, tag distracting apps, and let Pulse warn or block them while you work.",
    icon: (
      <>
        <circle cx="12" cy="12" r="8" />
        <circle cx="12" cy="12" r="3" />
      </>
    ),
  },
  {
    title: "Hearing",
    accent: "#23b39a",
    desc: "Real-time audio monitoring flags loud listening above 75 dB to protect your ears.",
    icon: <path d="M11 5 6 9H2v6h4l5 4zM15.5 8.5a5 5 0 0 1 0 7M19 5a9 9 0 0 1 0 14" />,
  },
  {
    title: "Insights",
    accent: "#ce6a8e",
    desc: "A weekly report with daily averages, peak times, top apps, and week-over-week trends.",
    icon: <path d="M3 3v18h18M7 14l3-3 3 3 4-5" />,
  },
  {
    title: "Private by design",
    accent: "#2ba3c7",
    desc: "No account, no cloud, no telemetry. Everything stays in a local database on your PC.",
    icon: <path d="M12 3 4 6v6c0 5 3.5 8 8 9 4.5-1 8-4 8-9V6z" />,
  },
];

const shots = [
  { src: "/screenshots/dashboard.png", label: "Today" },
  { src: "/screenshots/screentime.png", label: "Screen time" },
  { src: "/screenshots/appusage.png", label: "App usage" },
  { src: "/screenshots/focusmode.png", label: "Focus" },
  { src: "/screenshots/sound.png", label: "Hearing" },
  { src: "/screenshots/weeklyreport.png", label: "Insights" },
];

const SITE_URL = "https://digitalwellbeingpc.vercel.app";

// Structured data for rich results — a free Windows SoftwareApplication.
const jsonLd = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "Pulse - Digital Wellbeing PC",
  alternateName: "Pulse",
  applicationCategory: "UtilitiesApplication",
  operatingSystem: "Windows 10, Windows 11",
  description:
    "A free, private, open-source Digital Wellbeing app for Windows. Track screen time, app usage, and focus sessions, and protect your hearing — all on-device.",
  url: SITE_URL,
  downloadUrl: `${REPO_URL}/releases`,
  softwareHelp: REPO_URL,
  license: `${REPO_URL}/blob/main/LICENSE`,
  isAccessibleForFree: true,
  author: {
    "@type": "Person",
    name: "Swaraj Dhondge",
    url: "https://github.com/swarajdhondge",
  },
  offers: { "@type": "Offer", price: "0", priceCurrency: "USD" },
  featureList: [
    "Screen time tracking",
    "App usage monitoring",
    "Focus sessions with app blocking",
    "Hearing protection",
    "Break reminders",
    "Weekly reports",
  ],
};

export default function Home() {
  return (
    <div className="min-h-screen">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />
      <header className="sticky top-0 z-50 border-b border-line/60 bg-base/70 backdrop-blur-xl">
        <div className="mx-auto grid max-w-6xl grid-cols-[1fr_auto_1fr] items-center gap-4 px-6 py-4">
          <a href="#top" className="flex items-center gap-2.5 justify-self-start">
            <img src="/pulse-logo.png" alt="Pulse logo" width={32} height={32} className="h-8 w-8 rounded-xl" />
            <span className="whitespace-nowrap text-lg font-bold tracking-tight">Pulse<span className="hidden font-medium text-muted sm:inline"> - Digital Wellbeing PC</span></span>
            <span className="hidden items-center gap-1 whitespace-nowrap rounded-full border border-line bg-surface2 px-2 py-0.5 text-xs font-medium text-muted lg:inline-flex">
              <svg width="11" height="11" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M3 5.5 10.5 4.4v7.1H3zM10.5 12.5v7.1L3 18.5v-6zM11.6 4.2 21 3v8.5h-9.4zM21 12.5V21l-9.4-1.3v-7.2z"/></svg>
              for Windows
            </span>
          </a>
          <nav className="hidden items-center gap-8 text-sm text-muted md:flex">
            <a href="#features" className="transition hover:text-ink">Features</a>
            <a href="#screens" className="transition hover:text-ink">Screenshots</a>
            <a href="#download" className="transition hover:text-ink">Download</a>
          </nav>
          <a
            href={REPO_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="justify-self-end rounded-xl border border-line bg-surface px-4 py-2 text-sm font-medium transition hover:border-accent/60 hover:text-accent"
          >
            GitHub
          </a>
        </div>
      </header>

      <main id="top">
        <section className="pulse-glow relative overflow-hidden px-6 pt-20 pb-16 sm:pt-28">
          <div className="mx-auto max-w-3xl text-center rise">
            <img
              src="/pulse-logo.png"
              alt="Pulse"
              width={88}
              height={88}
              className="mx-auto mb-7 h-[88px] w-[88px] rounded-[24px] shadow-xl shadow-black/40 ring-1 ring-line/60"
            />
            <span className="inline-flex items-center gap-2 rounded-full border border-line bg-surface/60 px-4 py-1.5 text-xs font-semibold text-muted">
              <span className="h-2 w-2 rounded-full bg-[#30a46c]" /> Private · on-device · free
            </span>
            <h1 className="mt-6 text-balance text-5xl font-extrabold leading-[1.05] tracking-tight sm:text-6xl">
              Your day on the PC,
              <br />
              <span className="text-accent">calmly in view.</span>
            </h1>
            <p className="mx-auto mt-6 max-w-xl text-balance text-lg text-muted">
              Pulse brings Android-style Digital Wellbeing to Windows — screen time, app
              usage, focus sessions, and hearing protection, all on your machine.
            </p>
            <div className="mt-10">
              <DownloadButtons />
            </div>
          </div>

          <div className="mx-auto mt-16 max-w-5xl rise">
            <div className="overflow-hidden rounded-3xl border border-line bg-surface shadow-2xl shadow-black/40">
              {/* Demo video served same-origin from /public/demo (regenerated by
                  scripts/record-demo.ps1). NOTE: do NOT point this at a GitHub release asset —
                  those are served as application/octet-stream with attachment disposition, so
                  the browser won't play them inline (you'd only ever see the poster). */}
              <video
                src="/demo/demo.mp4"
                poster="/screenshots/dashboard.png"
                autoPlay
                muted
                loop
                playsInline
                width={1600}
                height={1000}
                className="w-full"
                aria-label="Pulse demo — a quick tour of the dashboard, screen time, app usage, hearing, focus, and insights"
              />
            </div>
          </div>
        </section>

        <section id="features" className="mx-auto max-w-6xl scroll-mt-20 px-6 py-20">
          <div className="mb-12 text-center">
            <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">Everything you track, calmly</h2>
            <p className="mx-auto mt-3 max-w-lg text-muted">
              Six tools that help you be mindful of your computer time — without nagging.
            </p>
          </div>
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {features.map((f) => (
              <div key={f.title} className="rounded-3xl border border-line bg-surface p-6 transition hover:bg-surface2">
                <span
                  className="grid h-11 w-11 place-items-center rounded-2xl"
                  style={{ backgroundColor: `${f.accent}22`, color: f.accent }}
                >
                  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round">
                    {f.icon}
                  </svg>
                </span>
                <h3 className="mt-5 text-lg font-semibold">{f.title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-muted">{f.desc}</p>
              </div>
            ))}
          </div>
        </section>

        <section id="screens" className="mx-auto max-w-6xl scroll-mt-20 px-6 py-20">
          <div className="mb-12 text-center">
            <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">See it in action</h2>
            <p className="mx-auto mt-3 max-w-lg text-muted">
              A calm graphite interface with a signature color for every section.
            </p>
          </div>
          <div className="grid gap-5 sm:grid-cols-2">
            {shots.map((s) => (
              <figure key={s.src} className="overflow-hidden rounded-3xl border border-line bg-surface">
                <img src={s.src} alt={`Pulse — ${s.label}`} loading="lazy" className="w-full" />
                <figcaption className="border-t border-line px-5 py-3 text-sm font-medium text-muted">
                  {s.label}
                </figcaption>
              </figure>
            ))}
          </div>
        </section>

        <section id="download" className="scroll-mt-20 px-6 py-20">
          <div className="pulse-glow mx-auto max-w-3xl rounded-[2rem] border border-line bg-surface px-8 py-16 text-center">
            <h2 className="text-3xl font-bold tracking-tight sm:text-4xl">Get Pulse for Windows</h2>
            <p className="mx-auto mt-3 max-w-md text-muted">
              Free and open source. Installs without admin rights and updates itself. Windows 10 / 11.
            </p>
            <div className="mt-9">
              <DownloadButtons />
            </div>
          </div>
        </section>
      </main>

      <footer className="border-t border-line/60">
        <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-4 px-6 py-10 text-sm text-muted sm:flex-row">
          <div className="flex items-center gap-2.5">
            <img src="/pulse-logo.png" alt="Pulse logo" width={28} height={28} className="h-7 w-7 rounded-lg" />
            <span className="font-semibold text-ink">Pulse - Digital Wellbeing PC</span>
          </div>
          <div className="flex items-center gap-6">
            <a href={REPO_URL} target="_blank" rel="noopener noreferrer" className="transition hover:text-ink">GitHub</a>
            <a href={`${REPO_URL}/releases`} target="_blank" rel="noopener noreferrer" className="transition hover:text-ink">Releases</a>
            <a href={`${REPO_URL}/blob/main/PRIVACY.md`} target="_blank" rel="noopener noreferrer" className="transition hover:text-ink">Privacy</a>
            <a href={`${REPO_URL}/blob/main/LICENSE`} target="_blank" rel="noopener noreferrer" className="text-faint transition hover:text-ink">GPL-3.0 licensed</a>
          </div>
        </div>
        <p className="px-6 pb-8 text-center text-xs text-faint">
          Pulse is an independent, open-source project — not affiliated with Google or Samsung.
        </p>
      </footer>
    </div>
  );
}
