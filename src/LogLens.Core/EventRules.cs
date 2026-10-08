namespace LogLens.Core;

// Provider AND channel AND identifier form an event identity. No message-language matching.
public static class EventRules
{
    public static IncidentCategory? Category(DiagnosticEvent e) => DiagnosticModules.For(e)?.Category;
    public static bool Is(DiagnosticEvent e, string provider, int id) => e.EventId == id && e.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase);
    public static bool IsBugCheck(DiagnosticEvent e) => e.Channel.Equals("System", StringComparison.OrdinalIgnoreCase) &&
        (Is(e, "Microsoft-Windows-WER-SystemErrorReporting", 1001) || Is(e, "BugCheck", 1001));
    public static string App(DiagnosticEvent e) => e.Field("AppName") is { Length: > 0 } app ? app : e.Field("P1");
    public static string Module(DiagnosticEvent e) => e.Field("ModuleName") is { Length: > 0 } module ? module :
        e.Field("EventName").Equals("APPCRASH", StringComparison.OrdinalIgnoreCase) ? e.Field("P4") : "";
    public static string ReportId(DiagnosticEvent e) => e.Field("IntegratorReportId") is { Length: > 0 } id ? id : e.Field("ReportId");
}
