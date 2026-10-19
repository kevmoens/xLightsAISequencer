# Coding Prompt — Phase 01: Foundation and Reference Baselines

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-01.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 01 (Foundation and Reference Baselines) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-01/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 01 only (001–008).
Working product behavior: As a sequencer, I can start a local desktop studio, create a project, close the app and reopen exactly the same project without touching my xLights show folder.
Explicit exclusions: Do not parse a real xLights layout, run music inference, build an effect editor, or ship upstream Docker/Python in the application.
Inputs: User-selected workspace directory; app configuration; upstream Git URL plus immutable commit; legally shareable synthetic fixtures.
Outputs: Avalonia shell; project manifest; SQLite database; versioned migration runner; immutable reference fixtures; CI build/test reports.
Mandatory gate: Start a fresh install; create “Demo Song”, close/reopen; open a second project; inject a migration failure; show recovery screen, unchanged pre-migration backup and diagnostic log.
Do not start Phase 02 without recorded validation.
```

## Steps to implement

- [ ] **001** Fork or vendor upstream source at an immutable commit; document license and downstream notices. **Acceptance:** Record URL, SHA, license and attribution; clean checkout resolves reference.
- [ ] **002** Audit upstream analysis, layout, theme, effects, XSQ writer, preview and dependency boundaries. **Acceptance:** Create a module-by-module reuse/port/replace/omit matrix with implementation risks.
- [ ] **003** Create baseline fixture corpus: synthetic notes, drum pulse, instrumental, complex mixed song and 2 layouts. **Acceptance:** Record input hashes, permitted usage, outputs and repeatable invocation.
- [ ] **004** Create .NET 10 Avalonia solution with basic navigation and no Python runtime. **Acceptance:** Launch Windows desktop executable on a clean build.
- [ ] **005** Add structured logging, crash handling, settings and diagnostics page. **Acceptance:** Restart restores settings and an injected error is reported without silent data loss.
- [ ] **006** Implement create/open/save project with versioned manifest and no direct writes to show folder. **Acceptance:** Round-trip sample project and reject newer unsupported schema safely.
- [ ] **007** Add SQLite persistence with transactional migrations and backups. **Acceptance:** Migration tests on fresh and prior-version databases succeed.
- [ ] **008** Add CI, unit/integration/golden test projects, formatting and artifact publishing. **Acceptance:** Clean checkout builds, runs fixtures, and stores test reports.

## Tests specifically required

- [ ] Create/open/restart preserves project ID and name
- [ ] Two simultaneous writers are detected or serialized
- [ ] Older schema migrates transactionally with backup
- [ ] Newer-than-supported schema refuses destructive downgrade
- [ ] Abrupt cancellation leaves either old or new manifest, not partial JSON
- [ ] Clean checkout `dotnet build` and `dotnet test` succeed
- [ ] Pinned upstream commit/license inventory is present and reviewable

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
