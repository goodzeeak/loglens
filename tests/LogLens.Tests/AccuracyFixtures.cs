using LogLens.Core;

namespace LogLens.Tests;

public sealed record Expected(IncidentCategory Category, string[] Observations, string[] Hypotheses, string[] Steps,
    int EvidenceCount = 1, int ContextCount = 0);
public sealed record AccuracyFixture(string Name, DiagnosticEvent[] Events, Expected[] Incidents)
{
    // Every fixture forbids categorical causal diagnoses, whether or not a crash was recorded.
    public string[] ProhibitedConclusions { get; } = ["PSU failed", "RAM is defective", "GPU is defective", "caused the restart", "root cause identified", "100%", "virus"];
}
public static class AccuracyFixtures
{
    public static readonly DateTimeOffset T = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    public static readonly ScanPeriod Period = new(T.AddDays(-1), T.AddDays(1));
    public static DiagnosticEvent E(string provider, int id, int seconds = 0, string channel = "System", long? record = null, params (string, string)[] fields)
        => new(channel, provider, id, record ?? (10000 + seconds + id), T.AddSeconds(seconds), fields.ToDictionary(x => x.Item1, x => x.Item2));
    public static DiagnosticEvent K(int seconds = 0) => E("Microsoft-Windows-Kernel-Power", 41, seconds);
    private static Expected Restart(int count = 1, int context = 0, bool bug = false) => new(IncidentCategory.UnexpectedRestart,
        bug ? ["restart", "kernel-power", "bugcheck"] : context > 0 ? ["restart", "kernel-power", "nearby"] : ["restart", "kernel-power"],
        bug ? ["stop-cause"] : [], bug ? ["dump", "reliability", "temperature"] : ["reliability", "temperature"], count, context);
    private static Expected Hardware() => new(IncidentCategory.Hardware, ["whea"], ["hardware-cause"], ["stock", "memory", "temperature"]);
    private static Expected Storage() => new(IncidentCategory.Storage, ["storage", "storage-detail"], ["storage-cause"], ["backup", "storage-check"]);
    private static Expected Display() => new(IncidentCategory.Display, ["display"], ["display-cause"], ["driver", "stock"]);
    private static Expected App(int count = 1, bool module = true) => new(IncidentCategory.ApplicationCrash,
        module ? ["app-failure", "module"] : ["app-failure"], ["app-cause"], ["app-update", "reliability"], count);
    private static DiagnosticEvent A(int seconds = 0, string module = "renderer.dll") => E("Application Error", 1000, seconds, "Application", fields: [("AppName", "game.exe"), ("ModuleName", module)]);
    public static IReadOnlyList<AccuracyFixture> All { get; } =
    [
        new("01 Kernel Power alone", [K()], [Restart()]),
        new("02 Restart and BugCheck", [K(), E("Microsoft-Windows-WER-SystemErrorReporting", 1001, 12)], [Restart(2, bug: true)]),
        new("03 WHEA before restart", [E("Microsoft-Windows-WHEA-Logger", 18, -45), K()], [Restart(context: 1), Hardware()]),
        new("04 Unrelated errors nearby", [K(), E("Service Control Manager", 7000, -3), A(-2), E("DNS Client Events", 1014, -1)], [Restart(), App()]),
        new("05 App and matching WER", [A(), E("Windows Error Reporting", 1001, 5, "Application", fields: [("EventName", "APPCRASH"), ("P1", "game.exe"), ("P4", "renderer.dll")])], [App(2)]),
        new("06 Repeated app different modules", [A(), A(15, "audio.dll")], [App(), App()]),
        new("07 Storage without crash", [E("disk", 7)], [Storage()]),
        new("08 Display recovery", [E("Display", 4101)], [Display()]),
        new("09 Duplicate restart records", [K(), K(), E("EventLog", 6008, 8)], [Restart(2)]),
        new("10 Malformed bugcheck field", [E("Microsoft-Windows-Kernel-Power", 41, fields: [("BugcheckCode", "not a number")])], [Restart()]),
        new("11 Same IDs wrong provider/channel", [E("Unrelated", 41), E("Application Error", 1000), E("BugCheck", 1001, channel: "Application"), E("Windows Error Reporting", 1001, channel: "Application", fields: [("EventName", "WindowsUpdateFailure")])], []),
        new("12 Out of order", [E("EventLog", 6008, 8), K(), E("disk", 7, -20)], [Restart(2, 1), Storage()]),
        new("13 Two rapid restarts remain distinct", [K(), K(45), E("EventLog", 6008, 48)], [Restart(2), Restart()]),
        new("14 Delayed bugcheck not forced into restart", [K(), E("BugCheck", 1001, 240)], [new(IncidentCategory.UnexpectedRestart, ["restart", "bugcheck"], ["stop-cause"], ["dump", "reliability", "temperature"]), Restart()]),
        new("15 Incomplete application evidence", [E("Application Error", 1000, channel: "Application")], [App(module: false)]),
        new("16 Contradictory report identifiers", [A() with { Fields = new Dictionary<string, string> { ["AppName"] = "game.exe", ["ModuleName"] = "renderer.dll", ["IntegratorReportId"] = "11111111-1111-1111-1111-111111111111" } }, E("Windows Error Reporting", 1001, 2, "Application", fields: [("EventName", "APPCRASH"), ("P1", "game.exe"), ("P4", "renderer.dll"), ("ReportId", "22222222-2222-2222-2222-222222222222")])], [App(), App()]),
        new("17 Zero code plus explicit bugcheck record", [E("Microsoft-Windows-Kernel-Power", 41, fields: [("BugcheckCode", "0")]), E("BugCheck", 1001, 3)], [Restart(2, bug: true) with { Observations = ["restart", "kernel-power", "bugcheck", "no-stop-code"] }]),
        new("18 Structured nonzero stop code", [E("Microsoft-Windows-Kernel-Power", 41, fields: [("BugcheckCode", "159")])], [Restart(bug: true) with { Observations = ["restart", "kernel-power", "bugcheck", "stop-code"], Steps = ["dump", "reliability", "temperature", "power-driver"] }]),
        new("19 Corrected WHEA no restart", [E("Microsoft-Windows-WHEA-Logger", 19)], [Hardware()]),
        new("20 Storage context not a causal diagnosis", [E("storahci", 129, -10), K()], [Restart(context: 1), Storage()]),
        new("21 Post-startup WHEA excluded from preceding context", [K(), E("Microsoft-Windows-WHEA-Logger", 17, 10)], [Hardware(), Restart()]),
        new("22 Empty scan", [], []),
        new("23 Application hang", [E("Application Hang", 1002, channel: "Application", fields: [("AppName", "editor.exe")])], [App(module: false)]),
        new("24 Unrecognized WHEA identifier", [E("Microsoft-Windows-WHEA-Logger", 999)], []),
        new("25 NTFS healthy information is not storage failure", [E("Microsoft-Windows-Ntfs", 98, fields: [("CorruptionActionState", "0")]) with { Level = 4 }], []),
        new("26 NTFS ambiguous state is not storage failure", [E("Microsoft-Windows-Ntfs", 98) with { Level = 2 }], []),
        new("27 NTFS explicit error and action required", [E("Microsoft-Windows-Ntfs", 98, fields: [("CorruptionActionState", "2")]) with { Level = 2 }], [Storage()]),
        new("28 NTFS healthy record cannot become restart context", [E("Microsoft-Windows-Ntfs", 98, -1, fields: [("CorruptionActionState", "0")]) with { Level = 4 }, K()], [Restart()]),
        new("29 Fatal processor cache error from validated CPER", [E("Microsoft-Windows-WHEA-Logger", 1, fields: [("RawData", Convert.ToHexString(CperTests.Record()))])], [new(IncidentCategory.Hardware, ["cper-severity", "cper-sections", "processor-error"], ["processor-cause"], ["cpu-stock", "firmware", "temperature"])]),
        new("30 Corrected processor cache error", [E("Microsoft-Windows-WHEA-Logger", 19, fields: [("RawData", Convert.ToHexString(CperTests.Record(severity: 2)))])], [new(IncidentCategory.Hardware, ["cper-severity", "cper-sections", "processor-error"], ["processor-cause"], ["cpu-stock", "firmware", "temperature"])]),
        new("31 Truncated CPER stays unclassified hardware", [E("Microsoft-Windows-WHEA-Logger", 1, fields: [("RawData", "43504552")])], [Hardware()]),
        new("32 No CPU field valid bits no processor diagnosis", [E("Microsoft-Windows-WHEA-Logger", 1, fields: [("RawData", Convert.ToHexString(CperTests.Record(valid: 0)))])], [Hardware() with { Observations = ["whea", "cper-severity", "cper-sections"] }]),
        new("33 Access violation is an application failure", [E("Application Error", 1000, channel: "Application", fields: [("AppName", "game.exe"), ("ModuleName", "renderer.dll"), ("ExceptionCode", "c0000005")])], [App() with { Observations = ["app-failure", "module", "exception-code"] }]),
        new("34 Held power button recorded", [E("Microsoft-Windows-Kernel-Power", 41, fields: [("LongPowerButtonPressDetected", "true")])], [Restart() with { Observations = ["restart", "kernel-power", "power-button"] }]),
        new("35 Sleep transition recorded", [E("Microsoft-Windows-Kernel-Power", 41, fields: [("SleepInProgress", "1")])], [Restart() with { Observations = ["restart", "kernel-power", "sleep-transition"] }]),
        new("36 Informational CPER does not imply hardware failure", [E("Microsoft-Windows-WHEA-Logger", 1, fields: [("RawData", Convert.ToHexString(CperTests.Record(severity: 3)))])], []),
    ];
}
