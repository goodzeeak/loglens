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
        return groups.Select(g => { cancellationToken.ThrowIfCancellationRequested(); return BuildIncident(g, ordered); }).OrderByDescending(i => i.Time).ThenBy(i => i.Id, StringComparer.Ordinal).ToArray();
    }
    private static bool CanJoin(List<DiagnosticEvent> group, DiagnosticEvent e, IncidentCategory category) => DiagnosticModules.For(group[0])!.CanJoin(group, e);
    private static Incident BuildIncident(List<DiagnosticEvent> evidence, List<DiagnosticEvent> all)
    {
        var incident = DiagnosticModules.For(evidence[0])!.Build(evidence, all);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", evidence.Select(Key)))))[..16];
        return incident with { Id = id };
    }
    private static string Key(DiagnosticEvent e) => e.RecordId is > 0 ? $"{e.Channel.ToUpperInvariant()}:{e.Provider.ToUpperInvariant()}:{e.EventId}:{e.RecordId}:{e.Time.UtcTicks}" :
        $"{e.Channel}:{e.Provider}:{e.EventId}:{e.Time.UtcTicks}:" + string.Join("|", e.Fields.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
}
