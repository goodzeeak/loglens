using LogLens.Core;
using static LogLens.Tests.AccuracyFixtures;

namespace LogLens.Tests;

public static class BroadAccuracyFixtures
{
    private static Expected X(IncidentCategory category, string code, params string[] steps) => new(category, [code], [code + "-cause"], steps);
    private static DiagnosticEvent D(int id = 1001) => E("Microsoft-Windows-Dhcp-Client", id, channel: "Microsoft-Windows-Dhcp-Client/Admin");
    private static DiagnosticEvent S(int id = 7000, int seconds = 0) => E("Service Control Manager", id, seconds, fields: [("param1", "Example service"), ("param2", "%%2")]);
    private static Expected Service() => X(IncidentCategory.Service, "service-failure", "service-relevance", "service-details");
    private static Expected Network(string code, string step) => X(IncidentCategory.Network, code, step, "network-compare");
    public static IReadOnlyList<AccuracyFixture> All { get; } =
    [
        new("37 Adapter reset", [E("Microsoft-Windows-NDIS", 10400, fields: [("AdapterName", "Example NIC"), ("ResetReason", "2")])], [Network("adapter-reset", "adapter-check")]),
        new("38 DNS timeout", [E("Microsoft-Windows-DNS-Client", 1014)], [Network("dns-timeout", "dns-check")]),
        new("39 DHCP address failure", [D()], [Network("dhcp-address", "dhcp-check")]),
        new("40 DHCP lease denied", [D(1002)], [Network("dhcp-lease", "dhcp-check")]),
        new("41 DHCP wrong channel", [D() with { Channel = "System" }], []),
        new("42 Service start failure", [S()], [Service()]),
        new("43 Service dependency startup failure", [S(7001)], [Service()]),
        new("44 Service unexpected termination", [S(7031)], [Service()]),
        new("45 Repeated service failures separate", [S(7034), S(7034, 5)], [Service(), Service()]),
        new("46 Missing service identity excluded", [E("Service Control Manager", 7000)], []),
        new("47 Windows update failure", [E("Microsoft-Windows-WindowsUpdateClient", 20, fields: [("updateTitle", "Example update KB1234567"), ("errorCode", "0x80070002")])], [X(IncidentCategory.Update, "update-install", "update-history", "update-support")]),
        new("48 Successful update excluded", [E("Microsoft-Windows-WindowsUpdateClient", 19)], []),
        new("49 Fast startup failure", [E("Microsoft-Windows-Kernel-Boot", 29)], [X(IncidentCategory.Boot, "fast-startup", "boot-compare", "boot-driver")]),
        new("50 Boot status not assumed failure", [E("Microsoft-Windows-Kernel-Boot", 20)], []),
        new("51 Device driver load failure", [E("Microsoft-Windows-Kernel-PnP", 219, fields: [("FailureName", "Example driver"), ("DriverName", "Example device")])], [X(IncidentCategory.DeviceDriver, "driver-load", "device-status", "device-driver")]),
        new("52 Duplicate network records", [D(), D()], [Network("dhcp-address", "dhcp-check")]),
        new("53 Unrelated network and service near restart", [K(), S(7031, -1), E("Microsoft-Windows-DNS-Client", 1014, -2)], [new(IncidentCategory.UnexpectedRestart, ["restart", "kernel-power"], [], ["reliability", "temperature"]), Service(), Network("dns-timeout", "dns-check")]),
        new("54 New IDs wrong provider", [E("Unrelated", 20), E("Unrelated", 29), E("Unrelated", 10400), E("Unrelated", 7000, fields: [("param1", "Example")])], []),
        new("55 Malformed update details are not decoded", [E("Microsoft-Windows-WindowsUpdateClient", 20, fields: [("errorCode", "not-an-error-code")])], [X(IncidentCategory.Update, "update-install", "update-history", "update-support")]),
        new("56 Out of order independent network failures", [E("Microsoft-Windows-DNS-Client", 1014, 10), D(), E("Microsoft-Windows-NDIS", 10400, -10)], [Network("dns-timeout", "dns-check"), Network("dhcp-address", "dhcp-check"), Network("adapter-reset", "adapter-check")]),
    ];
}
