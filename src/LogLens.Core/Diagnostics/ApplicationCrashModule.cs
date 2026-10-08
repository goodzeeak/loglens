namespace LogLens.Core;

internal sealed class ApplicationCrashModule : DiagnosticModule
{
    public override TimeSpan CorrelationWindow => TimeSpan.FromSeconds(120);
    public override bool CanJoin(List<DiagnosticEvent> group, DiagnosticEvent next)
    {
        var first = group[0]; var seconds = (next.Time - first.Time).TotalSeconds;
        if (seconds > 120 || group.Any(e => e.Provider.Equals(next.Provider, StringComparison.OrdinalIgnoreCase))) return false;
        if (Guid.TryParse(EventRules.ReportId(first), out var a) && a != Guid.Empty && Guid.TryParse(EventRules.ReportId(next), out var b) && b != Guid.Empty) return a == b;
        return seconds <= 30 && EventRules.App(first).Length > 0 && EventRules.Module(first).Length > 0 &&
            EventRules.App(first).Equals(EventRules.App(next), StringComparison.OrdinalIgnoreCase) && EventRules.Module(first).Equals(EventRules.Module(next), StringComparison.OrdinalIgnoreCase);
    }
    public override IncidentCategory Category => IncidentCategory.ApplicationCrash;
    public override IReadOnlyList<EventSource> Sources { get; } = [new("Application", "Application Error", [1000]), new("Application", "Application Hang", [1002]), new("Application", "Windows Error Reporting", [1001])];
    public override bool Matches(DiagnosticEvent e) => base.Matches(e) && (!e.Provider.Equals("Windows Error Reporting", StringComparison.OrdinalIgnoreCase) || new[] { "APPCRASH", "BEX", "BEX64", "AppHangB1", "MoAppCrash" }.Contains(e.Field("EventName"), StringComparer.OrdinalIgnoreCase));
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
        app = evidence.Select(EventRules.App).FirstOrDefault(s => s.Length > 0) ?? "";
                title = app.Length > 0 ? $"Application failure: {app}" : "Application failure recorded"; severity = Severity.Error;
                Fact("app-failure", evidence.Any(e => EventRules.Is(e, "Application Hang", 1002)) ? "Windows recorded an application hang or termination." : "Windows recorded an application crash report.");
                foreach (var module in evidence.Select(EventRules.Module).Where(s => s.Length > 0).Distinct())
                    Fact("module", $"The recorded faulting module is {module}. Its presence does not prove that module caused the failure.");
                Maybe("app-cause", "An application defect, plug-in or compatibility issue could be involved.");
                Unknown("A faulting module is where the failure was observed; these records alone do not identify the originating defect.");
                Step("app-update", "Check for an application update and review recently added plug-ins or overlays. Change one thing at a time and retest.");
                Step("reliability", "Compare this failure with Windows Reliability Monitor and the application's own support information.");
        findings.InsertRange(0, SpecificFindings.For(evidence));
        return new("", Category, severity, first.Time, title, evidence.ToArray(), context, findings, steps, app);
    }
}
