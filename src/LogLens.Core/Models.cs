namespace LogLens.Core;

public enum IncidentCategory { UnexpectedRestart, ApplicationCrash, Hardware, Storage, Display }
public enum EvidenceClass { ConfirmedObservation, PossibleCause, InsufficientEvidence }
public enum Severity { Warning, Error, Critical }
public sealed record DiagnosticEvent(string Channel, string Provider, int EventId, long? RecordId,
    DateTimeOffset Time, IReadOnlyDictionary<string, string> Fields, byte? Level = null)
{
    public string Field(string name) => Fields.TryGetValue(name, out var value) ? value : "";
    public string Reference => $"{Channel} / {Provider} / {EventId} / record {RecordId?.ToString() ?? "unavailable"}";
}
public sealed record Finding(string Code, EvidenceClass Classification, string Text);
public sealed record Recommendation(string Code, string Text);
public sealed record Incident(string Id, IncidentCategory Category, Severity Severity, DateTimeOffset Time,
    string Title, IReadOnlyList<DiagnosticEvent> Evidence, IReadOnlyList<DiagnosticEvent> Context,
    IReadOnlyList<Finding> Findings, IReadOnlyList<Recommendation> Recommendations, string Application = "")
{
    public string LocalTime => Time.ToLocalTime().ToString("g");
    public string Subtitle => $"{LocalTime} · {Category} · {Severity} · {Evidence.Count} records";
    public string Explanation => string.Join("\n\n", Findings.Select(f => $"{Label(f.Classification)}: {f.Text}"));
    public string NextSteps => string.Join("\n\n", Recommendations.Select((r, i) => $"{i + 1}. {r.Text}"));
    public string Recorded => string.Join("\n\n", Evidence.Select(Describe)) +
        (Context.Count == 0 ? "" : "\n\nNearby context — timing does not establish causation:\n" + string.Join("\n\n", Context.Select(Describe)));
    public static string Label(EvidenceClass value) => value switch
    { EvidenceClass.ConfirmedObservation => "Confirmed observation", EvidenceClass.PossibleCause => "Possible cause", _ => "Insufficient evidence" };
    private static string Describe(DiagnosticEvent e) => $"{e.Time.ToLocalTime():O}\n{e.Reference}\n" +
        string.Join("; ", e.Fields.Where(p => p.Key is not ("RawData" or "Binary")).Take(32).Select(p => $"{p.Key}: {p.Value}")) +
        (e.Fields.ContainsKey("RawData") ? "\nBinary WHEA data is available in the original Event Viewer record; LogLens does not decode it." : "");
    public override string ToString() => $"{Title} · {Subtitle}";
}
public sealed record ScanPeriod(DateTimeOffset Start, DateTimeOffset End)
{
    public void Validate()
    {
        if (End <= Start || End - Start > TimeSpan.FromDays(30)) throw new ArgumentOutOfRangeException(nameof(Start), "Choose a range from 24 hours to 30 days.");
    }
}
public sealed record CollectionIssue(string Channel, string Code, string Message);
public sealed record CollectionResult(IReadOnlyList<DiagnosticEvent> Events, IReadOnlyList<CollectionIssue> Issues);
public sealed record ScanResult(ScanPeriod Period, DateTimeOffset CompletedAt, IReadOnlyList<Incident> Incidents,
    IReadOnlyList<CollectionIssue> Issues, int EventCount);
public interface IEventCollector
{
    Task<CollectionResult> CollectAsync(ScanPeriod period, IProgress<string>? progress, CancellationToken cancellationToken);
}
public sealed class ScanService(IEventCollector collector, DiagnosticEngine engine)
{
    public async Task<ScanResult> ScanAsync(ScanPeriod period, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        period.Validate();
        var collected = await collector.CollectAsync(period, progress, cancellationToken).ConfigureAwait(false);
        progress?.Report("Connecting related records…");
        var incidents = await Task.Run(() => engine.Analyze(collected.Events, period, cancellationToken), cancellationToken).ConfigureAwait(false);
        return new(period, DateTimeOffset.UtcNow, incidents, collected.Issues, collected.Events.Count);
    }
}
