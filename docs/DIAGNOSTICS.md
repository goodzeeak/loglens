# Diagnostic model and accuracy contract

The engine accepts bounded immutable event snapshots and a UTC-based scan period. It has no WPF, process-launch or network dependency. Fixture expectations are defined in `tests/LogLens.Tests/AccuracyFixtures.cs` before UI implementation and are the release-blocking semantic gate.

## Rules

| Channel | Provider | IDs / restrictions | Classification |
| --- | --- | --- | --- |
| System | Microsoft-Windows-Kernel-Power | 41 | Unexpected restart; nonzero valid BugcheckCode confirms a recorded Stop error, not its cause |
| System | EventLog | 6008 | Unexpected restart |
| System | Microsoft-Windows-WER-SystemErrorReporting, BugCheck | 1001 | Recorded Stop error |
| System | Microsoft-Windows-WHEA-Logger | 1, 17, 18, 19, 20, 46, 47 | Hardware report; bounded CPER header/processor decoding |
| Application | Application Error | 1000 | Application failure |
| Application | Application Hang | 1002 | Hang/termination |
| Application | Windows Error Reporting | 1001 with recognized APPCRASH, BEX, BEX64, AppHangB1 or MoAppCrash type | Application failure report |
| System | disk | 7, 11, 15, 51, 153, 157 | Storage issue |
| System | storahci, stornvme, iaStorA, iaStorAC | 129 | Controller timeout/reset |
| System | Ntfs, Microsoft-Windows-Ntfs | 55, 140; 98 only with warning/error/critical level AND numeric nonzero CorruptionActionState | Filesystem issue |
| System | Display | 4101 | Driver timeout/recovery |

Unknown providers with familiar IDs never trigger these rules. NTFS 98 may be a healthy informational event; unknown/zero action state or informational level does not become an incident. This negative rule was added after native desktop validation exposed a false positive, and has four regression fixtures.

## Correlation

- Events outside inclusive absolute-time bounds are ignored. Offset-equivalent timestamps compare equally. Input is ordered by UTC time and stable record identity.
- Repeated channel/provider/event ID/record ID/UTC timestamp identities are deduplicated. Including time prevents a reused record number after log clearing from inheriting an old investigation. Missing IDs fall back to timestamp/structured-field content.
- Restart groups span at most 120 seconds from the first record and contain no repeated provider. A second Kernel-Power event starts a separate group. No transitive unbounded chaining.
- Application pairing permits complementary providers, never repeat Application Error records. Matching nonempty GUID report IDs pair records within 120 seconds. Conflicting valid GUIDs prevent fallback pairing. Otherwise the same nonempty app and module must match within 30 seconds. Missing metadata remains ambiguous.
- Same-provider storage/hardware/display events can aggregate within 60 seconds only when DeviceName and ID match. Broad grouping without device identity is avoided.
- Up to 20 hardware/storage/display records in the five minutes before a restart record are context, not causal findings. A record logged before the next startup may still have occurred after the actual crash.
- Each incident ID is deterministic from its evidence identities. Repeated runs and reversed input order are checked for every accuracy fixture.

## Bounds and limitations

Collection reads at most 5,000 candidates per channel plus one sentinel, newest first, within 1/7/30 days. Native queries bound timestamps and candidate event IDs; managed rules enforce full identity. Native reads use a one-second timeout; cancellation is checked between records, not inside an OS call. Individual XML is capped at 128 Ki characters, 64 fields, 2,048 characters per ordinary value; WHEA RawData is bounded to 65,536 hexadecimal characters for decoding. A total retained-payload budget of 8 Mi characters bounds collection memory. DTDs and external XML entities are forbidden. The engine rejects more than 10,000 inputs.

The MVP decodes only validated CPER headers, section types and generic-processor errors. It does not decode vendor-specific MCA registers, parse localized date strings inside EventLog 6008, infer exact shutdown time, inspect dump files, ingest arbitrary channels or identify every vendor-specific display/storage event. Delayed reports, missing report IDs, reboot boundaries and closely spaced unrelated events remain ambiguous. In particular, distinct complementary restart events within two minutes can still be combined. Grouping is disclosed as heuristic. No group claims a proven cause.

Structured named fields are preferred. Unnamed legacy payloads are retained locally as Data0/Data1/etc.; they are not guessed from localized message text. Unknown field layouts can limit application/module identification.

## Sources

### Specific observations added during desktop feedback

Validated CPER records identify their own fatal/recoverable/corrected/informational severity, recognized section types, and generic-processor cache/TLB/bus/microarchitecture error categories. Processor ID and hierarchy level appear only when their validity bits are set. All lengths, revisions, section bounds and overlaps are validated; malformed records fall back to unspecialized findings. Informational CPER records are excluded from failure incidents. A processor error can narrow the investigation without proving defective CPU silicon. Platform-memory section presence alone does not prove bad RAM.

The decoder uses Microsoft's `cper.h` and `cperguid.h` layouts and constants. It deliberately avoids vendor-specific MCA interpretation, memory addresses, CPU serial/brand strings and unvalidated fields. Raw data is never included in exported reports. Tests cover all four severities, recognized/unknown error kinds, missing validity bits, every truncated record prefix and invalid offsets/revisions.

Recognized Stop codes and application exception codes are translated into their specific recorded failure categories. Kernel-Power zero Stop code, held power button and sleep-transition fields are described separately. Storage events distinguish bad-block reports, controller errors, retries, surprise removal, reset/timeouts and filesystem corruption. These are observations of Windows' report, not invented root-cause probabilities.

- [Microsoft CPER layout and numeric constants](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/cper.h)
- [Microsoft CPER section GUIDs](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/cperguid.h)
- [WHEA generic processor fields](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddk/ns-ntddk-_whea_processor_generic_error_section)
- [WHEA error severity](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntddk/ne-ntddk-_whea_error_severity)

- [Microsoft: Event 41 is an unexpected shutdown indicator](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/event-id-41-restart)
- [Microsoft: storage error troubleshooting](https://learn.microsoft.com/en-us/troubleshoot/windows-server/backup-and-storage/troubleshoot-data-corruption-and-disk-errors)
- [Microsoft: EventLogReader](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.eventing.reader.eventlogreader)
- [Microsoft: Windows Hardware Error Architecture](https://learn.microsoft.com/en-us/windows-hardware/drivers/whea/)

Source descriptions guide conservative observations, not automatic hardware diagnoses. Native provider metadata and synthetic structured fixtures are used to validate field handling.


The broadened v0.1.0 module identities, workflow rules, collection sources, fixture counts and limitations are documented in [COVERAGE.md](COVERAGE.md). The collector also reads DHCP Admin, caps total retained events at 10,000, and emits a partial-result warning when that limit is reached. Native XPath selections are split into bounded chunks in a structured QueryList. The 36 original accuracy fixtures remain unchanged; BroadAccuracyFixtures adds 20. Raw scan events are ephemeral; only explicit user investigation entries persist.

Service Control Manager 7000 param2 tokens %%2, %%3 and %%5 use the meanings in Microsoft's [system error code reference](https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-). The file-not-found message does not identify the missing file. Other SCM event schemas are not interpreted as error codes.
