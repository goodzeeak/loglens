# LogLens privacy policy

Effective 8 October 2026. Publisher: Goodwin Labs.

LogLens processes diagnostic records locally on your computer. The MVP has no telemetry, accounts, analytics, advertisements, upload endpoint, cloud AI, background monitoring or automatic updates. Normal scanning does not require internet.

## Data read and stored

The app reads selected Windows System and Application log records within the requested period, with a maximum of 5,000 candidate records per channel. It keeps bounded structured fields and record identifiers in memory. It does not intentionally retain raw XML, localized event messages, the event's Computer field, or Security/UserID field. Some structured payload fields can still contain private information.

Scan duration and theme are stored under `%LOCALAPPDATA%\Goodwin Labs\LogLens\settings.json`. Diagnostics are not written to this file. The app has no automatic diagnostic history. Reports are written only to a location you select. Clipboard copying is explicit and subject to Windows clipboard history/synchronization and other apps' access.

## Report minimization

Reports omit arbitrary event payloads and retain evidence identifiers, relevant application/module descriptions, a bounded validated bug-check code where available, findings and recommendations. The redactor removes common Windows paths, URLs, email addresses, IP-like strings, SIDs and known current user/machine/domain identifiers. All event-derived HTML text is escaped. Reports contain no scripts or external resources.

This is **not a guarantee of anonymization**. Timestamps, program names, unusual identifiers and indirect context can still identify you or your device. A content preview appears before export. Review the complete preview and saved file before sharing. Raw details shown locally in incident details are not redacted. Do not publish crash dumps without a separate privacy review; LogLens does not analyze or sanitize dumps.

## External actions

The repository, issue and privacy links open GitHub in your browser only when selected; GitHub and your browser have their own privacy policies. LogLens does not attach event data to those links. Event Viewer and Reliability Monitor are local Windows tools opened only when selected.

Uninstall by deleting the extracted application directory. Delete saved reports and the settings directory separately if desired. Windows itself may retain execution history, event logs or crash diagnostics under its own policies.

For questions, [open an issue](https://github.com/goodzeeak/loglens/issues) without personal diagnostic data.
