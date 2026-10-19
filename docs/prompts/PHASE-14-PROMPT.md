# Coding Prompt — Phase 14: Whole Song Composition and Continuity

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-14.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 14 (Whole Song Composition and Continuity) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-14/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 14 only (090–093).
Working product behavior: As a sequencer, I can compose a complete song with coherent verses and choruses, repeated recognizable choreography, dramatic contrasts and a final climax rather than a collection of unrelated clips.
Explicit exclusions: No silent mass replacement of individual accepted phrases; no assertion fully automatic output equals expert human artistic quality.
Inputs: User-approved phrase plans, structure/motifs, `VisualStory`, layout paths, role assignments, scoped edits and existing sequence.
Outputs: `WholeSongPlan` and assembled `SequenceDocument` candidate with transition spans, motif consistency and continuity assessment.
Mandatory gate: Take a song with Intro/Verse/Chorus/Verse/Chorus/Bridge/Final chorus; show section intensity arc and shared motif variants, inspect continuity warnings, resolve one seam and export the entire editable XSQ.
Do not start Phase 15 without recorded validation.
```

## Steps to implement

- [ ] **090** Create song-level creative direction with palettes, motif identities, energy and focal points. **Acceptance:** Plan renders as inspectable timeline summary.
- [ ] **091** Assemble accepted phrase candidates with transition-aware overlap/gap handling. **Acceptance:** No unintended timing holes, conflicts or truncated effects.
- [ ] **092** Apply motif variations by occurrence and climax scheduling. **Acceptance:** Final chorus preserves recognizable motif with intentional lift.
- [ ] **093** Run whole-song rhythm/contrast/complexity checks; export and audition. **Acceptance:** Export opens in xLights and passes continuity report.

## Tests specifically required

- [ ] Accepted locked effect remains unchanged after full-song assembly
- [ ] Final chorus can be stronger without mutating earlier chorus
- [ ] A silence bridge is not filled indiscriminately
- [ ] Cross-phrase overlapping effect is flagged for review
- [ ] Motif source changes generate stale-instance warning
- [ ] Section-to-section brightness arc is computed consistently
- [ ] Reopen and export approved full song without recomposition

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
