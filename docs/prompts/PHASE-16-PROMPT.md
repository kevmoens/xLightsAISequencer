# Coding Prompt — Phase 16: Release Validation and Community Testing

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-16.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 16 (Release Validation and Community Testing) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-16/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 16 only (098–100).
Working product behavior: As a new xLights community user, I can install a released build, follow short onboarding, sequence a sample, review/edit it, export to my xLights version and understand any limitations.
Explicit exclusions: No guarantee every model type, xLights release, media codec or song genre behaves perfectly; no simulated community feedback passed off as real test results.
Inputs: Release candidate installer, supported xLights versions, fixtures, reference renders, beta testers with appropriate permission, license manifests.
Outputs: Release readiness report, compatibility matrix, meaningful blind evaluation results, onboarding/help, changelog, SBOM/notices, tagged version, support diagnostics.
Mandatory gate: Give a non-developer tester only the installer and demo show; observe install, import, song analysis or safe fallback, phrase editing, XSQ export and actual xLights rendering; record real feedback and cleanup effort.
Do not start Phase 17 without recorded validation.
```

## Steps to implement

- [ ] **098** Run tested OS/xLights/audio/layout compatibility matrix and record known limitations. **Acceptance:** Supported combinations each have installation and XSQ smoke evidence.
- [ ] **099** Conduct A/B musicality evaluations and manual-effort measurement on varied songs. **Acceptance:** Collect preference ratings and editing-time results, not only auto scores.
- [ ] **100** Finalize documentation, attribution/licenses, privacy defaults, diagnostics, changelog and versioned release. **Acceptance:** Reproducible artifacts and first-user walkthrough tested.

## Tests specifically required

- [ ] Full application workflow on clean supported systems
- [ ] Sequence opens with selected xLights versions and renders expected props
- [ ] No required Python, Docker, Git, cloud login or paid tokens
- [ ] First-run failure messages are comprehensible to beta testers
- [ ] Software/model third-party notices complete
- [ ] A/B trials preserve blind ordering and actual respondent data
- [ ] All blockers resolved or documented with constrained support
- [ ] Release artifacts produce matching checksum manifest

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
