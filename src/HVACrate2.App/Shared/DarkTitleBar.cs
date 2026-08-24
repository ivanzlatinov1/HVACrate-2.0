using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HVACrate2.App.Shared;

/// <summary>
/// Forces the native Windows title bar into dark mode via DWM, independent of the app's own
/// light/dark content theme (<see cref="ThemeManager"/>) — the title bar stays dark even when the
/// page content is in light mode. Windows 11 and Windows 10 2004+ use attribute 20; older 2018-2019
/// Windows 10 builds use 19 — both are tried since there is no reliable version check that maps
/// cleanly to which attribute a given build accepts.
/// </summary>
internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBeforeWin10Build20H1 = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        int enabled = 1;
        int result = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        if (result != 0)
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBeforeWin10Build20H1, ref enabled, sizeof(int));
    }
}
