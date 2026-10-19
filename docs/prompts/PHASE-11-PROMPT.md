# Coding Prompt — Phase 11: Selective AI Editing

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-11.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 11 (Selective AI Editing) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-11/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 11 only (072–078).
Working product behavior: As a sequencer, I can select arches at 42–48 seconds and say “make the piano move left-to-right, leave the roof alone,” then inspect a proposed diff before approving it.
Explicit exclusions: No background agent secretly editing or launching scripts, no arbitrary file system access by LLM, no mandatory paid inference.
Inputs: `SequenceDocument`, `EditScope`, selected phrase/note/stem/motif context, locks, available gesture grammar, user instruction and optional configured model provider.
Outputs: `EditProposal` with typed bounded operations, validation report, before/after diff, approval record and optional propagated motif edits.
Mandatory gate: Select 42–48 seconds on five arches, ask for rising piano pops, inspect affected models/times and before/after visualization; reject, repeat, approve, verify roofline hash unchanged.
Do not start Phase 12 without recorded validation.
```

## Steps to implement

- [ ] **072** Create persisted selection context: interval, props, instruments, phrases, locks. **Acceptance:** Serialization test preserves exact selection and scope.
- [ ] **073** Implement deterministic command AST for brightness/motion/direction/speed/replace. **Acceptance:** UI quick actions work offline and no token usage.
- [ ] **074** Add edit request panel, suggestions and change explanation. **Acceptance:** Request always displays target context.
- [ ] **075** Add optional Ollama/cloud provider behind opt-in structured-output adapter. **Acceptance:** Provider off means no outbound calls; malformed JSON rejected.
- [ ] **076** Implement dry-run proposals, before/after diff and Accept/Reject. **Acceptance:** Reject leaves state byte-equivalent; accept records transaction.
- [ ] **077** Implement explicit motif propagation and edit-scope intersection validation. **Acceptance:** Locked/out-of-scope effects cannot change even with malicious proposal.
- [ ] **078** Persist proposal audit, undo/redo and failure recovery. **Acceptance:** Crash/restart leaves committed edits intact, partial proposal uncommitted.

## Tests specifically required

- [ ] Deterministic “reverse selected arches” works offline
- [ ] Unknown instruction produces controlled no-op or model offer
- [ ] LLM cannot target Roofline outside selected arches
- [ ] Unknown effect recipe is rejected
- [ ] Prompt-injection-like lyrics remain untrusted data
- [ ] Locked effect blocks even valid-looking proposal
- [ ] User reject leaves document hashes unchanged
- [ ] Accept creates one undoable transaction and survives restart

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
