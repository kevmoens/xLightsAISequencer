# Coding Prompt — Phase 06: Instrument Separation and Activity Detection

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-06.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 06 (Instrument Separation and Activity Detection) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-06/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 06 only (038–044).
Working product behavior: As a sequencer, I can separate drums, bass and vocals locally, listen to each stem in sync and choose a stem as the source for my lighting movement.
Explicit exclusions: Do not claim six perfect sources, real-time low-latency separation, or guaranteed Demucs ONNX parity. Cloud inference and user uploads are not required.
Inputs: Licensed local music asset, appropriate model manifest and native/ONNX inference assets, CPU hardware capability, beat/timing metadata.
Outputs: A `StemManifest` with aligned derived audio assets and quality metadata; stem mixer/solo UI; activity regions by stem.
Mandatory gate: On CPU-only Windows, install model through app (or use offline pre-provisioned asset), separate a short sample, view aligned stems, solo vocals and drums, abort a long run and verify clean status.
Do not start Phase 07 without recorded validation.
```

## Steps to implement

- [ ] **038** Evaluate separation model licensing, .NET inference options, CPU baseline and memory profile. **Acceptance:** Decision document includes at least two feasible alternatives or explicit blocker.
- [ ] **039** Implement model asset manifest, verified download, consent and checksum validation. **Acceptance:** Tampered models rejected and clean offline restart supported.
- [ ] **040** Integrate selected ONNX/native inference via .NET with deterministic preprocessing/postprocessing. **Acceptance:** Output stem count/shape and audio sync assertions pass.
- [ ] **041** Add chunking/overlap, caching, progress, cancellation and bounded resource usage. **Acceptance:** Long fixture cancels cleanly and partial output never marked complete.
- [ ] **042** Add waveform tracks, solo/mute and synchronized stem playback. **Acceptance:** Audibly isolated stems stay aligned with source.
- [ ] **043** Detect instrument activity segments with scores. **Acceptance:** Known quiet/active regions appear correctly on timeline.
- [ ] **044** Compare reference outputs, audio metrics and listening tests; establish supported hardware. **Acceptance:** Measured separation quality/performance is documented and acceptance threshold met.

## Tests specifically required

- [ ] Tampered asset hash fails and cannot be loaded
- [ ] Missing model with offline mode gives actionable message
- [ ] Stem lengths and source offsets match within measured tolerance
- [ ] Cancellation does not mark partial files as completed
- [ ] Silent source yields finite output/no NaNs
- [ ] Stereo/multi-minute input stays within memory budget
- [ ] At least two listening reference clips meet declared quality bar
- [ ] Solo stems remain synchronized with original transport

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
