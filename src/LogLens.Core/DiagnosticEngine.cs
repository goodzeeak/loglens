using System.Security.Cryptography;
using System.Text;

namespace LogLens.Core;

public sealed class DiagnosticEngine
{
    public const int MaximumEvents = 10000;
    public IReadOnlyList<Incident> Analyze(IEnumerable<DiagnosticEvent> input, ScanPeriod period, CancellationToken cancellationToken = default)
    {
        period.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var events = input.Take(MaximumEvents + 1).ToArray();
        if (events.Length > MaximumEvents) throw new ArgumentException("Event limit exceeded.", nameof(input));
        var ordered = events.Where(e => e.Time >= period.Start && e.Time <= period.End && EventRules.Category(e) != null)
            .DistinctBy(Key).OrderBy(e => e.Time).ThenBy(Key, StringComparer.Ordinal).ToList();
        var groups = new List<List<DiagnosticEvent>>();
        foreach (var e in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = EventRules.Category(e)!.Value;
            // Only pair complementary restart/app records. Repeated same-provider events stay separate.
            var group = groups.LastOrDefault(g => EventRules.Category(g[0]) == category && CanJoin(g, e, category));
            if (group == null) groups.Add([e]); else group.Add(e);
        }
        return groups.Select(g => Build(g, ordered)).OrderByDescending(i => i.Time).ThenBy(i => i.Id, StringComparer.Ordinal).ToArray();
    }
    private static bool CanJoin(List<DiagnosticEvent> group, DiagnosticEvent e, IncidentCategory category)
    {
        var first = group[0];
        var seconds = (e.Time - first.Time).TotalSeconds;
        if (category == IncidentCategory.UnexpectedRestart)
            return seconds <= 120 && !group.Any(x => x.Provider.Equals(e.Provider, StringComparison.OrdinalIgnoreCase));
        if (category == IncidentCategory.ApplicationCrash)
        {
            if (seconds > 120 || group.Any(x => x.Provider.Equals(e.Provider, StringComparison.OrdinalIgnoreCase))) return false;
            var a = EventRules.ReportId(first); var b = EventRules.ReportId(e);
            if (Guid.TryParse(a, out var aid) && aid != Guid.Empty && Guid.TryParse(b, out var bid) && bid != Guid.Empty) return aid == bid;
            return seconds <= 30 && EventRules.App(first).Length > 0 && EventRules.Module(first).Length > 0 &&
                EventRules.App(first).Equals(EventRules.App(e), StringComparison.OrdinalIgnoreCase) &&
                EventRules.Module(first).Equals(EventRules.Module(e), StringComparison.OrdinalIgnoreCase);
        }
        // Device identity is required before aggregating noisy hardware/storage/display records.
        var device = first.Field("DeviceName");
        return seconds <= 60 && device.Length > 0 && device == e.Field("DeviceName") && first.Provider == e.Provider && first.EventId == e.EventId;
    }
    private static Incident Build(List<DiagnosticEvent> evidence, List<DiagnosticEvent> all)
    {
        var first = evidence[0]; var category = EventRules.Category(first)!.Value;
        var findings = new List<Finding>(); var steps = new List<Recommendation>();
        var context = new List<DiagnosticEvent>();
        string title; var severity = Severity.Warning; var app = "";
        void Fact(string code, string text) => findings.Add(new(code, EvidenceClass.ConfirmedObservation, text));
        void Maybe(string code, string text) => findings.Add(new(code, EvidenceClass.PossibleCause, text));
        void Unknown(string text) => findings.Add(new("unknown", EvidenceClass.InsufficientEvidence, text));
        void Step(string code, string text) => steps.Add(new(code, text));
        switch (category)
        {
            case IncidentCategory.UnexpectedRestart:
                title = "Unexpected restart recorded"; severity = Severity.Critical;
                Fact("restart", "Windows recorded an unexpected shutdown or restart. The displayed time is the record time, often the next startup, not the exact failure time.");
                if (evidence.Any(e => EventRules.Is(e, "Microsoft-Windows-Kernel-Power", 41)))
                    Fact("kernel-power", "Kernel-Power 41 indicates Windows did not shut down cleanly. It does not identify the root cause.");
                if (evidence.Any(EventRules.IsBugCheck) || evidence.Any(e => EventRules.Is(e, "Microsoft-Windows-Kernel-Power", 41) && PositiveCode(e.Field("BugcheckCode"))))
                {
                    Fact("bugcheck", "Windows recorded a bug check (Stop error). This does not identify a faulty driver or component.");
                    Maybe("stop-cause", "A driver or hardware stability problem could be involved; dump analysis is needed to investigate.");
                    Step("dump", "If a crash dump is available in Windows\\Minidump or MEMORY.DMP, inspect it with Microsoft WinDbg or ask a trusted technician. Dumps can contain private data.");
                }
                context.AddRange(all.Where(e => EventRules.Category(e) is IncidentCategory.Hardware or IncidentCategory.Storage or IncidentCategory.Display &&
                    e.Time <= first.Time && first.Time - e.Time <= TimeSpan.FromMinutes(5)).TakeLast(20));
                if (context.Count > 0) Fact("nearby", "Hardware, storage or display records were logged nearby and are shown as context. Timing alone does not establish a cause or prove they occurred before the actual shutdown.");
                Unknown("These records cannot establish the root cause. Power loss, a forced restart and other failures can leave similar records. Complementary restart records within two minutes are grouped heuristically.");
                Step("reliability", "Open Windows Reliability Monitor and compare the recorded time with what you were doing. Note any recent changes.");
                Step("temperature", "If restarts happen under load, review temperatures using your hardware vendor's tools and check ventilation.");
                break;
            case IncidentCategory.ApplicationCrash:
                app = evidence.Select(EventRules.App).FirstOrDefault(s => s.Length > 0) ?? "";
                title = app.Length > 0 ? $"Application failure: {app}" : "Application failure recorded"; severity = Severity.Error;
                Fact("app-failure", evidence.Any(e => EventRules.Is(e, "Application Hang", 1002)) ? "Windows recorded an application hang or termination." : "Windows recorded an application crash report.");
                foreach (var module in evidence.Select(EventRules.Module).Where(s => s.Length > 0).Distinct())
                    Fact("module", $"The recorded faulting module is {module}. Its presence does not prove that module caused the failure.");
                Maybe("app-cause", "An application defect, plug-in or compatibility issue could be involved.");
                Unknown("A faulting module is where the failure was observed; these records alone do not identify the originating defect.");
                Step("app-update", "Check for an application update and review recently added plug-ins or overlays. Change one thing at a time and retest.");
                Step("reliability", "Compare this failure with Windows Reliability Monitor and the application's own support information.");
                break;
            case IncidentCategory.Hardware:
                title = "Hardware error report";
                Fact("whea", "Windows Hardware Error Architecture (WHEA) recorded a hardware error report. Corrected reports do not necessarily imply a crash.");
                Maybe("hardware-cause", "Hardware, firmware, voltage or tuning instability could be involved. The affected component is not proven defective.");
                Unknown("LogLens does not decode WHEA binary error records or establish a defective component.");
                Step("stock", "If you use CPU, GPU or RAM overclocks or undervolts, return them to stock settings for controlled testing using vendor instructions.");
                Step("memory", "If memory instability is suspected, save your work and run Windows Memory Diagnostic. A passing test does not rule out all memory faults.");
                Step("temperature", "Review hardware temperatures and vendor firmware guidance, especially if these reports recur.");
                break;
            case IncidentCategory.Storage:
                title = "Storage problem recorded"; severity = Severity.Error;
                Fact("storage", "Windows recorded a disk, controller or filesystem problem. This does not by itself establish a system crash or a failed drive.");
                Maybe("storage-cause", "A connection, controller, device, firmware or filesystem issue could be involved.");
                Unknown("These records do not distinguish all storage failure mechanisms.");
                Step("backup", "Back up important files before investigating recurring storage errors.");
                Step("storage-check", "Review drive health using the manufacturer's diagnostic tool. If errors recur, ask a technician to inspect connections and controller or firmware history.");
                break;
            default:
                title = "Display driver recovered";
                Fact("display", "Windows recorded a display-driver timeout and recovery (Display 4101).");
                Maybe("display-cause", "Driver instability, graphics workload or hardware tuning could be involved; a defective GPU is not established.");
                Unknown("A recovered display timeout does not establish a whole-system crash.");
                Step("driver", "If this began after a graphics-driver change, consider the vendor's supported rollback; otherwise check for a stable vendor driver update.");
                Step("stock", "If graphics tuning or an overclock is active, test at stock settings and review GPU temperatures.");
                break;
        }
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", evidence.Select(Key)))))[..16];
        return new(id, category, severity, first.Time, title, evidence.ToArray(), context, findings, steps, app);
    }
    private static bool PositiveCode(string text) => text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? ulong.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hex) && hex > 0
        : ulong.TryParse(text, out var number) && number > 0;
    private static string Key(DiagnosticEvent e) => e.RecordId is > 0 ? $"{e.Channel.ToUpperInvariant()}:{e.Provider.ToUpperInvariant()}:{e.RecordId}" :
        $"{e.Channel}:{e.Provider}:{e.EventId}:{e.Time.UtcTicks}:" + string.Join("|", e.Fields.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
}
