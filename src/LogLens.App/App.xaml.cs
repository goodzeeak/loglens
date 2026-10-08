using System.Windows;
using System.Windows.Media;

namespace LogLens.App;

public partial class App : Application
{
    public static string CurrentTheme { get; private set; } = "Dark";
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = AppSettings.Load(); ApplyTheme(settings.Theme);
        var window = new MainWindow(settings); MainWindow = window; window.Show();
    }
    public static void ApplyTheme(string theme)
    {
        CurrentTheme = theme;
        var dark = theme == "Dark";
        var values = new Dictionary<string, string>
        {
            ["CanvasBrush"] = dark ? "#0B1220" : "#F3F6FB", ["PanelBrush"] = dark ? "#131E30" : "#FFFFFF",
            ["TextBrush"] = dark ? "#F0F5FC" : "#17243B", ["MutedBrush"] = dark ? "#ADBFD7" : "#455A75",
            ["BorderBrush"] = dark ? "#3C4F69" : "#B9C7D9", ["AccentBrush"] = dark ? "#83E6CC" : "#12634F",
            ["AccentTextBrush"] = dark ? "#071D1B" : "#FFFFFF", ["SelectionBrush"] = dark ? "#294960" : "#D9EEE8"
        };
        foreach (var (key, value) in values) Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        if (SystemParameters.HighContrast)
        {
            Current.Resources["CanvasBrush"] = SystemColors.WindowBrush; Current.Resources["PanelBrush"] = SystemColors.WindowBrush;
            Current.Resources["TextBrush"] = SystemColors.WindowTextBrush; Current.Resources["MutedBrush"] = SystemColors.WindowTextBrush;
            Current.Resources["AccentBrush"] = SystemColors.HighlightBrush; Current.Resources["AccentTextBrush"] = SystemColors.HighlightTextBrush;
            Current.Resources["SelectionBrush"] = SystemColors.WindowBrush; Current.Resources["BorderBrush"] = SystemColors.WindowTextBrush;
        }
        foreach (Window window in Current.Windows) WindowTheme.Apply(window);
    }
}
