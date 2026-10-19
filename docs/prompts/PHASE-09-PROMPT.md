# Coding Prompt — Phase 09: Spatial Choreography and Display Paths

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-09.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 09 (Spatial Choreography and Display Paths) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-09/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 09 only (059–065).
Working product behavior: As a sequencer, I can see my physical display and define an ordered movement route through props, then reuse the same rising melody gesture on arches, windows or roofline.
Explicit exclusions: No edits to xLights controller channel definitions; no guarantee layout screen coordinates perfectly match real-world depth, viewing angle or prop order.
Inputs: Normalized `DisplayGraph` and source file hash; optional model/submodel pixel point geometry; user path ordering and roles.
Outputs: `SpatialPath`s and role bindings with versioning; visual display editor; geometry-aware `ResolvedGesture`s independent of XSQ internals.
Mandatory gate: Load a layout, inspect suggested arch and roofline paths, reverse the arch route manually, preview C-D-E-G-E-D-C across both and prove geometry-aware effects target correct xLights props.
Do not start Phase 10 without recorded validation.
```

## Steps to implement

- [ ] **059** Normalize coordinates, model groups and relationships into display graph. **Acceptance:** Fixtures map consistent physical positions and stable IDs.
- [ ] **060** Infer neighbors, left/right, center and vertical relations with confidence. **Acceptance:** Expected order recognized; uncertain order flagged.
- [ ] **061** Create graphical layout/path editor with position correction. **Acceptance:** Drag and reorder saves across restart.
- [ ] **062** Define named custom paths and semantic spatial motions. **Acceptance:** Left-right, outside-in and vertical previews match ordered props.
- [ ] **063** Persist role tags: lead, rhythm, ambient, hero and accent. **Acceptance:** Role assignments are available in choreography UI.
- [ ] **064** Map pitch intervals/movement to jumps, holds and path traversal. **Acceptance:** Ascending/descending tests preserve contour and reach boundaries safely.
- [ ] **065** Add route validation, conflicting-write checks and incompatible prop fallback. **Acceptance:** Invalid path cannot silently produce mis-targeted XSQ effects.

## Tests specifically required

- [ ] Five arches auto-order left-to-right for normal fixture
- [ ] Manual reverse path is stable after reload
- [ ] Two layouts can reuse same gesture policy with distinct prop IDs
- [ ] Missing model warning preserves saved path for reconciliation
- [ ] Group membership does not create duplicate effect targeting
- [ ] Large jump skips nodes according to declared policy
- [ ] Layout reimport does not silently alter user path order

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
