# PHASE 09 — Step 059 Evidence

**Step:** "Normalize coordinates, model groups and relationships into display graph"
**Spec:** `docs/phase-specs/PHASE-09.md` §6, Step 059 (lines 200-215) — the first of seven numbered steps of
Phase 09 (059-065)
**Date:** 2026-10-15

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-09.md` in full, confirming Step 059's own text: required acceptance
  ("Fixtures map consistent physical positions and stable IDs"), implementation guidance ("Create stable
  graph node IDs, geometry metadata, group membership edges and controlled alias resolution for changing
  xLights model names"), failure/edge case ("Invalid model/format/version must fail safely without
  rewriting source files"), and Section 3's full proposed `DisplayNode`/`SpatialEdge`/`SpatialPath`/
  `SpatialDisplayGraph`/`GeometryPolicy` signatures. Also read Section 2's cross-phase ownership table
  (`SpatialDisplayGraph`/`SpatialPath` first introduced in Phase 09, "normalized geometry separate from
  imported xLights identities" — not a redefinition of Phase 02's own `DisplayGraph`).
- Read every file in `src/XlightsAI.Studio.Xlights/` relevant to the imported layout contract before
  writing any new type: `DisplayGraph.cs`, `DisplayModel.cs`, `DisplayModelGroup.cs`,
  `DisplayGroupMember.cs`, `DisplayGroupMemberKind.cs`, `Point2.cs`, and `ShowLayoutImporter.cs` in full
  (confirmed: only `xlights_rgbeffects.xml` is required to import — no `xlights_networks.xml` dependency
  for this step's own scope; group membership classification already distinguishes
  Model/Group/Submodel/Unresolved references, which this step's edge-building logic reuses unmodified).
  **No file in `XlightsAI.Studio.Xlights` was modified.**
- Read `tests/XlightsAI.Studio.Xlights.Tests/Step011AcceptanceTests.cs` in full as the direct structural
  precedent for this step's own real-importer, real-fixture acceptance test style (asserting persisted
  domain state, not a mocked call; a negative path that reads source bytes before/after a rejected
  operation).
- Read `src/XlightsAI.Studio.Structure/JsonBarFingerprintStore.cs` /
  `IBarFingerprintStore.cs` / `BarFingerprintInvalidException.cs` / `BarFingerprintCorruptException.cs` /
  `SongFingerprintSet.cs` in full as the direct structural precedent for this step's own
  `JsonSpatialDisplayGraphStore`/`ISpatialDisplayGraphStore`/`SpatialDisplayGraphInvalidException`/
  `SpatialDisplayGraphCorruptException`/`SpatialDisplayGraph.VersionId` (atomic temp-file-then-`File.Move`
  writes, deterministic `GetPath` naming, a `VersionId` that changes every build run even when the
  computed output is numerically identical).
- Searched the full repository for any prior "Step 059"/"STEP-059" reference, any existing
  `XlightsAI.Studio.Display` project, or any `SpatialDisplayGraph`/`DisplayGraphBuilder` type before
  starting — found only `PHASE-09.md` itself, confirming this is genuinely new work.
- Baseline taken by running the real, full solution test suite *before* any Step 059 code was written:
  **803/803 passing, 0 failed, 0 skipped** across the 10 pre-existing test assemblies (matches Step 058's
  own recorded final count exactly — no drift):

  ```
  dotnet test XlightsAI.Studio.slnx --nologo
  Passed! - XlightsAI.Studio.Xlights.Tests.dll      74/74
  Passed! - XlightsAI.Studio.Editing.Tests.dll      91/91
  Passed! - XlightsAI.Studio.Melody.Tests.dll      117/117
  Passed! - XlightsAI.Studio.Structure.Tests.dll    78/78
  Passed! - XlightsAI.Studio.Projects.Tests.dll     25/25
  Passed! - XlightsAI.Studio.Audio.Tests.dll        38/38
  Passed! - XlightsAI.Studio.Shell.Tests.dll       166/166
  Passed! - XlightsAI.Studio.Rhythm.Tests.dll       82/82
  Passed! - XlightsAI.Studio.Stems.Tests.dll       101/101
  Passed! - XlightsAI.Studio.Reference.Tests.dll    31/31
  Total: 803/803, 0 failed, 0 skipped
  ```

## 2. Scope actually implemented

**No file from any prior phase/step was modified.** This step adds a brand-new project
(`src/XlightsAI.Studio.Display/`), a brand-new test project
(`tests/XlightsAI.Studio.Display.Tests/`), and two additive `XlightsAI.Studio.slnx` edits (inserting both
new projects into the existing `/src/` and `/tests/` solution folders, alongside the pre-existing
`XlightsAI.Studio.Structure`/`XlightsAI.Studio.Structure.Tests` entries).

### New project: `src/XlightsAI.Studio.Display/`

References `XlightsAI.Studio.Contracts` and `XlightsAI.Studio.Xlights` (to consume Phase 02's own
`DisplayGraph`/`DisplayModel`/`DisplayModelGroup`/`DisplayGroupMember`/`Point2` as read-only input —
never modifies them).

- **`DisplayNode.cs`**, **`SpatialEdge.cs`**, **`SpatialPath.cs`**, **`SpatialDisplayGraph.cs`**,
  **`GeometryPolicy.cs`** — Section 3's own proposed records, implemented field-for-field, plus one
  disclosed additive field (`SpatialDisplayGraph.VersionId`, see §3).
- **`NodeAlias.cs`** — new, Step 059's own concrete expression of "controlled alias resolution for
  changing xLights model names": an explicit, caller-supplied `(ModelId NodeId, string XlightsName)`
  pairing. Never automatic/fuzzy name matching — a renamed model with no matching alias simply becomes a
  new node.
- **`IDisplayGraphBuilder.cs`** / **`DisplayGraphBuilder.cs`** — the pure, side-effect-free projection
  from an imported `DisplayGraph` to a `SpatialDisplayGraph`:
  - Every `DisplayModel` becomes a `DisplayNode` (model kind = the model's own `DisplayAs`); every
    `DisplayModelGroup` becomes its own `DisplayNode` (model kind = literal `"Group"`) when
    `GeometryPolicy.RespectGroups` is `true` (Step 059's own "model groups... into display graph" scope;
    skipped entirely when `false`).
  - Node ids are the model's own `ModelId` (same identity Phase 02's `DisplayModel.Id` already uses)
    unless an explicit `NodeAlias` pins a renamed model's current name to a prior stable id.
  - Geometry is normalized independently per axis to `[0, 1]` across every model with a source
    `WorldPosX`/`WorldPosY` point (Section 3: "Store normalized 2D coordinates only for initial
    preview"); a model with no source point gets `X=Y=0.5` and a `"NoGeometry"` tag — never guessed.
  - A group's own position is the centroid of its *resolvable, geometry-bearing* direct `Model` members
    only (never `Submodel`/`Unresolved`/nested-`Group` members, and never assumed pixel-equivalent to a
    simple prop — Section 3: "Keep group/submodel identities... without assuming they are
    pixel-equivalent to simple prop nodes"); a group with no resolvable member falls back to the same
    `0.5/0.5` + `"NoGeometry"` convention.
  - Edges: one `"GroupMember"` edge per direct `Model` member (member → group), one `"NestedGroup"` edge
    per direct `Group` member (nested group → parent group). `Submodel`/`Unresolved` members produce no
    edge in this step (Step 060's own later neighbor-inference scope is deliberately not touched here).
  - Validates before producing anything: a negative `GeometryPolicy.MaxNeighborDistance`, a duplicate
    model name, an alias set that is internally ambiguous (two different stable ids for the same current
    name, or two different current names collapsed onto the same stable id), or a model/group name
    collision that would produce an ambiguous node id — all throw
    `SpatialDisplayGraphInvalidException` before any node/edge is built.
- **`SpatialDisplayGraphInvalidException.cs`** / **`SpatialDisplayGraphCorruptException.cs`** — mirror
  `BarFingerprintInvalidException`/`BarFingerprintCorruptException`'s own convention.
- **`ISpatialDisplayGraphStore.cs`** / **`JsonSpatialDisplayGraphStore.cs`** — persistence, keyed by
  `SourceLayoutHash` (this aggregate is layout-scoped, not song-scoped — Section 3's own "Inputs:...
  source file hash"). Atomic temp-file-then-`File.Move` writes, mirroring
  `JsonBarFingerprintStore` exactly. **Never opens, reads for writing, or otherwise touches any
  `xlights_rgbeffects.xml`/show-directory file** — only its own cache directory.
- **`ReadOnlyStringSetJsonConverter.cs`** — a small, disclosed `System.Text.Json` converter (see §3) so
  `DisplayNode.Tags` can keep Section 3's own proposed `IReadOnlySet<string>` type while still being
  round-trippable through the store.

### New test project: `tests/XlightsAI.Studio.Display.Tests/`

- **`Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml`** — new golden fixture: five arches in
  a row (`WorldPosX` 0,10,20,30,40; `WorldPosY` 0), a three-segment roofline at a different height
  (`WorldPosX` 5,20,35; `WorldPosY` 8), grouped as `"Arches"`/`"Roofline"`, both nested under
  `"All-Props"` alongside an unresolved reference (`"Ghost-Prop"`) and a submodel reference
  (`"Arch-01/Segment-1"`) — exercises every member-kind branch of the edge-building logic in one real,
  real-importer-parsed fixture.
- **`Step059AcceptanceTests.cs`** — 12 tests against the real `ShowLayoutImporter`/`DisplayGraphBuilder`/
  `JsonSpatialDisplayGraphStore` (not mocks):
  - `Step059_Acceptance_Passes_FixturesMapConsistentPositionsAndStableIds` — the required acceptance
    test, verbatim name. Imports the real fixture, builds twice, and asserts: node ids/positions/
    `ModelKind` are identical across both builds (only `VersionId` differs); five arches normalize to
    strictly increasing X positions `0.0 → 1.0`; a group's centroid equals the average of its resolvable
    members' positions; `"GroupMember"`/`"NestedGroup"` edge counts are exactly as the fixture's
    structure predicts (5 + 3 + 2 = 10); no edge is ever produced for the unresolved or submodel
    reference; `Paths` is empty; `SourceLayoutHash` round-trips from the import.
  - `Step059_Negative_InvalidGeometryPolicy_FailsSafely_WithoutRewritingSourceFile` — the required
    failure/edge-case test, verbatim: an invalid policy is rejected via
    `SpatialDisplayGraphInvalidException`, and the real fixture's own on-disk bytes are read before and
    after and asserted byte-identical (the same before/after-bytes convention
    `Step011AcceptanceTests.ModelGroupMissingRequiredNameAttribute_FailsSafely_NegativePath` already
    established).
  - Six further negative-path unit tests against hand-built `DisplayGraph` fixtures: duplicate model
    name, group name colliding with a model id, an ambiguous alias (same name, two stable ids), an alias
    collapsing two distinct models onto the same stable id — each throwing
    `SpatialDisplayGraphInvalidException` with nothing built.
  - `AliasResolution_PinsRenamedModelToPriorStableId` — proves the alias mechanism itself: a renamed
    model with no alias gets a fresh id; the same rename with an explicit `NodeAlias` keeps the prior
    stable id.
  - `RespectGroupsFalse_SkipsGroupNodesAndEdges`, `NoGeometry_FallsBackToCenterWithTag` — policy-off and
    no-source-geometry behavior.
  - Three store tests: real save-then-load round trip over a real temporary directory, a missing-file
    `FileNotFoundException`, and a corrupt-file `SpatialDisplayGraphCorruptException` (mirroring
    `IBarFingerprintStore`'s own already-proven contract).

### `XlightsAI.Studio.slnx` changes (additive project-registration only)

Both new projects inserted into their respective existing `/src/` and `/tests/` solution folders,
immediately after the pre-existing `XlightsAI.Studio.Structure`/`XlightsAI.Studio.Structure.Tests`
entries. No existing `<Project>` entry was removed, reordered relative to any other entry, or modified.

## 3. Architecture deviations / decisions

- **`SpatialDisplayGraph.VersionId` (`Guid`) is additive beyond Section 3's bare four-field proposed
  signature.** Required so "Fixtures map consistent physical positions and stable IDs" is checkable at
  all: the *nodes/edges* must be provably stable across an independent rebuild, while the *build run
  itself* still needs its own changing provenance stamp (the same `SongFingerprintSet.VersionId`
  convention Phase 08 already established) so a caller/store can tell two builds apart even when their
  content is identical. Confirmed in the real demo (§5, Scenario 3): node ids are stable across an
  independent rebuild while `VersionId` always differs.
- **`IDisplayGraphBuilder.Build` has a second, additive 3-argument overload** (`..., IReadOnlyList<NodeAlias>
  aliases`) beyond Section 3's bare 2-argument proposed signature. Required to express "controlled alias
  resolution for changing xLights model names" at all — Section 3's own 2-argument signature has nowhere
  to pass an alias set. The 2-argument overload still exists unchanged and simply forwards to the
  3-argument one with an empty list, so every caller following the proposed signature verbatim still
  compiles and behaves identically.
- **`DisplayNode.Tags` keeps Section 3's own proposed `IReadOnlySet<string>` type**, which
  `System.Text.Json`'s default reflection-based serializer cannot deserialize (no settable concrete
  collection to populate into an interface) — confirmed by a real failing test run before the fix (see
  commands, §4). Rather than widen the contract to `IReadOnlyList<string>` just to satisfy persistence, a
  small `ReadOnlyStringSetJsonConverter` (reads/writes as a plain JSON string array, backed by a
  `HashSet<string>`) is applied via `[JsonConverter]` on the record parameter. A narrow, disclosed,
  persistence-layer-only accommodation; the domain contract itself is unchanged.
- **Group nodes are modeled as `DisplayNode`s with `ModelKind == "Group"`, not a separate type.** Chosen
  because Section 3's own proposed `SpatialDisplayGraph.Nodes` is a single `IReadOnlyList<DisplayNode>`
  (no separate group-node list in the proposed signature), and because a group is itself a valid,
  independently-targetable effect destination in xLights (PHASE-02's own prior documented conclusion) —
  treating it as "just another kind of node" rather than a second-class concept matches that reality.
- **Edge `Relation` is a plain `string`, not an enum** (`"GroupMember"`/`"NestedGroup"` in this step).
  Section 3's own proposed `SpatialEdge` signature already types `Relation` as `string`; kept as-is so
  Step 060's own later relation kinds (neighbor/left-right/above-below) can be added without a breaking
  contract change to this step's already-shipped type.
- **Submodel and Unresolved group members produce no node and no edge in this step.** Step 059's own
  scope is "model groups and relationships" at the model/group level; Phase 02's own
  `DisplayGroupMemberKind.Submodel`/`.Unresolved` classifications (Step 011's already-shipped scope) are
  read but not independently modeled here — a submodel is not a `DisplayModel` in Phase 02's own import
  contract, and an unresolved reference has no node to attach an edge to by definition. Disclosed, not
  silently dropped: proven by the fixture's own `"Arch-01/Segment-1"` and `"Ghost-Prop"` references
  producing zero edges in the required acceptance test.
- **Duplicate-model-name and model/group-id-collision checks are new validation this step introduces**,
  not present in Phase 02's own `ShowLayoutImporter` (which does not reject a literal duplicate `<model
  name="X">` entry, since nothing before this step depended on model names being globally unique node
  identities). A disclosed, narrow addition scoped to this step's own stable-id requirement, not a
  retroactive change to Phase 02's own accepted import behavior.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands (`echo test`, `dotnet --version`), the same pre-existing environment
limitation recorded in every prior step's own evidence; PowerShell was used for every `dotnet`/
file-system command below without incident.

```
# Baseline, before any Step 059 code (ran in background; see §1 for the 803/803 result):
dotnet test XlightsAI.Studio.slnx --nologo

