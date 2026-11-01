using digital_wellbeing_app.Services;
using Xunit;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>PATH editing used by the Setup.exe install hooks (pure string logic only).</summary>
    public class CommandLinePathTests
    {
        private const string Folder = @"C:\Users\me\AppData\Local\DigitalWellbeingPC\current\";

        [Fact]
        public void Add_AppendsFolderOnce()
        {
            var once = CommandLinePath.WithFolder(@"C:\Tools;%USERPROFILE%\bin", Folder);
            Assert.Equal(@"C:\Tools;%USERPROFILE%\bin;C:\Users\me\AppData\Local\DigitalWellbeingPC\current", once);
            Assert.Equal(once, CommandLinePath.WithFolder(once, Folder));
        }

        [Fact]
        public void Add_TreatsCaseAndTrailingSlashAsTheSameFolder()
        {
            var path = @"c:\users\ME\appdata\local\digitalwellbeingpc\CURRENT;C:\Tools";
            Assert.Equal(path, CommandLinePath.WithFolder(path, Folder));
        }

        [Fact]
        public void Add_ToEmptyPath()
            => Assert.Equal(@"C:\Users\me\AppData\Local\DigitalWellbeingPC\current", CommandLinePath.WithFolder(null, Folder));

        [Fact]
        public void Remove_DropsOnlyThatFolder_AndKeepsVariables()
        {
            var path = @"%USERPROFILE%\bin;C:\Users\me\AppData\Local\DigitalWellbeingPC\current;C:\Tools";
            Assert.Equal(@"%USERPROFILE%\bin;C:\Tools", CommandLinePath.WithoutFolder(path, Folder));
            Assert.Equal(@"C:\Tools", CommandLinePath.WithoutFolder(@"C:\Tools", Folder));
        }

        [Fact]
        public void OtherEntries_AreNeverRewritten()
        {
            // Empty entries, spaces and a trailing ';' belong to the user and stay as they are.
            var messy = @"C:\a;; C:\b ;";
            Assert.Equal(messy, CommandLinePath.WithoutFolder(messy, Folder));
            Assert.Equal(messy + @"C:\Users\me\AppData\Local\DigitalWellbeingPC\current", CommandLinePath.WithFolder(messy, Folder));
            Assert.Equal(@"C:\a;; C:\b ", CommandLinePath.WithoutFolder(messy + Folder, Folder));
        }

        [Fact]
        public void SameFolder_WrittenWithVariablesOrQuotes_IsRecognised()
        {
            var local = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            var folder = System.IO.Path.Combine(local, "DigitalWellbeingPC", "current");
            foreach (var entry in new[] { @"%LOCALAPPDATA%\DigitalWellbeingPC\current", "\"" + folder + "\"" })
            {
                var path = @"C:\Tools;" + entry;
                Assert.Equal(path, CommandLinePath.WithFolder(path, folder));
                Assert.Equal(@"C:\Tools", CommandLinePath.WithoutFolder(path, folder));
            }
        }
    }
}
