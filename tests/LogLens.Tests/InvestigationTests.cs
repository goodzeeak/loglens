using System.IO;
using LogLens.Core;
using Xunit;

namespace LogLens.Tests;

public sealed class InvestigationTests
{
    private static Incident Incident => new DiagnosticEngine().Analyze([AccuracyFixtures.E("disk", 7)], AccuracyFixtures.Period).Single();
    private static InvestigationEntry Entry(Incident incident, InvestigationOutcome outcome) => new(Guid.NewGuid(), incident.Id, incident.Category,
        "backup", "Back up important files", AccuracyFixtures.T, outcome, "");
    [Theory]
    [InlineData(InvestigationOutcome.IssueRecurred)]
    [InlineData(InvestigationOutcome.Inconclusive)]
    [InlineData(InvestigationOutcome.Skipped)]
    public void RecordedStepIsNotRepeated(InvestigationOutcome outcome)
    {
        var incident = Incident; var engine = new InvestigationEngine();
        Assert.Equal("backup", engine.Next(incident, []).StepId);
        var next = engine.Next(incident, [Entry(incident, outcome)]);
        Assert.Equal("storage-check", next.StepId);
        Assert.DoesNotContain("fixed", next.Why);
        if (outcome == InvestigationOutcome.Inconclusive) Assert.Contains("does not rule anything out", next.Why);
    }
    [Fact] public void NonRecurrenceCallsForObservationNotAFix()
    {
        var incident = Incident; var plan = new InvestigationEngine().Next(incident, [Entry(incident, InvestigationOutcome.IssueDidNotRecur)]);
        Assert.False(plan.CanRecord); Assert.Contains("does not establish a permanent fix", plan.Why);
    }
    [Fact] public void SimilarIncidentDoesNotInheritOutcome()
    {
        var incident = Incident;
        Assert.Equal("backup", new InvestigationEngine().Next(incident, [Entry(incident, InvestigationOutcome.IssueDidNotRecur) with { IncidentId = "different" }]).StepId);
    }
    [Fact] public void CompletedWorkflowRequestsNewEvidence()
    {
        var incident = Incident; var first = Entry(incident, InvestigationOutcome.IssueRecurred);
        var second = first with { Id = Guid.NewGuid(), StepId = "storage-check", PerformedAt = first.PerformedAt.AddDays(1) };
        var plan = new InvestigationEngine().Next(incident, [second, first]);
        Assert.False(plan.CanRecord); Assert.Contains("new evidence", plan.Action);
        Assert.Equal(plan, new InvestigationEngine().Next(incident, [first, second]));
    }
    [Fact] public void EverySupportedFixtureHasAnEvidenceBasedWorkflow()
    {
        foreach (var fixture in AccuracyFixtures.All.Concat(BroadAccuracyFixtures.All))
        foreach (var incident in new DiagnosticEngine().Analyze(fixture.Events, AccuracyFixtures.Period))
        {
            var plan = new InvestigationEngine().Next(incident, []);
            Assert.True(plan.CanRecord, fixture.Name); Assert.NotEmpty(plan.Safety); Assert.NotEmpty(plan.Outcomes);
            Assert.Contains(InvestigationEngine.Rules, r => r.Id == plan.StepId && r.Applies(incident));
        }
    }
    [Fact] public void MissingRequiredObservationExcludesRecommendation()
    {
        var incident = Incident with { Findings = [] };
        Assert.False(new InvestigationEngine().Next(incident, []).CanRecord);
    }
    [Fact] public void HistoryPersistsEditsDeletesAndClear()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new InvestigationStore(path); var entry = Entry(Incident, InvestigationOutcome.Inconclusive);
            store.Save([entry]); Assert.Equal(entry, new InvestigationStore(path).Load().Single());
            var edited = entry with { Outcome = InvestigationOutcome.IssueRecurred, Notes = "after normal use" };
            store.Save([edited]); Assert.Equal(edited, store.Load().Single());
            store.Save([]); Assert.Empty(store.Load()); store.Save([entry]); store.Clear(); Assert.Empty(store.Load()); Assert.False(File.Exists(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    [Fact] public void InvalidHistoryCannotReplaceValidFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var store = new InvestigationStore(path); var entry = Entry(Incident, InvestigationOutcome.Inconclusive); store.Save([entry]);
            Assert.Throws<InvalidDataException>(() => store.Save([entry with { Notes = new string('x', 2001) }]));
            Assert.Equal(entry, store.Load().Single());
            File.WriteAllText(path, "null"); Assert.Throws<InvalidDataException>(() => store.Load());
        }
        finally { File.Delete(path); }
    }
    [Fact] public void HistoryNotesAreOptInRedactedAndHtmlEncoded()
    {
        var incident = Incident; var entry = Entry(incident, InvestigationOutcome.Inconclusive) with { Notes = "private@example.com <script>alert(1)</script>" };
        var scan = new ScanResult(AccuracyFixtures.Period, AccuracyFixtures.T, [incident], [], 1);
        var builder = new ReportBuilder(new());
        var omitted = builder.Build(scan, "0.1.0", "Windows", AccuracyFixtures.T, [entry]);
        Assert.DoesNotContain("alert(1)", omitted.PlainText);
        var report = builder.Build(scan, "0.1.0", "Windows", AccuracyFixtures.T, [entry], true);
        Assert.DoesNotContain("private@example.com", report.PlainText); Assert.Contains("&lt;script&gt;", report.ToHtml()); Assert.DoesNotContain("<script>", report.ToHtml());
    }
    [Fact] public void FeedbackMinimizesPrivateFieldsAndEscapesExpectedBehavior()
    {
        var e = AccuracyFixtures.E("Microsoft-Windows-DNS-Client", 1014, fields: [("QueryName", "private.internal"), ("Address", "secret-address")]);
        var incident = new DiagnosticEngine().Analyze([e], AccuracyFixtures.Period).Single();
        var scan = new ScanResult(AccuracyFixtures.Period, AccuracyFixtures.T, [incident], [], 1);
        var report = new ReportBuilder(new()).Feedback(scan, incident, "0.1.0", "Windows", "<script>wrong</script> user@example.com");
        Assert.DoesNotContain("private.internal", report.PlainText); Assert.DoesNotContain("secret-address", report.PlainText);
        Assert.DoesNotContain("user@example.com", report.PlainText); Assert.DoesNotContain("<script>", report.ToHtml()); Assert.Contains("dns-timeout", report.PlainText);
    }
    [Fact] public void DeviceInstanceIdentifiersStayLocal()
    {
        var e = AccuracyFixtures.E("Microsoft-Windows-Kernel-PnP", 219, fields: [("DriverName", "USB\\PRIVATE-SERIAL"), ("FailureName", "ExampleDriver")]);
        var incident = new DiagnosticEngine().Analyze([e], AccuracyFixtures.Period).Single();
        Assert.Contains("PRIVATE-SERIAL", incident.Recorded);
        var scan = new ScanResult(AccuracyFixtures.Period, AccuracyFixtures.T, [incident], [], 1);
        var report = new ReportBuilder(new()).Feedback(scan, incident, "0.1.0", "Windows", "Unexpected warning");
        Assert.DoesNotContain("PRIVATE-SERIAL", report.PlainText); Assert.Contains("ExampleDriver", report.PlainText);
    }
}
