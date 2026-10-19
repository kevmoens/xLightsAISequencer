# PHASE 09 — Step 063 Evidence

**Step:** "Persist role tags: lead, rhythm, ambient, hero and accent"
**Spec:** `docs/phase-specs/PHASE-09.md` §6, Step 063 (lines 264-278) — the fifth of seven numbered steps
of Phase 09 (059-065)
**Date:** 2026-10-15

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-09.md` in full, confirming Step 063's own text: required acceptance ("Role
  assignments are available in choreography UI"), implementation guidance ("Assign roles independently of
  paths so same prop can play lead in one phrase and background later; provide conflict policy"),
  failure/edge case ("Invalid input, cancellation or stale persisted state must not corrupt the project").
  Section 3 does not propose a concrete role-tag contract for this step (it names `ResolvedGesture`,
  `ISpatialGestureResolver` etc. for Step 064+); Step 063 is the first step to introduce role persistence at
  all.
- Read `implementation-evidence/PHASE-09/STEP-059.md` through `STEP-062.md` in full as the accepted
  contract baseline, in particular Step 062's own disclosed three-way split (pure suggester/editor vs.
  persisted, versioned catalog) and its own "reject duplicate… unless intentionally allowed" interpretive
  choice, since this step needed an analogous interpretive choice for "provide conflict policy."
- Read every file in `src/XlightsAI.Studio.Display/` in full before writing any new type: `SpatialPath.cs`,
  `SpatialPathSet.cs`, `ISpatialPathCatalog.cs`/`SpatialPathCatalog.cs`, `ISpatialPathStore.cs`/
  `JsonSpatialPathStore.cs`, `DirectionPolicy.cs`, `SpatialDisplayGraph.cs`, `DisplayNode.cs`,
  `SpatialDisplayGraphInvalidException.cs`, `SpatialDisplayGraphCorruptException.cs` — confirming the exact
  current shape of every Step 059-062 contract, the established atomic-write/deterministic-path-by-hash
  store convention, and the "string constants, not an enum" convention (`SpatialEdge.Relation`,
  `DirectionPolicy`) before choosing the same shape for role tags.
- Read `tests/XlightsAI.Studio.Display.Tests/Step062AcceptanceTests.cs` and
  `tests/XlightsAI.Studio.Shell.Tests/Step062DisplayPathEditorViewModelTests.cs` as the direct structural
  precedent for this step's own test style (real importer/builder/catalog, not mocks; before/after-bytes
  and reject-before-any-write negative-path convention; restart-across-a-brand-new-instance persistence
  proof).
- Read `src/XlightsAI.Studio.Shell/DisplayPathEditorViewModel.cs` in full to confirm the exact
  loading/empty/error/loaded state machine and "catch `SpatialDisplayGraphInvalidException`, set
  `ActionErrorMessage`, leave bindable state completely unchanged on failure" convention before extending
  it with role-assignment support.
- Checked `src/XlightsAI.Studio.Display/XlightsAI.Studio.Display.csproj`: it references only
  `XlightsAI.Studio.Contracts` and `XlightsAI.Studio.Xlights`, **not** `XlightsAI.Studio.Structure`.
  Confirmed via `grep`/`Grep` that `XlightsAI.Studio.Structure`'s own `PhraseCandidate.Id` is a bare `Guid`
  (not yet a typed `PhraseId`), reinforcing the decision (§3 below) to key a role assignment's scope by an
  opaque caller-supplied `Guid`, not a cross-project reference to Phase 08's own phrase contracts.
- Searched the full repository for any prior "Step 063"/"STEP-063" reference, or any existing
  `RoleTag`/`RoleAssignment`/`IRoleAssignmentCatalog`/`IRoleAssignmentStore` type — found only
  `PHASE-09.md` itself, confirming this is genuinely new work.
- Baseline taken by running the real, full solution test suite *before* any Step 063 code was written:
  **846/846 passing, 0 failed, 0 skipped** (Step 062's own closing count), reconfirmed via
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean) immediately before starting.

## 2. Scope actually implemented

**No file from Step 059, 060, 061 or 062 was behaviorally changed.** This step adds new types to the
existing `src/XlightsAI.Studio.Display/` project, extends the existing `DisplayPathEditorViewModel` in
`src/XlightsAI.Studio.Shell/` with additive members only (no existing member's signature or behavior
changed), and adds two new test files.

### New files in `src/XlightsAI.Studio.Display/`

- **`RoleTag.cs`** — the five named role tags this step's own required text names verbatim: `Lead`,
  `Rhythm`, `Ambient`, `Hero`, `Accent`. String constants, not an enum — the same convention
  `SpatialEdge.Relation`/`DirectionPolicy` already established ("not an enum so later steps can add new
  kinds without a breaking contract change"). `RoleTag.All` is used only to produce a clear rejection
  message, the same pattern `DirectionPolicy.All` already uses.
- **`RoleAssignment.cs`** — one prop's role for one choreography scope: `(ModelId NodeId, string Role, Guid
  ScopeId, int Version)`. `ScopeId` is a caller-defined, opaque `Guid` (`Guid.Empty` is the default,
  phrase-independent scope) — deliberately not a reference to Phase 08's own phrase contracts (see §3).
- **`RoleAssignmentSet.cs`** — the full persisted set of role assignments for one imported layout (keyed by
  `SourceLayoutHash`, the same layout-scoped identity `ISpatialPathStore`/`ISpatialDisplayGraphStore`/
  `IDisplayLayoutCorrectionsStore` already use): `Assignments` (current, live role per `(NodeId, ScopeId)`
  pair) plus `History` (every superseded assignment, append-only) plus an overall `Version` counter — the
  exact same shape `SpatialPathSet` already established for named paths.
- **`IRoleAssignmentStore.cs`** / **`JsonRoleAssignmentStore.cs`** — persistence boundary for
  `RoleAssignmentSet`, mirroring `ISpatialPathStore`/`JsonSpatialPathStore`'s own save/load contract and
  atomic-write guarantee exactly (same-directory temp file + `File.Move(..., overwrite: true)`), at its own
  deterministic, separate on-disk path (`role-assignments-<hash>.json`) so saving/rebuilding role
  assignments never touches the graph cache, the layout corrections or the named path set. Reuses
  `SpatialDisplayGraphCorruptException` for a corrupt file, the same reuse rationale Step 061/062 already
  recorded.
- **`IRoleAssignmentCatalog.cs`** / **`RoleAssignmentCatalog.cs`** — the application-orchestration boundary:
  `GetAssignmentsAsync` (loads existing assignments or `RoleAssignmentSet.Empty`); `AssignAsync` (validates
  the node exists in the graph and the role is one of `RoleTag.All` *before* any load/write is attempted —
  this step's own required failure/edge case, verbatim; then applies this step's own required "provide
  conflict policy": at most one role is live per `(NodeId, ScopeId)` pair — a second assignment to the same
  pair replaces the first, moving it into `History` and incrementing that pair's own version, rather than
  silently stacking); `ClearAsync` (removes the live assignment for a `(NodeId, ScopeId)` pair, moving it
  into `History`; throws `SpatialDisplayGraphInvalidException` if no live assignment exists for that exact
  pair — never a silent no-op).

### Extended file in `src/XlightsAI.Studio.Shell/`

- **`DisplayPathEditorViewModel.cs`** — extended with additive members only: `RoleAssignments` (bindable,
  real `RoleAssignmentSet.Assignments`, empty exactly when no role assignment set has been loaded for the
  currently open layout), `RoleAssignmentSetVersion`, `LoadRoleAssignmentsAsync`, `AssignRoleAsync`,
  `ClearRoleAsync` — every mutating action follows the exact same "catch
  `SpatialDisplayGraphInvalidException`, set `ActionErrorMessage`, leave bindable state completely
  unchanged on failure" convention `CorrectSelectedNodePositionAsync`/`CreatePathAsync` already established.
  `OpenAsync` now also resets `_roleAssignmentSet` to `null` before loading, so opening a different layout
  never leaves a previously opened layout's role assignments dangling. No existing member's signature or
  behavior changed; `Rows`/`NamedPaths`/`SelectNode`/`CorrectSelectedNodePositionAsync`/`ReorderAsync`/
  `CreatePathAsync`/`ReorderPathAsync`/`ReversePathAsync` are untouched.

### New test files

- **`tests/XlightsAI.Studio.Display.Tests/Step063AcceptanceTests.cs`** (7 tests) — real
  `ShowLayoutImporter`/`DisplayGraphBuilder`/`RoleAssignmentCatalog`/`JsonRoleAssignmentStore` (not mocks)
  against the real Step 059 fixture:
  - **`Step063_Acceptance_Passes_RoleAssignmentsAreAvailableAfterLoad`** — the required acceptance test,
    verbatim ("Role assignments are available in choreography UI," proven at the real domain/catalog
    boundary a choreography UI would read through). Assigns `Lead` to Arch-01 in phrase A and `Ambient` to
    Arch-01 in phrase B (the implementation guidance's own worked example, verbatim: "same prop can play
    lead in one phrase and background later"), then reloads through a brand-new catalog/store instance and
    confirms both are available.
  - **`AssignAsync_SamePairTwice_ReplacesWithVersionedHistory_ConflictPolicy`** — the required "provide
    conflict policy," verbatim: reassigning the same `(node, scope)` pair replaces the live role (moved to
    `History`, versioned), rather than stacking; survives a restart through a brand-new catalog/store
    instance.
  - **`ClearAsync_RemovesLiveAssignmentAndRecordsHistory`** — a live assignment is removed from
    `Assignments` and recorded in `History`, never silently dropped.
  - **`AssignAsync_InvalidInput_RejectsBeforeAnyWrite_NegativePath`** — an unknown node id and an
    unrecognized role tag are each rejected before any file is ever written for that layout (`File.Exists`
    asserted `false` afterward).
  - **`ClearAsync_UnknownPair_Throws_NegativePath`** — clearing a `(node, scope)` pair with no live
    assignment throws rather than silently no-op'ing.
  - **`AssignAsync_Cancelled_DoesNotPersist_NegativePath`** — a pre-cancelled token propagates
    `OperationCanceledException` and never writes a role-assignment file — this step's own required
    failure/edge case, verbatim: "cancellation … must not corrupt the project."
  - **`Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`** — same convention as the sibling
    `JsonSpatialPathStore`/`JsonDisplayLayoutCorrectionsStore` tests — "stale persisted state must not
    corrupt the project," read as: a corrupt on-disk file is surfaced as a typed exception, never silently
    accepted or allowed to crash with an unhandled deserialization error.
- **`tests/XlightsAI.Studio.Shell.Tests/Step063DisplayPathEditorViewModelTests.cs`** (6 tests) — the view
  model slice against a real `RoleAssignmentCatalog`/`JsonRoleAssignmentStore` under a temporary directory:
  loading an empty role-assignment set, assigning the same prop two different roles in two different scopes
  and confirming both are visible on `RoleAssignments` (the required acceptance text, verbatim, at the
  bindable view-model layer), clearing a role, and three negative-path tests (nothing open, unrecognized
  role, unknown `(node, scope)` pair on clear — each proving `RoleAssignments` is left completely unchanged
  on failure).

## 3. Architecture deviations / decisions

- **A role assignment's `ScopeId` is an opaque, caller-supplied `Guid`, not a reference to Phase 08's own
  phrase contracts (`PhraseCandidate.Id`, `MotifLink`, etc.).** The implementation guidance's own worked
  example — "same prop can play lead in one phrase and background later" — names "phrase" as the scope a
  role is independent across, but `src/XlightsAI.Studio.Display/XlightsAI.Studio.Display.csproj` has no
  project reference to `XlightsAI.Studio.Structure` (confirmed by reading the `.csproj` directly), and
  Phase 09's own Section 1 "Owned boundary" is "Layout-derived display graph, visual geometric editor,
  explicit ordered paths, prop roles and geometry-aware gesture resolution" — phrase identity itself belongs
  to Phase 08, not this phase. `RoleAssignment.ScopeId` is therefore left as a bare `Guid` the caller
  supplies (a phrase id, a song section id, or `Guid.Empty` for "no specific scope") — Step 063 persists
  role tags and their conflict policy; wiring a real `PhraseCandidate.Id` into that same `Guid` slot is a
  consuming phase's own later integration, not a new contract this step needs to invent. Disclosed here as
  the one place this step's own prose required an explicit interpretive choice, the same kind of disclosure
  Step 062 recorded for "duplicate IDs" meaning duplicate *names*.
- **Conflict policy is "at most one live role per `(NodeId, ScopeId)` pair; a second assignment replaces the
  first."** This step's own required implementation guidance says only "provide conflict policy," without
  naming the exact rule. The chosen rule is the direct, unambiguous reading consistent with how
  `SpatialPathCatalog.CreateAsync`'s own `allowReplaceExisting` path already treats a second write to the
  same identity: a replace is a new version of the same thing, not silently stacked state, and the
  superseded value is preserved in `History`, never lost. Unlike `SpatialPathCatalog.CreateAsync`, no
  `allowReplaceExisting` flag was added here — a role reassignment is a strictly smaller, narrower mutation
  (no name-collision ambiguity to resolve, since the pair's own identity is the exact `(NodeId, ScopeId)`
  the caller supplies) — so there is nothing for a caller to opt out of; replacing is simply the unambiguous
  correct behavior for "assign a new role to a prop that already has one in this scope." Disclosed here as
  a deliberate, tested definition, not an ambiguity left unresolved.
- **`IRoleAssignmentCatalog` is a fresh, additive interface, not an extension of `ISpatialPathCatalog`.**
  Section 3 does not propose a role-tag contract for Step 063 at all — unlike Steps 060-062, there was no
  pre-existing proposed signature to reconcile against. A separate interface was chosen because role
  assignments are explicitly independent of paths (this step's own implementation guidance, verbatim), with
  their own persisted aggregate, their own on-disk cache file and their own conflict policy — folding it
  into `ISpatialPathCatalog` would conflate two deliberately separate concerns the spec itself keeps apart
  ("Assign roles independently of paths").
- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists** — unchanged, carried-forward disclosure from
  Step 059-062 (confirmed again via a full repository `Glob` for `*.axaml`: zero results). This step's
  required acceptance text ("Role assignments are available in choreography UI") is proven at the bindable
  view-model layer (`DisplayPathEditorViewModel.RoleAssignments`) a real SCREEN 12 view would bind to, the
  same precedent Step 059-062's own evidence already established for SCREEN 05 — not a pixel-rendering
  claim. Role support was added to the existing `DisplayPathEditorViewModel` (SCREEN 05's own view model)
  rather than a new, never-before-referenced SCREEN 12 view model, consistent with Step 062's own precedent
  of extending the one view model this project actually has rather than fabricating an unused one.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands, the same pre-existing environment limitation recorded in every prior
step's own evidence; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 063 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After implementing the new Display-project types:
dotnet build src\XlightsAI.Studio.Display\XlightsAI.Studio.Display.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After extending DisplayPathEditorViewModel:
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). Confirms the new types compile across the full solution
  # graph, not only in isolation.

dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo --filter "FullyQualifiedName~Step063"
  # Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 270 ms

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --filter "FullyQualifiedName~Step063"
  # Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 401 ms

dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 39, Skipped: 0, Total: 39, Duration: 229 ms
  # (32 pre-existing Step 059/060/061/062 + 7 new Step 063)

dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 183, Skipped: 0, Total: 183, Duration: 3 s
  # (177 pre-existing + 6 new Step 063)

dotnet format XlightsAI.Studio.slnx --verify-no-changes --include src/XlightsAI.Studio.Display tests/XlightsAI.Studio.Display.Tests src/XlightsAI.Studio.Shell tests/XlightsAI.Studio.Shell.Tests --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate.

dotnet test XlightsAI.Studio.slnx --nologo       # full regression, final
```

