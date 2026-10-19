# Coding Prompt — Phase 10: Layered Musical Composition

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-10.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 10 (Layered Musical Composition) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-10/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 10 only (066–071).
Working product behavior: As a sequencer, I can map piano to arches, kick/snare to accents, bass to a tree and generate a coherent phrase with space, contrast and a recognizable musical lead.
Explicit exclusions: No reliance on cloud LLM, no unconstrained random effects on every onset, no final whole-song mastering.
Inputs: Stem activity, melody gestures, beat grid, phrase structure, spatial paths, supported effect recipes and user-selected aesthetic settings.
Outputs: `VisualStory` and `ChoreographyPlan` with role-specific gestures, competition/budget decisions, and concrete `SequenceDocument` proposals.
Mandatory gate: Choose lead piano on arches, kick/snare on roofline and bass on tree for a 20-second song excerpt; preview verse vs louder chorus, show fewer active props in verse, export and verify relevant effects in xLights.
Do not start Phase 11 without recorded validation.
```

## Steps to implement

- [ ] **066** Define semantic musical roles and event precedence rules. **Acceptance:** Synthetic role mapping yields deterministic priorities.
- [ ] **067** Add configurable instrument-to-role and role-to-prop routing UI. **Acceptance:** Mappings persist and can change per section.
- [ ] **068** Create configurable visual density and focus budget. **Acceptance:** Clutter fixture is reduced while preserving focal musical event.
- [ ] **069** Build sectional energy and color/brightness story. **Acceptance:** Verse/chorus intensity curve follows plan.
- [ ] **070** Implement build, silence, transitions, release and phrase-ending recipes. **Acceptance:** Musical break can intentionally darken all permitted props.
- [ ] **071** Compose and resolve overlapping effects with layer/priority strategies. **Acceptance:** No unintended model conflicts; XSQ renders as planned.

## Tests specifically required

- [ ] Lead priority survives simultaneous bass and snare onset
- [ ] Budget caps excessive concurrent effects while retaining accents
- [ ] Silence produces intentional darkness when configured
- [ ] Two sources can share a path only with declared collision rule
- [ ] Same inputs+seed produce repeatable plan
- [ ] Manual locked effects cannot be replaced by new plan
- [ ] Choreography compilation uses only verified recipes and targetable props

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
