import type { Metadata } from "next";
import { Hanken_Grotesk } from "next/font/google";
import "./globals.css";

const hanken = Hanken_Grotesk({
  subsets: ["latin"],
  variable: "--font-hanken",
  display: "swap",
});

const SITE_URL = "https://digitalwellbeingpc.vercel.app";
const DESCRIPTION =
  "Pulse is a free, private, open-source Digital Wellbeing app for Windows. Track screen time, app usage, and focus sessions, and protect your hearing — all on-device. No account, no cloud, no telemetry.";

export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: {
    default: "Pulse - Digital Wellbeing PC — Screen Time Tracker for Windows",
    template: "%s · Pulse - Digital Wellbeing PC",
  },
  description: DESCRIPTION,
  applicationName: "Pulse - Digital Wellbeing PC",
  authors: [{ name: "Swaraj Dhondge", url: "https://github.com/swarajdhondge" }],
  creator: "Swaraj Dhondge",
  publisher: "Swaraj Dhondge",
  category: "productivity",
  keywords: [
    "digital wellbeing windows",
    "screen time tracker windows",
    "app usage monitor",
    "focus timer windows",
    "windows screen time app",
    "hearing protection app",
    "productivity",
    "Pulse",
    "digital wellbeing PC",
    "screen time for PC",
  ],
  alternates: { canonical: "/" },
  robots: {
    index: true,
    follow: true,
    googleBot: {
      index: true,
      follow: true,
      "max-image-preview": "large",
      "max-snippet": -1,
      "max-video-preview": -1,
    },
  },
  openGraph: {
    type: "website",
    url: SITE_URL,
    siteName: "Pulse - Digital Wellbeing PC",
    title: "Pulse - Digital Wellbeing PC — Screen Time Tracker for Windows",
    description:
      "Track screen time, app usage, focus, and hearing on Windows — calm, private, and 100% on-device. Free & open-source.",
    locale: "en_US",
  },
  twitter: {
    card: "summary_large_image",
    title: "Pulse - Digital Wellbeing PC",
    description:
      "Android-style Digital Wellbeing for Windows — screen time, app usage, focus, and hearing, 100% on-device.",
  },
  icons: {
    icon: "/favicon.ico",
    shortcut: "/favicon.ico",
  },
  verification: {
    google: "xkmsgFLzOPxSLto_JkhJpctpPQ9iGPlAUQgQx-A5oK8",
  },
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en" className={`${hanken.variable} antialiased`}>
      <body>{children}</body>
    </html>
  );
}
