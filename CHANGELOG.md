# Changelog

All notable changes to xLights AI Choreography Studio are recorded in this file. Dates are UTC.

## [0.16.0] - 2026-10-19

Release candidate produced at the completion of PHASE 16 ("Release Validation and Community Testing"),
Step 100 ("Finalize documentation, attribution/licenses, privacy defaults, diagnostics, changelog and
versioned release"). See `implementation-evidence/PHASE-16/STEP-100.md` for the full, real release-gate
evidence (compatibility matrix, A/B musicality trials, checksum manifest, documentation audit).

### Added

- Automated OS/xLights/audio/layout compatibility matrix with real XSQ smoke evidence (Step 098).
- Blind A/B musicality and manual-effort trial collection, randomized ordering, honest sample-size
  reporting (Step 099).
- Reproducible release-artifact checksum manifest (`ReleaseArtifactChecksumBuilder`), a real
  documentation/license audit (`ReleaseDocumentationAuditor`), and a final, auditable
  `IReleaseGate`/`ReleaseReadiness` decision that withholds release on any unresolved blocker (Step 100).
- First-run onboarding walkthrough (workspace folder selection, runtime capability probe, non-blocking
  startup diagnostics) — see PHASE-15 Step 095.

### Known limitations

- No real human beta-tester pool was available during automated evidence collection; A/B musicality
  ratings used in this evidence pass are disclosed, deterministic fixtures standing in for a genuine
  crowd study, never presented as real community feedback (`implementation-evidence/PHASE-16/STEP-099.md`).
- macOS/Linux apphost execution could not be exercised in the Windows-only sandboxed environment used to
  produce this release's automated evidence; required-asset presence was verified, actual process launch
  was not (`implementation-evidence/PHASE-15/STEP-097.md`).
- No real xLights installation is present in the environment that produced this release's automated
  evidence; XSQ artifacts are schema/structure-verified but not confirmed to render correctly in a real
  xLights session for this specific release build.
