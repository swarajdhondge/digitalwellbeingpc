import { ImageResponse } from "next/og";
import { readFile } from "node:fs/promises";
import { join } from "node:path";

// Branded social-share card. Next.js auto-wires this to og:image AND twitter:image.
export const alt =
  "Pulse - Digital Wellbeing PC — screen time, app usage, focus, and hearing on Windows";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default async function OpengraphImage() {
  const logo = await readFile(join(process.cwd(), "public/pulse-logo.png"));
  const logoSrc = `data:image/png;base64,${logo.toString("base64")}`;

  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: "80px",
          background:
            "radial-gradient(1200px 600px at 80% -10%, #2a2350 0%, #0b0b12 55%)",
          color: "#f4f4f6",
          fontFamily: "sans-serif",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "28px" }}>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={logoSrc}
            width={132}
            height={132}
            style={{ borderRadius: "30px" }}
            alt=""
          />
          <div style={{ display: "flex", flexDirection: "column" }}>
            <div style={{ fontSize: "68px", fontWeight: 800, letterSpacing: "-2px" }}>
              Pulse
            </div>
            <div style={{ fontSize: "34px", fontWeight: 500, color: "#a9a9b8" }}>
              Digital Wellbeing PC
            </div>
          </div>
        </div>

        <div
          style={{
            marginTop: "48px",
            fontSize: "40px",
            fontWeight: 600,
            lineHeight: 1.25,
            maxWidth: "980px",
          }}
        >
          Screen time, app usage, focus &amp; hearing — calm, private, and 100% on-device.
        </div>

        <div
          style={{
            marginTop: "36px",
            display: "flex",
            alignItems: "center",
            gap: "16px",
            fontSize: "26px",
            color: "#8ecfd8",
            fontWeight: 600,
          }}
        >
          <span>Free &amp; open-source</span>
          <span style={{ color: "#4a4a58" }}>·</span>
          <span>for Windows 10 / 11</span>
        </div>
      </div>
    ),
    { ...size }
  );
}
