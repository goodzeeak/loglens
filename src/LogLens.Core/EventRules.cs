namespace LogLens.Core;

// Provider AND channel AND identifier form an event identity. No message-language matching.
public static class EventRules
{
    public static IncidentCategory? Category(DiagnosticEvent e)
    {
        if (e.Channel.Equals("System", StringComparison.OrdinalIgnoreCase))
        {
            if (Is(e, "Microsoft-Windows-Kernel-Power", 41) || Is(e, "EventLog", 6008) || IsBugCheck(e)) return IncidentCategory.UnexpectedRestart;
            if (e.Provider.Equals("Microsoft-Windows-WHEA-Logger", StringComparison.OrdinalIgnoreCase) && new[] { 1, 17, 18, 19, 20, 46, 47 }.Contains(e.EventId))
                return CperDecoder.Decode(e.Field("RawData"))?.Severity == "Informational" ? null : IncidentCategory.Hardware;
            if ((e.Provider.Equals("disk", StringComparison.OrdinalIgnoreCase) && new[] { 7, 11, 15, 51, 153, 157 }.Contains(e.EventId)) ||
                (new[] { "storahci", "stornvme", "iaStorA", "iaStorAC" }.Contains(e.Provider, StringComparer.OrdinalIgnoreCase) && e.EventId == 129) ||
                (new[] { "Ntfs", "Microsoft-Windows-Ntfs" }.Contains(e.Provider, StringComparer.OrdinalIgnoreCase) &&
                    (new[] { 55, 140 }.Contains(e.EventId) || (e.EventId == 98 && e.Level is >= 1 and <= 3 &&
                     uint.TryParse(e.Field("CorruptionActionState"), out var action) && action > 0)))) return IncidentCategory.Storage;
            if (Is(e, "Display", 4101)) return IncidentCategory.Display;
        }
        if (e.Channel.Equals("Application", StringComparison.OrdinalIgnoreCase) &&
            (Is(e, "Application Error", 1000) || Is(e, "Application Hang", 1002) ||
            (Is(e, "Windows Error Reporting", 1001) && new[] { "APPCRASH", "BEX", "BEX64", "AppHangB1", "MoAppCrash" }.Contains(e.Field("EventName"), StringComparer.OrdinalIgnoreCase))))
            return IncidentCategory.ApplicationCrash;
        return null;
    }
    public static bool Is(DiagnosticEvent e, string provider, int id) => e.EventId == id && e.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase);
    public static bool IsBugCheck(DiagnosticEvent e) => e.Channel.Equals("System", StringComparison.OrdinalIgnoreCase) &&
        (Is(e, "Microsoft-Windows-WER-SystemErrorReporting", 1001) || Is(e, "BugCheck", 1001));
    public static string App(DiagnosticEvent e) => e.Field("AppName") is { Length: > 0 } app ? app : e.Field("P1");
    public static string Module(DiagnosticEvent e) => e.Field("ModuleName") is { Length: > 0 } module ? module :
        e.Field("EventName").Equals("APPCRASH", StringComparison.OrdinalIgnoreCase) ? e.Field("P4") : "";
    public static string ReportId(DiagnosticEvent e) => e.Field("IntegratorReportId") is { Length: > 0 } id ? id : e.Field("ReportId");
}
