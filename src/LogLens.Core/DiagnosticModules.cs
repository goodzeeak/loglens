namespace LogLens.Core;

public sealed record EventSource(string Channel, string Provider, int[] Identifiers)
{
    public bool Matches(DiagnosticEvent e) => Channel.Equals(e.Channel, StringComparison.OrdinalIgnoreCase) &&
        Provider.Equals(e.Provider, StringComparison.OrdinalIgnoreCase) && Identifiers.Contains(e.EventId);
}
public interface IDiagnosticModule
{
    IncidentCategory Category { get; }
    IReadOnlyList<EventSource> Sources { get; }
    bool Matches(DiagnosticEvent e);
    bool CanJoin(List<DiagnosticEvent> group, DiagnosticEvent next);
    Incident Build(List<DiagnosticEvent> evidence, List<DiagnosticEvent> all);
}
public abstract class DiagnosticModule : IDiagnosticModule
{
    public abstract IncidentCategory Category { get; }
    public abstract IReadOnlyList<EventSource> Sources { get; }
    public virtual bool Matches(DiagnosticEvent e) => Sources.Any(s => s.Matches(e));
    public abstract Incident Build(List<DiagnosticEvent> evidence, List<DiagnosticEvent> all);
    public virtual bool CanJoin(List<DiagnosticEvent> group, DiagnosticEvent e)
    {
        var first = group[0];
        var seconds = (e.Time - first.Time).TotalSeconds;
        // Device identity is required before aggregating noisy hardware/storage/display records.
        var device = first.Field("DeviceName");
        return seconds <= 60 && device.Length > 0 && device == e.Field("DeviceName") && first.Provider == e.Provider && first.EventId == e.EventId;
    }

}
public static class DiagnosticModules
{
    public static IReadOnlyList<IDiagnosticModule> All { get; } = [new UnexpectedRestartModule(), new ApplicationCrashModule(), new HardwareModule(), new StorageModule(), new DisplayModule(), .. BroadModules.Create()];
    public static IDiagnosticModule? For(DiagnosticEvent e) => All.FirstOrDefault(m => m.Matches(e));
}
