using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace digital_wellbeing_app.Platform.Windows
{
    public class FocusChangeListener
    {
        public delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        );

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags
        );

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr hWnd,
            out uint lpdwProcessId
        );

        private readonly WinEventDelegate _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private readonly Action<Process?> _onAppChanged;

        public FocusChangeListener(Action<Process?> onAppChanged)
        {
            _proc = Callback;
            _onAppChanged = onAppChanged;
        }

        public void Start()
        {
            _hookID = SetWinEventHook(
                0x0003,
                0x0003,
                IntPtr.Zero,
                _proc,
                0,
                0,
                0
            );
        }

        public void Stop()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWinEvent(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private void Callback(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime
        )
        {
            if (hwnd == IntPtr.Zero)
            {
                _onAppChanged(null);
                return;
            }

            uint threadId = GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0)
            {
                _onAppChanged(null);
                return;
            }

            try
            {
                var proc = Process.GetProcessById((int)pid);
                _onAppChanged(proc);
            }
            catch
            {
                _onAppChanged(null);
            }
        }
    }
}
