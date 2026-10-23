using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SE2WorkshopUGCFix.Wpf;

internal static class WindowAppearance
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);

    internal static void ApplyDarkTitleBar(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                int enabled = 1; // DWMWA_USE_IMMERSIVE_DARK_MODE (Windows 10/11)
                _ = DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int));
                int rounded = 2; // DWMWCP_ROUND (Windows 11; ignored when unsupported)
                _ = DwmSetWindowAttribute(hwnd, 33, ref rounded, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        };
    }
}
