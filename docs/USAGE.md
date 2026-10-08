# Using LogLens

Launch the extracted `LogLens.exe` as your ordinary Windows user. Select the scan duration (1, 7 or 30 days) and click **Scan My PC**. Progress describes the log currently being read. **Cancel** stops collection or analysis; the last completed scan remains visible and is not replaced by partial cancelled data. The scan period is frozen at scan start.

The dashboard reports incident count, latest recorded unexpected restart and most frequent identified failing application. Incidents include warnings as well as crashes: **incident count is not crash count**. Application names are grouped case-insensitively. Restart times are record/startup times, not necessarily the shutdown time.

Use the search box for application names, explanations, providers or event IDs. Use the category filter to narrow the timeline. The newest records appear first. Select an incident with the mouse or keyboard arrows. Details and supporting records can be scrolled and selected for inspection.

- **Confirmed observation** comes from recognized Windows records.
- **Possible cause** is a conditional hypothesis, not an established cause.
- **Insufficient evidence** states what cannot be concluded.

Nearby hardware events are explicitly context. They may also have their own timeline incident; context references do not add extra incidents. Windows may only record a power interruption or forced reset as an unexpected shutdown. Event 41 alone cannot diagnose the PSU, RAM, GPU or a driver.

**Event Viewer** opens Windows' viewer. Navigate to Windows Logs → System or Application, then match the displayed provider, event ID and record ID. Those identifiers preserve the original-record reference even if the list order changes. Logs can be cleared/overwritten, so references may expire. **Open Reliability Monitor** opens Windows' history view; it does not change settings.

**Preview / export report** includes the entire completed scan, regardless of timeline filters. Read the full content preview before choosing **Save HTML report**. The saved self-contained HTML opens in a browser without internet or JavaScript. **Copy redacted summary** copies the selected incident with context and privacy limitations; the clipboard is managed by Windows and other apps may read or synchronize it.

Choose **Light** or **Dark** at any time. Theme and scan duration are the only automatic persistent settings. Windows high-contrast colors override the theme at startup/theme application. No diagnostic history is stored automatically.

If access is denied, LogLens labels the scan partial. You may leave it that way. The optional **Reopen as administrator** button starts another instance and Windows requests UAC consent. LogLens itself never accepts that prompt. Scan again in the new instance if you choose to proceed.
