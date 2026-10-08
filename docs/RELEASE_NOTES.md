# LogLens v0.1.0 — Public Beta

## Introducing LogLens

LogLens is a free, privacy-focused Windows diagnostic tool from Goodwin Labs. It helps you understand recorded system errors and investigate common PC problems, while keeping facts separate from possible explanations.

## Features

- Scan the previous 24 hours, 7 days or 30 days of selected Windows diagnostic events.
- Review meaningful incidents across system restarts, application failures, hardware reports, display/device drivers, storage, networking, services, Windows Update and fast startup.
- Read what Windows recorded, what it could mean and what remains unknown.
- Follow guided troubleshooting steps and record outcomes in editable local investigation history.
- Preview and export self-contained HTML diagnostic reports, with optional reviewed history and notes.
- Prepare diagnostic feedback for issue reporting, including relevant rule identifiers.
- Use dark or light themes, search/category/time filters, cancellation and the new LogLens icon.
- Work offline without an account. The portable package includes the .NET desktop runtime.

## Privacy

Diagnostic processing stays on your PC. There are no automatic uploads, accounts, telemetry, advertisements or background monitoring services. Report exports are user-controlled. Redaction is best effort: review every exported report and note before sharing. LogLens never automatically changes drivers, services, registry settings or firmware.

## Installation

1. Download **LogLens-0.1.0-win-x64.zip** below.
2. Extract the complete archive into a folder you can access.
3. Open **LogLens.exe**, keeping its supporting files together.
4. Select **Scan My PC**.

The `.sha256` attachment lets you check ZIP integrity using PowerShell's `Get-FileHash -Algorithm SHA256`. A matching checksum does not establish executable safety.

## Important limitations

This is beta software. LogLens cannot guarantee identification of a root cause: some problems leave insufficient evidence, and nearby events do not prove causation. Coverage is selective; crash-dump analysis and general performance diagnosis are not included. Some event channels may require additional permissions; elevation is always optional and explicit.

Windows 11 x64 has local and automated validation. Windows 10 compatibility, clean-machine testing and accessibility/DPI validation remain limited or ongoing. The .NET 10 supported Windows editions/builds also vary; read the [compatibility notes](https://github.com/goodzeeak/loglens/blob/v0.1.0/README.md) before use. Do not interpret passing automated tests as completion of the manual validation matrix.

The release gate includes **146 passing automated tests**, including **56 predetermined diagnostic accuracy fixtures**. See the [coverage matrix](https://github.com/goodzeeak/loglens/blob/v0.1.0/docs/COVERAGE.md) and [validation record](https://github.com/goodzeeak/loglens/blob/v0.1.0/docs/VALIDATION.md).

## Security

The executable is unsigned. Microsoft Defender SmartScreen may show a warning for an unsigned or unfamiliar application. Review its source and provenance and follow your organization's security policy. Do not disable antivirus or operating-system security protections to run LogLens.

## Feedback

Please report bugs or misleading findings at [GitHub Issues](https://github.com/goodzeeak/loglens/issues). Use **Prepare diagnostic feedback** when useful, review the redacted export and share only information you intend to make public. Never attach raw crash dumps, credentials or unreviewed logs. LogLens does not upload feedback or create issues automatically.
