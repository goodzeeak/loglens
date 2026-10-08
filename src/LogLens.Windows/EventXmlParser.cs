using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using LogLens.Core;

namespace LogLens.Windows;

public static class EventXmlParser
{
    public const int MaximumXmlCharacters = 131072;
    public static DiagnosticEvent Parse(string xml)
    {
        if (xml.Length > MaximumXmlCharacters) throw new XmlException("Record exceeds the safe size limit.");
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumXmlCharacters });
        var root = XDocument.Load(reader).Root ?? throw new XmlException("Missing event.");
        XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
        var system = root.Element(ns + "System") ?? throw new XmlException("Missing System.");
        var channel = Required(system.Element(ns + "Channel")?.Value);
        if (channel is not ("System" or "Application")) throw new XmlException("Unsupported channel.");
        var provider = Required(system.Element(ns + "Provider")?.Attribute("Name")?.Value);
        if (!int.TryParse(system.Element(ns + "EventID")?.Value, out var id)) throw new XmlException("Invalid event ID.");
        var timeText = system.Element(ns + "TimeCreated")?.Attribute("SystemTime")?.Value;
        if (!DateTimeOffset.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)) throw new XmlException("Invalid time.");
        long? record = long.TryParse(system.Element(ns + "EventRecordID")?.Value, out var recordId) ? recordId : null;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var data in (root.Element(ns + "EventData")?.Elements(ns + "Data") ?? []).Take(64))
        {
            var name = data.Attribute("Name")?.Value;
            fields.TryAdd(Limit(string.IsNullOrWhiteSpace(name) ? $"Data{index}" : name, 128), Limit(data.Value, 2048));
            index++;
        }
        // Do not copy Computer, Security/UserID, raw XML or localized messages into the model.
        byte? level = byte.TryParse(system.Element(ns + "Level")?.Value, out var parsedLevel) ? parsedLevel : null;
        return new(channel, Limit(provider, 256), id, record, time, fields, level);
    }
    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) ? throw new XmlException("Required field missing.") : value;
    private static string Limit(string value, int maximum) => value.Length <= maximum ? value : value[..maximum] + " [truncated]";
}
