namespace LogLens.Core;

internal sealed class UnexpectedRestartModule : DiagnosticModule
{
    public override TimeSpan CorrelationWindow => TimeSpan.FromSeconds(120);
    public override bool CanJoin(List<DiagnosticEvent> group, DiagnosticEvent next) =>
        (next.Time - group[0].Time).TotalSeconds <= 120 && !group.Any(e => e.Provider.Equals(next.Provider, StringComparison.OrdinalIgnoreCase));
    public override IncidentCategory Category => IncidentCategory.UnexpectedRestart;
    public override IReadOnlyList<EventSource> Sources { get; } = [new("System", "Microsoft-Windows-Kernel-Power", [41]), new("System", "EventLog", [6008]), new("System", "Microsoft-Windows-WER-SystemErrorReporting", [1001]), new("System", "BugCheck", [1001])];
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
        title = "Unexpected restart recorded"; severity = Severity.Critical;
                Fact("restart", "Windows recorded an unexpected shutdown or restart. The displayed time is the record time, often the next startup, not the exact failure time.");
                if (evidence.Any(e => EventRules.Is(e, "Microsoft-Windows-Kernel-Power", 41)))
                    Fact("kernel-power", "Kernel-Power 41 indicates Windows did not shut down cleanly. It does not identify the root cause.");
                if (evidence.Any(EventRules.IsBugCheck) || evidence.Any(e => EventRules.Is(e, "Microsoft-Windows-Kernel-Power", 41) && SpecificFindings.StopCode(e) is > 0))
                {
                    Fact("bugcheck", "Windows recorded a bug check (Stop error). This does not identify a faulty driver or component.");
                    Maybe("stop-cause", "A driver or hardware stability problem could be involved; dump analysis is needed to investigate.");
                    Step("dump", "If a crash dump is available in Windows\\Minidump or MEMORY.DMP, inspect it with Microsoft WinDbg or ask a trusted technician. Dumps can contain private data.");
                }
                if (evidence.Any(e => SpecificFindings.StopCode(e) == 0x9F)) Step("power-driver", "Focus on drivers involved in sleep, wake or device power changes. Compare recent chipset, network, USB or storage driver changes; a dump can identify the stalled power request.");
                if (evidence.Any(e => SpecificFindings.StopCode(e) == 0x116)) Step("graphics", "Focus on the graphics driver and GPU workload. Compare driver changes, test stock GPU settings and inspect the dump for the named display driver.");
                context.AddRange(all.Where(e => e.Time <= first.Time && first.Time - e.Time <= TimeSpan.FromMinutes(5) &&
                    EventRules.Category(e) is IncidentCategory.Hardware or IncidentCategory.Storage or IncidentCategory.Display).TakeLast(20));
                if (context.Count > 0) Fact("nearby", "Hardware, storage or display records were logged nearby and are shown as context. Timing alone does not establish a cause or prove they occurred before the actual shutdown.");
                Unknown("These records cannot establish the root cause. Power loss, a forced restart and other failures can leave similar records. Complementary restart records within two minutes are grouped heuristically.");
                Step("reliability", "Open Windows Reliability Monitor and compare the recorded time with what you were doing. Note any recent changes.");
                Step("temperature", "If restarts happen under load, review temperatures using your hardware vendor's tools and check ventilation.");
        findings.InsertRange(0, SpecificFindings.For(evidence));
        return new("", Category, severity, first.Time, title, evidence.ToArray(), context, findings, steps, app);
    }
}
