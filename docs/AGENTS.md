# Agent Engineering Rules — xLights AI Choreography Studio

## Load context
1. Determine the current explicitly assigned phase and **single numbered step**. If no code exists, start Phase 01 / Step 001.
2. Read `phase-specs/PHASE-NN.md` completely. It is the authoritative standalone specification and includes architecture, C# contracts, class/process diagrams, test cases, risks and required handoff evidence.
3. Read existing source and tests, rather than assuming any sample API signature already exists. Upstream xLightsAI is a pinned comparison reference only; user downloads must not require Python, Git or Docker.

## Hard constraints
- Never edit an actual xLights show directory/source `xlights_rgbeffects.xml` or `xlights_networks.xml` without explicit approval and backup.
- Do not invent xLights effect XML settings. Use real version-pinned xLights XSQ fixtures and verify exported results in the target xLights build.
- Preserve user-approved effects and enforce `[start,end)` selection and model/lock boundaries inside deterministic code.
- Keep optional AI model outputs as schema-validated proposals and never permit unsolicited network access, cloud processing or unapproved edits.
- Prefer a small local feature slice rather than speculative distributed architecture. Use .NET 10/Avalonia/SQLite as architectural defaults unless a recorded ADR changes them.
- Do not claim builds, fixtures, renders or human reviews passed unless run and observed.

## One-step completion contract
For each step write targeted tests, implement the vertical path, run tests, inspect output/UI, add `implementation-evidence/PHASE-NN/STEP-XXX.md` and report PASS/BLOCKED. Do not skip steps; do not proceed to the next phase if its gate is blocked.
