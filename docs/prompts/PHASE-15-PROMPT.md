# Coding Prompt — Phase 15: Packaging and Distribution

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-15.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 15 (Packaging and Distribution) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-15/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 15 only (094–097).
Working product behavior: As a hobbyist, I can download a signed or integrity-verified desktop release, install it on a supported machine, select my show and optionally download ML models without installing Python or developer tools.
Explicit exclusions: No promise identical GPU backend on all platforms, no unsigned auto-execution of arbitrary update scripts, no mandatory remote account.
Inputs: Reproducible .NET build, native inference dependencies, model download manifest, license inventory, supported operating systems and architectures.
Outputs: Windows installer and self-contained binaries (macOS/Linux when supported), manifests/SBOM, first-run wizard, diagnostics and safe upgrade path.
Mandatory gate: Install on a clean Windows x64 machine, import sample show/song, generate/edit a simple XSQ, optionally download model with consent, disconnect internet and continue playback/export.
Do not start Phase 16 without recorded validation.
```

## Steps to implement

- [ ] **094** Publish self-contained signed/unsigned-labeled Windows x64 app with verified native assets. **Acceptance:** Launch on clean VM with no .NET SDK and no Python.
- [ ] **095** Build installer/uninstaller, show-folder onboarding and safe defaults. **Acceptance:** User completes first run without command line.
- [ ] **096** Add opt-in model asset installs, offline validation, logs and update notices. **Acceptance:** Core app works offline after assets installed; missing models explained.
- [ ] **097** Add supported macOS/Linux deliverables only after validating inference and xLights compatibility. **Acceptance:** Each advertised platform passes same smoke-test suite.

## Tests specifically required

- [ ] Fresh Windows VM starts app with no .NET SDK, Git, Python or Docker
- [ ] Offline basic editing after install works
- [ ] Model download hash tampering rejected
- [ ] No-model mode remains functional for simple timelines
- [ ] Upgrade preserves projects and backups migrations
- [ ] Uninstall leaves user projects intact unless expressly asked
- [ ] Invalid update manifest/signature rejected
- [ ] OS/arch incompatibility yields clear message

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
