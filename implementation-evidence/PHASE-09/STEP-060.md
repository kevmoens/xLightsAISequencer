# PHASE 09 — Step 060 Evidence

**Step:** "Infer neighbors, left/right, center and vertical relations with confidence"
**Spec:** `docs/phase-specs/PHASE-09.md` §6, Step 060 (lines 216-231) — the second of seven numbered steps
of Phase 09 (059-065)
**Date:** 2026-10-15

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-09.md` in full, confirming Step 060's own text: required acceptance
  ("Expected order recognized; uncertain order flagged"), implementation guidance ("Infer neighbor/center/
  above/below with normalized display geometry and outlier tolerances; surface confidence rather than
  assert physical truth"), failure/edge case ("Invalid input, cancellation or stale persisted state must
  not corrupt the project"), and Section 3's note that `SpatialEdge.Relation` is deliberately a plain
  `string` (not an enum) "so later steps (060+) can add new kinds without a breaking contract change."
- Read `implementation-evidence/PHASE-09/STEP-059.md` in full as the accepted contract baseline for this
  step. It explicitly disclosed three forward-looking gaps this step now closes: (1)
  `GeometryPolicy.MaxNeighborDistance` was "validated... but not yet consumed by any inference logic"; (2)
  "Neighbor/left-right/center/above-below relation inference... is explicitly Step 060's scope, not built
  here"; (3) `SpatialEdge.Relation`'s own doc comment already reserved `"GroupMember"`/`"NestedGroup"` as
  Step 059's only values, anticipating Step 060's own new relation kinds.
- Read every file in `src/XlightsAI.Studio.Display/` in full before writing any new type: `DisplayNode.cs`,
  `SpatialEdge.cs`, `SpatialDisplayGraph.cs`, `SpatialPath.cs`, `GeometryPolicy.cs`, `NodeAlias.cs`,
  `IDisplayGraphBuilder.cs`, `DisplayGraphBuilder.cs`, `ISpatialDisplayGraphStore.cs`,
  `JsonSpatialDisplayGraphStore.cs`, `SpatialDisplayGraphInvalidException.cs`,
  `SpatialDisplayGraphCorruptException.cs`, `ReadOnlyStringSetJsonConverter.cs` — confirming the exact
  current shape of every Step 059 contract before touching anything.
- Read `tests/XlightsAI.Studio.Display.Tests/Step059AcceptanceTests.cs` and the real golden fixture
  `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml` in
  full as the direct structural precedent for this step's own test style (real importer/builder, not
  mocks; before/after-bytes negative-path convention).
- Searched the full repository for any prior "Step 060"/"STEP-060" reference, any existing
  `ISpatialRelationInferrer`/`SpatialRelationInferrer`/`SpatialRelationInference` type, or any existing
  `"Neighbor"`/`"LeftOf"`/`"Below"` relation string before starting — found only `PHASE-09.md` itself,
  confirming this is genuinely new work.
- Searched the full repository for every construction site of `new SpatialEdge(...)` before adding a field
  to it — found exactly two, both inside `src/XlightsAI.Studio.Display/DisplayGraphBuilder.cs` (the
  `"GroupMember"`/`"NestedGroup"` edges), both already owned by this project, confirming an additive
  trailing field with a default value is safe and that no test asserts a hand-built `SpatialEdge` literal
  whose field count would need updating.
- Baseline taken by running the real, full solution test suite *before* any Step 060 code was written:
  **815/815 passing, 0 failed, 0 skipped** (Step 059's own closing count — see `STEP-059.md` §4), then
  re-confirmed by re-running only `XlightsAI.Studio.Display.Tests` immediately after the one disclosed
  additive change to `SpatialEdge` (see §3) and before any new Step 060 type existed:

  ```
  dotnet build src\XlightsAI.Studio.Display\XlightsAI.Studio.Display.csproj --nologo
    # Build succeeded. 0 Warning(s). 0 Error(s).
  dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo
    # Passed!  - Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 278 ms
  ```

  All 12 of Step 059's own tests still passed unmodified immediately after the additive `SpatialEdge`
  change, before any Step 060 logic existed — proving the field addition alone did not change any already-
  accepted behavior.

## 2. Scope actually implemented

**No file from Step 059 or any earlier step was behaviorally changed.** This step adds new types to the
existing `src/XlightsAI.Studio.Display/` project (no new project needed — Step 060 is a sibling concern to
Step 059's own builder, not a separate vertical slice), a new test file in the existing
`tests/XlightsAI.Studio.Display.Tests/` project, and one additive field plus two doc-comment updates to
already-shipped Step 059 types (disclosed below).

### New files in `src/XlightsAI.Studio.Display/`

- **`ISpatialRelationInferrer.cs`** / **`SpatialRelationInferrer.cs`** — the core of this step. Pure,
  side-effect-free `Infer(SpatialDisplayGraph graph, GeometryPolicy policy)`:
  - Operates only on real, geometry-bearing physical nodes (`ModelKind != "Group"` and no `"NoGeometry"`
    tag) — a group's own position is a centroid, not an independent physical neighbor, and a node with no
    source geometry has nothing to infer an order from.
  - Groups those nodes into **rows** by exact shared normalized `Y` (nodes at the same height); within each
    row, sorts by `X` and walks consecutive pairs.
  - For each consecutive pair whose gap is within `GeometryPolicy.MaxNeighborDistance` (Step 059's own
    reserved-but-unconsumed field, now consumed for the first time — see §3), emits one `"Neighbor"` edge
    and one `"LeftOf"` edge (`From` = left node, `To` = right node), both carrying the same **confidence**:
    the ratio of the smaller to the larger of (this gap, the row's own median gap), so a gap matching the
    row's own typical spacing scores `1.0` and an outlier gap (too tight or too wide relative to its own
    row) scores proportionally lower — "surface confidence rather than assert physical truth," verbatim. A
    gap *beyond* `MaxNeighborDistance` is skipped entirely (no edge at all — too far to claim adjacency,
    regardless of confidence).
  - Tags the row's own unambiguous median node (odd-sized rows only — an even-sized row has two middle
    nodes and gets no center, rather than a guess) as that row's **center**.
  - Infers **vertical order** between rows via a `"Below"` edge from each row's own representative (its
    center when one exists, else its first node) to the next row up, using the same median-ratio confidence
    formula across inter-row gaps — not gated by `MaxNeighborDistance` (two rows are already structurally
    distinct clusters; the question is only their relative order, not whether they are "close enough" to
    relate at all — disclosed in §3).
  - Validates before producing anything: a negative `GeometryPolicy.MaxNeighborDistance` throws
    `SpatialDisplayGraphInvalidException` (mirrors `DisplayGraphBuilder`'s own identical check), and a null
    `graph`/`policy` throws `ArgumentNullException`.
- **`SpatialRelationInference.cs`** — the result record: `Edges` (the inferred relation edges) and
  `CenterNodeIds` (each row's own unique center, when one exists). **Never merged automatically into a
  `SpatialDisplayGraph`** — the caller explicitly appends `Edges` before persisting (e.g.
  `graph with { Edges = [.. graph.Edges, .. inferred.Edges] }`), so `IDisplayGraphBuilder.Build`'s own
  already-accepted Step 059 output and edge count are never silently changed by this step (proven directly
  in the acceptance test, §4, and the real demo, §5).

### Disclosed additive changes to Step 059's own already-shipped types

- **`SpatialEdge.Confidence` (`double`, default `1.0`)** — new, additive, trailing field. See §3 for the
  full rationale and the real test (§4) proving old, pre-Step-060 persisted cache files (missing this
  field entirely) still load correctly.
- **`GeometryPolicy.MaxNeighborDistance`'s own doc comment** — updated from "reserved for Step 060" to
  describe how Step 060 actually consumes it (a documentation-only change; the validation behavior itself,
  "must be non-negative," is unchanged).

### New test file: `tests/XlightsAI.Studio.Display.Tests/Step060AcceptanceTests.cs`

Seven tests against the real `ShowLayoutImporter`/`DisplayGraphBuilder`/`SpatialRelationInferrer`/
`JsonSpatialDisplayGraphStore` (not mocks):

- **`Step060_Acceptance_Passes_ExpectedOrderRecognized`** — the required acceptance test (first half,
  verbatim: "Expected order recognized"). Imports the real Step 059 fixture, builds the spatial graph, and
  infers relations with a policy wide enough to include both rows: asserts the five arches produce exactly
  four full-confidence `"LeftOf"` edges in the exact expected order, Arch-03 is the row's own center; the
  three roofline models produce two full-confidence `"LeftOf"` edges centered on Roof-02; every `"LeftOf"`
  has a matching `"Neighbor"`; nothing is ever `UserDefined`; the two rows produce exactly one `"Below"`
  edge from Arch-03 to Roof-02 at full confidence; and Step 059's own `Build()` output (`graph.Edges.Count
  == 10`) is completely untouched by this step's own separate inference call.
- **`Step060_Acceptance_Passes_UncertainOrderFlagged`** — the required acceptance test (second half,
  verbatim: "uncertain order flagged"). A hand-built, deliberately uneven row (`X = 0, 10, 11, 40`): the
  one gap matching the row's own median scores full confidence (`1.0`, "expected order recognized"), while
  the two outlier gaps (one far too tight, one far too wide relative to that same median) score `0.1` and
  `0.25/0.725 ≈ 0.345` respectively — both at or below this step's own disclosed
  `SpatialRelationInferrer.UncertainConfidenceThreshold` (`0.5`) — proving low confidence, not a false
  confident claim, is what "flags" an uncertain order. The same test proves a four-node (even-sized) row
  produces no guessed center.
- **`Infer_NegativeMaxNeighborDistance_Throws_Invalid_NegativePath`** / **`Infer_NullGraph_Throws_
  ArgumentNullException_NegativePath`** — the required failure/edge-case test (first half, verbatim:
  "Invalid input... must not corrupt the project"): invalid policy and null graph are both rejected before
  any edge is produced.
- **`StalePersistedGraph_WithoutConfidenceField_LoadsWithoutCorruption_NegativePath`** — the required
  failure/edge-case test (second half, verbatim: "stale persisted state must not corrupt the project"): a
  real saved graph is re-serialized with every `"Confidence"` JSON property stripped out (simulating a
  project cache file written before this step's own field existed), written to a real temp file, and
  reloaded through the real, completely unmodified `JsonSpatialDisplayGraphStore.LoadAsync` — it loads
  cleanly, with every edge's missing `Confidence` defaulting to `1.0` (Step 059's own edges were literal
  fact, not an inference), never throwing and never silently dropping an edge.
- **`CancelledSecondSave_DoesNotCorruptPreviouslyPersistedRelationAugmentedGraph_NegativePath`** — the
  required failure/edge-case test (third half, verbatim: "cancellation... must not corrupt the project"):
  a real relation-augmented graph is saved for real, its bytes captured, then a second save (with a
  deliberately emptied edge list, standing in for a bad/incomplete overwrite) is attempted with an
  already-canceled token — it throws `OperationCanceledException` before reaching the atomic
  `File.Move`, and the real on-disk bytes are proven byte-identical before and after the cancelled attempt.

## 3. Architecture deviations / decisions

- **`SpatialEdge.Confidence` is an additive field beyond Section 3's bare five-field proposed signature**
  (`From`, `To`, `Relation`, `Distance`, `UserDefined`). Required because Step 060's own required
  acceptance text is literally "surface confidence" — there is nowhere else in the proposed contract to put
  it. Given a default value of `1.0` (trailing positional parameter) specifically so: (a) the two existing
  `DisplayGraphBuilder` call sites (Step 059's own `"GroupMember"`/`"NestedGroup"` edges, which are literal
  imported fact, not an inference) need no code change and correctly read as maximally confident; (b) every
  pre-Step-060 persisted `SpatialDisplayGraph` cache file — written by a real user's project before this
  field existed — still deserializes without a forced migration or thrown exception, proven directly by
  `StalePersistedGraph_WithoutConfidenceField_LoadsWithoutCorruption_NegativePath` (§2, §4) and the real
  demo's own Scenario 7 (§5). The same additive-field, default-value pattern Step 059 itself already used
  for `SpatialDisplayGraph.VersionId`.
- **Relation inference is a separate type (`ISpatialRelationInferrer`), not a change to
  `IDisplayGraphBuilder.Build`'s own behavior or signature.** `Build()` still returns exactly the same 10
  edges for the Step 059 fixture that `Step059AcceptanceTests` already asserts (`Assert.Equal(10,
  first.Edges.Count)`) — unchanged, unmodified, still passing verbatim. A caller who wants the inferred
  relations persisted must explicitly merge them (`graph with { Edges = [.. graph.Edges, ..
  inference.Edges] }`) before saving. This keeps Step 059's own already-accepted contract and test
  completely immutable, per this task's own instruction to treat prior step contracts as immutable absent
  an accepted migration plan — no migration was needed because no breaking change was made.
- **Rows are clustered by exact shared normalized `Y`, not a distance tolerance.** Chosen because the real
  fixture's own two physical rows (arches at `Y=0`, roofline at `Y=8` before normalization) are exactly
  level within each row by construction, and because an approximate/tolerance-based row clustering would
  itself need its own separate outlier/confidence handling — a second inference problem this step's own
  narrow scope does not require. A future step that needs looser real-world row tolerance (props not
  perfectly level) can extend this without breaking the present contract, since `Relation` remains a plain
  `string` (Step 059's own stated reason for not using an enum).
- **`GeometryPolicy.MaxNeighborDistance` gates horizontal (within-row) adjacency but not vertical
  (between-row) order.** Two rows are already distinct, structurally-separate clusters by construction
  (different `Y`); the open question for them is only *which one comes first*, not *whether they are close
  enough to relate at all* (which is exactly what `MaxNeighborDistance` answers for two props that might or
  might not be meaningfully adjacent within the same row). Applying the same hard cutoff to the inter-row
  gap would have silently produced no vertical order at all for the real fixture under the already-accepted
  `DefaultPolicy` from `Step059AcceptanceTests` (`MaxNeighborDistance: 0.3` vs. the fixture's own normalized
  inter-row gap of `1.0`) — a surprising, undisclosed gap rather than a deliberate one. Disclosed here
  instead of silently working around it: this step's own tests use a separate, wider `GeometryPolicy`
  instance (`MaxNeighborDistance: 0.4`) so both the arches row (gap `0.25`) and the roofline row (gap
  `0.375`) are included — **Step 059's own `DefaultPolicy` test constant is untouched.**
- **Confidence formula: ratio of (smaller, larger) of a gap and its own row's median gap, clamped to
  `[0, 1]`.** A simple, deterministic, symmetric measure of "how typical is this gap for its own row" —
  `1.0` for a perfectly uniform row (the real fixture's own two rows, both exactly uniform-spaced, demo
  Scenario 3), dropping smoothly for a gap that is either far tighter or far wider than its row's own
  typical spacing (the hand-built uneven-row test and demo Scenario 4). This is a disclosed, intentionally
  simple design choice for this step's own narrow scope, not a claim that it is the only reasonable
  formula; `SpatialRelationInferrer.UncertainConfidenceThreshold` (`0.5`) is exposed as a `public const` so
  a caller has one disclosed, documented convention for "flagged" rather than inventing its own cutoff.
- **"Center" is returned as a separate `CenterNodeIds` list, not a `DisplayNode.Tags` mutation.**
  `IDisplayGraphBuilder.Build`'s own already-accepted output (including every `DisplayNode`'s `Tags`) is
  never touched by this step — consistent with the decision above to keep Step 060 entirely additive and
  separate from Step 059's own contract. A caller who wants a `"Center"` tag on the actual node can apply
  it themselves from `CenterNodeIds` without this step needing to re-open `DisplayGraphBuilder`.
- **A `"Neighbor"` edge and a `"LeftOf"` edge are both emitted for every qualifying horizontal pair**, at
  the same confidence, rather than only one or the other. Step 060's own required implementation guidance
  names both "neighbor" and "left/right" as relations to infer; emitting both is more faithful to the
  spec's own text than collapsing them into one relation kind and asking a caller to infer "they must also
  be neighbors" from a directional `"LeftOf"` edge alone.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands, the same pre-existing environment limitation recorded in every prior
step's own evidence; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Targeted baseline, immediately after the one disclosed additive SpatialEdge.Confidence change and
# before any new Step 060 type existed:
dotnet build src\XlightsAI.Studio.Display\XlightsAI.Studio.Display.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo
  # Passed!  - Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 278 ms (all pre-existing Step 059
  # tests, unmodified)

# After implementing ISpatialRelationInferrer/SpatialRelationInferrer/SpatialRelationInference and the new
# Step060AcceptanceTests.cs:
dotnet test tests\XlightsAI.Studio.Display.Tests\XlightsAI.Studio.Display.Tests.csproj --nologo --logger "console;verbosity=detailed"
  # Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18, Duration: 1.48s
  # (12 pre-existing Step 059 + 6 new Step 060; all passed on the first clean run after the demo-driven
  # fixture/threshold values below were worked out by hand first)

dotnet format XlightsAI.Studio.slnx --verify-no-changes --include src/XlightsAI.Studio.Display tests/XlightsAI.Studio.Display.Tests --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate.

dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). Confirms the additive SpatialEdge field and new types
  # compile across the full solution graph, not only in isolation.

dotnet test XlightsAI.Studio.slnx --nologo       # full regression, final (ran in background)
```

