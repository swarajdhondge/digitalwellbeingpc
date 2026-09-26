using Xunit;
using digital_wellbeing_app.Platform.Windows;

namespace digital_wellbeing_app.Tests.Platform
{
    public class FocusChangeListenerTests
    {
        [Theory]
        [InlineData("explorer", "Progman", false)]
        [InlineData("explorer", "WorkerW", false)]
        [InlineData("explorer", "Shell_TrayWnd", false)]
        [InlineData("explorer", "", false)]
        [InlineData("explorer", "CabinetWClass", true)]
        [InlineData("EXPLORER", "ExploreWClass", true)]
        [InlineData("notepad", "Notepad", true)]
        public void ExplorerShellIsNotFileExplorer(string process, string windowClass, bool expected)
            => Assert.Equal(expected, FocusChangeListener.IsTrackableWindow(process, windowClass));
    }
}
