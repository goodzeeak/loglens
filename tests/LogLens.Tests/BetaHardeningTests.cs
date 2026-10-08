using System.IO;
using System.Windows.Media.Imaging;
using LogLens.Core;
using LogLens.Windows;
using Xunit;

namespace LogLens.Tests;

public sealed class BetaHardeningTests
{
    [Fact] public void EveryDeclaredSourceIsQueriedInItsChannel()
    {
        foreach (var source in DiagnosticModules.All.SelectMany(m => m.Sources))
        {
            Assert.Contains(source.Channel, WindowsEventCollector.Channels);
            var query = WindowsEventCollector.Query(AccuracyFixtures.Period, source.Channel);
            foreach (var id in source.Identifiers) Assert.Contains($"EventID={id}", query);
        }
    }
    [Fact] public void IconIncludesSmallAndLargeFrames()
    {
        var root = FindRepository();
        using var stream = File.OpenRead(Path.Combine(root, "src/LogLens.App/Assets/LogLens.ico"));
        var icon = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.Equal(new[] {16,20,24,32,40,48,64,128,256}, icon.Frames.Select(f => f.PixelWidth));
        Assert.All(icon.Frames, f => Assert.Equal(f.PixelWidth, f.PixelHeight));
    }
    [Fact] public void InvestigationExclusionOverridesRequiredEvidence()
    {
        var incident = new DiagnosticEngine().Analyze([AccuracyFixtures.E("disk", 7)], AccuracyFixtures.Period).Single();
        var rule = new InvestigationRule("backup", IncidentCategory.Storage, ["storage"], ["storage-detail"], "why", "safety");
        Assert.False(rule.Applies(incident));
    }
    [Fact] public void DhcpStructuredXmlAndWrongChannelAreHandled()
    {
        var xml = CollectionTests.Xml.Replace("Microsoft-Windows-Kernel-Power", "Microsoft-Windows-Dhcp-Client").Replace("<EventID>41</EventID>", "<EventID>1001</EventID>")
            .Replace("<Channel>System</Channel>", "<Channel>Microsoft-Windows-Dhcp-Client/Admin</Channel>");
        Assert.Equal(IncidentCategory.Network, EventRules.Category(EventXmlParser.Parse(xml)));
        Assert.Null(EventRules.Category(EventXmlParser.Parse(xml.Replace("<Channel>Microsoft-Windows-Dhcp-Client/Admin</Channel>", "<Channel>System</Channel>"))));
    }
    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "LogLens.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository unavailable.");
    }
}
