using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using System.Text.Json;
using LogLens.Core;

namespace LogLens.App;

public interface IDesktopActions
{
    void Preview(DiagnosticReport report);
    void Copy(string text);
    void OpenReliability();
    void OpenEventViewer();
    void ReopenElevated();
    void OpenLink(string name);
    void Investigate(Incident? incident) { }
    ReportSelection? SelectReportOptions(IReadOnlyList<InvestigationEntry> history) => new(false, false);
    string? RequestFeedback() => null;
}
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ScanService scanner;
    private readonly IDesktopActions desktop;
    private readonly ReportBuilder reportBuilder = new(new([Environment.UserName, Environment.MachineName, Environment.UserDomainName, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)]));
    private CancellationTokenSource? cancellation;
    private ScanResult? result;
    private bool busy;
    private Incident? selected;
    private string search = "", category = "All incidents", status = "Ready when you are. Your diagnostic data stays on this PC.", theme;
    private int days;
    private int viewDays = 30;
    private readonly InvestigationStore historyStore;
    public MainViewModel(ScanService scanner, IDesktopActions desktop, AppSettings settings, InvestigationStore? historyStore = null)
    {
        this.scanner = scanner; this.desktop = desktop; days = settings.Days; theme = settings.Theme;
        this.historyStore = historyStore ?? new(InvestigationViewModel.DefaultPath);
        ScanCommand = new(ScanAsync, Error, () => !IsBusy);
        CancelCommand = new(() => { cancellation?.Cancel(); Status = "Cancelling…"; }, () => IsBusy && cancellation != null);
        PreviewCommand = new(PreviewAsync, Error, () => result != null && !IsBusy);
        CopyCommand = new(() =>
        {
            if (Selected == null || result == null) return;
            var report = reportBuilder.Build(result with { Incidents = [Selected] }, Version, RuntimeInformation.OSDescription, DateTimeOffset.Now);
            TryAction(() => desktop.Copy(report.PlainText));
        }, () => Selected != null && !IsBusy);
        ReliabilityCommand = new(() => TryAction(desktop.OpenReliability));
        EventViewerCommand = new(() => TryAction(desktop.OpenEventViewer));
        ElevateCommand = new(() => TryAction(desktop.ReopenElevated), () => HasAccessDenied && !IsBusy);
        RepositoryCommand = new(() => TryAction(() => desktop.OpenLink("repository")));
        IssuesCommand = new(() => TryAction(() => desktop.OpenLink("issues")));
        PrivacyCommand = new(() => TryAction(() => desktop.OpenLink("privacy")));
        InvestigateCommand = new(() => TryAction(() => desktop.Investigate(Selected)), () => Selected != null && !IsBusy);
        HistoryCommand = new(() => TryAction(() => desktop.Investigate(null)), () => !IsBusy);
        FeedbackCommand = new(() => TryAction(() =>
        {
            if (Selected == null || result == null) return;
            var expected = desktop.RequestFeedback();
            if (expected != null) desktop.Preview(reportBuilder.Feedback(result, Selected, Version, RuntimeInformation.OSDescription, expected));
        }), () => Selected != null && !IsBusy);
    }
    public const string Version = "0.1.0";
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Incident> Incidents { get; } = [];
    public int[] Durations { get; } = [1, 7, 30];
    public string[] Themes { get; } = ["Dark", "Light"];
    public string[] Categories { get; } = ["All incidents", "Unexpected restart", "Application failure", "Hardware", "Storage", "Display", "Device driver", "Network", "Service", "Windows Update", "Boot"];
    public int ViewDays { get => viewDays; set { if (value is not (1 or 7 or 30)) return; viewDays = value; Notify(); Filter(); } }
    public RelayCommand InvestigateCommand { get; }
    public RelayCommand HistoryCommand { get; }
    public RelayCommand FeedbackCommand { get; }
    public AsyncCommand ScanCommand { get; }
    public AsyncCommand PreviewCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand ReliabilityCommand { get; }
    public RelayCommand EventViewerCommand { get; }
    public RelayCommand ElevateCommand { get; }
    public RelayCommand RepositoryCommand { get; }
    public RelayCommand IssuesCommand { get; }
    public RelayCommand PrivacyCommand { get; }
    public bool IsBusy { get => busy; private set { busy = value; Notify(); Notify(nameof(IsIdle)); RefreshCommands(); } }
    public bool IsIdle => !IsBusy;
    public int Days { get => days; set { if (value is not (1 or 7 or 30)) return; days = value; Notify(); SaveSettings(); } }
    public string Theme { get => theme; set { if (value is not ("Dark" or "Light")) return; theme = value; App.ApplyTheme(value); Notify(); SaveSettings(); } }
    public string Search { get => search; set { search = value; Notify(); Filter(); } }
    public string Category { get => category; set { category = value; Notify(); Filter(); } }
    public string Status { get => status; private set { status = value; Notify(); } }
    public Incident? Selected { get => selected; set { selected = value; Notify(); Notify(nameof(HasSelection)); CopyCommand?.Refresh(); InvestigateCommand?.Refresh(); FeedbackCommand?.Refresh(); } }
    public bool HasSelection => Selected != null;
    public bool HasAccessDenied => result?.Issues.Any(i => i.Code == "access-denied") == true;
    public string IncidentCount => result?.Incidents.Count.ToString("N0") ?? "—";
    public string LastScan => result is null ? "Not scanned yet" : $"Last scan {result.CompletedAt.ToLocalTime():g} · {result.Period.Start.ToLocalTime():g} – {result.Period.End.ToLocalTime():g}";
    public string LastRestart => result?.Incidents.FirstOrDefault(i => i.Category == IncidentCategory.UnexpectedRestart)?.LocalTime ?? "None recorded in this scan";
    public string FrequentApp => result?.Incidents.Where(i => i.Application.Length > 0).GroupBy(i => i.Application, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Select(g => $"{g.Key} ({g.Count()})").FirstOrDefault() ?? "None identified";
    public string Summary => result is null ? "Understand why your Windows PC keeps crashing." : result.Issues.Count > 0 ? "Partial scan — review the collection limitations below before drawing conclusions." : result.Incidents.Count == 0 ? "No matching incidents were found. Windows logs cannot rule out every problem." : "Select an incident to separate recorded facts from possible explanations.";
    public string Limitations => result is null ? "Read-only · No uploads · No account needed" : string.Join("\n", result.Issues.Select(i => $"{i.Channel}: {i.Message}"));
    public string EmptyMessage => result is null ? "Start with Scan My PC.\nWe’ll look for recent restarts, application failures and hardware warnings." : Incidents.Count == 0 ? "No incidents match this view.\nTry another filter or scan period. Missing records do not prove the PC is healthy." : "";
    private async Task ScanAsync()
    {
        cancellation = new(); IsBusy = true;
        try
        {
            var end = DateTimeOffset.UtcNow;
            var next = await scanner.ScanAsync(new(end.AddDays(-Days), end), new Progress<string>(s => Status = s), cancellation.Token);
            result = next; Filter();
            foreach (var property in new[] { nameof(IncidentCount), nameof(LastScan), nameof(LastRestart), nameof(FrequentApp), nameof(Summary), nameof(Limitations), nameof(HasAccessDenied) }) Notify(property);
            Status = $"Scan complete · {next.EventCount:N0} relevant records · {next.Incidents.Count:N0} incidents";
        }
        catch (OperationCanceledException) { Status = "Scan cancelled. Any previous completed scan is still shown."; }
        finally { IsBusy = false; cancellation.Dispose(); cancellation = null; }
    }
    private async Task PreviewAsync()
    {
        if (result == null) return;
        var snapshot = result;
        InvestigationEntry[] history;
        try { history = historyStore.Load().Where(h => snapshot.Incidents.Any(i => i.Id == h.IncidentId && i.Category == h.Category)).ToArray(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { Status = "History could not be read. Open Local history to review the problem; a report without history is still available."; history = []; }
        var selection = desktop.SelectReportOptions(history);
        if (selection == null) return;
        IsBusy = true; Status = "Preparing a minimized, redacted report…";
        try
        {
            var report = await Task.Run(() => reportBuilder.Build(snapshot, Version, RuntimeInformation.OSDescription, DateTimeOffset.Now,
                selection.IncludeHistory ? history : null, selection.IncludeReviewedNotes));
            desktop.Preview(report); Status = "Report preview closed. No report is uploaded by LogLens.";
        }
        finally { IsBusy = false; }
    }
    private void Filter()
    {
        var oldId = Selected?.Id; Incidents.Clear();
        if (result != null)
            foreach (var incident in result.Incidents.Where(i => i.Time >= result.Period.End.AddDays(-ViewDays) && (Category == "All incidents" || i.CategoryLabel == Category) &&
                (Search.Length == 0 || i.Title.Contains(Search, StringComparison.OrdinalIgnoreCase) || i.Explanation.Contains(Search, StringComparison.OrdinalIgnoreCase) || i.Evidence.Any(e => e.Provider.Contains(Search, StringComparison.OrdinalIgnoreCase) || e.EventId.ToString().Contains(Search, StringComparison.OrdinalIgnoreCase))))) Incidents.Add(incident);
        Selected = Incidents.FirstOrDefault(i => i.Id == oldId) ?? Incidents.FirstOrDefault(); Notify(nameof(EmptyMessage));
    }
    private void RefreshCommands() { ScanCommand.Refresh(); PreviewCommand.Refresh(); CancelCommand.Refresh(); CopyCommand.Refresh(); ElevateCommand.Refresh(); InvestigateCommand.Refresh(); HistoryCommand.Refresh(); FeedbackCommand.Refresh(); }
    private void SaveSettings() { if (!new AppSettings(Days, Theme).Save()) Status = "Settings apply for this session, but Windows could not save them."; }
    private void TryAction(Action action) { try { action(); } catch (Exception ex) { Error(ex); } }
    private void Error(Exception _) => Status = "LogLens could not complete that action. Check file permissions or Windows access, then try again. No system settings were changed.";
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public void Dispose() => cancellation?.Cancel();
}
