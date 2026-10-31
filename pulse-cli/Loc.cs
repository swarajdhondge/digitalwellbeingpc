using System.Globalization;
using System.Resources;

namespace digital_wellbeing_app.Helpers
{
    /// <summary>
    /// Command-line counterpart of the app's Loc (which depends on WPF): same resource files,
    /// same keys, same language setting. Missing translations fall back to English.
    /// </summary>
    public static class Loc
    {
        private static readonly ResourceManager Strings =
            new("digital_wellbeing_app.Properties.Strings", typeof(Loc).Assembly);

        public static string Get(string key) => Strings.GetString(key) ?? key;

        public static string Format(string key, params object?[] args)
            => string.Format(CultureInfo.CurrentCulture, Get(key), args);

        /// <summary>Applies the saved language (empty = follow Windows).</summary>
        public static void Apply(string? languageTag)
        {
            if (string.IsNullOrWhiteSpace(languageTag)) return;
            try
            {
                var culture = CultureInfo.GetCultureInfo(languageTag);
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = culture;
            }
            catch (CultureNotFoundException) { }
        }
    }
}
