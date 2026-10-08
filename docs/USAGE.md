# Using LogLens

Launch the extracted `LogLens.exe` as your ordinary Windows user. Select the scan duration (1, 7 or 30 days) and click **Scan My PC**. Progress describes the log currently being read. **Cancel** stops collection or analysis; the last completed scan remains visible and is not replaced by partial cancelled data. The scan period is frozen at scan start.

The dashboard reports incident count, latest recorded unexpected restart and most frequent identified failing application. Incidents include warnings as well as crashes: **incident count is not crash count**. Application names are grouped case-insensitively. Restart times are record/startup times, not necessarily the shutdown time.

Use the search box for application names, explanations, providers or event IDs. Use category and last-1/7/30-days filters to narrow the completed scan. The time filter is relative to the scan end; it does not collect additional records. The newest records appear first. Select an incident with the mouse or keyboard arrows. Details and supporting records can be scrolled and selected for inspection.

- **Confirmed observation** comes from recognized Windows records.
- **Possible cause** is a conditional hypothesis, not an established cause.
- **Insufficient evidence** states what cannot be concluded.

Nearby hardware events are explicitly context. They may also have their own timeline incident; context references do not add extra incidents. Windows may only record a power interruption or forced reset as an unexpected shutdown. Event 41 alone cannot diagnose the PSU, RAM, GPU or a driver.

**Event Viewer** opens Windows' viewer. Navigate to Windows Logs → System or Application, then match the displayed provider, event ID and record ID. Those identifiers preserve the original-record reference even if the list order changes. Logs can be cleared/overwritten, so references may expire. **Open Reliability Monitor** opens Windows' history view; it does not change settings.

**Preview / export report** includes the entire completed scan, regardless of timeline filters. Read the full content preview before choosing **Save HTML report**. The saved self-contained HTML opens in a browser without internet or JavaScript. **Copy summary** copies the selected incident with context and privacy limitations; the clipboard is managed by Windows and other apps may read or synchronize it.

Choose **Light** or **Dark** at any time. Theme and scan duration persist automatically. Explicitly saved investigation outcomes persist separately. Windows high-contrast colors override the theme at startup/theme application. Scan records themselves are not persisted.

If access is denied, LogLens labels the scan partial. You may leave it that way. The optional **Reopen as administrator** button starts another instance and Windows requests UAC consent. LogLens itself never accepts that prompt. Scan again in the new instance if you choose to proceed.

## Guided investigation and history

Select **Troubleshoot This Incident**. Read the known facts, unknowns, one next step, reason, precautions, outcome meanings and follow-up. Perform the step yourself only if applicable; LogLens never runs it. Choose Issue recurred, Issue did not recur, Inconclusive or Skipped, the local date/time performed, and optional notes. Save the outcome to update guidance. Non-recurrence asks you to observe; if it later recurs, select the saved entry and update its outcome.

Select a history entry to edit its outcome, date or notes. **New outcome** returns to the current step. **Delete selected** removes one entry; **Clear all history** asks before deleting all saved entries. **Show history for all incidents** and the dashboard's **Local history** make older records accessible even outside the current scan. Only the exact associated incident influences guidance. Similar symptoms do not prove the same cause.

History is limited to 500 entries and notes to 2,000 characters per entry. It contains no raw logs but notes can be private. A corrupt/unreadable file is preserved and saving is disabled until you restore it or explicitly clear history.

## Reviewed exports

Reports cover the full scan. The first dialog lets you opt into its associated investigation history. Notes are excluded unless you review the displayed original notes and explicitly select inclusion. The following preview redacts common identifiers; review every section before saving HTML. Outcomes remain user reports, not verified Windows observations.

**Prepare diagnostic feedback** exports the selected incident, rule IDs and your expected behavior through a separate preview. It never uploads or creates an issue. See FEEDBACK.md.

DHCP records can be located in Event Viewer under Applications and Services Logs → Microsoft → Windows → Dhcp-Client → Admin. Historical errors do not prove the current problem persists; compare later recovery or successful attempts in the relevant Windows tools.
