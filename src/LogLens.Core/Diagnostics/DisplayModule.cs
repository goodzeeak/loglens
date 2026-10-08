namespace LogLens.Core;

internal sealed class DisplayModule : DiagnosticModule
{
    public override IncidentCategory Category => IncidentCategory.Display;
    public override IReadOnlyList<EventSource> Sources { get; } = [new("System", "Display", [4101])];
    public override bool Matches(DiagnosticEvent e) => base.Matches(e);
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
        title = "Display driver recovered";
                Fact("display", "Windows recorded a display-driver timeout and recovery (Display 4101).");
                Maybe("display-cause", "Driver instability, graphics workload or hardware tuning could be involved; a defective GPU is not established.");
                Unknown("A recovered display timeout does not establish a whole-system crash.");
                Step("driver", "If this began after a graphics-driver change, consider the vendor's supported rollback; otherwise check for a stable vendor driver update.");
                Step("stock", "If graphics tuning or an overclock is active, test at stock settings and review GPU temperatures.");
        findings.InsertRange(0, SpecificFindings.For(evidence));
        return new("", Category, severity, first.Time, title, evidence.ToArray(), context, findings, steps, app);
    }
}
