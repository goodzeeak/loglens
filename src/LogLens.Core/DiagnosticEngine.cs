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
        var ordered = events.Where(e => e.Time >= period.Start && e.Time <= period.End)
            .Select(e => (Event: e, Module: DiagnosticModules.For(e))).Where(p => p.Module != null)
            .DistinctBy(p => Key(p.Event)).OrderBy(p => p.Event.Time).ThenBy(p => Key(p.Event), StringComparer.Ordinal).ToList();
        var all = ordered.Select(p => p.Event).ToList();
        var groups = new List<(IDiagnosticModule Module, List<DiagnosticEvent> Evidence)>();
        var active = new Dictionary<IDiagnosticModule, List<List<DiagnosticEvent>>>();
        foreach (var (e, matched) in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var module = matched!;
            if (!active.TryGetValue(module, out var candidates)) active[module] = candidates = [];
            // Only groups inside this module's anchored window can join. Expired groups remain in output,
            // but are never rescanned or reclassified for each new event in a large history.
            candidates.RemoveAll(g => e.Time - g[0].Time > module.CorrelationWindow);
            var group = module.CorrelationWindow == TimeSpan.Zero ? null : candidates.LastOrDefault(g => module.CanJoin(g, e));
            if (group == null) { group = [e]; groups.Add((module, group)); candidates.Add(group); }
            else group.Add(e);
        }
        return groups.Select(g => { cancellationToken.ThrowIfCancellationRequested(); return BuildIncident(g.Module, g.Evidence, all); }).OrderByDescending(i => i.Time).ThenBy(i => i.Id, StringComparer.Ordinal).ToArray();
    }
    private static Incident BuildIncident(IDiagnosticModule module, List<DiagnosticEvent> evidence, List<DiagnosticEvent> all)
    {
        var incident = module.Build(evidence, all);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", evidence.Select(Key)))))[..16];
        return incident with { Id = id };
    }
    private static string Key(DiagnosticEvent e) => e.RecordId is > 0 ? $"{e.Channel.ToUpperInvariant()}:{e.Provider.ToUpperInvariant()}:{e.EventId}:{e.RecordId}:{e.Time.UtcTicks}" :
        $"{e.Channel}:{e.Provider}:{e.EventId}:{e.Time.UtcTicks}:" + string.Join("|", e.Fields.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
}
