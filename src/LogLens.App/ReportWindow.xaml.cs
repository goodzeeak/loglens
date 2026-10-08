using System.IO;
using System.Text;
using System.Windows;
using LogLens.Core;
using Microsoft.Win32;

namespace LogLens.App;

public partial class ReportWindow : Window
{
    private readonly DiagnosticReport report;
    public ReportWindow(DiagnosticReport report)
    {
        InitializeComponent(); this.report = report; PreviewText.Text = report.PlainText;
    }
    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { FileName = $"LogLens-report-{DateTime.Now:yyyyMMdd-HHmm}.html", Filter = "HTML report (*.html)|*.html", DefaultExt = ".html", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            IsEnabled = false;
            await File.WriteAllTextAsync(dialog.FileName, await Task.Run(report.ToHtml), new UTF8Encoding(false));
            ExportStatus.Text = "Report saved. Review it again before sharing.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ExportStatus.Text = "Could not save the report. Choose a writable location and try again."; }
        finally { IsEnabled = true; }
    }
}