### Targeted test results (Step 063's own 13 new tests, real run)

```
Step063AcceptanceTests (Display, 7 new):
  Step063_Acceptance_Passes_RoleAssignmentsAreAvailableAfterLoad                     Passed
  AssignAsync_SamePairTwice_ReplacesWithVersionedHistory_ConflictPolicy              Passed
  ClearAsync_RemovesLiveAssignmentAndRecordsHistory                                  Passed
  AssignAsync_InvalidInput_RejectsBeforeAnyWrite_NegativePath                        Passed
  ClearAsync_UnknownPair_Throws_NegativePath                                         Passed
  AssignAsync_Cancelled_DoesNotPersist_NegativePath                                  Passed
  Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath                   Passed
Display.Tests total: 39/39 passed (32 Step 059/060/061/062 unmodified + 7 new Step 063), 0 failed, 0 skipped.

Step063DisplayPathEditorViewModelTests (Shell, 6 new):
  LoadRoleAssignmentsAsync_NothingSavedYet_ReturnsEmptyRoleAssignments                Passed
  AssignRoleAsync_SamePropDifferentScopes_BothAvailableInChoreographyUi               Passed
  ClearRoleAsync_RemovesAssignmentFromRoleAssignments                                 Passed
  AssignRoleAsync_NothingOpen_SetsActionErrorAndLeavesRoleAssignmentsEmpty_NegativePath  Passed
  AssignRoleAsync_UnrecognizedRole_RejectsWithoutPartialMutation_NegativePath          Passed
  ClearRoleAsync_UnknownPair_RejectsWithoutPartialMutation_NegativePath                Passed
Shell.Tests total: 183/183 passed (177 pre-existing unmodified + 6 new Step 063), 0 failed, 0 skipped.
```