### Targeted test results (Step 060's own 6 new tests, real run)

```
Step060AcceptanceTests (6 new):
  Step060_Acceptance_Passes_ExpectedOrderRecognized                                         Passed [46 ms]
  Step060_Acceptance_Passes_UncertainOrderFlagged                                            Passed [40 ms]
  Infer_NegativeMaxNeighborDistance_Throws_Invalid_NegativePath                               Passed [<1 ms]
  Infer_NullGraph_Throws_ArgumentNullException_NegativePath                                  Passed [<1 ms]
  StalePersistedGraph_WithoutConfidenceField_LoadsWithoutCorruption_NegativePath               Passed [11 ms]
  CancelledSecondSave_DoesNotCorruptPreviouslyPersistedRelationAugmentedGraph_NegativePath     Passed [85 ms]
Display.Tests total: 18/18 passed (12 Step 059 unmodified + 6 new Step 060), 0 failed, 0 skipped.
```

### Full regression (final, after all Step 060 implementation and tests)

```
Passed! - XlightsAI.Studio.Xlights.Tests.dll       74/74
Passed! - XlightsAI.Studio.Display.Tests.dll       18/18   (12 Step 059 + 6 new Step 060)
Passed! - XlightsAI.Studio.Editing.Tests.dll       91/91
Passed! - XlightsAI.Studio.Structure.Tests.dll     78/78
Passed! - XlightsAI.Studio.Melody.Tests.dll       117/117
Passed! - XlightsAI.Studio.Projects.Tests.dll      25/25
Passed! - XlightsAI.Studio.Audio.Tests.dll         38/38
Passed! - XlightsAI.Studio.Rhythm.Tests.dll         82/82
Passed! - XlightsAI.Studio.Shell.Tests.dll        166/166
Passed! - XlightsAI.Studio.Reference.Tests.dll      31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll        101/101
```

