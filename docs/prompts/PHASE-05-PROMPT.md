# Coding Prompt — Phase 05: Beat Measures and Rhythm Detection

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-05.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 05 (Beat Measures and Rhythm Detection) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-05/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 05 only (031–037).
Working product behavior: As a sequencer, I can inspect a detected beat grid, correct wrong beats/downbeats and generate light pops that synchronize with the song.
Explicit exclusions: No perfect genre-agnostic beat detector; no melody transcription or automatic chorus classification; do not hard-code every note to a beat.
Inputs: Original decoded song, canonical audio timeline, manually editable time-grid; baseline click and drum fixtures.
Outputs: Versioned `RhythmAnalysis` with onsets/tempo candidates, `BeatGrid` and confidence, light gesture plans compiled to selected props.
Mandatory gate: Analyze a click/drum sample, see beat markers, correct an intentionally wrong downbeat, generate restrained pops for two props and play them synchronized while comparing before/after.
Do not start Phase 06 without recorded validation.
```

## Steps to implement

- [ ] **031** Implement RMS/energy envelope with baseline comparison. **Acceptance:** Synthetic envelope matches expected energy peaks.
- [ ] **032** Implement onset/transient detection with configurable sensitivity. **Acceptance:** Onset precision/recall measured on annotated fixtures.
- [ ] **033** Implement BPM candidate detection including half/double-time alternatives. **Acceptance:** 120 BPM and 60/120 ambiguity tests pass.
- [ ] **034** Generate beats, likely downbeats, bars and confidence intervals. **Acceptance:** Tempo fixture aligns events within configured threshold.
- [ ] **035** Add beat-grid manual correction and dependency invalidation. **Acceptance:** Move beat, dependent effects refresh only when authorized.
- [ ] **036** Create beat-pop and alternating-chase recipe to real selected props. **Acceptance:** Rendered/exported effects follow detected beats.
- [ ] **037** Add confidence/quality gate and diagnostics. **Acceptance:** Low-confidence results are flagged and correction is possible.

## Tests specifically required

- [ ] 120 BPM click produces beats at expected intervals within configured tolerance
- [ ] 60/120 BPM ambiguity is represented as alternate hypotheses
- [ ] Swing fixture preserves off-grid note positions
- [ ] Manually moving one beat preserves other corrected beats
- [ ] Downbeat correction updates bar numbering without moving raw onsets
- [ ] Algorithm cache invalidation does not delete user-approved grid
- [ ] Generated effects align selected props and remain confined to time scope

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
