using System;
using System.Collections.Generic;
using System.IO;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Service to convert raw process/executable names to user-friendly display names.
    /// </summary>
    public static class AppNameService
    {
        private static readonly Dictionary<string, string> KnownApps = new(StringComparer.OrdinalIgnoreCase)
        {
            // Browsers
            { "chrome", "Google Chrome" },
            { "msedge", "Microsoft Edge" },
            { "firefox", "Firefox" },
            { "brave", "Brave Browser" },
            { "opera", "Opera" },
            { "vivaldi", "Vivaldi" },
            { "iexplore", "Internet Explorer" },
            
            // Microsoft Office
            { "winword", "Microsoft Word" },
            { "excel", "Microsoft Excel" },
            { "powerpnt", "PowerPoint" },
            { "outlook", "Outlook" },
            { "onenote", "OneNote" },
            { "msteams", "Microsoft Teams" },
            { "teams", "Microsoft Teams" },
            
            // Development
            { "code", "VS Code" },
            { "cursor", "Cursor" },
            { "devenv", "Visual Studio" },
            { "rider", "JetBrains Rider" },
            { "idea64", "IntelliJ IDEA" },
            { "pycharm64", "PyCharm" },
            { "webstorm64", "WebStorm" },
            { "phpstorm64", "PhpStorm" },
            { "notepad++", "Notepad++" },
            { "sublime_text", "Sublime Text" },
            { "atom", "Atom" },
            { "WindowsTerminal", "Windows Terminal" },
            { "wt", "Windows Terminal" },
            { "powershell", "PowerShell" },
            { "pwsh", "PowerShell" },
            { "cmd", "Command Prompt" },
            { "GitHubDesktop", "GitHub Desktop" },
            { "SourceTree", "Sourcetree" },
            
            // Communication
            { "discord", "Discord" },
            { "slack", "Slack" },
            { "zoom", "Zoom" },
            { "whatsapp", "WhatsApp" },
            { "telegram", "Telegram" },
            { "signal", "Signal" },
            { "skype", "Skype" },
            
            // Entertainment
            { "spotify", "Spotify" },
            { "vlc", "VLC Player" },
            { "netflix", "Netflix" },
            { "steam", "Steam" },
            { "epicgameslauncher", "Epic Games" },
            { "obs64", "OBS Studio" },
            { "obs", "OBS Studio" },
            
            // Design
            { "photoshop", "Photoshop" },
            { "illustrator", "Illustrator" },
            { "figma", "Figma" },
            { "xd", "Adobe XD" },
            { "afterfx", "After Effects" },
            { "premiere", "Premiere Pro" },
            { "blender", "Blender" },
            
            // System
            { "explorer", "File Explorer" },
            { "taskmgr", "Task Manager" },
            { "notepad", "Notepad" },
            { "calc", "Calculator" },
            { "mspaint", "Paint" },
            { "snippingtool", "Snipping Tool" },
            { "ScreenSketch", "Snip & Sketch" },
            { "SystemSettings", "Settings" },
            { "mmc", "Console" },
            { "regedit", "Registry Editor" },
            { "control", "Control Panel" },
            
            // Productivity
            { "notion", "Notion" },
            { "obsidian", "Obsidian" },
            { "todoist", "Todoist" },
            { "evernote", "Evernote" },
            { "trello", "Trello" },
            
            // Utilities
            { "7zfm", "7-Zip" },
            { "winrar", "WinRAR" },
            { "everything", "Everything Search" },
            { "ditto", "Ditto Clipboard" },
            { "bitwarden", "Bitwarden" },
            { "1password", "1Password" },
            { "keepass", "KeePass" },
            
            // Cloud Storage
            { "onedrive", "OneDrive" },
            { "googledrivesync", "Google Drive" },
            { "dropbox", "Dropbox" },
            
            // PDF
            { "acrobat", "Adobe Acrobat" },
            { "acrord32", "Adobe Reader" },
            { "foxitreader", "Foxit Reader" },
            { "sumatrapdf", "SumatraPDF" },
            
            // Games
            { "minecraft", "Minecraft" },
            { "valorant", "Valorant" },
            { "fortnite", "Fortnite" },
            { "leagueclient", "League of Legends" },
            
            // Our app
            { "digitalwellbeing", "Pulse" },
            { "digital-wellbeing-app", "Pulse" },
        };

        /// <summary>
        /// Gets a user-friendly display name for an application.
        /// </summary>
        /// <param name="processName">The process name (without .exe extension)</param>
        /// <param name="executablePath">Optional full path to the executable</param>
        /// <returns>A user-friendly display name</returns>
        public static string GetDisplayName(string processName, string? executablePath = null)
        {
            if (string.IsNullOrEmpty(processName))
                return "Unknown";

            // Clean up the process name
            var cleanName = processName.Trim();
            if (cleanName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                cleanName = cleanName[..^4];

            // Check known apps dictionary
            if (KnownApps.TryGetValue(cleanName, out var displayName))
                return displayName;

            // Try to get file description from executable
            if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
            {
                try
                {
                    var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(executablePath);
                    if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
                    {
                        var desc = versionInfo.FileDescription.Trim();
                        // Filter out generic descriptions
                        if (!string.IsNullOrEmpty(desc) && 
                            !desc.Equals(cleanName, StringComparison.OrdinalIgnoreCase) &&
                            !desc.Equals("Windows Application", StringComparison.OrdinalIgnoreCase) &&
                            !desc.Equals("Application", StringComparison.OrdinalIgnoreCase))
                        {
                            return desc;
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
                    {
                        return versionInfo.ProductName.Trim();
                    }
                }
                catch
                {
                    // Ignore errors reading version info
                }
            }

            // Fallback: capitalize and clean up the process name
            return PrettifyName(cleanName);
        }

        /// <summary>
        /// Prettifies a raw process name by capitalizing and adding spaces.
        /// </summary>
        private static string PrettifyName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // Handle common patterns
            var result = name;

            // Insert spaces before uppercase letters (camelCase/PascalCase)
            var withSpaces = System.Text.RegularExpressions.Regex.Replace(
                result, 
                "([a-z])([A-Z])", 
                "$1 $2");

            // Capitalize first letter
            if (withSpaces.Length > 0)
            {
                withSpaces = char.ToUpper(withSpaces[0]) + withSpaces[1..];
            }

            return withSpaces;
        }
    }
}