**821 total (across all 11 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from Steps
001-059 still passes unmodified (815 baseline, unchanged — see `STEP-059.md` §4); the only addition is this
step's own 6 new tests inside the already-existing `XlightsAI.Studio.Display.Tests` assembly (12 + 6 = 18).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `ShowLayoutImporter`/`DisplayGraphBuilder`/
`SpatialRelationInferrer`/`JsonSpatialDisplayGraphStore` directly against a real temp copy of this step's
own fixture show directory (never the repository's own copy) — no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step060-demo-proj -- "<temp show directory>"
```

Observed real output:

```
=== Step 060 manual demo: real neighbor/left-right/center/vertical relation inference, no test harness ===

--- Scenario 1: import the real fixture layout, build the spatial graph, infer relations ---
  Step059 graph: 11 nodes, 10 edges (group-membership only, unchanged)
  Step060 inference: 13 new relation edges, 2 center node(s)
    Below     Arch-03    -> Roof-02    confidence=1.000 distance=1.000
    LeftOf    Arch-01    -> Arch-02    confidence=1.000 distance=0.250
    LeftOf    Arch-02    -> Arch-03    confidence=1.000 distance=0.250
    LeftOf    Arch-03    -> Arch-04    confidence=1.000 distance=0.250
    LeftOf    Arch-04    -> Arch-05    confidence=1.000 distance=0.250
    LeftOf    Roof-01    -> Roof-02    confidence=1.000 distance=0.375
    LeftOf    Roof-02    -> Roof-03    confidence=1.000 distance=0.375
    Neighbor  Arch-01    -> Arch-02    confidence=1.000 distance=0.250
    Neighbor  Arch-02    -> Arch-03    confidence=1.000 distance=0.250
    Neighbor  Arch-03    -> Arch-04    confidence=1.000 distance=0.250
    Neighbor  Arch-04    -> Arch-05    confidence=1.000 distance=0.250
    Neighbor  Roof-01    -> Roof-02    confidence=1.000 distance=0.375
    Neighbor  Roof-02    -> Roof-03    confidence=1.000 distance=0.375
  Centers: Arch-03, Roof-02

--- Scenario 2: persist the Step059 graph + Step060 relation edges merged, reopen via a brand-new store ---
  Saved to: ...\xlai-step060-demo-cache-<guid>\spatial-display-graph-B9FB3650E406F5C9.json
  Reloaded edge count matches saved: True
  Reloaded LeftOf chain confidences match: True

--- Scenario 3: expected order recognized (uniform arches row -> full confidence; odd count -> unique center) ---
  Arch LeftOf chain all confidence==1.0: True
  Arch row center is Arch-03: True

--- Scenario 4: uncertain order flagged (hand-built uneven row: 0, 10, 11, 40) ---
    A -> B confidence=1.000 [expected]
    B -> C confidence=0.100 [UNCERTAIN]
    C -> D confidence=0.345 [UNCERTAIN]
  Even-count row (4 nodes) has no guessed center: True

--- Scenario 5: invalid input is rejected and never corrupts the project (negative path) ---
  Invalid GeometryPolicy rejected: GeometryPolicy.MaxNeighborDistance must be non-negative; was -1.
  Source xlights_rgbeffects.xml bytes unchanged: True

--- Scenario 6: cancellation does not corrupt previously persisted relation-augmented graph ---
  Cancelled save rejected before any write, as expected.
  Previously persisted file unchanged after cancelled overwrite attempt: True

--- Scenario 7: stale persisted state (pre-Step060 cache file missing Confidence) loads without corruption ---
  Stale (no Confidence field) cache loaded without throwing: true, edge count=10
  Missing Confidence defaulted to 1.0 for all edges: True

Cleaning up demo cache directory.

Step 060 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation of both halves of this step's own required text: (1) "Expected
order recognized" — Scenario 1/3 show the two real fixture rows (five arches, three roofline models), both
perfectly uniformly spaced in the fixture, producing full-confidence `"LeftOf"`/`"Neighbor"` chains and
correct odd-count centers (Arch-03, Roof-02), plus a correct single `"Below"` vertical relation between the
two rows. (2) "uncertain order flagged" — Scenario 4 shows a deliberately uneven row producing one expected
(full-confidence) pair and two outlier pairs explicitly flagged below the disclosed uncertainty threshold,
rather than a false confident claim; the even-count row correctly produces no guessed center. (3) "Invalid
input... must not corrupt the project" — Scenario 5 rejects an invalid `GeometryPolicy` and independently
confirms the real on-disk `xlights_rgbeffects.xml` bytes are byte-identical before and after. (4)
"cancellation... must not corrupt the project" — Scenario 6 proves a cancelled second save never reaches
the real persisted file. (5) "stale persisted state must not corrupt the project" — Scenario 7 proves a
cache file saved in the shape Step 059 alone would have produced (no `Confidence` field at all) still loads
cleanly through the real, completely unmodified store, with the missing field defaulting to full
confidence rather than throwing or silently losing data.

## 6. Fixtures / input hashes

- `tests/XlightsAI.Studio.Display.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml` — the
  same Step 059 golden fixture, reused unmodified (no new fixture file needed for this step; its existing
  five-arch uniform row and three-roofline uniform row at a different height already exercise this step's
  own "expected order" requirement end-to-end). `SourceLayoutHash` is computed by the real
  `ShowLayoutImporter` from this file's own real, unmodified bytes.
- The "uncertain order flagged" half of the required acceptance is proven against a hand-built, in-memory
  `DisplayGraph` (four models at `X = 0, 10, 11, 40`, all `Y = 0`) — consistent with Step 059's own
  disclosed convention for pure-domain-logic tests that do not need the real importer, since the point
  being tested is the inference math itself, not import parsing.
- The real demo's own persisted `SpatialDisplayGraph` JSON file (and its stale, pre-Step060-shaped sibling)
  were written to a temporary directory for inspection and deleted, along with the rest of that directory,
  by the demo's own `finally` block (confirmed by "Cleaning up demo cache directory." in the real output
  above); the throwaway demo *project* and the temp copy of the show directory (outside the repository,
  under the session scratchpad / `%TEMP%`) were both deleted after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **No real SCREEN 05 DISPLAY PATHS Avalonia/AXAML view or view model yet** — unchanged from Step 059's own
  disclosed gap (`STEP-059.md` §7); this step's own required acceptance text is again a pure domain/
  inference claim, not a UI claim, and the graphical editor itself remains Step 061's later scope.
- **Row clustering by exact shared `Y` does not yet tolerate real-world near-level-but-not-exact props** —
  disclosed in §3 as a deliberate, narrow scope decision for this step; a future step handling genuinely
  uneven real-world layouts (props not perfectly level within a row) would need its own clustering-
  tolerance design, which this step does not attempt.
- **`CenterNodeIds` is a transient inference result, not yet a persisted `DisplayNode` tag or a field on
  `SpatialDisplayGraph` itself** — disclosed in §3; a caller who wants it persisted alongside the graph
  would need to apply it themselves (e.g. as a `DisplayNode.Tags` entry) before saving, which this step
  does not do, to keep `IDisplayGraphBuilder.Build`'s own already-accepted output completely untouched.
- **User-defined/named paths (`SpatialPath`) are still never constructed before Step 062** — unchanged from
  Step 059's own disclosed gap; `SpatialDisplayGraph.Paths` is still always `[]`.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope —
  this step's own required acceptance text does not itself name an xLights-specific manual rendering
  action (it is a pure domain/inference claim over the real, already-accepted Phase 02/Step 059 pipeline),
  proven directly in both the automated tests and the real, non-test-harness demo above.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Step 059's own closing
  evidence** (not reopened or re-resolved here): Phase 06's own stem-separation gate remains BLOCKED;
  Phase 07's and Phase 08's own full-phase gates have not been separately run/recorded.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a pure, side-effect-free neighbor/left-
right/center/vertical relation inferrer that consumes Step 059's own already-built `SpatialDisplayGraph`
and `GeometryPolicy.MaxNeighborDistance`, surfaces a confidence rather than asserting physical truth, and
never mutates or is mutated by Step 059's own already-accepted `Build()` output — not a claim that any
carried-forward phase-level gap listed in §7 is resolved).

