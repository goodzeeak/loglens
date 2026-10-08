using System.Diagnostics.Eventing.Reader;
using System.Xml;
using LogLens.Core;
using LogLens.Windows;
using Xunit;

namespace LogLens.Tests;

public sealed class CollectionTests
{
    public const string Xml = "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><Provider Name='Microsoft-Windows-Kernel-Power'/><EventID>41</EventID><TimeCreated SystemTime='2026-09-20T12:00:00Z'/><EventRecordID>123</EventRecordID><Channel>System</Channel><Computer>private-pc</Computer></System><EventData><Data Name='BugcheckCode'>159</Data><Data Name='Localized'>日本語 français</Data></EventData></Event>";
    [Fact] public void StructuredXmlIsLanguageIndependent()
    {
        var e = EventXmlParser.Parse(Xml);
        Assert.Equal("159", e.Field("BugcheckCode")); Assert.Equal("日本語 français", e.Field("Localized"));
        Assert.DoesNotContain(e.Fields, p => p.Key == "Computer"); Assert.Equal(123, e.RecordId);
    }
    [Theory]
    [InlineData("<Event/>")]
    [InlineData("not xml")]
    [InlineData("<!DOCTYPE Event [<!ENTITY test SYSTEM 'file:///c:/windows/win.ini'>]><Event>&test;</Event>")]
    public void MalformedAndExternalEntityXmlRejected(string xml) => Assert.Throws<XmlException>(() => EventXmlParser.Parse(xml));
    [Fact] public void OversizedXmlRejected() => Assert.Throws<XmlException>(() => EventXmlParser.Parse(new string('x', 131073)));
    [Fact] public void DuplicateFieldsAndOversizedValuesAreBounded()
    {
        var parsed = EventXmlParser.Parse(Xml.Replace("</EventData>", $"<Data Name='BugcheckCode'>0</Data><Data Name='Large'>{new string('x', 4000)}</Data></EventData>"));
        Assert.Equal("159", parsed.Field("BugcheckCode")); Assert.True(parsed.Field("Large").Length < 2100);
    }
    [Theory]
    [InlineData("access-denied")]
    [InlineData("missing")]
    [InlineData("read-error")]
    public async Task ChannelFailuresBecomePartialResults(string failure)
    {
        var result = await new WindowsEventCollector(new FailureFactory(failure)).CollectAsync(AccuracyFixtures.Period, null, default);
        Assert.Equal(2, result.Issues.Count); Assert.All(result.Issues, i => Assert.Equal(failure, i.Code)); Assert.Empty(result.Events);
    }
    [Fact] public async Task MalformedRecordDoesNotPreventNextRecord()
    {
        var result = await new WindowsEventCollector(new FakeFactory(["<bad/>", Xml])).CollectAsync(AccuracyFixtures.Period, null, default);
        Assert.Single(result.Events); Assert.Equal(2, result.Issues.Count);
    }
    [Fact] public async Task ScanLimitProducesWarning()
    {
        var result = await new WindowsEventCollector(new FakeFactory(Enumerable.Repeat(Xml, 5001).ToArray())).CollectAsync(AccuracyFixtures.Period, null, default);
        Assert.Equal(5000, result.Events.Count); Assert.Equal(2, result.Issues.Count(i => i.Code == "limit"));
    }
    [Fact] public async Task CancellationStopsReaderLoop()
    {
        using var cts = new CancellationTokenSource();
        var factory = new CancelFactory(cts);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new WindowsEventCollector(factory).CollectAsync(AccuracyFixtures.Period, null, cts.Token));
        Assert.True(factory.Disposed);
    }
    [Fact] public async Task MissingLogsPropagateThroughScanService()
    {
        var scan = await new ScanService(new WindowsEventCollector(new FailureFactory("missing")), new()).ScanAsync(AccuracyFixtures.Period, null, default);
        Assert.Empty(scan.Incidents); Assert.Equal(2, scan.Issues.Count);
    }
    [Fact] public async Task NativeWindowsReadOnlyIntegration()
    {
        var end = DateTimeOffset.UtcNow;
        var result = await new WindowsEventCollector().CollectAsync(new(end.AddHours(-24), end), null, default);
        Assert.InRange(result.Events.Count, 0, DiagnosticEngine.MaximumEvents);
        Assert.All(result.Events, e => Assert.NotNull(EventRules.Category(e)));
        Assert.DoesNotContain(result.Issues, i => i.Code == "read-error");
    }
    [Fact] public void QueryUsesUtcBounds()
    {
        var query = WindowsEventCollector.Query(new(AccuracyFixtures.T.ToOffset(TimeSpan.FromHours(10)), AccuracyFixtures.T.AddDays(1)), "System");
        Assert.Contains("2026-09-20T12:00:00.0000000Z", query); Assert.Contains("EventID=41", query);
    }
    private sealed class FailureFactory(string code) : IRecordReaderFactory
    {
        public IRecordReader Open(string channel, string query) => throw code switch
        { "access-denied" => new UnauthorizedAccessException(), "missing" => new EventLogNotFoundException(), _ => new EventLogException() };
    }
    private sealed class FakeFactory(string[] records) : IRecordReaderFactory
    {
        public IRecordReader Open(string channel, string query) => new FakeReader(records);
        private sealed class FakeReader(string[] records) : IRecordReader
        {
            private int index;
            public string? Read() => index < records.Length ? records[index++] : null;
            public void Dispose() { }
        }
    }
    private sealed class CancelFactory(CancellationTokenSource cts) : IRecordReaderFactory, IRecordReader
    {
        public bool Disposed { get; private set; }
        public IRecordReader Open(string channel, string query) => this;
        public string? Read() { cts.Cancel(); return Xml; }
        public void Dispose() => Disposed = true;
    }
}
