# LogLens

![LogLens icon](branding/LogLens.svg)

**Understand why your Windows PC keeps crashing.** A free, local-first Windows diagnostic app by **Goodwin Labs**.

LogLens reads selected System, Application and DHCP Admin event records, groups related records into incidents, and separates confirmed observations from possible explanations and missing evidence. It never automatically repairs your PC.

**Status: v0.1.0 Public Beta preparation; not production-certified.** See [validation](docs/VALIDATION.md) for checks actually performed and the remaining manual release gate. No public binary release has been authorized.

## Run

1. Extract the complete `LogLens-0.1.0-win-x64.zip` archive into a folder you can access.
2. Open `LogLens.exe`. The portable package includes the .NET runtime; no installation, account or administrator privileges are normally needed.
3. Click **Scan My PC**. The default is seven days; choose 1 (24 hours), 7 or 30 scan days.
4. Select an incident and read **What Windows recorded**, **What it could mean**, and **What to try next**.
5. Choose **Troubleshoot This Incident**, perform a relevant step, then record its outcome. Local history can be edited or deleted.
6. Choose **Preview / export report**, review the minimized and redacted text, then save HTML. Nothing is uploaded.

Windows 11 x64 is the primary tested platform. Windows 10 x64 compatibility is targeted, but not yet manually validated. Microsoft's .NET 10 support policy covers only qualifying Windows editions/builds; ordinary Windows 10 Home/Pro should not be described as fully supported by Microsoft. Check the [official .NET OS support matrix](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

Unsigned executables may trigger Microsoft Defender SmartScreen. A checksum checks file integrity, not trustworthiness or safety. Review the source and publisher, and follow your organization's security policy. Do not disable antivirus or Windows security protections to run LogLens.

## Privacy and safety

- No cloud processing, telemetry, uploads, ads, background service, startup task or automatic system changes.
- Ordinary scans are read-only. LogLens runs with the invoking user's permissions.
- If a channel denies access, a visible optional action opens a separate elevated instance through the Windows UAC prompt. It never silently elevates. The original scan remains open.
- Raw diagnostic data stays in memory until the app closes. Scan duration/theme settings and explicitly saved investigation history persist under `%LOCALAPPDATA%\Goodwin Labs\LogLens`.
- HTML reports and copied summaries minimize and redact data, but can still identify a person or device. Review before sharing. Local evidence details have not been redacted.
- Browser links, Event Viewer and Reliability Monitor open only when clicked. Normal scans require no internet.

Read [usage](docs/USAGE.md), [troubleshooting](docs/TROUBLESHOOTING.md), [privacy](docs/PRIVACY.md), [diagnostic rules](docs/DIAGNOSTICS.md), and [release notes](docs/RELEASE_NOTES.md).

See the [coverage matrix](docs/COVERAGE.md) for selected networking, services, update, boot, device, application, storage and hardware detections. General performance diagnosis is unsupported. [Diagnostic feedback](docs/FEEDBACK.md) is a voluntary, reviewed export.

## Build and test

Requires Windows x64 and a .NET 10 SDK with WPF support. No paid tools or services are required.

```powershell
dotnet restore LogLens.sln
dotnet build LogLens.sln -c Release --no-restore
dotnet test LogLens.sln -c Release --no-build
pwsh ./scripts/package.ps1
```

`package.ps1` runs the full test gate, publishes the self-contained application, checks required runtime files, and creates a ZIP and SHA-256 checksum in `artifacts/`. It does not upload anything. Tests use xUnit, synthetic fixtures and a read-only native Windows collection test. WPF tests run in an STA dispatcher and do not require crashing the machine.

## Architecture

| Project | Responsibility |
| --- | --- |
| `LogLens.Core` | Immutable diagnostic models, deterministic rule/correlation engine, scan orchestration and safe reports; no Windows or UI dependency |
| `LogLens.Windows` | Bounded, cancellable EventLogReader collection and secure structured XML parsing; replaceable reader seam |
| `LogLens.App` | WPF/MVVM dashboard, themes, progress, filtering, preview/export and explicit platform actions |
| `LogLens.Tests` | Predetermined accuracy fixtures, negative cases, collection faults, privacy, HTML safety and desktop workflow regression |

Future importers can implement `IEventCollector`; richer rules and report projections can evolve independently of WPF. Dump analysis, technician case management, multi-machine support, authentication and billing are deliberately outside this MVP.

## Contribute / release

[Repository](https://github.com/goodzeeak/loglens) · [Report an issue](https://github.com/goodzeeak/loglens/issues) · [Privacy](docs/PRIVACY.md)

Do not attach unreviewed logs, raw XML or crash dumps to public issues. Use a redacted report and explain the expected versus actual behavior.

GitHub Actions builds and tests on Windows. Version tags build release assets; publishing additionally requires the protected `github-release` environment approval. See [release procedure](docs/RELEASING.md). No version tag or release should be created before the owner authorizes it.

Copyright © 2026 Goodwin Labs. [MIT license](LICENSE).
