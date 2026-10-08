# Release procedure

Publishing needs explicit owner authorization. Merely pushing application source or running CI is not release authorization.

1. Run the full accuracy suite, build/analyzers and native Windows integration test. Any misleading diagnostic fixture is a release blocker. Correct the rule, add a regression fixture and rerun the full suite.
2. Complete the outstanding manual checks in VALIDATION.md, including supported Windows 10/11 x64, ordinary non-admin launch, high DPI, keyboard/screen reader, clean-machine extraction and offline use.
3. Update Directory.Build.props, MainViewModel.Version, branding and release notes together. Run `pwsh ./scripts/package.ps1 -Version 0.1.0`.
4. Inspect the ZIP and SHA-256 file. The ZIP must contain the application, the full desktop runtime, README, license and documentation; no diagnostic data, settings, source build intermediates or secrets.
5. Ensure the GitHub environment **github-release** requires owner review. If protection is unavailable or absent, do not tag. Set no paid runner or spending budget.
6. Only after explicit release authorization, create and push the matching `v0.1.0` tag. The tag triggers build/test/package. Publishing waits at the protected environment for review.
7. Approve the release environment only after inspecting the successful build and intended commit. The final job uploads the ZIP and checksum to GitHub Releases with notes. It verifies that the tag already exists and never creates an unreviewed tag.

The workflow marks 0.x releases as prereleases. There is no signing certificate or paid infrastructure. Explain SmartScreen transparently. Never claim a checksum proves the executable is safe or instruct users to bypass antivirus/OS protections.

For local integrity checking:

```powershell
Get-FileHash ./LogLens-0.1.0-win-x64.zip -Algorithm SHA256
```

Compare the hash with the published `.sha256` file obtained from the same intended release. This detects accidental changes; it is not a substitute for publisher trust or a digital signature.
