using System.Diagnostics;
using LogLens.Core;
using Xunit;
using Xunit.Abstractions;

namespace LogLens.Tests;

public sealed class HardeningTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1, Severity.Critical)]
    [InlineData(2, Severity.Error)]
    [InlineData(3, Severity.Warning)]
    public void WheaSeverityReflectsRecordedLevel(byte level, Severity expected)
    {
        var e = AccuracyFixtures.E("Microsoft-Windows-WHEA-Logger", 18) with { Level = level };
        var incident = Assert.Single(new DiagnosticEngine().Analyze([e], AccuracyFixtures.Period));
        Assert.Equal(expected, incident.Severity);
        Assert.DoesNotContain(incident.Findings, f => f.Classification == EvidenceClass.ConfirmedObservation && f.Code == "hardware-cause");
    }
    [Fact]
    public void MaximumSizedHistoryCompletesAndRemainsCancellable()
    {
        var events = Enumerable.Range(0, DiagnosticEngine.MaximumEvents).Select(n => AccuracyFixtures.E("disk", 7, -n)).ToArray();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var stopwatch = Stopwatch.StartNew();
        var result = new DiagnosticEngine().Analyze(events, AccuracyFixtures.Period, timeout.Token);
        Assert.Equal(DiagnosticEngine.MaximumEvents, result.Count);
        output.WriteLine($"10,000 distinct storage incidents: {stopwatch.ElapsedMilliseconds} ms");
    }
    [Fact]
    public void InvalidZeroReportIdsCannotOverrideConflictingModules()
    {
        var first = AccuracyFixtures.E("Application Error", 1000, channel: "Application", fields: [("AppName", "game.exe"), ("ModuleName", "first.dll"), ("IntegratorReportId", Guid.Empty.ToString())]);
        var second = AccuracyFixtures.E("Windows Error Reporting", 1001, 1, "Application", fields: [("EventName", "APPCRASH"), ("P1", "game.exe"), ("P4", "second.dll"), ("ReportId", Guid.Empty.ToString())]);
        Assert.Equal(2, new DiagnosticEngine().Analyze([first, second], AccuracyFixtures.Period).Count);
    }
    [Fact]
    public void KnownMatchingReportIdCanPairDelayedApplicationRecord()
    {
        const string id = "33333333-3333-3333-3333-333333333333";
        var first = AccuracyFixtures.E("Application Error", 1000, channel: "Application", fields: [("IntegratorReportId", id)]);
        var second = AccuracyFixtures.E("Windows Error Reporting", 1001, 110, "Application", fields: [("EventName", "APPCRASH"), ("ReportId", id)]);
        Assert.Equal(2, Assert.Single(new DiagnosticEngine().Analyze([first, second], AccuracyFixtures.Period)).Evidence.Count);
    }
}
