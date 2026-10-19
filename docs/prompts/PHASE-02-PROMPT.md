# Coding Prompt — Phase 02: First Working xLights Sequence

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-02.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 02 (First Working xLights Sequence) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-02/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 02 only (009–015).
Working product behavior: As a sequencer, I can point the desktop application at a show directory, choose real props, generate three elementary effects and open a completely editable resulting sequence in xLights.
Explicit exclusions: Do not implement the full effect catalog, arbitrary multi-layer editing, controller upload or fseq renderer emulation; do not write changed groups back to the show.
Inputs: A test show folder including `xlights_rgbeffects.xml`; optionally `xlights_networks.xml`; supported version-identified xLights-generated XSQ fixtures; three chosen model targets.
Outputs: Normalized `DisplayGraph`; initial `SequenceDocument`; three `EffectInstance`s; new XML `.xsq` file; verified xLights open/render evidence.
Mandatory gate: Import a sample layout with Roofline, Arch 1 and Arch 2; click Generate 3 Effects; save into app exports folder; open resulting XSQ in target xLights version and visually verify timing, props, colors and duration.
Do not start Phase 03 without recorded validation.
```

## Steps to implement

- [ ] **009** Implement show-folder picker with read-only initial discovery. **Acceptance:** Missing folder/file produces actionable error; no mutation.
- [ ] **010** Parse xlights_rgbeffects.xml into normalized stable model IDs/groups/geometry where present. **Acceptance:** Golden fixture verifies model names, groups and positions.
- [ ] **011** Validate referenced models, channel/network data where relevant and missing layout elements. **Acceptance:** Fixture with broken references generates warnings rather than partial destructive output.
- [ ] **012** Create effect plan containing three hardcoded simple effects on selected real props. **Acceptance:** Plan is serialized and inspectable independently of XSQ.
- [ ] **013** Port minimal editable XSQ writer and required sequencing metadata. **Acceptance:** Generated file passes XML/structural tests and round-trip parser.
- [ ] **014** Open output in installed xLights; verify duration, timing, models and visible effects. **Acceptance:** Human-attested integration test with screenshot/render notes.
- [ ] **015** Implement atomic export, filename safety, backups and golden XSQ regression tests. **Acceptance:** Never overwrite source without explicit approval; no regression in fixture output.

## Tests specifically required

- [ ] Missing `xlights_rgbeffects.xml` is non-destructive
- [ ] XXE / oversized XML protections reject malicious or corrupt fixtures
- [ ] Fixture models and group membership match expected names
- [ ] Duplicate and renamed model targets produce disambiguation warnings
- [ ] Three effects reference valid effect database and color palettes
- [ ] Export never overwrites original show or existing XSQ without explicit user action
- [ ] Gold-standard test opens and visibly renders in installed xLights

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
