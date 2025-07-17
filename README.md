# 🧭 Digital Wellbeing PC — Bring balance to your Windows usage

**Digital Wellbeing PC** is an open-source project that brings the core features of Android's Digital Wellbeing to Windows desktops. It tracks your screen time, app usage, and audio exposure, while staying lightweight and respectful of your privacy.

---

## ✨ Features

- 🖥️ **Screen Time Tracking** – Daily + per-session activity tracking
- 📊 **App Usage Monitoring** – Foreground app usage time (like Android)
- 🔊 **Audio Exposure Tracking** – Warns if you're exposed to loud sound
- 🧠 **Break Reminders** – (Coming soon) Health-focused nudges
- 🧱 **Modular WPF Architecture** – CoreLogic + Platform layers
- 📦 **Clean MSIX Installer** – Local certificate and sideload ready
- 💽 **Local SQLite Storage** – All data is stored on your machine

---

## 📦 How to Install (Manual)

1. Visit the [Releases](https://github.com/swarajdhondge/digitalwellbeingpc/releases) page.
2. Download the latest `.zip` file (e.g. `DigitalWellbeingPC_v1.x.x.zip`).
3. Extract the contents. You should see:
   - `installer_<version>_x86.appxbundle`
   - `installer_<version>_x86.appxsym`
   - `installer_<version>_x86` (Security Certificate)
   - `Add-AppDevPackage.ps1`
4. Install the certificate:
   - Right-click on `installer_<version>_x86` → **Install Certificate**
   - Select **Current User**
   - Choose **Place all certificates in the following store**
   - Select **Trusted People**, then finish.
5. Right-click `Add-AppDevPackage.ps1` → **Run with PowerShell** as Administrator.
6. After the script completes, **double-click `installer_<version>_x86.appxbundle`** to launch the installer and complete setup.
7. Open **Digital Wellbeing PC** from the Start Menu.

---

✅ The app will run in the system tray and begin tracking usage.  
⚠️ **Ensure only one instance is running** — multiple instances may corrupt usage data.

---

## 🔊 Audio Safety Thresholds

To promote safe listening habits, audio exposure is tracked and limited. The current logic:

```csharp
ThresholdDb = 85.0                   // Real-world exposure limit
ThresholdTime = 30 mins			// Set low for testing
```

---

## 🛠️ How This App Was Built

**Digital Wellbeing PC** is a real-world example of modern app development with and through AI.
Most of the design, debugging, and problem-solving work for this project was done using **ChatGPT o3 mini high** and **o4 mini high** — not just for code, but for exploring packaging issues, .NET migration, and feature scoping.

Instead of traditional “code-first” engineering, this project was shaped by prompt engineering — using natural language to outline problems, design UIs, fix packaging bugs, and test edge cases. This allowed rapid iterations, bug fixes, and research, all within a single conversation. Most core technical issues (MSIX packaging, SQLite quirks, WPF/Tray bugs, installer design) were solved via AI-powered prompts.

The ultimate goal:
**Bring the best features of Android’s Digital Wellbeing to Windows**, making it possible for anyone to monitor screen time, app usage, and audio exposure — all in a privacy-focused desktop tool.

---

### This app is still in active development.
Expect incomplete features, bugs, and evolving design — but the core is already usable.
This project demonstrates that with prompt engineering and modern AI, anyone can build and ship a full-featured Windows app, no matter their starting point.