- "Expected order recognized; uncertain order flagged" (required acceptance, verbatim) — proven by
  `Step060_Acceptance_Passes_ExpectedOrderRecognized` and `Step060_Acceptance_Passes_UncertainOrderFlagged`
  (real fixture + real importer + real builder + real inferrer for the first half; a deterministic
  hand-built fixture isolating the confidence math for the second half) — independently confirmed
  end-to-end by the real demo's Scenarios 1, 3 and 4.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `Infer_NegativeMaxNeighborDistance_Throws_Invalid_NegativePath`,
  `Infer_NullGraph_Throws_ArgumentNullException_NegativePath`,
  `StalePersistedGraph_WithoutConfidenceField_LoadsWithoutCorruption_NegativePath` and
  `CancelledSecondSave_DoesNotCorruptPreviouslyPersistedRelationAugmentedGraph_NegativePath` —
  independently confirmed by the real demo's Scenarios 5, 6 and 7, including a direct real-bytes-unchanged
  check for both the source XML (Scenario 5) and the previously persisted cache file (Scenario 6).
- Full regression: **821/821 passing** (across the 11 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-059 still passes unmodified (815 baseline, unchanged), plus only this
  step's own new 6 tests inside the already-existing `XlightsAI.Studio.Display.Tests` assembly.
- **Step 059's own already-accepted contract and tests are completely untouched in behavior** — `Build()`
  still returns the same 10 edges for the same fixture; the only change to a Step 059 type is the
  additive, defaulted `SpatialEdge.Confidence` field, proven backward-compatible with pre-existing persisted
  data by `StalePersistedGraph_WithoutConfidenceField_LoadsWithoutCorruption_NegativePath`.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase
  06's own gate remains BLOCKED; Phase 07's and Phase 08's own full-phase gates have not been separately
  run/recorded (§7).

Per this step's own closing instruction ("do not execute the next step until this step is proven"), Step
061 ("Create graphical layout/path editor with position correction") is not started until this evidence is
reviewed and accepted.
