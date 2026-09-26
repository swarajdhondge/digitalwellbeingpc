using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Windows;
using System.Windows.Markup;

namespace digital_wellbeing_app.Helpers
{
    /// <summary>
    /// UI text from Properties/Strings.resx (English) and its per-language files
    /// (e.g. Strings.zh-Hans.resx), resolved for <see cref="CultureInfo.CurrentUICulture"/>.
    /// Missing translations fall back to English.
    /// </summary>
    public static class Loc
    {
        private static readonly ResourceManager Strings =
            new("digital_wellbeing_app.Properties.Strings", typeof(Loc).Assembly);

        public static string Get(string key) => Strings.GetString(key) ?? key;

        public static string Format(string key, params object?[] args)
            => string.Format(CultureInfo.CurrentCulture, Get(key), args);

        /// <summary>English plus every language whose translation ships with the app.</summary>
        public static IReadOnlyList<CultureInfo> AvailableLanguages()
        {
            var satellite = typeof(Loc).Assembly.GetName().Name + ".resources.dll";
            var translated = Directory.EnumerateDirectories(AppContext.BaseDirectory)
                .Where(dir => File.Exists(Path.Combine(dir, satellite)))
                .Select(dir => TryGetCulture(Path.GetFileName(dir)))
                .OfType<CultureInfo>()
                .OrderBy(c => c.NativeName, StringComparer.CurrentCulture);
            return new[] { CultureInfo.GetCultureInfo("en") }.Concat(translated).ToList();
        }

        /// <summary>
        /// Applies the saved language (empty = follow Windows). Call once at startup, before any
        /// window is created; a change takes effect on the next launch.
        /// </summary>
        public static void Apply(string? languageTag)
        {
            // An explicit choice also drives day/month names and number formats, so a page never
            // mixes the chosen language with the Windows region's.
            if (TryGetCulture(languageTag) is CultureInfo culture)
            {
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = culture;
            }

            // WPF picks script-specific glyphs (e.g. Simplified vs Traditional Han) from this.
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag)));
        }

        private static CultureInfo? TryGetCulture(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            try { return CultureInfo.GetCultureInfo(tag); }
            catch (CultureNotFoundException) { return null; }
        }
    }

    /// <summary>XAML access to <see cref="Loc"/>: <c>Text="{l:Loc Settings_Theme}"</c>.</summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public LocExtension(string key) => Key = key;

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
    }
}
