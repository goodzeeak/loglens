using System.Text.Json;
using LogLens.Core;
using Xunit;

namespace LogLens.Tests;

public sealed class AccuracyTests
{
    public static IEnumerable<object[]> Fixtures => AccuracyFixtures.All.Concat(BroadAccuracyFixtures.All).Select(f => new object[] { f.Name });
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void MandatoryDiagnosticAccuracy(string name)
    {
        var fixture = AccuracyFixtures.All.Concat(BroadAccuracyFixtures.All).Single(f => f.Name == name);
        var engine = new DiagnosticEngine();
        var actual = engine.Analyze(fixture.Events, AccuracyFixtures.Period);
        Assert.Equal(fixture.Incidents.Length, actual.Count);
        // Pair by the complete expected semantic signature, independent of incidental record ordering.
        var remaining = actual.ToList();
        foreach (var expected in fixture.Incidents)
        {
            var incident = remaining.FirstOrDefault(i => i.Category == expected.Category &&
                i.Evidence.Count == expected.EvidenceCount && i.Context.Count == expected.ContextCount &&
                i.Findings.Where(f => f.Classification == EvidenceClass.ConfirmedObservation).Select(f => f.Code).Order().SequenceEqual(expected.Observations.Order()));
            Assert.NotNull(incident);
            remaining.Remove(incident);
            Assert.Equal(expected.Hypotheses.Order(), incident.Findings.Where(f => f.Classification == EvidenceClass.PossibleCause).Select(f => f.Code).Order());
            Assert.Equal(expected.Steps.Order(), incident.Recommendations.Select(r => r.Code).Order());
            Assert.Contains(incident.Findings, f => f.Classification == EvidenceClass.InsufficientEvidence);
            foreach (var prohibited in fixture.ProhibitedConclusions)
                Assert.DoesNotContain(prohibited, incident.Explanation, StringComparison.OrdinalIgnoreCase);
        }
        // Repeat, and permute input, to expose unstable correlation and IDs.
        var json = JsonSerializer.Serialize(actual);
        Assert.Equal(json, JsonSerializer.Serialize(engine.Analyze(fixture.Events, AccuracyFixtures.Period)));
        Assert.Equal(json, JsonSerializer.Serialize(engine.Analyze(fixture.Events.Reverse(), AccuracyFixtures.Period)));
    }
    [Fact]
    public void PeriodBoundariesUseAbsoluteTime()
    {
        var period = new ScanPeriod(AccuracyFixtures.T, AccuracyFixtures.T.AddHours(24));
        var start = AccuracyFixtures.K() with { Time = period.Start.ToOffset(TimeSpan.FromHours(11)) };
        var end = AccuracyFixtures.K(24 * 3600);
        var excluded = AccuracyFixtures.K(-1);
        Assert.Equal(2, new DiagnosticEngine().Analyze([start, end, excluded], period).Count);
    }
    [Fact]
    public void CancellationIsObservedBeforeAnalysis()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => new DiagnosticEngine().Analyze([AccuracyFixtures.K()], AccuracyFixtures.Period, cts.Token));
    }
    [Fact]
    public void MaximumInputIsEnforced() => Assert.Throws<ArgumentException>(() => new DiagnosticEngine().Analyze(Enumerable.Repeat(AccuracyFixtures.K(), 10001), AccuracyFixtures.Period));
    [Fact]
    public void InvalidPeriodIsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticEngine().Analyze([], new(AccuracyFixtures.T, AccuracyFixtures.T)));
    [Fact]
    public void NoCausalHardwareClaimFromTemporalContext()
    {
        var result = new DiagnosticEngine().Analyze([AccuracyFixtures.K(), AccuracyFixtures.E("Microsoft-Windows-WHEA-Logger", 18, -3)], AccuracyFixtures.Period);
        var restart = result.Single(i => i.Category == IncidentCategory.UnexpectedRestart);
        Assert.DoesNotContain(restart.Findings, f => f.Classification == EvidenceClass.PossibleCause);
        Assert.Single(restart.Context);
    }
    [Fact]
    public void FixedWindowDoesNotChainSeparateRestarts()
    {
        var result = new DiagnosticEngine().Analyze([AccuracyFixtures.K(), AccuracyFixtures.E("EventLog", 6008, 100), AccuracyFixtures.E("BugCheck", 1001, 200)], AccuracyFixtures.Period);
        Assert.Equal(2, result.Count);
    }
}
