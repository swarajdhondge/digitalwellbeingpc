"use client";

import { useEffect, useState } from "react";

const REPO = "swarajdhondge/digitalwellbeingpc";
const RELEASES_URL = `https://github.com/${REPO}/releases`;

type Asset = { name: string; browser_download_url: string; size: number; download_count: number };
type Release = {
  tag_name: string;
  name: string | null;
  published_at: string;
  html_url: string;
  assets: Asset[];
};

type State =
  | { status: "loading" }
  | { status: "ok"; release: Release }
  | { status: "none" }
  | { status: "error" };

function formatSize(bytes: number) {
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

export default function DownloadButtons() {
  const [state, setState] = useState<State>({ status: "loading" });

  useEffect(() => {
    let alive = true;
    (async () => {
      try {
        const res = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, {
          headers: { Accept: "application/vnd.github+json" },
        });
        if (!alive) return;
        if (res.status === 404) {
          setState({ status: "none" });
          return;
        }
        if (!res.ok) throw new Error(`GitHub ${res.status}`);
        const data = (await res.json()) as Release;
        if (alive) setState({ status: "ok", release: data });
      } catch {
        if (alive) setState({ status: "error" });
      }
    })();
    return () => {
      alive = false;
    };
  }, []);

  if (state.status === "loading") {
    return (
      <p className="text-sm text-muted" aria-live="polite">
        <span className="mr-2 inline-block h-2 w-2 animate-pulse rounded-full bg-accent align-middle" />
        Checking the latest release…
      </p>
    );
  }

  if (state.status !== "ok") {
    // No published release yet, or the API was unreachable — always offer the releases page.
    return (
      <div className="flex flex-col items-center gap-2">
        <a
          href={RELEASES_URL}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-2 rounded-2xl bg-accent px-6 py-3 font-semibold text-white transition hover:bg-accent2"
        >
          View releases on GitHub
        </a>
        <span className="text-xs text-faint">
          {state.status === "none"
            ? "First release coming soon."
            : "Couldn’t reach GitHub — open the releases page."}
        </span>
      </div>
    );
  }

  const { release } = state;
  const installers = release.assets
    .filter((a) => a.name.toLowerCase().endsWith(".exe"))
    .sort((a, b) => (a.name.toLowerCase().includes("setup") ? -1 : 1));
  const published = new Date(release.published_at).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });

  return (
    <div className="flex flex-col items-center gap-4">
      <div className="flex flex-wrap items-center justify-center gap-x-3 gap-y-1 text-sm text-muted">
        <span className="rounded-full border border-line bg-surface2 px-3 py-1 font-semibold text-ink">
          {release.tag_name}
        </span>
        <span>latest release · {published}</span>
      </div>

      <div className="flex flex-wrap items-center justify-center gap-3">
        {installers.length > 0 ? (
          installers.map((a) => (
            <a
              key={a.name}
              href={a.browser_download_url}
              className="group inline-flex items-center gap-3 rounded-2xl bg-accent px-6 py-3.5 font-semibold text-white shadow-lg shadow-accent/20 transition hover:bg-accent2"
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                <polyline points="7 10 12 15 17 10" />
                <line x1="12" y1="15" x2="12" y2="3" />
              </svg>
              <span className="flex flex-col items-start leading-tight">
                <span>Download for Windows</span>
                <span className="text-xs font-normal text-white/70">
                  {release.tag_name} · {formatSize(a.size)}
                </span>
              </span>
            </a>
          ))
        ) : (
          <a
            href={RELEASES_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-2 rounded-2xl bg-accent px-6 py-3.5 font-semibold text-white transition hover:bg-accent2"
          >
            Get it on GitHub
          </a>
        )}
      </div>

      <a
        href={RELEASES_URL}
        target="_blank"
        rel="noopener noreferrer"
        className="text-sm text-muted underline-offset-4 transition hover:text-ink hover:underline"
      >
        All versions &amp; changelog →
      </a>
    </div>
  );
}
