# START HERE — Copy/paste into Copilot CLI, Claude Code or Codex

```text
Act as the senior architect and implementation agent for this repository.
First read `phase-specs/PHASE-01.md` in full. It is a self-contained implementation specification with product architecture, C# interfaces, class relationships, workflow, screen requirements, build steps 001–008, acceptance tests and evidence rules.

Implement only Phase 01. First inspect current repo and record upstream xLightsAI pinned commit/license/dependency inventory; reconcile existing source with proposed signatures. For each numbered step create or update failing/acceptance tests, code the smallest complete vertical path, run tests, record exact evidence and commit if permitted. Do not proceed past an unpassed gate. Demonstrate real Avalonia create/save/reopen and safe migration, with working CI. At the end summarize tests actually run, hashes/artifacts, decisions and PASS/BLOCKED result. Do not begin Phase 02 until the gate is verifiably passed.
```

For a later phase, open `prompts/PHASE-NN-PROMPT.md` and give the agent that prompt with its `phase-specs/PHASE-NN.md` file. Each phase specification also contains its own complete phase-local prompt near the end.
