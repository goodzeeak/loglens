using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LogLens.App;

public static class WindowTheme
{
    public static void Attach(Window window) => window.SourceInitialized += (_, _) => Apply(window);
    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var dark = App.CurrentTheme == "Dark" && !SystemParameters.HighContrast ? 1 : 0;
        // Unsupported attributes fail harmlessly; never change global Windows settings.
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        var background = ColorRef("CanvasBrush"); var foreground = ColorRef("TextBrush");
        _ = DwmSetWindowAttribute(handle, 35, ref background, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref foreground, sizeof(int));
    }
    private static int ColorRef(string resource)
    {
        if (SystemParameters.HighContrast) return -1;
        var color = ((SolidColorBrush)Application.Current.Resources[resource]).Color;
        return color.R | (color.G << 8) | (color.B << 16);
    }
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