### Full regression (final, after all Step 063 implementation and tests)

```
Passed! - XlightsAI.Studio.Xlights.Tests.dll        74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll        91/91
Passed! - XlightsAI.Studio.Display.Tests.dll        39/39   (32 Step 059/060/061/062 + 7 new Step 063)
Passed! - XlightsAI.Studio.Projects.Tests.dll       25/25
Passed! - XlightsAI.Studio.Structure.Tests.dll      78/78
Passed! - XlightsAI.Studio.Melody.Tests.dll        117/117
Passed! - XlightsAI.Studio.Audio.Tests.dll          38/38
Passed! - XlightsAI.Studio.Shell.Tests.dll         183/183  (177 pre-existing + 6 new Step 063)
Passed! - XlightsAI.Studio.Rhythm.Tests.dll         82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll      31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll         101/101
```

**859 total (across all 11 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from Steps
001-062 still passes unmodified (846 baseline, unchanged); the only additions are this step's own 7 new
tests in `XlightsAI.Studio.Display.Tests` (32 + 7 = 39) and 6 new tests in `XlightsAI.Studio.Shell.Tests`
(177 + 6 = 183).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad at
`%TEMP%\xlai-step063-demo-proj`, deleted after this run) exercises the real `ShowLayoutImporter`/
`DisplayGraphBuilder`/`RoleAssignmentCatalog`/`JsonRoleAssignmentStore` directly against a real temp copy of
this step's own fixture show directory (never the repository's own copy) — no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step063-demo-proj
```

Observed real output:

```
=== Step 063 manual demo: real persisted role tags (lead/rhythm/ambient/hero/accent), no test harness ===

