# LogLens 0.1.0 — Public Beta

Goodwin Labs' local diagnostic application now covers selected failures across system instability, applications, hardware, display/device drivers, storage, networking, services, Windows Update and fast startup. See COVERAGE.md for exact sources, tested behavior and unsupported areas.

- Independent diagnostic modules with provider/channel/ID matching, structured evidence and conservative correlation.
- Guided investigation: known facts, unknowns, one next step, reason, safety, outcome meanings and follow-up.
- Local editable outcome history, persisted without raw event logs; delete individual entries or clear all.
- Reports optionally include reviewed history; free-text notes require separate review and opt-in.
- Voluntary diagnostic feedback export with rule IDs and minimized evidence; no automatic upload or issue creation.
- Category, time and search filters; existing native title-bar theming and matched controls retained.
- Original LogLens lens/log logo, embedded multi-resolution Windows icon and reusable SVG/PNG assets.
- 56 predetermined accuracy fixtures, including the 36 preserved original fixtures and 20 added broad-coverage cases.
- Self-contained Windows x64 packaging, checksums, read-only collection and Windows CI.

Known limits: selected events only; heuristic restart timing; incomplete WER identity; no dump or vendor MCA decoding; no general performance diagnosis; no comprehensive installer/servicing/boot-recovery parser; history is exact-incident scoped and may remain separate when evidence grouping changes; no automatic fixes; no signed executable. Windows 10, clean-machine and accessibility/DPI checks remain manual validation work.

No public release is authorized by this preparation. Version remains 0.1.0. See VALIDATION.md and RELEASE_DRAFT.md before any release action.
