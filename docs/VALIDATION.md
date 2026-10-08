# Validation and release readiness

Date: 8 October 2026. Status: **MVP release candidate; not production-ready pending the remaining manual matrix and owner release approval.**

## Executed locally

Environment: Windows 11 x64, OS build 26200. Workspace-local .NET SDK 10.0.401, desktop/runtime 10.0.12. No pre-existing SDK was available. NuGet restores use nuget.org. Product collection is read-only and never intentionally crashes the machine.

| Check | Result |
| --- | --- |
| Predetermined diagnostic accuracy fixtures | **36 passed, 0 failed** |
| Complete xUnit suite | **98 passed, 0 failed, 0 skipped** |
| Release build with .NET and xUnit analyzers; warnings treated as errors | Passed, zero warnings/errors |
| Native EventLogReader integration, previous 24 hours | Passed on the local Windows host |
| WPF STA workflow regression | Passed: initial state, scan, search/filter, copy, preview, light/dark brushes, cancellation preserving prior result |
| NuGet vulnerability audit, including transitive packages | No known vulnerable packages reported by configured sources |
| Self-contained win-x64 publish | Passed |
| Interactive real seven-day scan | Passed; led to the NTFS correction described below |
| Synthetic HTML report opened in a browser | Passed; evidence, uncertainty, recommendations and privacy notice rendered correctly |
| Dark/light main-window, small-window and report-preview layout | Inspected using synthetic WPF renders; dark native title bar and matched controls also visually verified in the updated running Windows 11 build |
| 10,000-incident processing limit | Passed; approximately 1.8 seconds locally in the initial stress run (not a hardware-independent guarantee) |
| GitHub Windows CI on initial implementation | Passed: [run 37725500356](https://github.com/goodzeeak/loglens/actions/runs/37725500356); later refinement requires a new run |

## Accuracy findings and corrections

Before UI implementation, 24 fixtures and six additional engine checks passed. During real desktop testing, healthy informational NTFS 98 records were incorrectly classified as storage incidents. The rule was corrected to require a warning/error/critical level and a valid nonzero structured CorruptionActionState. Four negative/positive regression fixtures cover healthy, unknown and actionable states, including restart context. The entire suite passed after the correction.

A visual check also found WPF's implicit Window style was not applied to the derived windows. Explicit style references corrected dark/light backgrounds and inherited text colors. The STA regression now asserts both main-window and report-preview theme brushes.

User desktop feedback prompted native title-bar dark/light styling, matched 40-pixel input/dropdown heights and themed dropdown templates. It also exposed the need for specific hardware explanations. A bounded CPER decoder now reads documented severity and generic-processor fields, so supported records can identify a fatal processor cache error and validated processor ID rather than only showing a hardware warning. The additional tests and eight accuracy fixtures validate these details and negative cases. No personal raw diagnostic payload has been committed or uploaded.

Fixture contracts explicitly define category, confirmed observation codes, permitted hypotheses, expected recommendations, evidence/context counts and prohibited causal conclusions. Every fixture is checked across repeated execution and reversed input order. Additional tests cover wrong provider/channel IDs, time bounds, collection access/missing/corrupt conditions, oversized XML, DTD rejection, record limits, cancellation, privacy minimization and HTML encoding.

## Remaining manual release checks

- Windows 10 x64 on a qualifying edition/build, and a second clean Windows 11 machine without a developer SDK.
- Explicit standard-user account scan and optional UAC elevation/cancel flow. No UAC/security prompt is automatically accepted by test automation.
- Keyboard-only and screen-reader experience, Windows high contrast, 125/150/200% DPI, small displays and multiple monitors.
- Long-running storage-heavy history, mid-read native cancellation and restricted/corrupt real log environments. Synthetic faults are already covered.
- Offline clean-machine extraction and export, locale matrix, externally cleared logs, two rapid real restarts and delayed WER behavior.
- Owner review of evidence language, executable provenance/SmartScreen experience and release authorization.

## Unresolved diagnostic limitations

Passing synthetic tests does not establish diagnostic reliability across all Windows devices. Restart grouping uses record-time heuristics and cannot always separate adjacent restarts or match delayed bug-check reports. Missing/contradictory application metadata may remain separate or unidentified. WHEA decoding is limited to validated CPER headers, section types and generic-processor fields; vendor-specific machine-check registers and dumps are not decoded. Unknown vendor-specific events are excluded. No event group proves a hardware component is defective or that a nearby event caused a restart. Reports minimize and redact common identifiers without promising perfect anonymity.

See DIAGNOSTICS.md for the exact supported identities and bounds. CI outcomes, interactive export checks and package checksum are recorded separately in the handoff once completed; this document must not imply unexecuted checks have passed.
