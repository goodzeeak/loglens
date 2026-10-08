using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using LogLens.App;
using LogLens.Core;
using Xunit;

namespace LogLens.Tests;

public sealed class DesktopTests
{
    [Fact]
    public async Task WpfWorkflowAndThemeRegression()
    {
        var completed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var application = new App.App(); application.InitializeComponent();
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async () =>
            {
                Exception? failure = null;
                try
                {
                    var actions = new FakeActions(); var collector = new FakeCollector();
                    using var vm = new MainViewModel(new(collector, new()), actions, new());
                    App.App.ApplyTheme("Dark");
                    var window = new MainWindow(new()) { DataContext = vm };
                    window.Measure(new(1280, 880)); window.Arrange(new(0, 0, 1280, 880)); window.UpdateLayout();
                    Assert.Same(application.Resources["CanvasBrush"], window.Background);
                    Assert.Same(application.Resources["TextBrush"], window.Foreground);
                    Assert.False(vm.PreviewCommand.CanExecute(null));
                    vm.ScanCommand.Execute(null); await UntilIdle(vm);
                    Assert.Equal("1", vm.IncidentCount); Assert.NotNull(vm.Selected);
                    Assert.Contains("Scan complete", vm.Status);
                    vm.Search = "no-such-app"; Assert.Empty(vm.Incidents); Assert.False(vm.HasSelection);
                    vm.Search = ""; Assert.Single(vm.Incidents);
                    vm.ViewDays = 1; var dashboard = (Grid)window.Content; dashboard.Measure(new(1280, 840)); dashboard.Arrange(new(0, 0, 1280, 840)); dashboard.UpdateLayout();
                    Assert.Contains(Descendants<TextBlock>(dashboard), t => t.Text == "1 day");
                    Assert.DoesNotContain(Descendants<TextBlock>(dashboard), t => t.Text == "1 days");
                    vm.ViewDays = 30;
                    Capture(window, "dashboard-dark", 1280, 840);
                    vm.Category = "Storage"; Assert.Empty(vm.Incidents);
                    vm.Category = "All incidents"; Assert.Single(vm.Incidents);
                    collector.Hardware = true; vm.ScanCommand.Execute(null); await UntilIdle(vm);
                    Assert.Equal("Fatal processor cache error", vm.Selected?.Title);
                    Capture(window, "processor-detail-dark", 1280, 840);
                    var historyPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
                    var investigation = new InvestigationWindow(vm.Selected, new(historyPath));
                    investigation.Measure(new(1080, 820)); investigation.Arrange(new(0, 0, 1080, 820)); investigation.UpdateLayout();
                    var investigationVm = (InvestigationViewModel)investigation.DataContext;
                    Assert.Equal("cpu-stock", investigationVm.Plan.StepId);
                    investigationVm.Notes = "Synthetic fixture notes"; investigationVm.Outcome = "Issue recurred";
                    investigationVm.SaveCommand.Execute(null);
                    Assert.Single(new InvestigationStore(historyPath).Load()); Assert.Equal("firmware", investigationVm.Plan.StepId);
                    Capture(investigation, "investigation-dark", 1080, 760);
                    var historyOnly = new InvestigationWindow(null, new(historyPath));
                    historyOnly.Measure(new(1080, 820)); historyOnly.Arrange(new(0, 0, 1080, 820)); historyOnly.UpdateLayout();
                    Capture(historyOnly, "history-only-dark", 1080, 760);
                    Assert.False(((InvestigationViewModel)historyOnly.DataContext).ShowEditor);
                    var historyRoot = (Grid)historyOnly.Content; historyRoot.Measure(new(1080, 760)); historyRoot.Arrange(new(0, 0, 1080, 760)); historyRoot.UpdateLayout();
                    var historyList = Descendants<ListBox>(historyRoot).Single();
                    var historyItem = (ListBoxItem)historyList.ItemContainerGenerator.ContainerFromIndex(0);
                    Assert.Equal(new InvestigationStore(historyPath).Load().Single().AccessibilityLabel,
                        System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(historyItem)!.GetName());
                    var viewer = new ScrollViewer { Width = 200, Height = 120, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Width = 1000, Height = 1000 } };
                    viewer.Measure(new(200, 120)); viewer.Arrange(new(0, 0, 200, 120)); viewer.UpdateLayout();
                    var bars = Descendants<ScrollBar>(viewer).ToArray(); Assert.Equal(2, bars.Length);
                    var vertical = bars.Single(b => b.Orientation == Orientation.Vertical);
                    var horizontal = bars.Single(b => b.Orientation == Orientation.Horizontal);
                    Assert.NotNull(vertical.Template.FindName("PART_Track", vertical));
                    ScrollBar.PageDownCommand.Execute(null, vertical); viewer.UpdateLayout(); Assert.True(viewer.VerticalOffset > 0);
                    ScrollBar.PageRightCommand.Execute(null, horizontal); viewer.UpdateLayout(); Assert.True(viewer.HorizontalOffset > 0);
                    var options = new ExportOptionsWindow(new InvestigationStore(historyPath).Load());
                    options.Measure(new(760, 620)); options.Arrange(new(0, 0, 760, 620)); options.UpdateLayout();
                    Assert.False(options.Selection.IncludeHistory); Assert.False(options.Selection.IncludeReviewedNotes);
                    Capture(options, "export-options-dark", 760, 580);
                    var feedback = new ExportOptionsWindow([], true);
                    feedback.Measure(new(760, 620)); feedback.Arrange(new(0, 0, 760, 620)); feedback.UpdateLayout();
                    Capture(feedback, "feedback-dark", 760, 580);
                    vm.CopyCommand.Execute(null); Assert.Contains("Confirmed observation", actions.Copied);
                    vm.PreviewCommand.Execute(null); await UntilIdle(vm);
                    Assert.NotNull(actions.Report);
                    var preview = new ReportWindow(actions.Report);
                    preview.Measure(new(900, 760)); preview.Arrange(new(0, 0, 900, 760)); preview.UpdateLayout();
                    Assert.Same(application.Resources["CanvasBrush"], preview.Background);
                    Capture(preview, "report-preview", 900, 720);
                    App.App.ApplyTheme("Light"); window.UpdateLayout();
                    Assert.Same(application.Resources["CanvasBrush"], window.Background);
                    Capture(window, "dashboard-light", 1280, 840);
                    Capture(window, "dashboard-small", 960, 660);
                    Capture(investigation, "investigation-light", 1080, 760);
                    Capture(historyOnly, "history-only-light", 1080, 760); historyOnly.Close();
                    Capture(investigation, "investigation-small", 800, 540);
                    investigation.Close(); options.Close(); feedback.Close(); File.Delete(historyPath);
                    var artifactDirectory = Environment.GetEnvironmentVariable("LOGLENS_TEST_ARTIFACTS");
                    if (!string.IsNullOrEmpty(artifactDirectory))
                    {
                        Directory.CreateDirectory(artifactDirectory);
                        await File.WriteAllTextAsync(Path.Combine(artifactDirectory, "synthetic-example-report.html"), actions.Report.ToHtml());
                    }
                    collector.WaitForCancellation = true;
                    vm.ScanCommand.Execute(null); Assert.True(vm.IsBusy); vm.CancelCommand.Execute(null); await UntilIdle(vm);
                    Assert.Contains("cancelled", vm.Status); Assert.Equal("1", vm.IncidentCount);
                    Assert.False(vm.CancelCommand.CanExecute(null));
                    preview.Close(); window.Close();
                }
                catch (Exception ex) { failure = ex; }
                finally { completed.TrySetResult(failure); application.Shutdown(); }
            }));
            application.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        var error = await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
    private static async Task UntilIdle(MainViewModel vm)
    {
        var watch = Stopwatch.StartNew();
        while (vm.IsBusy && watch.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(10);
        Assert.False(vm.IsBusy);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Capture(Window window, string name, int width, int height)
    {
        var directory = Environment.GetEnvironmentVariable("LOGLENS_TEST_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        var root = (System.Windows.Controls.Grid)window.Content;
        root.Background = window.Background;
        root.Measure(new(width, height)); root.Arrange(new(0, 0, width, height)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(file);
    }
    private sealed class FakeCollector : IEventCollector
    {
        public bool WaitForCancellation { get; set; }
        public bool Hardware { get; set; }
        public async Task<CollectionResult> CollectAsync(ScanPeriod period, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
            var e = Hardware ? AccuracyFixtures.E("Microsoft-Windows-WHEA-Logger", 1, fields: [("RawData", Convert.ToHexString(CperTests.Record()))]) : AccuracyFixtures.K();
            return new([e with { Time = period.End.AddSeconds(-10) }], []);
        }
    }
    private sealed class FakeActions : IDesktopActions
    {
        public string Copied { get; private set; } = "";
        public DiagnosticReport? Report { get; private set; }
        public void Copy(string text) => Copied = text;
        public void Preview(DiagnosticReport report) => Report = report;
        public void OpenReliability() { }
        public void OpenEventViewer() { }
        public void ReopenElevated() { }
        public void OpenLink(string name) { }
    }
}
