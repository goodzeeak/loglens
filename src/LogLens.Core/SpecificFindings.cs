using System.Globalization;
using System.Text.RegularExpressions;

namespace LogLens.Core;

public static class SpecificFindings
{
    public static IReadOnlyList<Finding> For(IReadOnlyList<DiagnosticEvent> evidence)
    {
        var result = new List<Finding>();
        void Fact(string code, string text) => result.Add(new(code, EvidenceClass.ConfirmedObservation, text));
        var category = EventRules.Category(evidence[0]);
        if (category == IncidentCategory.UnexpectedRestart)
        {
            foreach (var e in evidence.Where(e => EventRules.Is(e, "Microsoft-Windows-Kernel-Power", 41)))
            {
                if (e.Field("BugcheckCode") == "0") Fact("no-stop-code", "Kernel-Power's BugcheckCode is 0: this record contains no Stop code. This does not prove a power-supply fault or rule out a blue screen recorded elsewhere.");
                if (e.Field("LongPowerButtonPressDetected").Equals("true", StringComparison.OrdinalIgnoreCase)) Fact("power-button", "Windows recorded a long power-button press. If you held the button because the PC froze, that describes the shutdown action, not why it froze.");
                if (e.Field("SleepInProgress") == "1") Fact("sleep-transition", "The Kernel-Power record says a sleep transition was in progress.");
            }
            foreach (var code in evidence.Select(StopCode).Where(c => c is > 0).Distinct())
            {
                var label = code switch
                {
                    0x124 => "WHEA_UNCORRECTABLE_ERROR — Windows stopped after a fatal hardware error report",
                    0x9F => "DRIVER_POWER_STATE_FAILURE — a driver did not complete a power-state request correctly",
                    0x116 => "VIDEO_TDR_FAILURE — Windows could not successfully recover the display driver after a timeout",
                    0x133 => "DPC_WATCHDOG_VIOLATION — a deferred procedure/interrupt routine exceeded its allowed time",
                    0x1A => "MEMORY_MANAGEMENT — Windows detected a serious memory-management error; this alone does not prove bad RAM",
                    0xA => "IRQL_NOT_LESS_OR_EQUAL — an invalid memory access occurred at a raised interrupt level",
                    0x3B => "SYSTEM_SERVICE_EXCEPTION — an exception occurred during a system service routine",
                    0xEF => "CRITICAL_PROCESS_DIED — a critical Windows process ended",
                    _ => "Stop code recorded; this code is not translated by this version of LogLens"
                };
                Fact("stop-code", $"Stop code 0x{code:X8}: {label}.");
            }
        }
        if (category == IncidentCategory.ApplicationCrash)
        {
            foreach (var exception in evidence.Select(e => e.Field("ExceptionCode")).Where(s => s.Length > 0).Distinct())
            {
                if (!uint.TryParse(exception.Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)) continue;
                var meaning = code switch { 0xC0000005 => "access violation: the application attempted an invalid memory access", 0xC0000409 => "fail-fast termination: Windows ended the application after an unrecoverable condition (the code alone does not prove a stack-buffer overrun)", 0xE0434352 => "a .NET exception reached Windows error reporting", 0xC000001D => "illegal instruction", 0xC00000FD => "stack overflow", _ => "exception code recorded" };
                Fact("exception-code", $"Exception 0x{code:X8}: {meaning}.");
            }
        }
        if (category == IncidentCategory.Storage)
        {
            foreach (var e in evidence.DistinctBy(e => (e.Provider, e.EventId)))
            {
                var meaning = e.EventId switch
                {
                    7 => "Windows reported a bad block on a storage device. This is a recorded read/storage failure, not merely an unexpected-restart message.",
                    11 => "The storage driver reported a controller error while communicating with a device.",
                    15 => "Windows reported that a storage device was not ready for access.",
                    51 => "Windows recorded a storage I/O error during a paging operation. Paging I/O can include ordinary file transfers; it does not identify bad RAM.",
                    153 => "A storage I/O operation had to be retried. The retry is confirmed; permanent drive failure is not.",
                    157 => "Windows recorded that a disk was unexpectedly removed or disconnected.",
                    129 => "The storage driver issued a device/controller reset after a request timed out.",
                    55 => "NTFS reported filesystem corruption. The record establishes a filesystem problem, not which physical component caused it.",
                    98 => "NTFS recorded a nonzero corruption action with warning/error severity; this is not the healthy informational form of Event 98.",
                    140 => "NTFS could not flush data to its transaction log, which can affect filesystem consistency.",
                    _ => "Windows recorded a storage error."
                };
                Fact("storage-detail", meaning);
                var device = e.Field("DeviceName");
                if (device.Length > 0) Fact("storage-device", $"Recorded storage target: {device}. Use this identifier to distinguish affected devices in Event Viewer.");
            }
        }
        return result.Distinct().ToArray();
    }
    public static uint? StopCode(DiagnosticEvent e)
    {
        if (EventRules.Is(e, "Microsoft-Windows-Kernel-Power", 41))
        {
            var text = e.Field("BugcheckCode");
            return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? uint.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) ? hex : null
                : uint.TryParse(text, out var number) ? number : null;
        }
        if (!EventRules.IsBugCheck(e)) return null;
        var field = e.Field("param1");
        if (field.Length > 4096) return null;
        var match = Regex.Match(field, @"^\s*0x([0-9a-fA-F]{1,8})(?:\s|\(|$)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return match.Success && uint.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
