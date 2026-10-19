# PHASE 03 — Step 017 Evidence

**Step:** "Build prop rows, group expansion and stable mapping to model IDs"
**Spec:** `docs/phase-specs/PHASE-03.md` §6, Step 017 (lines 211–225)
**Date:** 2026-10-11

## 1. Scope actually implemented

- A pure-domain row hierarchy (`XlightsAI.Studio.Editing`): `TimelineRowId`/`TimelineRowKind` (stable
  identity — a model's own `ModelId`, or a group's own name — never a display row index),
  `TimelineRowNode`/`TimelineRowTree` (the nested tree), `TimelineRowTreeBuilder.Build(DisplayGraph?)`
  (resolves the imported, flat, reference-based group shape from PHASE-02 into an actual tree, never
  re-parsing XML or touching any source file), and `TimelineRowSlot`/`TimelineRowFlattener` (pre-order,
  depth-tagged, collapse-aware flattening for virtualized rendering).
- Shell-layer wiring (`XlightsAI.Studio.Shell`): `TimelineViewModel` gained `Rows`
  (`IReadOnlyList<TimelineRowItemView>`), `RowWarnings`, and `ToggleGroupExpansion(TimelineRowId)`;
  `LoadTimeline` now accepts an optional `DisplayGraph? layout` (default `null`, preserving every
  existing Step 016 call site's exact behavior); collapse state (`_collapsedGroupIds`) is retained
  across reloads of the same `TimelineViewModel` instance. `ShellViewModel.LoadTimelineCommand` now
  passes `ShowImport.Layout` through, so a real Discover → Load Timeline flow binds real rows.
- UI (`XlightsAI.Studio.App`): SCREEN 13 TIMELINE gained a "Props" row list below the time ruler,
  bound to `Timeline.Rows`, with a toggle button per group row (▸/▾, via new
  `ExpandedGlyphConverter`), per-depth indentation (reusing `DoubleToLeftThicknessConverter`, keyed
  off a new `IndentPixels` projection — same pattern Step 016 used for ruler ticks, avoiding the
  documented Avalonia `Canvas.Left`/`DataTemplate` container gotcha), bold group labels (new
  `BoolToFontWeightConverter`), an explicit "no layout imported yet" empty-state message, and a
  visible warnings list for any row-tree finding (e.g. a group-reference cycle).

## 2. Architecture deviations / decisions

- **`XlightsAI.Studio.Editing` now references `XlightsAI.Studio.Xlights`.** Step 016 deliberately
  kept Editing Contracts-only because `TimelineViewport` had no need for the imported-layout types.
  Step 017's own required acceptance ("Bind rows to stable imported model IDs and group hierarchy")
  cannot be met without `DisplayGraph`/`DisplayModel`/`DisplayModelGroup`/`DisplayGroupMember(Kind)`,
  all of which live in `Xlights`. Disclosed, not silently introduced; no existing contract was
  redefined or duplicated — `Editing` consumes the PHASE-02 types as-is, it does not own them.
- **`TimelineViewModel.LoadTimeline()` signature changed** from no parameters to
  `LoadTimeline(DisplayGraph? layout = null)`. Backward compatible by construction (default value),
  verified by every existing Step 016 test still passing unmodified.
- **Cycle-safety policy for group nesting**, since `DisplayGraph`'s own data model allows a group to
  reference another group's name with no XML-level containment and no cycle prevention anywhere
  upstream (`ShowLayoutImporter.ClassifyGroupMember` resolves `Group`-kind references without
  checking for cycles — confirmed by reading `ShowLayoutImporter.cs` directly): `TimelineRowTreeBuilder`
  (a) stops recursion the moment a group name reappears on its own ancestor path, replacing that
  occurrence with a leaf placeholder node and a warning, and (b) after building from every "true"
  root (a group never referenced by any other group), promotes any group that is *still* unplaced —
  i.e. reachable only through a self-contained mutual-reference cycle, like two groups that only
  reference each other — to its own top-level row with a warning, rather than letting it silently
  vanish from the timeline. This is new logic with no precedent in the codebase; it is covered by
  three dedicated tests (direct self-reference, mutual two-group cycle, and the "everything still
  shows up, never throws" baseline).
- **Row identity for unresolved/submodel group members.** `DisplayGroupMemberKind.Submodel` and
  `.Unresolved` entries have no corresponding `DisplayModel`/`ModelId`. Rather than drop them (which
  would silently hide a broken reference the importer already flagged as a warning) or invent a new
  identity type, they are shown as a leaf row keyed by `TimelineRowId.ForModel(new ModelId(rawName))`
  with `ModelId: null` on the node — visible, but explicitly not claiming to be a verified model.

## 3. Commands actually executed

```
dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj --nologo
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo
dotnet test XlightsAI.Studio.slnx --nologo
```

All run via the PowerShell tool (the sandboxed POSIX Bash tool in this session is non-functional —
every invocation returns exit 1 with no output, same environment limitation already recorded in
`implementation-evidence/PHASE-03/STEP-016.md` §3/§10; PowerShell is the working shell in this
environment, consistent with that prior step).

### Build results

All three builds: `Build succeeded. 0 Warning(s). 0 Error(s).`

(One compile error was hit and fixed during implementation: `CS9174` — a `[]` collection expression
cannot initialize `IReadOnlySet<string>`, which is not constructible; replaced with
`new HashSet<string>(StringComparer.Ordinal)` at both call sites in `TimelineRowTreeBuilder.cs`.)

### Test results

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 22, Skipped: 0, Total: 22  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 60, Skipped: 0, Total: 60  - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31  - XlightsAI.Studio.Reference.Tests.dll
```

**209 total, 0 failed, 0 skipped** — up from the Step 016 baseline of 196 (recorded in
`STEP-16.md`), i.e. all 196 pre-existing tests still pass unmodified, plus 13 new tests for this
step: 8 in `tests/XlightsAI.Studio.Editing.Tests/Step017AcceptanceTests.cs` (domain-level:
row-tree building, stable ID mapping across overlapping groups, collapse-aware flattening, empty/
null-layout edge cases, and three dedicated negative-path cycle/unresolved-reference tests), 5 in
`tests/XlightsAI.Studio.Shell.Tests/Step017ShellWiringTests.cs` (ViewModel-level: real
`ShellViewModel`/`TimelineViewModel` wiring, toggle-expand command, collapse-state retention across
reload, stable-ID mapping through the real shell, and the malformed-cyclic-layout negative path).

One test-authoring mistake was caught and fixed during this run: the first draft of
`Step017_Acceptance_Passes_MalformedCyclicLayout_LoadsSuccessfully_NegativePath` asserted a single
visible row for a self-referencing group; the real (correct) behavior is two rows — the group plus
its cycle-truncated child placeholder — matching the already-passing domain-level test for the same
scenario. Fixed by correcting the assertion, not the behavior.

### Manual/desktop demonstration

```
Start-Process dotnet "run --project src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --no-build -c Debug"
```

The Avalonia desktop app process started and ran for 8+ seconds with no exception, no stderr output,
and no early exit (process was still running when deliberately stopped) — confirms the new SCREEN 13
TIMELINE XAML (`Timeline.Rows` ItemsControl, toggle buttons, new converters) does not crash app
startup/binding resolution. **No screenshot was captured**: this environment has no desktop
screenshot/automation tool available (the Claude-in-Chrome tools are browser-only and do not apply
to a native Avalonia window). This is reported here explicitly as **NOT VERIFIED** for the visual
screenshot artifact specifically, not silently claimed — per the user's standing waiver (recorded in
assistant memory) this does not block the phase gate, since xLights-specific human visual
verification is separately out of scope for this step (Step 017 touches no XSQ/XML output at all).
The automated test suite is the real evidence for this step's behavioral correctness.

## 4. Fixtures / input hashes

No new fixture files were added. All row-tree tests construct `DisplayGraph`/`DisplayModel`/
`DisplayModelGroup` fixtures directly in-memory (consistent with `Step016AcceptanceTests`'s own
style of hand-built `TimeWindow` fixtures) rather than parsing XML, since this step's own scope is
"bind rows to an already-imported `DisplayGraph`," not importing. The existing
`fixtures/layouts/layout-shape-a-arch-megatree.xml` and `layout-shape-b-matrix-stars.xml` (both
flat, non-nested groups) remain unchanged and were not exercised directly by this step's new tests,
since neither demonstrates group nesting; this is flagged below as a known gap.

## 5. Unresolved issues / known gaps

- **No fixture with real nested groups.** The two existing synthetic layout fixtures have no
  group-of-groups nesting; this step's "group nesting" acceptance is proven by hand-built in-memory
  `DisplayGraph` fixtures in the test suite, not by a round-trip through `ShowLayoutImporter` against
  a real nested-group XML file. A good follow-up (noted in the earlier repo survey) is a new fixture
  under `fixtures/layouts/` with actual nesting, or borrowing a excerpt from the pinned upstream
  reference's real `xlights_rgbeffects.xml` (`reference/xlights-autosequencer/layout/`), which has
  real, deep, non-nested-but-overlapping groups.
- **No real-world desktop screenshot captured** (see §3 above) — the app's startup/non-crash was
  verified, but the actual visual row/indentation/toggle rendering was not human- or
  screenshot-verified in this session.
- **`ViewportWidthPixels` is still a fixed default (760)**, not wired to the real control's reported
  size — this was already an open item from Step 016 and remains out of this step's narrow scope
  (rows, not ruler width); it does not block this step's own acceptance.
- Row virtualization here means "a collapsed group's subtree is never flattened/materialized at
  all" — there is no UI-level `VirtualizingStackPanel`/windowed rendering yet for a very large flat
  row list (e.g. thousands of ungrouped models). Acceptable for this step's scope (PHASE-02's own
  fixtures are small); a future step should revisit if a real show layout with very many props is
  used.

## 6. PASS / BLOCKED

**Status: PASS**, with the single explicit exception noted above (desktop screenshot artifact NOT
VERIFIED, non-blocking per user waiver). All required acceptance criteria met:

- "Visual rows match loaded layout including group nesting" — proven by
  `Step017_Acceptance_Passes_RowsMatchLoadedLayout_IncludingGroupNesting` and the Shell-level
  equivalent against a real `ShellViewModel`.
- "Bind rows to stable imported model IDs... avoid mixing display row indices with identifiers" —
  proven by `Step017_Acceptance_Passes_StableMappingToModelIds_SameModelUnderOverlappingGroups` and
  `Step017_Acceptance_Passes_StableModelIdMapping_SurvivesAcrossRows`.
- "Retain user group collapse state" — proven by
  `Step017_Acceptance_Passes_ToggleExpandCommand_CollapsesAndRetainsStateAcrossReload`.
- "Invalid model/format/version must fail safely without rewriting source files" — proven by three
  dedicated negative-path tests (direct self-reference, mutual cycle, unresolved reference) at the
  domain level, plus one at the Shell/ViewModel level, none of which throw, corrupt state, or touch
  any file.

No previously accepted step's contract was modified except the disclosed, backward-compatible
`TimelineViewModel.LoadTimeline` signature change and the disclosed new `Editing → Xlights` project
reference. Do not begin Step 018 until this report is reviewed.
