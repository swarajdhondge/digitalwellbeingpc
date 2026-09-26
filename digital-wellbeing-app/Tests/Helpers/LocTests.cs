using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using digital_wellbeing_app.Helpers;
using Xunit;

namespace digital_wellbeing_app.Tests.Helpers
{
    /// <summary>Keeps Strings.resx, its translations and the keys used in code in sync.</summary>
    public class LocTests
    {
        private static readonly string AppDir = FindAppDir();
        private static readonly string ResxDir = Path.Combine(AppDir, "Properties");

        [Fact]
        public void Get_ReturnsEnglishText_AndFallsBackToTheKey()
        {
            Assert.Equal("Language", Loc.Get("Settings_Language"));
            Assert.Equal("No_Such_Key", Loc.Get("No_Such_Key"));
        }

        [Fact]
        public void EveryKeyUsedInCode_ExistsInStringsResx()
        {
            var defined = ReadResx(Path.Combine(ResxDir, "Strings.resx")).Keys.ToHashSet();
            var missing = UsedKeys().Where(k => !defined.Contains(k.Key))
                .Select(k => $"{k.Key} ({k.File})").ToList();
            Assert.True(missing.Count == 0, "Keys missing from Strings.resx:\n" + string.Join("\n", missing));
        }

        [Fact]
        public void EveryKeyInStringsResx_IsUsed()
        {
            var used = UsedKeys().Select(k => k.Key).ToHashSet();
            var unused = ReadResx(Path.Combine(ResxDir, "Strings.resx")).Keys.Where(k => !used.Contains(k)).ToList();
            Assert.True(unused.Count == 0, "Unused keys in Strings.resx:\n" + string.Join("\n", unused));
        }

        [Fact]
        public void Translations_UseKnownKeys_AndKeepPlaceholders()
        {
            var english = ReadResx(Path.Combine(ResxDir, "Strings.resx"));
            var problems = new List<string>();
            foreach (var file in Directory.GetFiles(ResxDir, "Strings.*.resx"))
            {
                foreach (var (key, text) in ReadResx(file))
                {
                    if (!english.TryGetValue(key, out var source))
                        problems.Add($"{Path.GetFileName(file)}: unknown key {key}");
                    // A missing placeholder drops a value; an extra one crashes string.Format.
                    else if (!Placeholders(text).SetEquals(Placeholders(source)))
                        problems.Add($"{Path.GetFileName(file)}: {key} placeholders differ from English");
                }
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        private static IEnumerable<(string Key, string File)> UsedKeys()
        {
            var xaml = new Regex(@"\{l:Loc\s+([A-Za-z0-9_]+)\}");
            var code = new Regex(@"Loc\.(?:Get|Format)\(\s*""([A-Za-z0-9_]+)""");
            foreach (var file in Directory.EnumerateFiles(AppDir, "*.*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"))
                    continue;
                var pattern = file.EndsWith(".xaml") ? xaml : file.EndsWith(".cs") ? code : null;
                if (pattern == null) continue;
                foreach (Match m in pattern.Matches(File.ReadAllText(file)))
                    yield return (m.Groups[1].Value, Path.GetFileName(file));
            }
        }

        private static Dictionary<string, string> ReadResx(string path)
            => XDocument.Load(path).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

        private static HashSet<string> Placeholders(string text)
            => Regex.Matches(text, @"\{(\d+)(?:[,:][^}]*)?\}").Select(m => m.Groups[1].Value).ToHashSet();

        private static string FindAppDir()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "digital-wellbeing-app");
                if (File.Exists(Path.Combine(candidate, "digital-wellbeing-app.csproj")))
                    return candidate;
            }
            throw new DirectoryNotFoundException("digital-wellbeing-app source folder not found");
        }
    }
}
