// SettingsService (linked from the app) names the theme enum from the app's ViewModels; the
// command-line tool never reads the theme, so it only needs the type to exist.
namespace digital_wellbeing_app.ViewModels
{
    public enum AppTheme { Light, Dark, Auto }
}
