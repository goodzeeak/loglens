namespace LogLens.Core;

// Each declaration is a reviewable rule: identity, minimum fields, impact, facts, limits and actions.
// New categories keep separate occurrences separate. Exact duplicate records are removed by the engine.
public sealed record EventDetection(string Channel, string Provider, int Id, string Code, string Title,
    string Observation, string Hypothesis, string Unknown, Recommendation[] Steps, string[] DetailFields,
    string? RequiredField = null, Severity Severity = Severity.Warning);

internal sealed class DeclaredModule(IncidentCategory category, EventDetection[] rules) : DiagnosticModule
{
    public override IncidentCategory Category => category;
    public override TimeSpan CorrelationWindow => TimeSpan.Zero;
    public override IReadOnlyList<EventSource> Sources { get; } = rules.Select(r => new EventSource(r.Channel, r.Provider, [r.Id])).ToArray();
    public IReadOnlyList<EventDetection> Rules => rules;
    private EventDetection? Match(DiagnosticEvent e) => rules.FirstOrDefault(r => e.Channel.Equals(r.Channel, StringComparison.OrdinalIgnoreCase)
        && EventRules.Is(e, r.Provider, r.Id) && (r.RequiredField == null || !string.IsNullOrWhiteSpace(e.Field(r.RequiredField))));
    public override bool Matches(DiagnosticEvent e) => Match(e) != null;
    public override bool CanJoin(List<DiagnosticEvent> group, DiagnosticEvent next) => false;
    public override Incident Build(List<DiagnosticEvent> evidence, List<DiagnosticEvent> all)
    {
        var first = evidence[0]; var rule = Match(first)!;
        var details = rule.DetailFields.Where(k => first.Field(k).Length > 0).Select(k => $"{FieldLabel(k, first.EventId)}: {first.Field(k)}");
        var fact = rule.Observation + (details.Any() ? "\nRecorded details: " + string.Join("; ", details) : "\nOptional identifying details were not recorded or could not be read.");
        var subject = Category == IncidentCategory.Service ? first.Field("param1") : Category == IncidentCategory.Update ? first.Field("updateTitle") : Category == IncidentCategory.Network ? first.Field("AdapterName") : "";
        var title = rule.Title + (subject.Length > 0 ? ": " + (subject.Length > 120 ? subject[..120] + "…" : subject) : "");
        return new("", Category, rule.Severity, first.Time, title, evidence.ToArray(), [],
            [new(rule.Code, EvidenceClass.ConfirmedObservation, fact), new(rule.Code + "-cause", EvidenceClass.PossibleCause, rule.Hypothesis),
             new("unknown", EvidenceClass.InsufficientEvidence, rule.Unknown)], rule.Steps);
    }
    private static string FieldLabel(string field, int id) => field switch
    {
        "param1" => "Service", "param2" when id == 7000 => "Service error", "param2" when id == 7001 => "Dependency service",
        "param2" when id is 7031 or 7034 => "Recorded termination count", "FailureName" => "Driver", "DriverName" => "Device",
        "AdapterName" => "Adapter", "ResetReason" => "Reset reason value", "ResetCount" => "Reset count since initialization",
        "updateTitle" => "Update", "errorCode" => "Installation error code", "FailureStatus" => "Startup status code", "StatusCode" => "Status code", _ => field
    };
}

