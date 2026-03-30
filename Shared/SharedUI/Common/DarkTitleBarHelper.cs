using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Shared.Core.Settings;

namespace CommonControls
{
    /// <summary>
    /// Enables dark mode title bar on Windows 11+ via DWM API.
    /// Automatically follows the current theme - dark themes get dark title bar, light themes get light title bar.
    /// Call DarkTitleBarHelper.Enable(this) in a Window constructor.
    /// </summary>
    public static class DarkTitleBarHelper
    {
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static void Enable(Window window)
        {
            if (window.IsLoaded)
                Apply(window);
            else
                window.SourceInitialized += (s, e) => Apply(window);
        }

        private static void Apply(Window window)
        {
            var isDark = !ThemesController.CurrentTheme.ToString().Contains("Light");
            var value = isDark ? 1 : 0;
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
                DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
    }
}
