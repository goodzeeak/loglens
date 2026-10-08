# LogLens v0.1.0 — Public Beta

**Draft release text only. No GitHub release or tag has been created.**

Understand why your Windows PC keeps crashing—and investigate selected application, driver, storage, network, service, update and startup failures with Goodwin Labs' free local diagnostic app.

This beta turns recognized Windows records into incidents, distinguishes observations from hypotheses, and offers one evidence-based investigation step at a time. Record an outcome, revisit local history and prepare a reviewed HTML report or voluntary diagnostic feedback. No cloud AI, account, telemetry, automatic uploads or automatic repair.

Download the Windows x64 portable ZIP and its SHA-256 file after publication. Extract the whole archive and open LogLens.exe. The .NET runtime is included. Scanning is read-only; some channels may be unavailable without optional elevation. Choose 24 hours, 7 days or 30 days.

Included: original LogLens logo/icon, light/dark WPF interface, cancellation, category/time/search filters, guided troubleshooting, editable local outcome history, redacted previews and self-contained HTML exports.

Coverage is deliberately selective. Network records do not prove an internet outage; a service failure may be irrelevant; an update or driver may subsequently recover. WHEA or Event 41 alone does not prove defective hardware. No dump analysis or general performance diagnosis. See [the coverage matrix](COVERAGE.md) and [validation record](VALIDATION.md).

Windows 11 x64 has local and automated validation. Windows 10 x64 is targeted but not manually validated; check the README for Microsoft's edition/build support limits. Clean-machine, accessibility, DPI and broader real-device checks remain open. This is a Public Beta, not a production-readiness certification.

The executable is unsigned and may trigger SmartScreen. Review source and provenance and follow your security policy. Do not disable antivirus or operating-system protections. A checksum confirms integrity, not that an executable is safe.

Found a misleading result? Use Prepare diagnostic feedback, review the redacted export and optionally open an issue. Do not post raw logs, dumps or private notes. No file is uploaded automatically.

Before publication, the owner must review the final passing CI run, local package checksum, remaining manual checks and release authorization. Keep the version **0.1.0** and mark the GitHub release as a prerelease.
