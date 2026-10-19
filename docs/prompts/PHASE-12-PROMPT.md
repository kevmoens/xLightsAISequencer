# Coding Prompt — Phase 12: Loop-Engineered Refinement

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-12.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 12 (Loop-Engineered Refinement) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-12/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 12 only (079–084).
Working product behavior: As a sequencer, I can ask the app to try several variants of a phrase, see exactly why they were scored, and keep the best candidate without risking my approved edits.
Explicit exclusions: No claim that a weighted quality score proves emotional impact; no infinite LLM loop; no auto-commit without human approval.
Inputs: Scoped choreography proposal, source music/gesture features, optional model provider, display path, locks, per-user quality presets.
Outputs: Persisted `LoopRun`, candidate lineage, metric breakdown, best technical candidate, stop reason and chosen accepted transaction.
Mandatory gate: Start with a knowingly over-busy 8-bar phrase, generate three candidates, inspect density/timing metrics and previews, accept one, then use Undo and show previous state restored.
Do not start Phase 13 without recorded validation.
```

## Steps to implement

- [ ] **079** Create persisted run/candidate/result schema with hashes, seeds and state transitions. **Acceptance:** Interrupted run resumes idempotently.
- [ ] **080** Build seeded candidate generator constrained to selected gesture family. **Acceptance:** Same seed + inputs yields same structured candidate.
- [ ] **081** Implement quality evaluators: timing, melodic alignment, density, motif reuse, legal effects. **Acceptance:** Known good/bad fixtures produce expected scores and findings.
- [ ] **082** Implement targeted repair strategy and preserve best-so-far candidate. **Acceptance:** Repair only addresses reported dimension and respects locks.
- [ ] **083** Add retry, time, compute and quality thresholds; cancellation. **Acceptance:** Unproductive loop stops deterministically with reason.
- [ ] **084** Add candidate gallery, score explanations, preview and explicit selection. **Acceptance:** User can compare alternatives and commit chosen candidate.

## Tests specifically required

- [ ] Same seed + inputs yields reproducible candidate
- [ ] A hard gate rejects scope leak even if all other scores high
- [ ] Best candidate retained when later attempt is worse
- [ ] MaxAttempts enforced with exact attempt count
- [ ] Cancelled run never commits
- [ ] Restart resumes persisted run state without repeating accepted work
- [ ] Quality metric evaluator-version mismatch flagged
- [ ] User approval checks base revision to prevent stale overwrite

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
