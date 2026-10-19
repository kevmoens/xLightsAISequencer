# Agent prompt - xLights XSQ engineering verification

You are implementing the xLights XML sequence integration for the local-first Avalonia/.NET studio. Read `XLIGHTS-SEQUENCE-ENGINEERING-GUIDE.md`, `SYSTEM-DESIGN-AND-PHASE-MAP.md`, `ARCHITECTURE-AND-GUARDRAILS.md`, `phase-specs/PHASE-02.md`, and current code/tests.

**Task:** Do not immediately generate a fake XSQ. First establish a verified target xLights version, real hand-created tiny XSQ fixture(s), and concrete effect/palette/ref encoding. Inspect the pinned upstream xLightsAI code at `src/generator/xsq_writer.py` and `src/grouper/layout.py`, then compare against xLights' own generated files. Extract a typed, version-aware XSQ contract, preserve unknown sections, and implement the smallest three-effect vertical slice.

Work as a bounded test-driven workflow:

1. State inspected xLights version(s), available source fixtures, and missing dependencies.
2. Characterize actual XML sections, references, effect settings, palettes, model targeting, timing units, and model groups.
3. Save `docs/FORMAT-FINDINGS.md` and `docs/EFFECT-RECIPE-REGISTRY.md` with every verified claim linked to a fixture or source file.
4. Write structural and semantic golden tests before/alongside the serializer.
5. Implement a read-only show importer and `SequenceDocument` compiler separated from XDocument serialization.
6. Generate one effect, verify actual xLights open and render; then three effects, timing, and layers.
7. Verify no changes to original show/layout files and no leaks to out-of-scope effects.
8. Capture actual build/test commands, fixtures, render results and known compatibility gaps.

Hard stop: If actual xLights validation cannot be performed, mark the gate BLOCKED/UNVERIFIED, supply exact manual validation steps and do not fabricate successful rendering. Keep experimental effects outside production recipes until verified.
