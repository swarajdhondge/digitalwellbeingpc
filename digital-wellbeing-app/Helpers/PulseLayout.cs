using System;
using System.Windows;
using System.Windows.Controls;

namespace digital_wellbeing_app.Helpers
{
    /// <summary>
    /// Shared layout helper for Pulse pages. The hosting ContentControl hands views an
    /// unbounded width, so a page's scrolling content would stretch full-width. CapCenter
    /// constrains the content to the scroll viewport (capped at <paramref name="max"/>) and,
    /// with the content's HorizontalAlignment set to Center, keeps it as a centered ~1080
    /// column like the prototype.
    /// </summary>
    public static class PulseLayout
    {
        public static void CapCenter(ScrollViewer scroll, FrameworkElement content, double max = 1080)
        {
            scroll.SizeChanged += (_, e) =>
            {
                var inner = e.NewSize.Width - scroll.Padding.Left - scroll.Padding.Right;
                content.Width = Math.Min(Math.Max(inner, 0), max);
            };
        }
    }
}
