using System.Text.Json;

namespace LogLens.Core;

public enum InvestigationOutcome { IssueRecurred, IssueDidNotRecur, Inconclusive, Skipped }
public sealed record InvestigationEntry(Guid Id, string IncidentId, IncidentCategory Category, string StepId,
    string Step, DateTimeOffset PerformedAt, InvestigationOutcome Outcome, string Notes)
{
    public string Summary => $"{PerformedAt.ToLocalTime():g} · {OutcomeLabel(Outcome)} · {Step}";
    public static string OutcomeLabel(InvestigationOutcome value) => value switch
    { InvestigationOutcome.IssueRecurred => "Issue recurred", InvestigationOutcome.IssueDidNotRecur => "Issue did not recur", InvestigationOutcome.Inconclusive => "Inconclusive", _ => "Skipped" };
}

public sealed record InvestigationRule(string Id, IncidentCategory Category, string[] RequiredObservations, string[] ExcludedObservations,
    string Why, string Safety)
{
    public bool Applies(Incident incident) => incident.Category == Category && incident.Recommendations.Any(r => r.Code == Id) &&
        RequiredObservations.All(code => incident.Findings.Any(f => f.Code == code && f.Classification == EvidenceClass.ConfirmedObservation)) &&
        !ExcludedObservations.Any(code => incident.Findings.Any(f => f.Code == code));
}
public sealed record InvestigationPlan(string? StepId, string Action, string Why, string Safety, string Outcomes, string FollowUp)
{
    public bool CanRecord => StepId != null;
}
public sealed class InvestigationEngine
{
    public static IReadOnlyList<InvestigationRule> Rules { get; } = CreateRules();
    private const string Outcomes = "Issue recurred: the symptom remains unresolved; record when and under what conditions. Issue did not recur: keep observing under comparable conditions; this does not prove a permanent fix. Inconclusive: record what prevented a useful comparison. Skipped: record why the step was unsuitable; no test conclusion follows.";
    public InvestigationPlan Next(Incident incident, IEnumerable<InvestigationEntry> history)
    {
        var entries = history.Where(h => h.IncidentId == incident.Id && h.Category == incident.Category).OrderBy(h => h.PerformedAt).ThenBy(h => h.Id).ToArray();
        var last = entries.LastOrDefault();
        if (last?.Outcome == InvestigationOutcome.IssueDidNotRecur)
            return new(null, "Observe during comparable normal use before making another change.", "One non-recurrence is encouraging but does not establish a permanent fix, especially for intermittent faults.", "Do not deliberately provoke dangerous failures or stress failing storage.", Outcomes, "If the issue returns, edit the last outcome to Issue recurred. The next untried step will then be offered.");
        var rule = incident.Recommendations.Select(r => Rules.FirstOrDefault(rule => rule.Id == r.Code && rule.Applies(incident)))
            .FirstOrDefault(r => r != null && !entries.Any(h => h.StepId == r.Id));
        if (rule == null)
            return new(null, "Collect new evidence if the symptom recurs, or share a reviewed report with the appropriate vendor or a trusted technician.", "The supported steps have been recorded. Repeating them without new evidence may not narrow the cause.", "Protect important data and avoid changes outside vendor-supported procedures.", Outcomes, "Rescan after recurrence. A new incident is investigated separately; similar symptoms do not prove a shared cause.");
        return new(rule.Id, incident.Recommendations.First(r => r.Code == rule.Id).Text,
            (last?.Outcome == InvestigationOutcome.Inconclusive ? "The previous result was inconclusive, so it does not rule anything out. " : last?.Outcome == InvestigationOutcome.Skipped ? "The previous step was skipped; no diagnostic conclusion follows. " : "") + rule.Why,
            rule.Safety, Outcomes, "Record the outcome below. LogLens will offer observation or the next applicable untried step; it will not perform any system changes.");
    }
    private static InvestigationRule[] CreateRules()
    {
        var rules = new List<InvestigationRule>();
        void Add(IncidentCategory category, string observation, string why, string safety, params string[] ids)
        { foreach (var id in ids) rules.Add(new(id, category, [observation], [], why, safety)); }
        Add(IncidentCategory.UnexpectedRestart, "restart", "Compare the failure with its workload and recorded timing to narrow which evidence to investigate next.", "Save your work. Do not disable protections or deliberately interrupt power.", "reliability", "temperature");
        Add(IncidentCategory.UnexpectedRestart, "bugcheck", "The recorded Stop error makes crash-dump and driver evidence relevant; the event alone cannot identify the culprit.", "Dumps can contain private data. Use trusted tools and vendor-supported changes, one at a time.", "dump", "power-driver", "graphics");
        Add(IncidentCategory.ApplicationCrash, "app-failure", "Compare application versions and recent changes with the recorded application and faulting module. A named module may only be where the failure surfaced.", "Save documents first. Use official application support and avoid downloading replacement DLLs.", "app-update", "reliability");
        Add(IncidentCategory.Hardware, "processor-error", "The validated processor-error category supports a controlled investigation of processor stability and supporting firmware.", "Only change tuning if it was enabled and you understand how to restore it. Follow vendor instructions; firmware updates carry interruption risk.", "cpu-stock", "firmware", "temperature");
        Add(IncidentCategory.Hardware, "whea", "A controlled comparison can narrow a hardware-reported error without assuming a defective part.", "Save work before diagnostics. Use stock settings only through documented vendor procedures and avoid unsafe stress testing.", "stock", "memory", "temperature");
        Add(IncidentCategory.Storage, "storage", "Protect data first, then compare non-destructive device health evidence with the recorded I/O or filesystem failure.", "Do not format, initialize, run destructive surface tests or repair a failing drive before protecting important data. Seek help if it is unreadable.", "backup", "storage-check");
        Add(IncidentCategory.Display, "display", "Compare the display recovery with driver changes and graphics workload to test a relevant explanation.", "Save work; use official vendor drivers. Do not use driver-cleaner tools or change voltages as a first step.", "driver", "stock");
        Add(IncidentCategory.DeviceDriver, "driver-load", "Current device status can distinguish a transient startup warning from a persistent problem.", "Inspect before changing anything. Do not disable an unfamiliar device or a security component.", "device-status", "device-driver");
        Add(IncidentCategory.Network, "adapter-reset", "A comparison across time and devices helps isolate the adapter's recorded reset from a shared network problem.", "Do not reset network settings or replace drivers during work that requires connectivity.", "adapter-check", "network-compare");
        Add(IncidentCategory.Network, "dns-timeout", "Comparing names and devices helps distinguish a single failed lookup from a broader connectivity symptom.", "Do not disable firewalls or change DNS based on one historical event.", "dns-check", "network-compare");
        foreach (var code in new[] { "dhcp-address", "dhcp-lease" }) Add(IncidentCategory.Network, code, "Current address assignment and another device provide evidence about recovery and the local network.", "Do not assign an arbitrary static address or change a managed router's settings.", "dhcp-check", "network-compare");
        Add(IncidentCategory.Service, "service-failure", "Determine whether the service belongs to the affected feature before treating its failure as relevant.", "Do not change startup type, permissions or restart unfamiliar services.", "service-relevance", "service-details");
        Add(IncidentCategory.Update, "update-install", "Update history can show a later successful attempt; the earlier error alone does not establish a current failure.", "Save work before any restart. Use supported Windows troubleshooting; do not delete servicing files.", "update-history", "update-support");
        Add(IncidentCategory.Boot, "fast-startup", "Comparing Restart and shutdown/startup helps determine whether the symptom follows the fast-startup path.", "Save work first. Do not interrupt boot, edit boot configuration or disable security protections.", "boot-compare", "boot-driver");
        return rules.ToArray();
    }
}