internal static class BroadModules
{
    public static IEnumerable<IDiagnosticModule> Create()
    {
        yield return new DeclaredModule(IncidentCategory.DeviceDriver,
        [new("System", "Microsoft-Windows-Kernel-PnP", 219, "driver-load", "Device driver failed to load",
            "Windows recorded a driver-load failure for a device. This may be transient during startup.",
            "A driver startup, compatibility or device-availability issue could be involved.",
            "This warning does not establish a broken device or a lasting failure. Check whether the device subsequently works.",
            [new("device-status", "Open Device Manager and check whether the affected device currently works and shows an error. Record the error code before changing drivers."),
             new("device-driver", "If the device remains affected, compare recent driver changes with the PC or device vendor's supported driver guidance.")], ["FailureName", "Status"]) ]);
        yield return new DeclaredModule(IncidentCategory.Network,
        [new("System", "Microsoft-Windows-NDIS", 10400, "adapter-reset", "Network adapter reset began",
            "Windows recorded the start of a network-interface reset, with a temporary connectivity disruption expected.",
            "An adapter driver, firmware or device-state problem could have prompted the reset.",
            "The reset reason is a recorded value, not a confirmed physical fault. This does not prove an internet outage or continuous packet loss.",
            [new("adapter-check", "Compare the recorded time with connection interruptions. Review the adapter in Device Manager and recent vendor driver changes."), new("network-compare", "Compare another device on the same network during recurrence to distinguish a PC-specific symptom from a shared connection problem.")], ["AdapterName", "ResetReason", "ResetCount"]),
         new("System", "Microsoft-Windows-DNS-Client", 1014, "dns-timeout", "DNS name lookup timed out",
            "A DNS name-resolution attempt timed out without a response from the configured DNS servers.",
            "DNS-server availability or the network path could be involved; a transient lookup failure is also possible.",
            "A failed lookup does not prove all websites were unavailable or that the DNS server itself failed. Historical records do not measure current connectivity.",
            [new("dns-check", "When the symptom recurs, compare whether other websites and another device work. Record whether only one name fails; use Windows Network and Internet troubleshooting if needed."), new("network-compare", "If several devices are affected, check router or provider status using their support guidance. Do not change DNS or security settings blindly.")], []),
         new("Microsoft-Windows-Dhcp-Client/Admin", "Microsoft-Windows-Dhcp-Client", 1001, "dhcp-address", "DHCP address assignment failed",
            "Windows recorded that an adapter was not assigned an address by DHCP and would continue trying.",
            "Local link availability, DHCP-server availability or network configuration could be involved.",
            "This record does not show whether a later attempt succeeded or whether the internet service was down.",
            [new("dhcp-check", "Check the affected connection in Windows Network and Internet settings and compare another device on the same network. Record whether Windows later obtains an address."), new("network-compare", "If the issue recurs, use Windows network troubleshooting or contact the network administrator. Do not set an arbitrary static address.")], ["StatusCode"]),
         new("Microsoft-Windows-Dhcp-Client/Admin", "Microsoft-Windows-Dhcp-Client", 1002, "dhcp-lease", "DHCP lease was denied",
            "The DHCP server denied an adapter's address lease (DHCPNACK).",
            "A changed network or an outdated lease could be involved; the client may recover by obtaining another lease.",
            "A lease denial is not evidence of an attack, an internet outage or permanent loss of connectivity.",
            [new("dhcp-check", "Check whether Windows now has a working connection. If the problem recurs, compare another device and use Windows network troubleshooting."), new("network-compare", "Ask the network administrator to compare DHCP lease records if address assignment remains unreliable.")], [])]);
        yield return new DeclaredModule(IncidentCategory.Service,
            new[] { 7000, 7001, 7031, 7034 }.Select(id => new EventDetection("System", "Service Control Manager", id, "service-failure",
                id is 7031 or 7034 ? "Service terminated unexpectedly" : "Service failed to start",
                id is 7031 or 7034 ? "Service Control Manager recorded an unexpected service termination." : "Service Control Manager recorded a service startup failure.",
                "A service-specific configuration, dependency or application problem could be involved.",
                "Importance depends on whether you use the affected service. This record alone does not establish the underlying cause or justify restarting or disabling it.",
                [new("service-relevance", "Compare the named service with the feature that failed. Check its status in Services and the owning application's support information without changing startup settings."), new("service-details", "For a recurring relevant failure, review adjacent events from that service and its application logs. Give the recorded error to the application's support team.")], ["param1", "param2"], RequiredField: "param1", Severity: Severity.Error)).ToArray());
        yield return new DeclaredModule(IncidentCategory.Update,
        [new("System", "Microsoft-Windows-WindowsUpdateClient", 20, "update-install", "Windows update installation failed",
            "Windows Update recorded an installation failure.", "The update's prerequisites, download, servicing state or another installation condition could be involved.",
            "A generic error code does not establish the root cause. This event does not indicate whether a later attempt installed successfully.",
            [new("update-history", "Open Settings → Windows Update → Update history. Match the update and error code and check whether a later installation succeeded."), new("update-support", "If the same update still fails, use Windows Update troubleshooting and the update's Microsoft support page. Save work before any prompted restart; do not delete servicing files or modify the registry.")], ["updateTitle", "errorCode"], Severity: Severity.Error)]);
        yield return new DeclaredModule(IncidentCategory.Boot,
        [new("System", "Microsoft-Windows-Kernel-Boot", 29, "fast-startup", "Fast startup attempt failed",
            "Windows recorded a failed fast-startup attempt.", "A hibernation-resume, driver or boot-state issue could be involved.",
            "This is not evidence that every boot failed, and it does not measure boot duration or identify the faulty driver.",
            [new("boot-compare", "Save your work, then compare a normal Windows Restart with your next usual shutdown and startup. Record whether the symptom occurs only after shutdown; Restart follows a different startup path."), new("boot-driver", "If failures recur, compare recent driver and Windows changes with Reliability Monitor and your PC vendor's guidance. Do not alter firmware or boot configuration without a supported procedure.")], ["FailureStatus"], Severity: Severity.Error)]);
    }
}
