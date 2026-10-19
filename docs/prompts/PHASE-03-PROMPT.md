# Coding Prompt — Phase 03: Visual Timeline and Manual Editing

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-03.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 03 (Visual Timeline and Manual Editing) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-03/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 03 only (016–023).
Working product behavior: As a sequencer, I can select a prop and time region, drag an effect, resize it, undo mistakes and lock approved parts so later generation cannot destroy my work.
Explicit exclusions: No waveform playback, note extraction, AI prompt editor or real-time playback graphics; use dummy effects from Phase 02.
Inputs: Saved `SequenceDocument`, `DisplayGraph`, effect recipes and timeline view state from earlier phases.
Outputs: Timeline interactions producing validated `EditTransaction`s, persisted locks and revision history, exported updated XSQ.
Mandatory gate: Open a three-effect sample, drag Arch 1 effect, resize Roofline, undo/redo both operations, lock Arch 2, attempt forbidden modification, export and verify other effects stay bit-for-bit semantically identical.
Do not start Phase 04 without recorded validation.
```

## Steps to implement

- [ ] **016** Build time ruler, horizontal scroll and zoom. **Acceptance:** Pixel-to-time conversion tests at zoom extremes.
- [ ] **017** Build prop rows, group expansion and stable mapping to model IDs. **Acceptance:** Visual rows match loaded layout including group nesting.
- [ ] **018** Render effect blocks and inspector selection. **Acceptance:** Selection maps exactly to effect ID and time span.
- [ ] **019** Drag effect starts with optional snap. **Acceptance:** Start time persists after save/reopen and XSQ export.
- [ ] **020** Resize duration and edit supported effect parameters. **Acceptance:** Edited parameters survive project reload and xLights import.
- [ ] **021** Multi-row time-range selection with inclusive/exclusive boundary semantics. **Acceptance:** Selected model IDs and time range match UI; edge-case tests.
- [ ] **022** Undo/redo transactions and lock semantics for approved effects. **Acceptance:** Undo restores exact snapshot; locked effects reject programmatic edits.
- [ ] **023** Scope-aware export and regeneration contract. **Acceptance:** Hash unaffected effects before/after scoped edit; no changes outside scope.

## Tests specifically required

- [ ] Move an effect by exactly one xLights frame and round-trip
- [ ] Resize effect and preserve original outside protected scope
- [ ] Locks forbid manual and automated edits consistently
- [ ] Undo then redo restores same document hashes
- [ ] Editing a stale revision fails without any write
- [ ] Across restart, selected locks and committed changes persist
- [ ] Randomized property tests confirm unrelated effect hashes never change

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