// Minimal local JSON; no raw events, machine names or system inventory. Atomic replacement preserves an older valid file on failure.
public sealed class InvestigationStore(string path)
{
    public const int MaximumEntries = 500;
    public const int MaximumBytes = 4 * 1024 * 1024;
    public IReadOnlyList<InvestigationEntry> Load()
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("History file exceeds its safety limit.");
        var entries = JsonSerializer.Deserialize<InvestigationEntry[]>(File.ReadAllText(path)) ?? throw new InvalidDataException("History is empty or invalid.");
        Validate(entries); return entries;
    }
    public void Save(IReadOnlyList<InvestigationEntry> entries)
    {
        Validate(entries);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entries);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("History exceeds its safety limit.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Clear() { if (File.Exists(path)) File.Delete(path); }
    private static void Validate(IReadOnlyList<InvestigationEntry> entries)
    {
        if (entries.Count > MaximumEntries || entries.Select(e => e?.Id).Distinct().Count() != entries.Count || entries.Any(e => e == null || e.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(e.IncidentId) || e.IncidentId.Length > 128 || string.IsNullOrWhiteSpace(e.StepId) || e.StepId.Length > 128 ||
            string.IsNullOrWhiteSpace(e.Step) || e.Step.Length > 4096 || e.Notes == null || e.Notes.Length > 2000 || !Enum.IsDefined(e.Outcome) || !Enum.IsDefined(e.Category)))
            throw new InvalidDataException("History contains invalid or excessive data.");
    }
}
