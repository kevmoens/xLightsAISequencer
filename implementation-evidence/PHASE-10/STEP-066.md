# PHASE 10 — Step 066 Evidence

**Step:** "Define semantic musical roles and event precedence rules"
**Spec:** `docs/phase-specs/PHASE-10.md` §6, Step 066 (lines 203-217) — the first of six numbered steps of
Phase 10 (066-071)
**Date:** 2026-10-16

## 0. Instruction-vs-spec deviation (flagged before any code was written)

The task instruction said to read `docs/phase-specs/PHASE-09.md` in full and implement "ONLY Step 66 from
that document." **`PHASE-09.md` does not contain Step 66** — Phase 09's own numbered steps run 059-065
only (confirmed via `docs/PHASE-INDEX.md`'s own table: "09 | 059–065"; all seven already carry independently
recorded PASS evidence, `implementation-evidence/PHASE-09/STEP-059.md` through `STEP-065.md`). Step 066
("Define semantic musical roles and event precedence rules") is specified in **`docs/phase-specs/PHASE-10.md`**
§6 (lines 203-217), the first step of Phase 10 ("Layered Musical Composition," steps 066-071), whose own
`Depends on:` line names Phase 09 explicitly.

**Resolution taken (not asked of the user mid-task, per this session's own standing auto-mode guidance to
make the reasonable call and flag it rather than block):** implemented Step 066 against its real, correct
home, `PHASE-10.md`, and wrote this evidence file at the path `PHASE-10.md` itself prescribes
(`implementation-evidence/PHASE-10/STEP-066.md`), not under `PHASE-09/`. Phase 09's own seven steps were not
re-touched, re-verified or regressed by this work (confirmed in §4's full regression — every pre-existing
Display/Shell test still passes unmodified).

