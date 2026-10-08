using System.IO;
using LogLens.App;
using LogLens.Core;
using Xunit;

namespace LogLens.Tests;

public sealed class HistoryUiTests
{
    [Fact] public void OutcomeEditorSurvivesRestartAndSupportsEditingDeletionAndClear()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var incident = new DiagnosticEngine().Analyze([AccuracyFixtures.E("disk", 7)], AccuracyFixtures.Period).Single();
            var store = new InvestigationStore(path); var vm = new InvestigationViewModel(incident, store, () => true);
            vm.Outcome = "Issue did not recur"; vm.Notes = "normal use"; vm.SaveCommand.Execute(null);
            Assert.Single(vm.History); Assert.False(vm.Plan.CanRecord);
            var reopened = new InvestigationViewModel(incident, new(path), () => true);
            reopened.Selected = reopened.History.Single(); reopened.Outcome = "Issue recurred"; reopened.SaveCommand.Execute(null);
            Assert.Equal("storage-check", reopened.Plan.StepId); Assert.Equal("normal use", store.Load().Single().Notes);
            reopened.Selected = reopened.History.Single(); reopened.DeleteCommand.Execute(null); Assert.Empty(store.Load());
            Assert.Equal("backup", reopened.Plan.StepId);
            reopened.SaveCommand.Execute(null); reopened.ClearCommand.Execute(null); Assert.False(File.Exists(path));
        }
        finally { File.Delete(path); }
    }
    [Fact] public void CorruptHistoryIsPreservedUntilExplicitClear()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "bad json"); var vm = new InvestigationViewModel(null, new(path), () => false);
            Assert.False(vm.SaveCommand.CanExecute(null)); vm.ClearCommand.Execute(null); Assert.Equal("bad json", File.ReadAllText(path));
            var cleared = new InvestigationViewModel(null, new(path), () => true); cleared.ClearCommand.Execute(null); Assert.False(File.Exists(path));
        }
        finally { File.Delete(path); }
    }
    [Fact] public void ReusedEventRecordNumberDoesNotInheritHistory()
    {
        var e = AccuracyFixtures.K(); var later = e with { Time = e.Time.AddHours(1) };
        var incidents = new DiagnosticEngine().Analyze([e, later], AccuracyFixtures.Period);
        Assert.Equal(2, incidents.Count); Assert.NotEqual(incidents[0].Id, incidents[1].Id);
    }
}