--- Scenario 1: import the real fixture layout, build the spatial graph ---
  Graph: 11 nodes, 10 edges, VersionId=31f454d4-d8e4-4e93-bcad-c6e71869c476

--- Scenario 2: same prop (Arch-01) plays Lead in phrase A, Ambient in phrase B ---
  Node Arch-01: role=Lead, scope=5c5a0623-2a7f-4475-9510-bc4c46cb1178, version=0
  Node Arch-01: role=Ambient, scope=8ce67937-3d07-4492-9475-1a95fec15ed9, version=0

--- Scenario 3: conflict policy -- reassigning the same (node, scope) pair replaces, not stacks ---
  Live role for Arch-01/phraseA is now: Hero (version 1)
  History now holds 1 superseded assignment(s): Lead@v0

--- Scenario 4: assign Accent to a second prop, then clear it ---
  Live assignments after clear: 2 (Arch-02 removed, Arch-01 untouched)

--- Scenario 5: restart (brand-new catalog/store instance over the same on-disk directory) ---
  Reloaded version: 5, live assignments: 2, history: 2
  Reloaded Arch-01/phraseA role survived restart as Hero: True

--- Scenario 6: invalid input is rejected and never corrupts the project (negative path) ---
  Unknown node rejected: True, error="Node 'Does-Not-Exist' does not exist in the layout with source hash 'AD8C72D435486452C2A90EE98082AF30BE90000334E88BC9C31D1DBF9DC0EC76'."
  Unrecognized role rejected: True, error="Unknown role tag 'Soloist'; expected one of: Lead, Rhythm, Ambient, Hero, Accent."
  Source xlights_rgbeffects.xml bytes unchanged: True

