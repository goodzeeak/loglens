using System.Windows;
using LogLens.Core;

namespace LogLens.App;

public sealed record ReportSelection(bool IncludeHistory, bool IncludeReviewedNotes);
public partial class ExportOptionsWindow : Window
{
    public ExportOptionsWindow(IReadOnlyList<InvestigationEntry> history, bool feedback = false)
    {
        InitializeComponent(); WindowTheme.Attach(this);
        NotesReview.Text = history.Count == 0 ? "No investigation history for this report." : string.Join("\n\n", history.Select(h => h.Summary + "\n" + h.Notes));
        if (feedback) { Heading.Text = "Prepare diagnostic feedback"; HistoryOptions.Visibility = Visibility.Collapsed; FeedbackOptions.Visibility = Visibility.Visible; }
    }
    public ReportSelection Selection => new(IncludeHistory.IsChecked == true, IncludeHistory.IsChecked == true && IncludeNotes.IsChecked == true);
    public string Feedback => ExpectedBehavior.Text;
    private void ContinueClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
