using System.Reflection;

// Single source of the app version (GenerateAssemblyInfo is off to avoid the WPF temp-project
// duplicate-attribute issue). CI patches these three lines from the git tag on release
// (see .github/workflows/release.yml -> .github/scripts/stamp-version.ps1).
[assembly: AssemblyVersion("2.2.0.0")]
[assembly: AssemblyFileVersion("2.2.0.0")]
[assembly: AssemblyInformationalVersion("2.2.0")]
[assembly: AssemblyTitle("Pulse - DigitalWellbeingPC")]
[assembly: AssemblyProduct("Pulse - DigitalWellbeingPC")]
