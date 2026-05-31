using System;
using System.IO;
using Microsoft.Win32;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// One-time, idempotent migrations from the legacy "Digital Wellbeing" identity
    /// to the "Pulse" identity. Runs at startup BEFORE any service touches the data
    /// folder, so updates/reinstalls keep all existing history, logs, and settings.
    /// Never throws — a failed migration must not block app startup.
    /// </summary>
    public static class DataMigrationService
    {
        private const string OldFolderName = "Digital Wellbeing";
        public const string FolderName = "Pulse";

        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string OldRunValue = "Digital Wellbeing";
        private const string NewRunValue = "Pulse";

        public static void RunMigrations()
        {
            try { MigrateDataFolder(); } catch { /* never crash startup */ }
            try { MigrateStartupEntry(); } catch { /* never crash startup */ }
        }

        /// <summary>
        /// Move %LocalAppData%\Digital Wellbeing -> %LocalAppData%\Pulse (DB + logs + settings),
        /// but only when the new folder doesn't already exist (so we never clobber data and
        /// fresh installs are untouched).
        /// </summary>
        private static void MigrateDataFolder()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var oldPath = Path.Combine(localAppData, OldFolderName);
            var newPath = Path.Combine(localAppData, FolderName);

            if (Directory.Exists(newPath)) return;   // already migrated, or fresh install on the new name
            if (!Directory.Exists(oldPath)) return;  // nothing to migrate (brand-new user)

            Directory.Move(oldPath, newPath);        // same-volume move: fast, preserves everything
        }

        /// <summary>
        /// Carry the "run at login" registry entry over to the new name and remove the old one,
        /// so users who enabled autostart aren't left with a stale/duplicate entry.
        /// </summary>
        private static void MigrateStartupEntry()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;

            if (key.GetValue(OldRunValue) is not string oldCommand) return; // autostart wasn't enabled
            if (key.GetValue(NewRunValue) == null) key.SetValue(NewRunValue, oldCommand);
            key.DeleteValue(OldRunValue, throwOnMissingValue: false);
        }
    }
}
