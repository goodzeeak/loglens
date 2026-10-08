# LogLens 0.1.0 — MVP release candidate

Goodwin Labs' local Windows diagnostic app translates selected recent event records into evidence-based incident reports.

- Unexpected restart, application failure/hang, WHEA, storage and display recovery incidents.
- Provider/channel/ID matching, exact-record deduplication and conservative correlation.
- Confirmed observations, conditional hypotheses and explicit missing-evidence statements.
- 24-hour, seven-day and 30-day read-only scans with cancellation and collection limits.
- Dark/light WPF dashboard, filters, evidence details and safe next-step guidance.
- Native Windows 11 title-bar theming, consistently sized inputs and themed dropdowns.
- Specific Stop/exception/storage error explanations and validated CPER processor/cache findings, including severity and processor ID where recorded.
- Minimized/redacted report preview, self-contained HTML export and summary copying.
- Portable self-contained Windows x64 packaging, checksums and Windows CI.

Hardening found and corrected a false-positive rule for healthy NTFS 98 records and a WPF theme inheritance issue. Both have regression coverage.

Known limitations: heuristic restart timing; delayed or incomplete WER metadata; no dump or vendor-specific MCA decoding; no automatic fixes; no diagnostic history; no signed executable; Windows 10 and accessibility/DPI matrix still require manual release validation. No production-readiness claim is made solely from passing tests.

No public release has been authorized or published as part of this implementation.
