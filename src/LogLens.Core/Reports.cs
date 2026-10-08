using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LogLens.Core;

public sealed class ReportRedactor(IEnumerable<string>? knownIdentifiers = null)
{
    private readonly string[] identifiers = (knownIdentifiers ?? []).Where(s => s.Length > 1).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(s => s.Length).ToArray();
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    public string Redact(string input)
    {
        var text = input.Length > 32768 ? input[..32768] + " [truncated]" : input;
        try
        {
            // Deliberately remove whole paths and URLs instead of trying to recognize every user directory.
            text = Replace(text, @"(?:[A-Za-z]:\\|\\\\)[^\r\n<>"";]*", "[path redacted]");
            text = Replace(text, @"\b(?:https?|ftp)://[^\s<>""']+", "[URL redacted]");
            text = Replace(text, @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b", "[email redacted]");
            text = Replace(text, @"\b(?:\d{1,3}\.){3}\d{1,3}\b", "[IP redacted]");
            text = Replace(text, @"(?<![\w:])(?:[0-9a-f]{0,4}:){2,}[0-9a-f:.%\w-]*(?!\w)", "[IP redacted]");
            text = Replace(text, @"\b(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}\b", "[address redacted]");
            text = Replace(text, @"\bS-1-\d+(?:-\d+){1,15}\b", "[SID redacted]");
            foreach (var identifier in identifiers) text = text.Replace(identifier, "[identity redacted]", StringComparison.OrdinalIgnoreCase);
            return new string(text.Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t').ToArray());
        }
        catch (RegexMatchTimeoutException) { return "[content omitted: privacy processing limit]"; }
    }
    private static string Replace(string text, string pattern, string replacement) => Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
}
public sealed record ReportBlock(string Heading, IReadOnlyList<string> Paragraphs);
public sealed record DiagnosticReport(IReadOnlyList<ReportBlock> Blocks)
{
    public string PlainText => string.Join("\n\n", Blocks.Select(b => b.Heading + "\n" + string.Join("\n\n", b.Paragraphs)));
    public string ToHtml()
    {
        var html = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><title>LogLens diagnostic report</title><style>body{font:16px/1.65 system-ui,sans-serif;color:#17243b;background:#f4f7fb;max-width:980px;margin:32px auto;padding:0 24px}section{background:white;border:1px solid #c5cfdd;border-radius:12px;padding:20px 28px;margin:20px 0}h1{font-size:30px}h2{font-size:20px;color:#153e71}p{white-space:pre-wrap;overflow-wrap:anywhere}@media print{body{background:white;margin:0}section{break-inside:avoid}}</style></head><body><h1>LogLens · Goodwin Labs</h1>");
        foreach (var block in Blocks)
        {
            html.Append("<section><h2>").Append(WebUtility.HtmlEncode(block.Heading)).Append("</h2>");
            foreach (var paragraph in block.Paragraphs) html.Append("<p>").Append(WebUtility.HtmlEncode(paragraph)).Append("</p>");
            html.Append("</section>");
        }
        return html.Append("</body></html>").ToString();
    }
}
public sealed class ReportBuilder(ReportRedactor redactor)
{
    public const string Limitations = "Windows logs may be incomplete, delayed, inaccessible or absent. Record times are not necessarily failure times. Nearby events do not prove causation. Two-minute restart grouping is heuristic; separate restarts may be ambiguous. WHEA binary data and crash dumps are not decoded. No finding establishes a defective component. No detected incidents does not prove the PC is healthy.";
    public const string PrivacyNotice = "Windows logs may contain sensitive information. This report omits raw messages, raw XML, computer/UserID fields and arbitrary event payloads. Known identities, paths, email and IP addresses are redacted where practical. Redaction is not perfect: application names, timestamps and unusual identifiers can still be identifying. Review every section before sharing.";
    public DiagnosticReport Build(ScanResult scan, string version, string osVersion, DateTimeOffset created)
    {
        var blocks = new List<ReportBlock>();
        void Add(string heading, params string[] paragraphs) => blocks.Add(new(redactor.Redact(heading), paragraphs.Select(redactor.Redact).ToArray()));
        Add("Report details", $"LogLens {version} · Goodwin Labs\nCreated: {created:O}\nWindows: {osVersion}\nScan period: {scan.Period.Start:O} to {scan.Period.End:O}\nCompleted: {scan.CompletedAt:O}");
        Add("Privacy — review before sharing", PrivacyNotice);
        Add("Incident summary", $"{scan.Incidents.Count} incidents from {scan.EventCount} relevant records. {(scan.Issues.Count > 0 ? "Partial results: collection limitations are listed below." : "Selected System and Application records were scanned within the configured limits.")}",
            string.Join("\n", scan.Incidents.GroupBy(i => i.Category).Select(g => $"{g.Key}: {g.Count()}")));
        foreach (var issue in scan.Issues) Add($"Collection limitation: {issue.Channel}", issue.Message);
        foreach (var incident in scan.Incidents)
        {
            Add($"{incident.Time:O} · {incident.Title}", $"Category: {incident.Category} · Severity: {incident.Severity}");
            Add("What Windows recorded", incident.Evidence.Select(Evidence).ToArray());
            if (incident.Context.Count > 0) Add("Nearby context — no causal relationship established", incident.Context.Select(Evidence).ToArray());
            Add("What it could mean", incident.Findings.Select(f => $"{Incident.Label(f.Classification)}: {f.Text}").ToArray());
            Add("What to try next", incident.Recommendations.Select((r, index) => $"{index + 1}. {r.Text}").ToArray());
        }
        Add("Diagnostic limitations", Limitations);
        Add("Original records and support", "Use Windows Event Viewer → Windows Logs → System or Application. Match the channel, provider, event ID and record ID printed above. Records may be cleared or overwritten. LogLens never executes event content.",
            "Repository: github.com/goodzeeak/loglens\nIssues: github.com/goodzeeak/loglens/issues\nPrivacy: github.com/goodzeeak/loglens/blob/main/docs/PRIVACY.md");
        return new(blocks);
    }
    private static string Evidence(DiagnosticEvent e)
    {
        // Evidence references survive export; arbitrary fields and localized messages do not.
        var details = new List<string> { $"{e.Time:O}\n{e.Reference}" };
        if (e.Field("BugcheckCode") is { Length: > 0 } code && code.Length <= 32 && code.All(c => char.IsAsciiHexDigit(c) || c is 'x' or 'X')) details.Add($"Recorded bug-check code: {code}");
        return string.Join("\n", details);
    }
}
