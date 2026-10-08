using LogLens.Core;
using Xunit;

namespace LogLens.Tests;

public sealed class ReportTests
{
    [Theory]
    [InlineData(@"C:\Users\Alice\dump.dmp", "Alice")]
    [InlineData(@"\\PRIVATE-PC\share\dump", "PRIVATE-PC")]
    [InlineData("contact alice@example.com", "alice@example.com")]
    [InlineData("server 192.168.1.42", "192.168.1.42")]
    [InlineData("server 2001:db8::1", "2001:db8::1")]
    [InlineData("address ::1", "::1")]
    [InlineData("PRIVATE-PC account Alice", "Alice")]
    [InlineData("S-1-5-21-123-456-789-1001", "S-1-5")]
    public void SensitiveValuesRedacted(string input, string prohibited)
    {
        var result = new ReportRedactor(["Alice", "PRIVATE-PC"]).Redact(input);
        Assert.DoesNotContain(prohibited, result, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public void AllUntrustedHtmlIsEncoded()
    {
        var html = new DiagnosticReport([new("<script>alert(1)</script>", ["<img src=x onerror=alert(1)>", "&\"'"])]).ToHtml();
        Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;script&gt;", html); Assert.Contains("default-src 'none'", html);
    }
    [Fact] public void ArbitraryEventPayloadIsNotExported()
    {
        var e = AccuracyFixtures.K() with { Fields = new Dictionary<string, string> { ["Secret"] = "SENSITIVE_UNKNOWN_CUSTOMER" } };
        var report = Build(e);
        Assert.DoesNotContain("SENSITIVE_UNKNOWN_CUSTOMER", report.ToHtml());
        Assert.Contains("record", report.PlainText);
    }
    [Fact] public void ApplicationAndModuleAreRedactedAndEncoded()
    {
        var e = AccuracyFixtures.E("Application Error", 1000, channel: "Application", fields: [("AppName", @"C:\Users\Alice\<script>.exe"), ("ModuleName", "<script>alert(1)</script>")]);
        var report = Build(e);
        Assert.DoesNotContain("Alice", report.ToHtml()); Assert.DoesNotContain("<script>", report.ToHtml());
        Assert.Contains("&lt;script&gt;", report.ToHtml());
    }
    [Fact] public void EmptyPartialReportExplainsLimitations()
    {
        var scan = new ScanResult(AccuracyFixtures.Period, AccuracyFixtures.T, [], [new("System", "missing", "Missing log")], 0);
        var report = new ReportBuilder(new()).Build(scan, "0.1.0", "Windows", AccuracyFixtures.T);
        Assert.Contains("Partial results", report.PlainText); Assert.Contains("does not prove", report.PlainText);
    }
    private static DiagnosticReport Build(DiagnosticEvent e)
    {
        var scan = new ScanResult(AccuracyFixtures.Period, AccuracyFixtures.T, new DiagnosticEngine().Analyze([e], AccuracyFixtures.Period), [], 1);
        return new ReportBuilder(new(["Alice"])).Build(scan, "0.1.0", "Windows", AccuracyFixtures.T);
    }
}