Step 063 manual demo completed successfully.

Cleaning up demo show/cache directories.
```

Direct, real, non-test-harness confirmation of this step's own required text: (1) "Role assignments are
available in choreography UI" — Scenarios 2 and 5 show real assignments persisted and reloaded through a
brand-new catalog/store instance (the real read path a choreography UI would use). (2) "Assign roles
independently of paths so same prop can play lead in one phrase and background later" — Scenario 2 shows
the exact same prop (Arch-01) holding two different live roles in two different scopes simultaneously,
proven without any `SpatialPath` ever being constructed in this demo. (3) "provide conflict policy" —
Scenario 3 shows a second assignment to the same `(node, scope)` pair replacing the first, with the prior
value preserved in `History`. (4) "Invalid input, cancellation or stale persisted state must not corrupt the
project" — Scenario 6 confirms the real on-disk `xlights_rgbeffects.xml` bytes are byte-identical before and
after both rejected attempts.

## 6. Fixtures / input hashes

- `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml` — the
  same Step 059/060/061/062 golden fixture, reused unmodified (no new fixture file needed for this step).
  Its five-arch/three-roofline layout is sufficient to exercise role assignment, replacement, clearing and
  cross-scope independence deterministically.
- Negative-path tests that isolate validation logic alone (unknown node, unrecognized role, unknown
  `(node, scope)` pair on clear, cancellation, corrupt JSON) use the same real fixture-built graph, consistent
  with Step 062's own disclosed convention.
- The real demo's own persisted `RoleAssignmentSet` JSON file was written to a temporary directory for
  inspection and deleted, along with the temp copy of the show directory (outside the repository, under
  `%TEMP%`), by the demo's own `finally` block (confirmed by "Cleaning up demo show/cache directories." in
  the real output above); the throwaway demo *project* was deleted after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **No real SCREEN 12 CHOREOGRAPHY Avalonia/AXAML view exists** — unchanged from Step 059-062's own
  disclosed gap; this step's required acceptance text is proven directly by both the automated tests and the
  real, non-test-harness demo above at the bindable view-model layer a real SCREEN 12 view would bind to,
  not a pixel-rendering claim. Every other screen in this repository has the identical gap (confirmed via a
  full repository `Glob` for `*.axaml` — zero results) — this is the project's own pre-existing, consistent
  convention, not a shortcut unique to this step.
- **`RoleAssignment.ScopeId` is an opaque `Guid`, not wired to any real Phase 08 phrase identity** — disclosed
  in §3 as a deliberate boundary decision (`XlightsAI.Studio.Display` has no project reference to
  `XlightsAI.Studio.Structure`); a consuming phase (choreography planning, Phase 10's own `ChoreographyPlan`)
  is expected to pass a real phrase/section id into this same `Guid` slot, not a gap this step itself needs
  to close.
- **Geometry-aware gesture resolution (`ISpatialGestureResolver`, "C-D-E-G-E-D-C across both" and "prove
  geometry-aware effects target correct xLights props") is not implemented here** — unchanged from Step
  062's own disclosed deferral; the phase's own numbered-step table assigns that scope to Step 064.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope — this
  step's own required acceptance text does not itself name an xLights-specific manual rendering action (it
  is a domain/persistence/conflict-policy claim over the real, already-accepted Phase 02/Step 059-062
  pipeline), proven directly in both the automated tests and the real, non-test-harness demo above.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 059-062's own
  closing evidence** (not reopened or re-resolved here): Phase 06's own stem-separation gate remains
  BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately run/recorded.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — persisted, versioned, conflict-policy-safe
role-tag assignments independent of any `SpatialPath` — not a claim that any carried-forward phase-level gap
listed in §7 is resolved, nor a claim that geometry-aware gesture resolution, Step 064's own later scope, is
complete).

- "Role assignments are available in choreography UI" (required acceptance, verbatim) — proven by
  `Step063_Acceptance_Passes_RoleAssignmentsAreAvailableAfterLoad` (Display, real catalog/store) plus
  `AssignRoleAsync_SamePropDifferentScopes_BothAvailableInChoreographyUi` (Shell, real view-model/catalog/
  store) — independently confirmed end-to-end by the real demo's Scenarios 2 and 5.
- "Assign roles independently of paths so same prop can play lead in one phrase and background later;
  provide conflict policy" (implementation guidance, verbatim) — proven by
  `Step063_Acceptance_Passes_RoleAssignmentsAreAvailableAfterLoad` and
  `AssignAsync_SamePairTwice_ReplacesWithVersionedHistory_ConflictPolicy` (Display) plus
  `AssignRoleAsync_SamePropDifferentScopes_BothAvailableInChoreographyUi` (Shell) — independently confirmed
  by the real demo's Scenarios 2 and 3. No `SpatialPath` is ever constructed or referenced anywhere in the
  new role-assignment code path, confirming genuine independence from paths.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `AssignAsync_InvalidInput_RejectsBeforeAnyWrite_NegativePath`,
  `ClearAsync_UnknownPair_Throws_NegativePath`, `AssignAsync_Cancelled_DoesNotPersist_NegativePath`,
  `Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath` and the two Shell-layer "rejects without
  partial mutation" tests — independently confirmed by the real demo's Scenario 6, including a direct
  real-bytes-unchanged check for the source XML.
- Full regression: **859/859 passing** (across the 11 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-062 still passes unmodified (846 baseline, unchanged), plus this step's
  own 7 new Display tests + 6 new Shell tests.
- **Step 059's, Step 060's, Step 061's and Step 062's own already-accepted contracts and tests are
  completely untouched in behavior** — no file any of those steps produced was edited; the only
  Shell-project change is the additive extension of `DisplayPathEditorViewModel` (no existing member's
  signature or behavior changed).
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase 06's
  own gate remains BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately
  run/recorded (§7); geometry-aware gesture resolution remains Step 064's own later scope (§7).

Per this step's own closing instruction ("do not execute the next step until this step is proven"), Step
064 ("Map pitch intervals/movement to jumps, holds and path traversal") is not started until this evidence
is reviewed and accepted.
