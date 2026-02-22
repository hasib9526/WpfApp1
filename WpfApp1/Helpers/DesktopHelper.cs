using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WpfApp1.Helpers
{
    /// <summary>
    /// Embeds a WPF window into the desktop wallpaper layer.
    /// The window will only be visible on the desktop — it hides
    /// behind any open application (Chrome, Explorer, etc.).
    /// Works like Rainmeter / Windows Desktop Gadgets.
    /// </summary>
    public static class DesktopHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint SMTO_NORMAL = 0x0000;

        /// <summary>
        /// Pins the given WPF window to the desktop layer.
        /// Call this AFTER the window is loaded (e.g., in Loaded event).
        /// </summary>
        public static void PinToDesktop(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;

            var workerW = GetDesktopWorkerW();
            if (workerW != IntPtr.Zero)
            {
                SetParent(handle, workerW);
            }
        }

        /// <summary>
        /// Finds the WorkerW window that sits behind desktop icons.
        /// This is the same technique used by Rainmeter and Wallpaper Engine.
        /// </summary>
        private static IntPtr GetDesktopWorkerW()
        {
            // Step 1: Find Progman (the desktop program manager)
            var progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return IntPtr.Zero;

            // Step 2: Send a special undocumented message (0x052C)
            // This forces Windows to create a WorkerW window behind desktop icons
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero,
                SMTO_NORMAL, 1000, out _);

            // Step 3: Find the WorkerW that has a SHELLDLL_DefView child
            IntPtr workerW = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                var shellView = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    // Found it! Now get the WorkerW that comes AFTER this one
                    workerW = FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null);
                }
                return true; // continue enumeration
            }, IntPtr.Zero);

            return workerW;
        }
    }
}
