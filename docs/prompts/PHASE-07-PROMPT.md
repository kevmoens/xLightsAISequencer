# Coding Prompt — Phase 07: Melody Pitch and Note Analysis

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-07.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 07 (Melody Pitch and Note Analysis) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-07/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 07 only (045–051).
Working product behavior: As a sequencer, I can watch a detected melody rise and fall in a piano-roll view, correct wrong notes, and see my arches step in the same direction as the melody.
Explicit exclusions: No promise of polyphonic note-perfect transcription from a full mastered song, no arbitrary chord voicing reconstruction, no style LLM yet.
Inputs: User-selected stem/source, trusted MIDI where present, common timing grid, named target models or initial path order.
Outputs: Versioned `MelodyTrack` of note events and pitch-contour gestures; editable piano roll; note-driven effect placements for a limited gesture set.
Mandatory gate: Import trusted MIDI or simple isolated melody, view notes on piano roll, correct an incorrect note, select five arches, preview stepping up and reversing on descent, export and validate in xLights.
Do not start Phase 08 without recorded validation.
```

## Steps to implement

- [ ] **045** Define pitch/note/onset and relative melodic gesture contracts, with source/confidence. **Acceptance:** Fixture JSON round-trips with units and source IDs.
- [ ] **046** Implement fundamental-frequency/pitch tracking for isolated lead source. **Acceptance:** Synthetic sine/recorded single notes within cents and timing tolerance.
- [ ] **047** Derive up/down/repeat/hold/leap/rest from melodic contour with noise filtering. **Acceptance:** Synthetic melodic shapes classify correctly.
- [ ] **048** Support MIDI import and evaluate transcription backend behind an interface. **Acceptance:** Imported MIDI note timings map to canonical event structure.
- [ ] **049** Implement editable piano roll and low-confidence markers. **Acceptance:** User corrections persist and regenerate only dependent phrases.
- [ ] **050** Compile melody to stepping, holding, jumping choreography across ordered props. **Acceptance:** Five/seven-note fixtures produce correct path order and durations.
- [ ] **051** Handle polyphony, rests, note gaps, variable durations and frame quantization gracefully. **Acceptance:** Fallback to contour or onsets when pitch unreliable; no invalid effects.

## Tests specifically required

- [ ] Synthetic C-D-E-G-E-D-C generates correct up/peak/down sequence
- [ ] Repeated note pulses same target, not random prop
- [ ] Hold length follows note duration within expected frame quantization
- [ ] Rest introduces silence/no stray effect
- [ ] Jump interval skips target positions by declared mapping policy
- [ ] Low-confidence pitch falls back to contour or onset with provenance
- [ ] Note correction updates proposal without overriding locked effects

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
