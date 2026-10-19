# Coding Prompt — Phase 13: xLights Rendering and Visual Evaluation

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-13.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 13 (xLights Rendering and Visual Evaluation) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-13/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 13 only (085–089).
Working product behavior: As a sequencer, I can see a fast local approximation of a phrase, then run xLights itself to confirm how supported effects actually render before accepting an AI revision.
Explicit exclusions: No full reimplementation of the xLights C++ rendering engine, no assumption every installed release supports `--headless`, no controller hardware playback.
Inputs: A candidate `SequenceDocument`, display geometry, exported XSQ, target xLights installation and optional show directory/renderer arguments.
Outputs: `RenderArtifact` with success/error/warnings, version/hash evidence; approximate preview frames; optional FSEQ/render sample metrics and A/B comparison.
Mandatory gate: Preview simple arch stepping, export a temp XSQ and render it using a verified installed xLights mode (headless if supported), compare a frame/time metric with the local approximation and show evidence links.
Do not start Phase 14 without recorded validation.
```

## Steps to implement

- [ ] **085** Build fast lightweight internal preview for supported primitive gestures. **Acceptance:** Manual known fixtures match approximate rendered path.
- [ ] **086** Probe installed xLights capabilities and implement version-aware render adapter. **Acceptance:** Supported build renders fixture; unsupported build gracefully falls back.
- [ ] **087** Decode rendered output/frame or channel data into display preview, preserving timing. **Acceptance:** Frame sample matches known pixel/channel values.
- [ ] **088** Compute display-level metrics for brightness, coverage, spatial motion and density. **Acceptance:** Designed render samples produce sensible metric order.
- [ ] **089** Add render/evaluation gate to refinement and report limitations. **Acceptance:** Broken/unrenderable proposal cannot auto-apply; clear diagnostics.

## Tests specifically required

- [ ] Approximate StepAlongPath preview emits expected active nodes
- [ ] Unsupported effect is labeled approximate/unsupported
- [ ] Capability probe handles missing executable and unsupported version
- [ ] Render process times out and kills child cleanly
- [ ] Bad XSQ fails rather than becoming good score
- [ ] Valid tested fixture yields a traceable render artifact
- [ ] Metrics agree with synthetic known active-frame patterns
- [ ] Manual verification path remains possible without headless support

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