# New project scaffolding and build:
dotnet build src\XlightsAI.Studio.Display\XlightsAI.Studio.Display.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# First targeted test run surfaced two real failures, both fixed before proceeding:
dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo --logger "console;verbosity=detailed"
  # 1. XML comment in the new fixture contained "--", rejected by the real hardened XML reader
  #    (ShowLayoutImporter's own DTD/comment parsing) -- fixed by rewording the comment.
  # 2. IReadOnlySet<string> (DisplayNode.Tags) is not deserializable by System.Text.Json's default
  #    reflection converter -- fixed by adding ReadOnlyStringSetJsonConverter (see §3).
  # Re-ran clean: 12/12 passed.

dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). Confirms both new projects compile as part of the full
  # solution graph, not only in isolation.

dotnet format XlightsAI.Studio.slnx --verify-no-changes --include src/XlightsAI.Studio.Display tests/XlightsAI.Studio.Display.Tests --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate (Step 008's own
  # "dotnet format ... --verify-no-changes" requirement).

dotnet test XlightsAI.Studio.slnx --nologo       # full regression, final (ran in background; ~22 minutes
                                                  # wall clock, dominated by XlightsAI.Studio.Reference.Tests
                                                  # at 17m20s and XlightsAI.Studio.Stems.Tests at 1m50s --
                                                  # both pre-existing, unrelated to this step's own changes)

# Real, non-test-harness manual demo (built under the session scratchpad, deleted after this run):
dotnet new console -o <scratchpad>\xlai-step059-demo-proj --force --no-restore
dotnet add <scratchpad>\xlai-step059-demo-proj reference src\XlightsAI.Studio.Display\XlightsAI.Studio.Display.csproj
dotnet add <scratchpad>\xlai-step059-demo-proj reference src\XlightsAI.Studio.Xlights\XlightsAI.Studio.Xlights.csproj
dotnet add <scratchpad>\xlai-step059-demo-proj reference src\XlightsAI.Studio.Contracts\XlightsAI.Studio.Contracts.csproj
dotnet run --project <scratchpad>\xlai-step059-demo-proj -- "<temp copy of the fixture show directory>"
```

### Targeted test results (Step 059's own 12 new tests, real run)

```
Display.Tests (12 new): Total tests: 12  Passed: 12  Total time: 1.55s
```

### Full regression (final, after all Step 059 implementation and tests)

```
Passed! - XlightsAI.Studio.Xlights.Tests.dll       74/74
Passed! - XlightsAI.Studio.Display.Tests.dll       12/12   (brand new)
Passed! - XlightsAI.Studio.Editing.Tests.dll       91/91
Passed! - XlightsAI.Studio.Melody.Tests.dll       117/117
Passed! - XlightsAI.Studio.Structure.Tests.dll     78/78
Passed! - XlightsAI.Studio.Projects.Tests.dll      25/25
Passed! - XlightsAI.Studio.Audio.Tests.dll         38/38
Passed! - XlightsAI.Studio.Shell.Tests.dll        166/166
Passed! - XlightsAI.Studio.Rhythm.Tests.dll        82/82
Passed! - XlightsAI.Studio.Stems.Tests.dll        101/101
Passed! - XlightsAI.Studio.Reference.Tests.dll     31/31
```

**815 total (across all 11 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from
Steps 001-058 still passes unmodified (803 baseline, unchanged); the only addition is this step's own new
`XlightsAI.Studio.Display.Tests` assembly (0 existing + 12 new = 12).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `ShowLayoutImporter`/`DisplayGraphBuilder`/
`JsonSpatialDisplayGraphStore` directly against a real copy of this step's own fixture show directory (a
temp copy of `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/
xlights_rgbeffects.xml`, kept outside the repository so nothing in the repo itself was ever written to) —
no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step059-demo-proj -- "<temp show directory>"
```

Observed real output:

```
=== Step 059 manual demo: real spatial display graph builder, no test harness ===

--- Scenario 1: import the real fixture layout and build the spatial graph ---
  Imported models: 8, groups: 3
  Spatial nodes: 11 (models + groups), edges: 10, VersionId: 4990e7a7-d73e-47ef-bb7d-c46aabf0c49f
    Model 'Arch-01' Id=Arch-01 X=0.000 Y=0.000
    Model 'Arch-02' Id=Arch-02 X=0.250 Y=0.000
    Model 'Arch-03' Id=Arch-03 X=0.500 Y=0.000
    Model 'Arch-04' Id=Arch-04 X=0.750 Y=0.000
    Model 'Arch-05' Id=Arch-05 X=1.000 Y=0.000
    Model 'Roof-01' Id=Roof-01 X=0.125 Y=1.000
    Model 'Roof-02' Id=Roof-02 X=0.500 Y=1.000
    Model 'Roof-03' Id=Roof-03 X=0.875 Y=1.000
    Group 'Arches' Id=Arches X=0.500 Y=0.000
    Group 'Roofline' Id=Roofline X=0.500 Y=1.000
    Group 'All-Props' Id=All-Props X=0.500 Y=0.500

--- Scenario 2: persist, then reopen via a brand-new store instance over the same directory ---
  Saved to: ...\xlai-step059-demo-cache-<guid>\spatial-display-graph-B9FB3650E406F5C9.json
  Node ids stable after reopen: True
  Physical positions consistent after reopen: True

--- Scenario 3: rebuild from the same import a second time (no reopen) ---
  Node ids stable across independent rebuild: True
  VersionId changes every rebuild (provenance, not identity): True

--- Scenario 4: a model renamed in a fresh import still resolves to its prior stable id via an explicit alias ---
  Renamed node keeps original stable id: True

--- Scenario 5: invalid input is rejected and never corrupts the project (negative path) ---
  Invalid GeometryPolicy rejected: GeometryPolicy.MaxNeighborDistance must be non-negative; was -1.
  Source xlights_rgbeffects.xml bytes unchanged: True
  Corrupt cache file rejected without crash: Spatial display graph cache at '...\corrupt-demo.json' is
    corrupt or not a valid serialized SpatialDisplayGraph.

Step 059 manual demo completed successfully.

Cleaning up demo cache directory.
```

Direct, real, non-test-harness confirmation of both halves of this step's own required text: (1)
"Fixtures map consistent physical positions and stable IDs" — Scenario 1 shows five arches normalized to
consistent, monotonically increasing X positions and two groups centered on their own members' real
centroid; Scenario 2 proves ids and positions survive a full save-then-reopen round trip through a
brand-new store instance; Scenario 3 proves ids are stable across an independent in-memory rebuild of the
same import (only the run's own `VersionId` provenance stamp changes); Scenario 4 proves a renamed model
still resolves to its prior stable id via an explicit, controlled alias — never a guess. (2) "Invalid
model/format/version must fail safely without rewriting source files" — Scenario 5 rejects an invalid
`GeometryPolicy` and independently confirms the real on-disk `xlights_rgbeffects.xml` bytes are
byte-identical before and after, and separately proves a corrupt cache file is rejected cleanly via
`SpatialDisplayGraphCorruptException` rather than crashing or silently accepting bad data.

## 6. Fixtures / input hashes

- `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml` —
  new golden fixture, committed to the repository (not an in-memory-only construction): 8 models (5
  arches, 3 roofline segments), 3 model groups (`Arches`, `Roofline`, `All-Props`), one deliberate
  unresolved reference (`Ghost-Prop`) and one deliberate submodel reference (`Arch-01/Segment-1`).
  `SourceLayoutHash` is computed by the real `ShowLayoutImporter` from this file's own real bytes (SHA-256
  of the literal on-disk content) — not a placeholder, unlike several prior Structure-phase steps' own
  in-memory-only fixtures.
- Hand-built `DisplayGraph` fixtures used by the nine unit-level negative-path/alias tests are constructed
  in-memory at test-run time with the literal placeholder source hash `"hash"`/`"hash-1"`/`"hash-2"` —
  consistent with prior steps' own disclosed convention for pure-domain-logic tests that do not exercise
  the real importer.
- The real demo's own persisted `SpatialDisplayGraph` JSON file was written to its own temporary directory
  for inspection (`%TEMP%\xlai-step059-demo-cache-*\spatial-display-graph-*.json`) and deleted, along with
  the rest of that directory, by the demo's own `finally` block (confirmed by "Cleaning up demo cache
  directory." in the real output above); the throwaway demo *project* and the temp copy of the show
  directory (outside the repository, under the session scratchpad / `%TEMP%`) were both deleted after this
  evidence was recorded.

## 7. Unresolved issues / known gaps

- **No real SCREEN 05 DISPLAY PATHS Avalonia/AXAML view or view model yet.** This step's own required
  acceptance text ("Fixtures map consistent physical positions and stable IDs") is a pure domain/builder
  claim, not a UI claim — consistent with Section 6's own numbered-step pattern, where the graphical
  editor itself is Step 061's later scope ("Create graphical layout/path editor with position
  correction"). No `ShellViewModel`/`ShellScreen` wiring was added in this step either, since there is no
  screen-level interaction to wire yet (unlike Phase 08's own Step 058, which added a inspector view
  model even without an AXAML view) — Step 059's own scope is strictly the graph-building domain slice.
- **Neighbor/left-right/center/above-below relation inference (Section 3's own broader "Edge relationships
  include neighbor, higher/lower...") is explicitly Step 060's scope, not built here** — disclosed in §3;
  `GeometryPolicy.MaxNeighborDistance` is validated (non-negative) but not yet consumed by any inference
  logic.
- **User-defined/named paths (`SpatialPath`) are declared but never constructed before Step 062** —
  `SpatialDisplayGraph.Paths` is always `[]` from this step's own `IDisplayGraphBuilder.Build`, as
  disclosed in `SpatialPath.cs`'s own doc comment.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope —
  this step's own required acceptance text does not itself name an xLights-specific manual rendering
  action (it is a pure domain/builder claim over the real, already-accepted Phase 02 import pipeline),
  proven directly against the real `ShowLayoutImporter`/`DisplayGraphBuilder`/
  `JsonSpatialDisplayGraphStore` in both the automated tests and the real, non-test-harness demo above.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Phase 08's own closing
  evidence** (not reopened or re-resolved here): Phase 06's own stem-separation gate remains BLOCKED;
  Phase 07's own full-phase gate (demonstration + final gate report) has not been separately run/recorded;
  Phase 08's own full-phase gate (Section 7 demonstration, Section 8 final report) likewise has not been
  separately run/recorded, per Step 058's own closing note that this is left for an explicit, separate
  pass.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a spatial display graph builder that
normalizes an imported layout's coordinates, projects model groups as their own targetable nodes, builds
group-membership/nested-group edges, and resolves a renamed model back to its prior stable node id via an
explicit, controlled alias — not a claim that any carried-forward phase-level gap listed in §7 is
resolved).

- "Fixtures map consistent physical positions and stable IDs" (required acceptance, verbatim) — proven by
  `Step059_Acceptance_Passes_FixturesMapConsistentPositionsAndStableIds` (real fixture, real importer,
  real builder, rebuilt twice with identical node ids/positions) and independently confirmed end-to-end by
  the real demo's Scenarios 1-4 (including the alias-based rename-resolution claim, which the automated
  acceptance test's own companion `AliasResolution_PinsRenamedModelToPriorStableId` also proves in
  isolation).
- "Invalid model/format/version must fail safely without rewriting source files" (failure/edge case,
  verbatim) — proven by `Step059_Negative_InvalidGeometryPolicy_FailsSafely_WithoutRewritingSourceFile`
  plus six further negative-path unit tests (duplicate model name, model/group id collision, two distinct
  ambiguous-alias shapes, a missing cache file, a corrupt cache file) — independently confirmed by the
  real demo's Scenario 5, which also directly verifies the real on-disk source XML's bytes are unchanged.
- Full regression: **815/815 passing** (across the 11 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-058 still passes unmodified (803 baseline, unchanged), plus only this
  step's own new 12-test assembly.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase
  06's own gate remains BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately
  run/recorded (§7).

Per this step's own closing instruction ("do not execute the next step until this step is proven"), Step
060 ("Infer neighbors, left/right, center and vertical relations with confidence") is not started until
this evidence is reviewed and accepted.
