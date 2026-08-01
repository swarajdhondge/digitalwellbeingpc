using System;

namespace digital_wellbeing_app.Platform.Windows
{
    /// <summary>
    /// Read/react-only view of the OS-level Windows Focus session (Settings > Focus). Mirrors
    /// ScreenTimeTracker's IdleTimeProvider injectable seam: production code uses the real
    /// WindowsFocusIntegration, tests drive a fake so the reaction logic doesn't need a live
    /// WinRT call to exercise.
    /// </summary>
    public interface IWindowsFocusSource
    {
        /// <summary>True if the OS Focus Session API is available on this device/channel.</summary>
        bool IsSupported { get; }

        /// <summary>Current OS Focus session state. False (never throws) when unsupported.</summary>
        bool IsFocusActive { get; }

        /// <summary>Fired when the OS Focus session starts or ends; argument is the new state.</summary>
        event Action<bool>? FocusActiveChanged;
    }
}
