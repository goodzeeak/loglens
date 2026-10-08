namespace LogLens.Core;

internal sealed class HardwareModule : DiagnosticModule
{
    public override IncidentCategory Category => IncidentCategory.Hardware;
    public override IReadOnlyList<EventSource> Sources { get; } = [new("System", "Microsoft-Windows-WHEA-Logger", [1, 17, 18, 19, 20, 46, 47])];
    public override bool Matches(DiagnosticEvent e) => base.Matches(e) && CperDecoder.Decode(e.Field("RawData"))?.Severity != "Informational";
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
        title = "Hardware error report";
                severity = evidence.Any(e => e.Level == 1) ? Severity.Critical : evidence.Any(e => e.Level == 2) ? Severity.Error : Severity.Warning;
                var decoded = evidence.Select(e => CperDecoder.Decode(e.Field("RawData"))).FirstOrDefault(c => c != null);
                if (decoded != null)
                {
                    Fact("cper-severity", $"The WHEA/CPER record classifies this hardware error as {decoded.Severity.ToLowerInvariant()}. This is the severity recorded by the hardware/firmware reporting path.");
                    Fact("cper-sections", $"The record contains: {string.Join(", ", decoded.Sections)}. A section's presence alone does not identify a failed replaceable part.");
                    severity = decoded.Severity == "Fatal" ? Severity.Critical : decoded.Severity == "Recoverable" ? Severity.Error : Severity.Warning;
                    title = $"{decoded.Severity} hardware error report";
                }
                if (decoded?.Processor is { } processor)
                {
                    title = $"{processor.Severity} processor {processor.Kind} error";
                    Fact("processor-error", $"The validated processor section reports a {processor.Kind} error ({processor.Severity.ToLowerInvariant()})." +
                        (processor.ProcessorId is { } processorId ? $" Logical processor/APIC identifier: {processorId}." : "") +
                        (processor.CacheLevel is { } level ? $" Reported hierarchy level: {level} (as encoded by the platform)." : ""));
                    Maybe("processor-cause", "The investigation can focus on processor/cache stability and its supporting firmware, voltage and tuning. The error category is established; the failing physical part is not.");
                    Unknown("The record does not distinguish CPU silicon failure from voltage/tuning, firmware or supporting-board problems. It does not by itself establish that this record and a nearby restart describe the same failure.");
                    Step("cpu-stock", "First, if CPU overclocking, undervolting or Curve Optimizer tuning is enabled, test at vendor stock settings. Record whether the same processor error recurs before changing anything else.");
                    Step("firmware", "Check the motherboard/PC vendor's BIOS and chipset release notes for processor-stability fixes. Follow the vendor's instructions if you choose an update; do not change settings blindly.");
                }
                else
                {
                    Fact("whea", "Windows Hardware Error Architecture (WHEA) recorded a hardware error report. Corrected reports do not necessarily imply a crash.");
                    Maybe("hardware-cause", "Hardware, firmware, voltage or tuning instability could be involved. The affected component is not proven defective.");
                    Unknown("No supported, validated processor-error details are available in this record. Vendor-specific MCA details and crash dumps are not decoded.");
                    Step("stock", "If you use CPU, GPU or RAM overclocks or undervolts, return them to stock settings for controlled testing using vendor instructions.");
                    Step("memory", "If memory instability is suspected, save your work and run Windows Memory Diagnostic. A passing test does not rule out all memory faults.");
                }
                Step("temperature", "Review hardware temperatures and vendor firmware guidance, especially if these reports recur.");
        findings.InsertRange(0, SpecificFindings.For(evidence));
        return new("", Category, severity, first.Time, title, evidence.ToArray(), context, findings, steps, app);
    }
}
