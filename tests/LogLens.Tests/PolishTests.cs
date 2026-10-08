using LogLens.Core;
using LogLens.App;
using Xunit;

namespace LogLens.Tests;

public sealed class PolishTests
{
    [Theory]
    [InlineData(7000, "%%2", "cannot find the file specified (Windows error 2)")]
    [InlineData(7000, "%%3", "cannot find the path specified (Windows error 3)")]
    [InlineData(7000, "%%5", "Access is denied (Windows error 5)")]
    [InlineData(7000, "%%999999", "no supported translation")]
    [InlineData(7000, "%%malformed", "Service error: %%malformed")]
    [InlineData(7001, "%%2", "Dependency service: %%2")]
    [InlineData(7031, "2", "Recorded termination count: 2")]
    public void ServiceInsertionsAreTranslatedOnlyInTheCorrectSchema(int id, string value, string expected)
    {
        var e = AccuracyFixtures.E("Service Control Manager", id, fields: [("param1", "ExampleService"), ("param2", value)]);
        var incident = new DiagnosticEngine().Analyze([e], AccuracyFixtures.Period).Single();
        Assert.Contains(expected, incident.Known); Assert.Equal(value, incident.Evidence.Single().Field("param2"));
        if (id == 7000 && value == "%%2") Assert.Contains("does not identify which file", incident.Known);
    }
    [Fact] public void HistoryOnlyViewHasNoNewStepEditorAndShortNamesExcludeNotes()
    {
        var store = new InvestigationStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json"));
        var vm = new InvestigationViewModel(null, store, () => false);
        Assert.False(vm.HasIncident); Assert.False(vm.ShowEditor); Assert.Contains("No saved outcomes", vm.HistoryHint);
        var entry = new InvestigationEntry(Guid.NewGuid(), "example", IncidentCategory.Storage, "backup", new string('x', 500), AccuracyFixtures.T, InvestigationOutcome.Inconclusive, "private notes");
        Assert.True(entry.ToString().Length < 100); Assert.DoesNotContain("private notes", entry.ToString());
        vm.Selected = entry; Assert.True(vm.ShowEditor); vm.Selected = null; Assert.False(vm.ShowEditor);
    }
}
