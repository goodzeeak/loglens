using System.Diagnostics;
using System.IO;
using System.Windows;
using LogLens.Core;
using LogLens.Windows;

namespace LogLens.App;

public partial class MainWindow : Window, IDesktopActions
{
    public MainWindow(AppSettings settings)
    {
        InitializeComponent();
        WindowTheme.Attach(this);
        DataContext = new MainViewModel(new(new WindowsEventCollector(), new()), this, settings);
        Closed += (_, _) => ((MainViewModel)DataContext).Dispose();
    }
    public void Preview(DiagnosticReport report) => new ReportWindow(report) { Owner = this }.ShowDialog();
    public void Copy(string text) => Clipboard.SetText(text);
    public void OpenReliability() => Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "perfmon.exe"), "/rel") { UseShellExecute = true });
    public void OpenEventViewer() => Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "mmc.exe"), "eventvwr.msc") { UseShellExecute = true });
    public void ReopenElevated()
    {
        var path = Environment.ProcessPath ?? throw new InvalidOperationException("Executable unavailable.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" });
    }
    public void OpenLink(string name)
    {
        var url = name switch
        { "repository" => "https://github.com/goodzeeak/loglens", "issues" => "https://github.com/goodzeeak/loglens/issues", "privacy" => "https://github.com/goodzeeak/loglens/blob/main/docs/PRIVACY.md", _ => throw new ArgumentException("Unknown link.", nameof(name)) };
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
