namespace digital_wellbeing_app.Services
{
    /// <summary>Where a click on an alert takes the user, after Pulse has been brought forward.</summary>
    public enum NotificationTarget { ShowOnly, Dashboard, Limits, BreakOverlay, FocusOverlay, Settings, Sound }

    /// <summary>
    /// Maps the launch argument a toast carries to the place its click opens. The same mapping
    /// serves toast clicks and the tray balloon fallback.
    /// </summary>
    public static class NotificationRouting
    {
        public const string ArgumentName = "action";
        public const string Goal = "goal";
        public const string Limit = "limit";
        public const string Break = "break";
        public const string Focus = "focus";
        public const string WindDown = "winddown";
        public const string Hearing = "hearing";
        public const string Hint = "hint";

        /// <param name="breakPending">The break reminder was not snoozed or dismissed meanwhile.</param>
        /// <param name="distractionActive">There still is a distracting app to warn about.</param>
        public static NotificationTarget Resolve(string? action, bool breakPending, bool distractionActive) => action switch
        {
            Goal => NotificationTarget.Dashboard,
            Limit => NotificationTarget.Limits,
            Break => breakPending ? NotificationTarget.BreakOverlay : NotificationTarget.ShowOnly,
            Focus => distractionActive ? NotificationTarget.FocusOverlay : NotificationTarget.ShowOnly,
            WindDown => NotificationTarget.Settings,
            Hearing => NotificationTarget.Sound,
            _ => NotificationTarget.ShowOnly, // Hint, or an unknown argument
        };
    }
}
