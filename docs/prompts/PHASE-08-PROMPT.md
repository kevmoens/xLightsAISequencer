# Coding Prompt — Phase 08: Song Structure and Repeated Phrases

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-08.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 08 (Song Structure and Repeated Phrases) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-08/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 08 only (052–058).
Working product behavior: As a sequencer, I can see verses and choruses divided into phrases, confirm recurring melodies and reuse a recognizable visual idea in repeated sections.
Explicit exclusions: No guarantee every pop song has a standard verse/chorus form; do not equate identical BPM with identical motif.
Inputs: Rhythm grid, spectral/chroma features, stem activity, melody tracks, song duration and previously approved choreography.
Outputs: Versioned `SongStructure` with `Section`, `Phrase`, `MotifGroup` and inheritance/variation links; editable structure timeline.
Mandatory gate: Analyze a song with repeated chorus; show two matching phrases highlighted; accept the first chorus as a template and propagate only melody movement into the second, preserving destination drums and locked effects.
Do not start Phase 09 without recorded validation.
```

## Steps to implement

- [ ] **052** Compute normalized bar/phrase feature fingerprints from audio and events. **Acceptance:** Similar motifs compare consistently across changed gain.
- [ ] **053** Detect candidate novelty/section boundaries with confidence. **Acceptance:** Annotated song has useful boundaries and error report.
- [ ] **054** Assign candidate intro/verse/chorus/bridge labels and support overrides. **Acceptance:** UI shows provenance/confidence; overrides survive restart.
- [ ] **055** Segment sections into phrase units with manual corrections. **Acceptance:** Phrase boundaries align to bars or user-chosen events.
- [ ] **056** Cluster exact and similar motifs with tolerance for variation. **Acceptance:** Repeated eight-bar fixture shares motif ID; distinct passage does not.
- [ ] **057** Create parameterized choreography template and instance overrides. **Acceptance:** Edit master/motif occurrence without surprising cross-section changes.
- [ ] **058** Build phrase relationship/propagation inspector. **Acceptance:** User links/unlinks motifs and previews related effects.

## Tests specifically required

- [ ] Synthetic song with two repeated 8-bar choruses links correct phrases
- [ ] Same rhythm but unrelated melody does not auto-accept a motif
- [ ] Manual split/merge survives re-analysis
- [ ] Motif inheritance retimes source note events to destination markers
- [ ] Approved destination locks prevent propagation
- [ ] Link graph rejects cycles and stale sources
- [ ] User can unlink one chorus without affecting others

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