**Carried-forward gap, not this step's own blocker:** Phase 09 has no single consolidated
`implementation-evidence/PHASE-09/GATE-REPORT.md` synthesizing all seven steps into one phase-level
PASS/BLOCKED verdict (unlike e.g. Phase 02, which has `GATE-REPORT.md`) — Step 065's own evidence (§9 there)
already disclosed this as the sole remaining phase-level item. This step does not author that report (out
of this step's own narrow scope); it is flagged here again as a standing, unresolved process gap the user
may want closed before further Phase 10 steps accumulate.

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-10.md` in full, confirming Step 066's own text: required acceptance
  ("Synthetic role mapping yields deterministic priorities"), implementation guidance ("Represent
  lead/accompaniment/accent and instrument track linkage per phrase rather than hardcode one stem per
  display model"), failure/edge case ("Invalid input, cancellation or stale persisted state must not
  corrupt the project"), and Section 2/3's own broader design text this step is the foundation for:
  "`MusicalRoleAssignment` says which source is lead/rhythm/bass/harmony/accent and target path" and
  "Default precedence: user-locked > manual > accepted motif > new automated candidate."
- Read `docs/PHASE-INDEX.md` and `docs/phase-specs/PHASE-09.md` in full, establishing the §0 deviation above
  before writing any code.
- Confirmed no `src/XlightsAI.Studio.Composition` project exists anywhere in the repository (`Glob` for
  `src/*/` returned no match for `Composition`) — this is genuinely new work, the first step of a brand-new
  project, not an extension of an existing contract.
- Read every file in `src/XlightsAI.Studio.Display/` relevant to Phase 09's own established conventions
  before choosing shapes for this step's new types, in particular `RoleTag.cs`/`RoleAssignment.cs`/
  `RoleAssignmentSet.cs`/`IRoleAssignmentCatalog.cs`/`RoleAssignmentCatalog.cs`/`IRoleAssignmentStore.cs`/
  `JsonRoleAssignmentStore.cs` (the closest existing precedent for a named-role, versioned, persisted,
  conflict-policy'd assignment set with a catalog/store split) and `SpatialPathCatalog.cs`/`SpatialPath.cs`/
  `SpatialPathSet.cs`/`DirectionPolicy.cs` (needed to build a real `SpatialPathSet` for this step's own
  `SpatialPathId` validation) and `SpatialDisplayGraphInvalidException.cs`/`SpatialDisplayGraphCorruptException.cs`
  (the exception-naming convention mirrored for this new project).
- Read `tests/XlightsAI.Studio.Display.Tests/Step063AcceptanceTests.cs` in full as the direct structural
  precedent for this step's own acceptance test file (real importer/builder/catalog, not mocks; a
  `NewCacheDirectory` per-test temp directory; a `finally` block deleting it).
- Searched the repository for any prior "Step 066"/"STEP-066"/`MusicalRole`/`MusicalRoleAssignment`/
  `IRolePrecedenceResolver` reference: found only `PHASE-10.md` itself (the numbered-step text and Section
  3's proposed signatures) — confirmed this is genuinely new work with a proposed (not yet implemented) API
  to reconcile against.
- Baseline taken by running the real, full solution build *before* any Step 066 code was written:
  `dotnet build XlightsAI.Studio.slnx --nologo` (clean, 0 warnings/errors, 13 projects including the full
  Phase 01-09 tree) immediately before starting.

## 2. Scope actually implemented

**No file from any prior phase/step was behaviorally changed.** This step adds a brand-new project,
`src/XlightsAI.Studio.Composition/` (referencing only `XlightsAI.Studio.Contracts` and
`XlightsAI.Studio.Display`, per `PHASE-10.md` Section 3's own proposed `using` list), plus a new test
project `tests/XlightsAI.Studio.Composition.Tests/`, and registers both in `XlightsAI.Studio.slnx`.

### New files in `src/XlightsAI.Studio.Composition/`

- **`MusicalRole.cs`** — `enum MusicalRole { Lead, Rhythm, Bass, Harmony, Accent, Atmosphere }`, declared
  verbatim as `PHASE-10.md` Section 3's own proposed signature names and orders it. That declaration order
  doubles as this step's own baseline *role* precedence tie-break (see `RolePrecedenceResolver` below).
- **`PrecedenceTier.cs`** — string constants (`UserLocked`, `Manual`, `AcceptedMotif`, `Automated`), the
  same "string constants, not an enum" convention `RoleTag`/`DirectionPolicy` already established, taken
  verbatim from Section 2's own architectural design-notes sentence: "Default precedence: user-locked >
  manual > accepted motif > new automated candidate." `All`'s own declared order is the tier's own rank.
- **`MusicalRoleAssignment.cs`** — one source track's role for one phrase: `PhraseId`, `SourceTrackId`,
  `Role`, `SpatialPathId`, `PrecedenceTier`, `Priority`, `Weight`, `Version`. Named `MusicalRoleAssignment`,
  not the bare `RoleAssignment` `PHASE-10.md` Section 3's own code block names it — disclosed as an
  architecture decision in §3 below (collision avoidance against the already-accepted
  `XlightsAI.Studio.Display.RoleAssignment`, a different concept, now that this project references
  `Display` directly).
- **`MusicalRoleAssignmentSet.cs`** — the full persisted set for one phrase (`PhraseId`-keyed), mirroring
  `RoleAssignmentSet`'s shape exactly: live `Assignments` plus an append-only `History`, plus `Version`.
- **`CompositionInvalidException.cs`** / **`CompositionCorruptException.cs`** — mirror
  `SpatialDisplayGraphInvalidException`/`SpatialDisplayGraphCorruptException`'s own conventions exactly, for
  this project's own invalid-input and corrupt-cache-file cases respectively.
- **`IMusicalRoleAssignmentStore.cs`** / **`JsonMusicalRoleAssignmentStore.cs`** — persistence boundary,
  byte-for-byte the same atomic-write pattern (same-directory temp file + `File.Move(..., overwrite: true)`)
  `JsonRoleAssignmentStore` already established, keyed by `phraseId:N` rather than a layout-hash (this
  aggregate is phrase-scoped, not layout-scoped).
- **`IMusicalRoleAssignmentCatalog.cs`** / **`MusicalRoleAssignmentCatalog.cs`** — application-orchestration
  boundary: `GetAssignmentsAsync`/`AssignAsync`/`ClearAsync`. Conflict policy mirrors
  `IRoleAssignmentCatalog`'s own exactly: at most one live assignment per (`SourceTrackId`, `PhraseId`)
  pair; reassigning the same track in the same phrase replaces (moves the prior value into `History`,
  increments `Version`) rather than stacking — directly realizing this step's own implementation guidance,
  verbatim: "instrument track linkage per phrase rather than hardcode one stem per display model." Every
  rejection (blank track id, undefined `MusicalRole`, unrecognized `PrecedenceTier`, a `SpatialPathId` not
  present in the supplied `SpatialPathSet`, or an unknown-pair clear) happens *before* any load/write.
- **`IRolePrecedenceResolver.cs`** / **`RolePrecedenceResolver.cs`** — this step's own "...and event
  precedence rules" half, and the direct implementation of the required acceptance text, verbatim: "Synthetic
  role mapping yields deterministic priorities." Pure, stateless: `ResolveOrder` ranks a set of
  `MusicalRoleAssignment`s highest-precedence-first by (1) `PrecedenceTier` rank (Section 2's own order), (2)
  `Priority` descending, (3) `MusicalRole` declared rank, (4) `SourceTrackId` ordinal — the same output
  regardless of the caller's own input order. Resolves no actual time-overlap/effect-layer collision itself;
  that is Step 071's own later, disclosed scope ("Compose and resolve overlapping effects with layer/priority
  strategies") — this step only defines the ranking rule a later compiler will consult.

### New test file

- **`tests/XlightsAI.Studio.Composition.Tests/Step066AcceptanceTests.cs`** (9 tests) — real
  `RolePrecedenceResolver`/`MusicalRoleAssignmentCatalog`/`JsonMusicalRoleAssignmentStore` (not mocks)
  and, for `SpatialPathId` validation, a real `SpatialPathSet` built from the real Step 059/Phase 09 golden
  fixture (`tests/XlightsAI.Studio.Composition.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml`,
  reused byte-for-byte from `Display.Tests`' own fixture except for one comment-wording fix, see §6) via the
  real `ShowLayoutImporter`/`DisplayGraphBuilder`/`SpatialPathCatalog`:
  - **`Step066_Acceptance_SyntheticRoleMappingYieldsDeterministicPriorities`** — the required acceptance
    test, verbatim. Five synthetic assignments across all four tiers/multiple roles/priorities, fed to
    `ResolveOrder` in two different (including fully reversed) input orders; asserts both runs return the
    exact same, hand-verified expected sequence.
  - **`ResolveOrder_UnknownPrecedenceTier_Throws_NegativePath`** — an assignment with an unrecognized tier
    string is rejected even as the *only* element in the list (the specific case that exposed and forced the
    fix described in §3/§4 below).
  - **`AssignAsync_RoleMappingIsAvailableAfterSave_PerPhraseTrackLinkage`** — the same source track
    (`"T-Piano"`) is assigned a different role in two different phrases (implementation guidance, verbatim),
    reloaded through a brand-new catalog/store instance over the same directory (restart).
  - **`AssignAsync_SameTrackTwiceInSamePhrase_ReplacesWithVersionedHistory_ConflictPolicy`** — reassigning
    the same (track, phrase) pair replaces rather than stacks, with versioned history, persisting across
    restart.
  - **`ClearAsync_RemovesLiveAssignmentAndRecordsHistory`**.
  - **`AssignAsync_InvalidInput_RejectsBeforeAnyWrite_NegativePath`** — blank track id, undefined
    `MusicalRole`, unrecognized tier, and an unknown `SpatialPathId` are each rejected before any file is
    written.
  - **`ClearAsync_UnknownPair_Throws_NegativePath`**.
  - **`AssignAsync_Cancelled_DoesNotPersist_NegativePath`** — a pre-cancelled token propagates
    `OperationCanceledException` and never writes a cache file.
  - **`Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`**.

## 3. Architecture deviations / decisions

- **Phase-document mismatch (§0).** Already covered above in full; the single largest deviation this step's
  own evidence records.
- **`MusicalRoleAssignment`, not the bare `RoleAssignment` `PHASE-10.md` Section 3's own proposed C# code
  block names it.** Section 3's own prose one paragraph above that code block already calls the concept
  `MusicalRoleAssignment` ("`MusicalRoleAssignment` says which source is lead/rhythm/bass/harmony/accent and
  target path") — the code block's own bare `RoleAssignment` name is an internal inconsistency in the spec
  itself. Reusing the bare name here would create a same-shape-different-meaning collision against the
  already-accepted `XlightsAI.Studio.Display.RoleAssignment` (Phase 09's own *prop*-role contract) the moment
  a single call site has both namespaces in scope (which this project's own `AssignAsync`, taking a
  `SpatialPathSet`, already does). Resolved in favor of the spec's own prose name, `MusicalRoleAssignment`,
  not its code block's name.
- **`ResolveOrder` precomputes every element's tier rank in an explicit loop before sorting, not inside the
  `OrderBy` key selector directly.** Discovered as a genuine, non-hypothetical bug during this step's own
  first test run (§4): .NET's `Enumerable.OrderBy` may elide invoking the key selector entirely for a
  0- or 1-element source sequence (correct per LINQ's own contract — no comparison is ever needed to "sort"
  a single element) — so `ResolveOrder_UnknownPrecedenceTier_Throws_NegativePath`'s single-assignment input
  silently never triggered the intended validation when the tier check lived inside the key selector itself.
  Fixed by validating every element's tier explicitly in a `foreach` loop first (building a precomputed
  `(TierRank, Assignment)` list), then sorting that already-validated list — now correct regardless of
  input count. Disclosed here as a real implementation-detail pitfall worth recording, not merely a test
  artifact: any future step relying on an `OrderBy` key selector's side effects for validation should route
  validation through an explicit loop instead, the same lesson this step now encodes.
- **`AssignAsync` validates `SpatialPathId` against a caller-supplied `SpatialPathSet`, not an opaque
  unchecked `Guid`.** `PHASE-10.md` Section 3's own proposed `RoleAssignment`/`MusicalRoleAssignment` record
  carries a bare `Guid SpatialPathId` with no validation shown at the (not-yet-written) `ICompositionPlanner`
  layer. This step's own required failure/edge case is unconditional ("Invalid input... must not corrupt the
  project"), and Phase 09's own established precedent (`RoleAssignmentCatalog.AssignAsync` validates its
  `ModelId` against the supplied `SpatialDisplayGraph` before accepting) is the nearest sibling convention to
  follow rather than inventing a new, more permissive shape. A caller with no real path set yet (e.g. a unit
  test exercising only the precedence resolver) is unaffected — `ResolveOrder` itself takes no `SpatialPathSet`
  and performs no such check, since path identity is irrelevant to ranking a set of already-accepted
  assignments.
- **No real SCREEN 12 CHOREOGRAPHY / SCREEN 13 TIMELINE Avalonia/AXAML view, and no `DisplayPathEditorViewModel`-style
  Shell-layer view-model wiring, exists for this step.** `PHASE-10.md`'s own Step 067 (not this step) is
  explicitly titled "Add configurable instrument-to-role and role-to-prop routing **UI**" and names its own
  required evidence as "Avalonia screenshot or repeatable desktop interaction recording" — Step 066's own
  required evidence is narrower ("Versioned, inspectable project output plus a reproducible test report"),
  matching every Phase 09 domain-only step's own identical precedent (Steps 059-060, 062-065 all recorded
  this same disclosure). Proven here at the real, non-UI catalog/resolver boundary a future Step 067 view
  model would read/write through; not reopened or partially built ahead of its own numbered step.
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), no live
  xLights render/desktop-interaction check was attempted for this step — its own required acceptance text is
  a pure domain/persistence/ranking claim (no XSQ/effect-recipe output exists yet at this point in Phase 10),
  proven directly by the automated tests below.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands (`git status`, `find`, etc.), the same pre-existing environment limitation
every prior step's own evidence has recorded; PowerShell was used for every `dotnet`/file-system command
below without incident.

```
# Baseline, before any Step 066 code existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (13 pre-existing projects)

# After creating src/XlightsAI.Studio.Composition (new project):
dotnet build src\XlightsAI.Studio.Composition\XlightsAI.Studio.Composition.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After creating tests/XlightsAI.Studio.Composition.Tests (new project) and Step066AcceptanceTests.cs:
dotnet build tests\XlightsAI.Studio.Composition.Tests\XlightsAI.Studio.Composition.Tests.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test tests\XlightsAI.Studio.Composition.Tests\XlightsAI.Studio.Composition.Tests.csproj --nologo
  # First run: 3 failures --
  #   (a) ClearAsync_RemovesLiveAssignmentAndRecordsHistory / AssignAsync_Cancelled_DoesNotPersist_NegativePath
  #       / AssignAsync_InvalidInput_RejectsBeforeAnyWrite_NegativePath: ShowLayoutMalformedException --
  #       the copied fixture XML comment contained a literal "--", invalid inside an XML comment. Fixed by
  #       rewording the comment (no behavior change to the fixture's own models/groups).
  #   (b) ResolveOrder_UnknownPrecedenceTier_Throws_NegativePath: "No exception was thrown" -- the real bug
  #       disclosed in full in SS3 (OrderBy eliding the key selector for a single-element sequence). Fixed by
  #       precomputing tier ranks in an explicit loop before sorting.
  # Re-run after both fixes:
  # Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: 326 ms

dotnet format XlightsAI.Studio.slnx --verify-no-changes --include src/XlightsAI.Studio.Composition tests/XlightsAI.Studio.Composition.Tests --no-restore
  # No output (clean) -- matches the repository's own CI formatting gate.

dotnet build XlightsAI.Studio.slnx --nologo       # full solution build, after all Step 066 changes
  # Build succeeded. 0 Warning(s). 0 Error(s). (15 projects: 13 pre-existing + Composition + Composition.Tests)

dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Step025&FullyQualifiedName!~Step028&FullyQualifiedName!~Step029&FullyQualifiedName!~Step030&FullyQualifiedName!~Step042"
dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --filter "FullyQualifiedName~Step042"
  # Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 438 ms
  # (the blunt numeric filter above also excludes Stems.Tests' own unrelated, legitimate
  # Step042AcceptanceTests.cs -- the same filter side effect disclosed in every prior Phase 09 step's own
  # evidence; reconfirmed here passing in isolation.)
```

### Targeted test results (Step 066's own 9 new tests, real run)

```
Step066AcceptanceTests (Composition, 9 new):
  Step066_Acceptance_SyntheticRoleMappingYieldsDeterministicPriorities              Passed
  ResolveOrder_UnknownPrecedenceTier_Throws_NegativePath                           Passed
  AssignAsync_RoleMappingIsAvailableAfterSave_PerPhraseTrackLinkage                Passed
  AssignAsync_SameTrackTwiceInSamePhrase_ReplacesWithVersionedHistory_ConflictPolicy Passed
  ClearAsync_RemovesLiveAssignmentAndRecordsHistory                                Passed
  AssignAsync_InvalidInput_RejectsBeforeAnyWrite_NegativePath                      Passed
  ClearAsync_UnknownPair_Throws_NegativePath                                       Passed
  AssignAsync_Cancelled_DoesNotPersist_NegativePath                                Passed
  Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath                  Passed
Composition.Tests total: 9/9 passed, 0 failed, 0 skipped.
```

### Full regression (final, after all Step 066 implementation and tests, real-audio-device Shell tests excluded — unchanged pre-existing condition, see Step 064/065's own §6)

```
Passed! - XlightsAI.Studio.Composition.Tests.dll     9/9     (new)
Passed! - XlightsAI.Studio.Audio.Tests.dll          23/23
Passed! - XlightsAI.Studio.Structure.Tests.dll       78/78
Passed! - XlightsAI.Studio.Melody.Tests.dll         117/117
Passed! - XlightsAI.Studio.Xlights.Tests.dll         74/74
Passed! - XlightsAI.Studio.Editing.Tests.dll         83/83
Passed! - XlightsAI.Studio.Display.Tests.dll         56/56
Passed! - XlightsAI.Studio.Shell.Tests.dll          129/129 (real-audio-device tests excluded, unchanged)
Passed! - XlightsAI.Studio.Projects.Tests.dll        25/25
Passed! - XlightsAI.Studio.Rhythm.Tests.dll          82/82
Passed! - XlightsAI.Studio.Reference.Tests.dll       31/31
Passed! - XlightsAI.Studio.Stems.Tests.dll           93/93  (+8 Step042AcceptanceTests reconfirmed separately, below)
```

**808 total passing** (799 carried forward from Phase 09's own closing count, unmodified, + this step's own
9 new tests), 0 failed, 0 skipped. Every pre-existing non-real-audio-device test from Steps 001-065 still
passes unmodified; the only addition is `XlightsAI.Studio.Composition.Tests` (brand new, 9/9).

## 5. Persisted-output inspection / observable demonstration for this step

The real, unfiltered `Step066_Acceptance_SyntheticRoleMappingYieldsDeterministicPriorities` test run above
*is* the observable demonstration this step's own required "User-visible outcome" asks for ("Versioned,
inspectable project output plus a reproducible test report") — it ranks a real, hand-constructed set of
`MusicalRoleAssignment`s (not a mock of the resolver) and asserts on the actual returned order. Reproduced
here directly (recomputed by hand against the committed, real resolver logic, matching the test's own
assertions byte-for-byte):

```
Synthetic assignments (same PhraseId, same SpatialPathId for all five):
  lockedAccent     UserLocked   Accent   priority 0   track "T-Stab"
  manualLeadHorn   Manual       Lead     priority 1   track "T-Horn"
  manualLeadPiano  Manual       Lead     priority 1   track "T-Piano"
  automatedBassSub Automated    Bass     priority 10  track "T-Sub"
  automatedBassLow Automated    Bass     priority 5   track "T-Low"

ResolveOrder (fed in two different input orders, including fully reversed) always returns:
  1. lockedAccent     (UserLocked outranks every other tier, regardless of role/priority)
  2. manualLeadHorn   (Manual, Lead, priority 1 -- ties manualLeadPiano; "T-Horn" < "T-Piano" ordinal)
  3. manualLeadPiano  (Manual, Lead, priority 1)
  4. automatedBassSub (Automated, Bass, priority 10 -- higher priority wins within the same tier)
  5. automatedBassLow (Automated, Bass, priority 5)
```

This is the exact, real sequence `Step066_Acceptance_SyntheticRoleMappingYieldsDeterministicPriorities`
asserts for both input orderings, and the targeted test run in §4 confirms Passed.

A second, persistence-layer demonstration: `AssignAsync_RoleMappingIsAvailableAfterSave_PerPhraseTrackLinkage`
assigns the real track `"T-Piano"` as `Lead` in one phrase and `Harmony` in a different phrase, writes both
to real on-disk JSON files via `JsonMusicalRoleAssignmentStore`, and reloads both through a brand-new catalog
instance over the same directory (a real restart) — proving "Represent lead/accompaniment/accent and
instrument track linkage per phrase rather than hardcode one stem per display model" directly, not by mock.

## 6. Fixtures / input hashes

- `tests/XlightsAI.Studio.Composition.Tests/Fixtures/sample-show-spatial-display/xlights_rgbeffects.xml` —
  the same Step 059/Phase 09 golden fixture (five arches at WorldPosX 0,10,20,30,40; a three-segment
  roofline), copied into this new test project (each test project owns its own `Fixtures/` directory, the
  same convention every existing test project already follows) with one wording-only comment edit (removing
  a literal `--` that is invalid inside an XML comment, disclosed in full in §4); no model/group data
  changed. Used only to build a real `SpatialPathSet` (via `SpatialPathCatalog`/`SpatialPathSuggester`/
  `SpatialPathEditor`, exactly as Phase 09's own tests already construct one) so this step's own
  `MusicalRoleAssignment.SpatialPathId` validation exercises a real path id, not an unchecked placeholder
  `Guid`.
- No new audio/MIDI/model fixture was needed for this step — `MusicalRoleAssignment`/`MusicalRoleAssignmentSet`/
  `RolePrecedenceResolver` operate on caller-supplied synthetic data (`SourceTrackId` is an opaque string;
  this step owns no stem/melody-track import of its own).
- **Real-audio-device test exclusion, disclosed (unchanged, carried forward from every Phase 09 step's own
  §6):** `Step025`/`Step028`/`Step029`/`Step030`/`Step042ShellWiringTests` construct a real
  `WaveOutAudioTransport` (the real Windows winmm audio device) unavailable in this session's own sandboxed
  execution environment — a pre-existing, environment-caused condition predating this step entirely. No file
  this step touched is on the call path of any of those five classes (confirmed: none reference
  `Composition`/`MusicalRoleAssignmentCatalog`/`RolePrecedenceResolver` in any way).

## 7. Unresolved issues / known gaps

- **Phase 09 still has no consolidated `GATE-REPORT.md`** — disclosed in full in §0; a standing, carried-forward
  process gap, not this step's own scope to close.
- **No real SCREEN 12 CHOREOGRAPHY / SCREEN 13 TIMELINE Avalonia/AXAML view or Shell-layer view-model wiring
  exists yet for Phase 10** — disclosed in §3; Step 067's own explicitly later scope, not reopened here.
- **`MusicalRoleAssignment.Weight` is accepted and persisted but not yet consumed by any budget/brightness
  logic** — Section 3's own `VisualBudget`/`SectionVisualDirection` contracts (a later Phase 10 step's own
  scope, Steps 068-069) are the intended consumer; this step only carries the field through faithfully.
- **`IRolePrecedenceResolver` resolves no actual time-window/effect-layer collision** — disclosed in §2/§3 as
  a deliberate scope boundary; Step 071's own later, named responsibility ("Compose and resolve overlapping
  effects with layer/priority strategies"), not reopened or partially implemented here.
- **No real `ICompositionPlanner`/`IChoreographyCompiler` exists yet** — `PHASE-10.md` Section 3's own
  proposed signatures for those interfaces are later steps' own scope (Steps 068-071); this step builds only
  the `MusicalRole`/`MusicalRoleAssignment`/precedence foundation those interfaces will consume.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Phase 09's own closing
  evidence** (not reopened or re-resolved here): Phase 06's own stem-separation gate remains BLOCKED; Phase
  07's and Phase 08's own full-phase gates have not been separately run/recorded; real-audio-device Shell
  tests remain environment-blocked (§6).
- **Per the user's own standing waiver** (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope — no
  XSQ/effect-recipe output exists yet at this point in Phase 10 for such a check to apply to.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — semantic musical role definition, per-phrase
track-to-role-to-path linkage with conflict policy, and a deterministic event-precedence ranking rule — not
a claim that any carried-forward phase-level gap listed in §7 is resolved, and not a claim that Phase 09's
own missing consolidated gate report, disclosed in §0, has been authored).

- "Synthetic role mapping yields deterministic priorities" (required acceptance, verbatim) — proven by
  `Step066_Acceptance_SyntheticRoleMappingYieldsDeterministicPriorities` (five synthetic assignments across
  every precedence tier, fed in two different input orders including fully reversed, always producing the
  same hand-verified output sequence) and `ResolveOrder_UnknownPrecedenceTier_Throws_NegativePath`
  (validation is never silently skipped, even for a single-element input — the real bug this step's own
  first test run caught and fixed, §3/§4).
- "Represent lead/accompaniment/accent and instrument track linkage per phrase rather than hardcode one
  stem per display model" (implementation guidance, verbatim) — proven by
  `AssignAsync_RoleMappingIsAvailableAfterSave_PerPhraseTrackLinkage` (the same track plays a different role
  in two different phrases, persisted and reloaded across a real restart).
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `AssignAsync_InvalidInput_RejectsBeforeAnyWrite_NegativePath` (blank track id,
  undefined role, unrecognized tier, unknown path id, each rejected before any write),
  `ClearAsync_UnknownPair_Throws_NegativePath`, `AssignAsync_Cancelled_DoesNotPersist_NegativePath`, and
  `Store_LoadAsync_CorruptFile_ThrowsCorruptException_NegativePath`.
- "Versioned, inspectable project output plus a reproducible test report" (User-visible outcome, verbatim) —
  proven by `MusicalRoleAssignmentSet`'s own `Version`/`History` provenance (asserted directly in
  `AssignAsync_SameTrackTwiceInSamePhrase_ReplacesWithVersionedHistory_ConflictPolicy`) and
  `AssignAsync_RoleMappingIsAvailableAfterSave_PerPhraseTrackLinkage`'s own real cross-restart reload.
- Full regression (real-audio-device tests excluded, disclosed in full in §6): **808/808 passing**, 0
  failed, 0 skipped; every pre-existing test from Phase 01-09 (Steps 001-065) still passes unmodified, plus
  this step's own 9 new Composition tests. `dotnet format --verify-no-changes` clean.
- **No file from any prior step/phase was behaviorally changed** — this step only adds the new
  `XlightsAI.Studio.Composition`/`XlightsAI.Studio.Composition.Tests` projects and registers them in the
  solution file.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase 09's
  own consolidated gate report is still unwritten (§0); Phase 06's own gate remains BLOCKED; Phase 07's and
  Phase 08's own full-phase gates have not been separately run/recorded; real-audio-device Shell tests are
  environment-blocked (§6); no real `ICompositionPlanner`/`IChoreographyCompiler`/UI wiring exists yet (§7 —
  later Phase 10 steps' own scope).

Per this step's own closing instruction ("do not execute the next step until this step is proven"): Step 067
("Add configurable instrument-to-role and role-to-prop routing UI") should not begin until this evidence is
reviewed and accepted, and ideally until the Phase 09 consolidated gate report gap (§0) is explicitly
acknowledged or closed.
