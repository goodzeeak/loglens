# Validation and beta readiness

8 October 2026. **v0.1.0 Public Beta preparation; not production-certified.** Public release/tag creation remains owner-authorized only.

## Independently verified baseline and current checks

The starting commit was 1f6c72afa0f3d303980ffe4b4c8ed0fddea4bce2. Its 98 tests and 36 diagnostic fixtures passed again before changes. GitHub tags/releases were empty before implementation and again before packaging. Version remains 0.1.0 in project and UI metadata. The github-release environment still requires owner review.

| Check | Result |
| --- | --- |
| Mandatory semantic fixtures | **56 passed, 0 failed** (36 preserved + 20 new) |
| Complete xUnit suite | **146 passed, 0 failed, 0 skipped** |
| Release build and enabled .NET/xUnit analyzers, warnings as errors | Passed |
| Native read-only EventLogReader integration | Passed locally on Windows 11 x64 build 26200; System/Application/DHCP Admin paths exercised |
| WPF STA workflow | Passed: scan/search/filter/copy/preview/themes/cancellation; investigation save and next-step transition; export/feedback window construction |
| History | Persist/reload/edit/delete/clear, non-recurrence/inconclusive/skipped handling, exact incident scope, corrupt data preservation tested |
| Reports / feedback | Notes omitted by default, opt-in redaction, HTML injection prevention, arbitrary field/DNS-name minimization tested |
| Icon | Nine embedded ICO frame dimensions tested; WPF assembly-qualified resource path corrected after regression caught test-host startup failure |
| Synthetic visual QA | Main dashboard, dark/light investigation, small-window layouts, export options and feedback renders inspected |
| NuGet vulnerability audit including transitive packages | No known vulnerable packages reported by configured source on this date |
| Native desktop smoke test | Portable launch, embedded icon, dark title bar, real seven-day scan and service-specific guided window verified on Windows 11 |
| Self-contained publish / ZIP / CI | Run by scripts/package.ps1; final run links and exact deliverable checksum recorded in the delivery report |

Native visual testing caught doubled padding in the custom TextBox template that clipped the 40-pixel date input. The redundant content-host margin was removed; standard TextBox padding remains.

Windows CI exposed a regression in the 10,000-event test: repeatedly searching and classifying every old group exceeded the unchanged 20-second limit. Correlation now caches module classification and discards expired candidate groups using the declared anchored windows. The original performance test and all 56 accuracy fixtures pass after the correction.

The local SDK is .NET 10.0.401 with runtime 10.0.12. New provider meanings and channel mappings were inspected from Microsoft-shipped provider metadata. No real diagnostic payloads were committed. CI test output does not include private event fields.

Every mandatory fixture asserts expected category, complete confirmed-finding codes, permitted hypothesis codes, recommendations and correlation counts; it checks prohibited causal conclusions and deterministic repeated/reversed input. General performance diagnosis is unsupported, not inferred from passing tests.

## Accuracy corrections retained

The original NTFS 98 false-positive fix, CPER validity/bounds checks and ambiguous report-ID rules remain covered. Broad fixtures include successful updates ignored, wrong-provider/channel IDs, missing service identity, duplicate events, malformed optional metadata and unrelated network/service events near a restart. Record timestamps now participate in persistent incident identity so reused event numbers after log clearing do not inherit history.

## Remaining manual matrix

- Windows 10 x64 on a qualifying edition/build; second clean Windows 11 host without an SDK.
- Standard-user account and optional UAC consent/cancel flow; automation never accepts security prompts.
- Keyboard-only complete workflow, screen reader, high contrast, 125/150/200% DPI and multiple monitors.
- Actual failures from every new provider/category, alternate schemas/locales, delayed WER and close real restarts; no destructive failure injection is required or performed.
- Clean offline extraction, OS Save dialog/export interaction, SmartScreen provenance review, restricted/corrupt real channels and mid-native-read cancellation.
- Owner review of diagnostic language and authorization to publish.

A successful build, synthetic UI render or positive test count does not establish these unperformed checks. Consult COVERAGE.md for the implemented/tested, limited and unsupported distinctions. Local handoff records any additional interactive checks performed after this document was prepared.

## Diagnostic limitations

Restart correlation is heuristic. Current logs cannot prove every freeze, physical component failure or internet outage. Optional app/service/update/device fields can be missing. Selected update/boot detection is not comprehensive servicing or recovery analysis. General performance measurement, crash dumps and vendor MCA interpretation remain unsupported. Similar incidents do not share history automatically; if evidence grouping changes at a scan boundary, the prior entry remains available in All history. Redaction is conservative but cannot promise anonymity.

UI polish regression checks cover singular day wording, short history accessibility names, history-only layout, and horizontal/vertical scrollbar page commands. Known SCM 7000 error tokens 2, 3 and 5 are translated only in their documented field; unrelated schemas and malformed tokens remain unchanged.
