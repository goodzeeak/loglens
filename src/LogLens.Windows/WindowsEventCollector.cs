using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Security;
using System.Xml;
using LogLens.Core;

namespace LogLens.Windows;

public interface IRecordReader : IDisposable { string? Read(); }
public interface IRecordReaderFactory { IRecordReader Open(string channel, string query); }
public sealed class NativeRecordReaderFactory : IRecordReaderFactory
{
    public IRecordReader Open(string channel, string query) => new NativeReader(channel, query);
    private sealed class NativeReader(string channel, string query) : IRecordReader
    {
        private readonly EventLogReader reader = new(new EventLogQuery(channel, PathType.LogName, query) { ReverseDirection = true });
        public string? Read()
        {
            using var record = reader.ReadEvent(TimeSpan.FromSeconds(1));
            return record?.ToXml();
        }
        public void Dispose() => reader.Dispose();
    }
}
public sealed class WindowsEventCollector(IRecordReaderFactory? factory = null) : IEventCollector
{
    private readonly IRecordReaderFactory factory = factory ?? new NativeRecordReaderFactory();
    public const int MaximumRecordsPerChannel = 5000;
    public const int MaximumRetainedCharacters = 8 * 1024 * 1024;
    public Task<CollectionResult> CollectAsync(ScanPeriod period, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        period.Validate();
        return Task.Run(() => Collect(period, progress, cancellationToken), cancellationToken);
    }
    public static string Query(ScanPeriod period, string channel)
    {
        period.Validate();
        var start = period.Start.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        var end = period.End.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        var ids = channel == "System" ? new[] { 41, 6008, 1001, 1, 17, 18, 19, 20, 46, 47, 7, 11, 15, 51, 153, 157, 129, 55, 98, 140, 4101 } : new[] { 1000, 1001, 1002 };
        // Native filter bounds time and candidate IDs; managed rules enforce the full provider/channel identity.
        return $"*[System[TimeCreated[@SystemTime >= '{start}' and @SystemTime <= '{end}'] and ({string.Join(" or ", ids.Select(id => $"EventID={id}"))})]]";
    }
    private CollectionResult Collect(ScanPeriod period, IProgress<string>? progress, CancellationToken token)
    {
        var events = new List<DiagnosticEvent>(); var issues = new List<CollectionIssue>(); var retained = 0;
        foreach (var channel in new[] { "System", "Application" })
        {
            token.ThrowIfCancellationRequested(); progress?.Report($"Reading {channel} records…");
            var malformed = 0;
            try
            {
                using var reader = factory.Open(channel, Query(period, channel));
                for (var count = 0; count <= MaximumRecordsPerChannel; count++)
                {
                    token.ThrowIfCancellationRequested();
                    var xml = reader.Read();
                    token.ThrowIfCancellationRequested();
                    if (xml == null) break;
                    if (count == MaximumRecordsPerChannel)
                    {
                        issues.Add(new(channel, "limit", "The candidate-record limit was reached. Results are partial; choose a shorter scan period.")); break;
                    }
                    try
                    {
                        var item = EventXmlParser.Parse(xml);
                        if (item.Channel == channel && item.Time >= period.Start && item.Time <= period.End && EventRules.Category(item) != null)
                        {
                            var characters = item.Fields.Sum(p => p.Key.Length + p.Value.Length) + item.Provider.Length + item.Channel.Length;
                            if (retained + characters > MaximumRetainedCharacters)
                            {
                                issues.Add(new(channel, "memory-limit", "The retained-data safety limit was reached. This scan is partial; choose a shorter period."));
                                return new(events, issues);
                            }
                            retained += characters; events.Add(item);
                        }
                    }
                    catch (XmlException) { malformed++; }
                    if (count % 100 == 0) progress?.Report($"Reading {channel}: {count + 1:N0} candidate records…");
                }
            }
            catch (UnauthorizedAccessException) { issues.Add(Denied(channel)); }
            catch (SecurityException) { issues.Add(Denied(channel)); }
            catch (EventLogNotFoundException) { issues.Add(new(channel, "missing", "This event log is unavailable or missing. Results are partial.")); }
            catch (EventLogException) { issues.Add(new(channel, "read-error", "Windows could not finish reading this log. It may be unavailable or damaged. Results are partial.")); }
            if (malformed > 0) issues.Add(new(channel, "malformed", $"Skipped {malformed} malformed or oversized records. Results are partial."));
        }
        return new(events, issues);
    }
    private static CollectionIssue Denied(string channel) => new(channel, "access-denied", "Windows denied read access. Results are partial. You can explicitly reopen LogLens as administrator and scan again; this is optional.");
}
