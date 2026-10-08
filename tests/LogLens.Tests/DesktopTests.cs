using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
                    Capture(window, "dashboard-dark", 1280, 840);
                    vm.Category = "Storage"; Assert.Empty(vm.Incidents);
                    vm.Category = "All incidents"; Assert.Single(vm.Incidents);
                    collector.Hardware = true; vm.ScanCommand.Execute(null); await UntilIdle(vm);
                    Assert.Equal("Fatal processor cache error", vm.Selected?.Title);
                    Capture(window, "processor-detail-dark", 1280, 840);
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
