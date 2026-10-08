# Troubleshooting LogLens and interpreting results

## The app does not start

Extract the **whole** ZIP; keep its DLLs and runtime files together. Use Windows x64. No separately installed .NET runtime is required for the portable package. Check the release checksum and the OS support notes in the README. An unsigned binary may trigger SmartScreen; do not disable antivirus or OS security protections. If your policy blocks it, stop and consult your administrator.

## Empty results

Try 30 days and clear search/category filters. LogLens recognizes a deliberately limited set of event identities, not every Windows event. Logs may have been cleared or overwritten; a hard power interruption may leave no useful explanation. Empty results do not prove the PC is healthy.

## Partial results or inaccessible logs

The dashboard and report show collection limitations. If access is denied, you may explicitly reopen as administrator through the provided button and accept Windows' own UAC prompt yourself. Elevation is optional. Missing/corrupt logs and skipped oversized records are reported without silently ignoring the limitation. A 5,000-candidate-record limit per channel keeps work bounded; choose a shorter period if reached.

## Why no definitive cause or percentage?

Windows records often describe symptoms. Event 41 means an unclean shutdown, not a defective PSU. WHEA reports can reflect corrected errors and firmware/tuning interactions; a recorded component is not automatically faulty. A faulting module is where the application failure surfaced. Nearby events do not establish causation. LogLens does not invent confidence percentages or inspect crash dumps.

## Duplicate or separated incidents

Exact record copies are deduplicated. Complementary restart providers within two minutes are grouped; repeated records from the same provider stay separate. This is heuristic, especially around closely spaced reboots or delayed BugCheck reports. Application records use matching nonempty GUID report IDs, or matching application and module within 30 seconds. Ambiguous records can remain separate rather than being forced together.

## Safe next steps

Follow the recommendations specific to your incident. Record changes and test one at a time. Back up files before investigating recurring storage errors. Save work before Windows Memory Diagnostic. Driver changes and stock-clock testing are user decisions; LogLens does not perform them. A passing diagnostic test cannot rule out every intermittent problem.

## Export problems

Choose a writable folder and verify free disk space. Review the exported HTML before sharing. It deliberately omits raw messages and payloads. Reports are plain local files; no cloud upload occurs. If clipboard access temporarily fails, close the competing clipboard application and try again.
