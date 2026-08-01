using System;
using System.Collections.Generic;
using System.Windows.Automation;

namespace digital_wellbeing_app.Platform.Windows
{
    /// <summary>
    /// Best-effort UI Automation read of a browser's address bar, reduced to a bare hostname.
    ///
    /// Element selectors below are from a live spike against real installs (2026-07-17, this
    /// dev machine) using a throwaway UIA-walker tool, not guessed from documentation:
    ///   - Chrome:  Name='Address and search bar' AutomationId='view_1012' ClassName='OmniboxViewViews'
    ///   - Edge:    Name='Address and search bar' AutomationId='view_1021' ClassName='OmniboxViewViews'
    ///   - Firefox: Name='Search with Google or enter address' AutomationId='urlbar-input'
    /// AutomationId differed between Chrome and Edge in the same spike run, confirming it's not a
    /// stable cross-browser (or even necessarily cross-session) selector - ClassName
    /// 'OmniboxViewViews' is a Chromium Views-toolkit internal class name shared by every
    /// Chromium-based browser (Chrome/Edge/Brave/Opera/Vivaldi all use the same omnibox
    /// implementation), so it's used as the Chromium selector instead. Firefox's 'urlbar-input' is
    /// a stable Gecko/XUL element ID and is used directly.
    ///
    /// Firefox was included in v1 (the plan had flagged it as possibly Chromium-only-forever,
    /// unverified) because this same spike found its UIA support just as clean as Chromium's -
    /// concrete evidence beat the plan's hedge. Brave/Opera/Vivaldi are inferred, not directly
    /// spiked, on the strength of sharing Chromium's Views omnibox implementation.
    /// </summary>
    public static class BrowserTabInspector
    {
        private const string ChromiumOmniboxClassName = "OmniboxViewViews";
        private const string FirefoxUrlBarAutomationId = "urlbar-input";

        private static readonly HashSet<string> ChromiumProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "msedge", "brave", "opera", "vivaldi"
        };

        private static readonly HashSet<string> FirefoxProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "firefox"
        };

        // A conservative list of common two-label public suffixes so the "eTLD+1 only" privacy
        // bar holds for the most frequent non-.com cases (bbc.co.uk -> bbc.co.uk, not co.uk).
        // Not a full Public Suffix List - a deliberate v1 approximation, not a guess passed off as
        // complete; genuinely obscure suffixes fall back to the naive last-two-labels result.
        private static readonly HashSet<string> CommonTwoLabelSuffixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "co.uk", "org.uk", "gov.uk", "ac.uk", "me.uk", "net.uk",
            "co.jp", "co.in", "co.nz", "co.za", "co.kr",
            "com.au", "com.br", "com.mx", "com.cn", "com.sg", "com.hk", "com.tw",
            "org.au", "net.au", "gov.au"
        };

        public static bool IsKnownBrowser(string processName)
            => ChromiumProcessNames.Contains(processName) || FirefoxProcessNames.Contains(processName);

        /// <summary>
        /// Best-effort read of the raw address bar text for a known browser's foreground window.
        /// Returns null on any failure (tab/window closed mid-read, UIA tree shape changed,
        /// unsupported browser, etc.) - callers must treat null as "nothing to record this tick,"
        /// never as an error to surface.
        /// </summary>
        public static string? TryGetAddressBarText(IntPtr windowHandle, string processName)
        {
            bool isChromium = ChromiumProcessNames.Contains(processName);
            bool isFirefox = !isChromium && FirefoxProcessNames.Contains(processName);
            if (!isChromium && !isFirefox) return null; // short-circuit before any UIA call

            try
            {
                var root = AutomationElement.FromHandle(windowHandle);
                if (root == null) return null;

                var field = isChromium
                    ? root.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ClassNameProperty, ChromiumOmniboxClassName))
                    : root.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.AutomationIdProperty, FirefoxUrlBarAutomationId));

                if (field == null) return null;
                if (!field.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObj) ||
                    patternObj is not ValuePattern valuePattern)
                    return null;

                return valuePattern.Current.Value;
            }
            catch (ElementNotAvailableException)
            {
                return null; // window/tab closed between GetForegroundWindow and this read
            }
            catch (Exception)
            {
                return null; // never let a UIA quirk take down the poll loop
            }
        }

        /// <summary>
        /// Reduce raw address-bar text to a bare registrable-domain hostname, or null if the text
        /// isn't a real http(s) address (new-tab page, browser-internal page, empty, garbage).
        /// Privacy contract: the path, query string, and fragment are discarded here and never seen
        /// by any caller - only the returned hostname is ever persisted.
        /// </summary>
        public static string? ExtractHostname(string? rawAddressBarText)
        {
            if (string.IsNullOrWhiteSpace(rawAddressBarText)) return null;

            var text = rawAddressBarText.Trim();

            // Chrome's omnibox omits "https://" for plain https pages; Edge showed the full
            // scheme for the same page in the same spike run - handle both.
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                if (!Uri.TryCreate("https://" + text, UriKind.Absolute, out uri))
                    return null;
            }

            if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return null; // chrome://, edge://, about:, file://, extension pages, etc.

            if (string.IsNullOrEmpty(uri.Host)) return null;

            return ToRegistrableDomain(uri.Host.ToLowerInvariant());
        }

        private static string ToRegistrableDomain(string host)
        {
            var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (labels.Length <= 2) return host;

            var lastTwo = $"{labels[^2]}.{labels[^1]}";
            if (labels.Length >= 3 && CommonTwoLabelSuffixes.Contains(lastTwo))
                return $"{labels[^3]}.{lastTwo}";

            return lastTwo;
        }
    }
}
