using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LogLens.Core;

namespace LogLens.App;

public sealed class InvestigationViewModel : INotifyPropertyChanged
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Goodwin Labs", "LogLens", "investigations.json");
    private readonly InvestigationStore store;
    private readonly Incident? incident;
    private readonly Func<bool> confirmClear;
    private List<InvestigationEntry> all = [];
    private InvestigationEntry? selected;
    private bool available, showAll;
    private string notes = "", outcome = "Inconclusive", performedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), status = "History is stored only on this PC. Notes may contain private information.";
    public InvestigationViewModel(Incident? incident, InvestigationStore store, Func<bool> confirmClear)
    {
        this.incident = incident; this.store = store; this.confirmClear = confirmClear; showAll = incident == null;
        SaveCommand = new(Save, () => available && (Selected != null || Plan.CanRecord));
        NewCommand = new(() => Selected = null, () => incident != null);
        DeleteCommand = new(Delete, () => available && Selected != null);
        ClearCommand = new(Clear);
        try { all = store.Load().ToList(); available = true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { Status = "History could not be read. It has been preserved. Clear history explicitly to start again, or restore the local file from a backup."; }
        Refresh();
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<InvestigationEntry> History { get; } = [];
    public string[] Outcomes { get; } = Enum.GetValues<InvestigationOutcome>().Select(InvestigationEntry.OutcomeLabel).ToArray();
    public string Title => incident?.Title ?? "Local investigation history";
    public string Known => incident?.Known ?? "Saved actions are user reports, not confirmed Windows observations.";
    public string Unknowns => incident?.Unknowns ?? "Similar symptoms can have different causes. Outcomes only influence the exact associated incident.";
    public InvestigationPlan Plan => incident == null ? new(null, "Select an incident on the dashboard to start an investigation.", "History can be edited or deleted here.", "No system changes are made.", "", "") : new InvestigationEngine().Next(incident, all);
    public bool ShowAll { get => showAll; set { showAll = value; Notify(); Refresh(); } }
    public string Notes { get => notes; set { notes = value; Notify(); } }
    public string Outcome { get => outcome; set { outcome = value; Notify(); } }
    public string PerformedDate { get => performedDate; set { performedDate = value; Notify(); } }
    public string Status { get => status; private set { status = value; Notify(); } }
    public string EditorTitle => Selected == null ? "Record the current step" : "Edit the selected history entry";
    public string EditingStep => Selected?.Step ?? Plan.Action;
    public InvestigationEntry? Selected
    {
        get => selected;
        set { selected = value; Notes = value?.Notes ?? ""; Outcome = InvestigationEntry.OutcomeLabel(value?.Outcome ?? InvestigationOutcome.Inconclusive); PerformedDate = (value?.PerformedAt.ToLocalTime() ?? DateTimeOffset.Now).ToString("yyyy-MM-dd HH:mm"); Notify(); Notify(nameof(EditorTitle)); Notify(nameof(EditingStep)); SaveCommand.Refresh(); DeleteCommand.Refresh(); }
    }
    public RelayCommand SaveCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ClearCommand { get; }
    private void Save()
    {
        if (!DateTimeOffset.TryParseExact(PerformedDate, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var date) || date > DateTimeOffset.Now.AddMinutes(1))
        { Status = "Enter a valid local date and time (yyyy-MM-dd HH:mm), not in the future."; return; }
        var index = Array.IndexOf(Outcomes, Outcome);
        if (index < 0 || Notes.Length > 2000) { Status = "Choose an outcome and keep notes to 2,000 characters."; return; }
        var entry = Selected != null ? Selected with { Notes = Notes, Outcome = (InvestigationOutcome)index, PerformedAt = date } :
            new(Guid.NewGuid(), incident!.Id, incident.Category, Plan.StepId!, Plan.Action, date, (InvestigationOutcome)index, Notes);
        var next = all.Where(e => e.Id != entry.Id).Append(entry).ToList();
        Persist(next, "Outcome saved locally. The recommended next action has been updated.");
    }
    private void Delete() { if (Selected != null) Persist(all.Where(e => e.Id != Selected.Id).ToList(), "Entry deleted. Its step may now be recommended again."); }
    private void Clear()
    {
        if (!confirmClear()) return;
        try { store.Clear(); all.Clear(); available = true; Selected = null; Refresh(); Status = "All local investigation history was deleted."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status = "Windows could not delete the history file. No changes were made in this window."; }
    }
    private void Persist(List<InvestigationEntry> next, string message)
    {
        try { store.Save(next); all = next; Selected = null; Refresh(); Status = message; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { Status = "History could not be saved. Check file access and the 500-entry limit. The previous history is preserved."; }
    }
    private void Refresh()
    {
        History.Clear();
        foreach (var entry in all.Where(e => ShowAll || (e.IncidentId == incident?.Id && e.Category == incident.Category)).OrderByDescending(e => e.PerformedAt)) History.Add(entry);
        Notify(nameof(Plan)); Notify(nameof(EditingStep)); SaveCommand.Refresh(); DeleteCommand.Refresh();
    }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
