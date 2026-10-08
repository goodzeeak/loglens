namespace LogLens.Core;

internal sealed class StorageModule : DiagnosticModule
{
    public override IncidentCategory Category => IncidentCategory.Storage;
    public override IReadOnlyList<EventSource> Sources { get; } = [new("System", "disk", [7, 11, 15, 51, 153, 157]), new("System", "storahci", [129]), new("System", "stornvme", [129]), new("System", "iaStorA", [129]), new("System", "iaStorAC", [129]), new("System", "Ntfs", [55, 98, 140]), new("System", "Microsoft-Windows-Ntfs", [55, 98, 140])];
    public override bool Matches(DiagnosticEvent e) => base.Matches(e) && (e.EventId != 98 || (e.Level is >= 1 and <= 3 && uint.TryParse(e.Field("CorruptionActionState"), out var action) && action > 0));
    public override Incident Build(List<DiagnosticEvent> evidence, List<DiagnosticEvent> all)
    {
        var first = evidence[0];
        var findings = new List<Finding>(); var steps = new List<Recommendation>();
        var context = new List<DiagnosticEvent>();
        string title; var severity = Severity.Warning; var app = "";
        void Fact(string code, string text) => findings.Add(new(code, EvidenceClass.ConfirmedObservation, text));
        void Maybe(string code, string text) => findings.Add(new(code, EvidenceClass.PossibleCause, text));
        void Unknown(string text) => findings.Add(new("unknown", EvidenceClass.InsufficientEvidence, text));
        void Step(string code, string text) => steps.Add(new(code, text));
        title = "Storage problem recorded"; severity = Severity.Error;
                Fact("storage", "Windows recorded a disk, controller or filesystem problem. This does not by itself establish a system crash or a failed drive.");
                Maybe("storage-cause", "A connection, controller, device, firmware or filesystem issue could be involved.");
                Unknown("These records do not distinguish all storage failure mechanisms.");
                Step("backup", "Back up important files before investigating recurring storage errors.");
                Step("storage-check", "Review drive health using the manufacturer's diagnostic tool. If errors recur, ask a technician to inspect connections and controller or firmware history.");
        findings.InsertRange(0, SpecificFindings.For(evidence));
        return new("", Category, severity, first.Time, title, evidence.ToArray(), context, findings, steps, app);
    }
}
