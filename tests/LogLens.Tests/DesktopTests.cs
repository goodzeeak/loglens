using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows;
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
                    vm.Category = "Storage"; Assert.Empty(vm.Incidents);
                    vm.Category = "All incidents"; Assert.Single(vm.Incidents);
                    vm.CopyCommand.Execute(null); Assert.Contains("Confirmed observation", actions.Copied);
                    vm.PreviewCommand.Execute(null); await UntilIdle(vm);
                    Assert.NotNull(actions.Report);
                    var preview = new ReportWindow(actions.Report);
                    preview.Measure(new(900, 760)); preview.Arrange(new(0, 0, 900, 760)); preview.UpdateLayout();
                    Assert.Same(application.Resources["CanvasBrush"], preview.Background);
                    App.App.ApplyTheme("Light"); window.UpdateLayout();
                    Assert.Same(application.Resources["CanvasBrush"], window.Background);
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
    private sealed class FakeCollector : IEventCollector
    {
        public bool WaitForCancellation { get; set; }
        public async Task<CollectionResult> CollectAsync(ScanPeriod period, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new([AccuracyFixtures.K() with { Time = period.End.AddSeconds(-10) }], []);
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
