import type { Metadata } from "next";
import { Hanken_Grotesk } from "next/font/google";
import "./globals.css";

const hanken = Hanken_Grotesk({
  subsets: ["latin"],
  variable: "--font-hanken",
  display: "swap",
});

export const metadata: Metadata = {
  title: "Pulse - Digital Wellbeing PC",
  description:
    "A calm, private digital-wellbeing app for Windows. Track screen time, app usage, and focus sessions, and protect your hearing — all on-device, no account, no cloud.",
  applicationName: "Pulse - Digital Wellbeing PC",
  keywords: [
    "screen time",
    "digital wellbeing",
    "Windows",
    "focus mode",
    "app usage",
    "hearing protection",
    "Pulse",
  ],
  openGraph: {
    title: "Pulse - Digital Wellbeing PC",
    description:
      "Track your screen time, app usage, and focus — privately, on your Windows PC.",
    type: "website",
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
