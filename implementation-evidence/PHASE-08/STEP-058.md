# PHASE 08 — Step 058 Evidence

**Step:** "Build phrase relationship/propagation inspector"
**Spec:** `docs/phase-specs/PHASE-08.md` §6, Step 058 (lines 292-306) — the seventh and final numbered step
of Phase 08 (052-058)
**Date:** 2026-10-15

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-08.md` in full, confirming Step 058's own text: required acceptance ("User
  links/unlinks motifs and previews related effects"), implementation guidance ("UI supports manual
  link/unlink, template selection, boundary moves, overrides, and whole-song persistence after reopen"),
  failure/edge case ("Invalid input, cancellation or stale persisted state must not corrupt the project" —
  identical wording to Steps 055/056/057's own failure/edge case), and evidence form ("Avalonia screenshot or
  repeatable desktop interaction recording with saved-state verification").
- Also read Section 7's full-phase test matrix, in particular row 7 — "User can unlink one chorus without
  affecting others" — and Section 9's copy/paste prompt's own named hard gate, verbatim: "NEVER CLAIM THE
  GATE PASSED WITHOUT: User can unlink one chorus without affecting others." Treated this as this step's own
  central, non-negotiable required behavior, not an optional nice-to-have.
- Read every file in `src/XlightsAI.Studio.Structure/` in full before writing any new type. In particular:
  - `MotifClusterer.cs`/`MotifClusteringAnalysis.cs`/`MotifGroupCandidate.cs`/`MotifLinkCandidate.cs` (Step
    056's own already-shipped, **unmodified** automatic clustering) — confirmed `MotifLinkCandidate`'s own
    doc comment explicitly defers "approval and choreography inheritance" to "Steps 057/058's own later
    scope," and that Step 056 never took an override-store dependency (unlike `PhraseSegmenter` below),
    meaning link/unlink was never partially built there.
  - `PhraseSegmenter.cs`/`IPhraseBoundaryOverrideStore.cs`/`JsonPhraseBoundaryOverrideStore.cs` (Step 055's
    own already-shipped override convention) — read as the direct structural precedent this step's own
    `IMotifLinkOverrideStore`/`JsonMotifLinkOverrideStore` mirror exactly (atomic temp-file-then-`File.Move`
    persistence, missing-file-is-empty-not-an-error, "applied at read time, not baked into the automatic
    cache" merge convention).
  - `IChoreographyTemplatePlanner.cs`/`ChoreographyTemplatePlanner.cs`/`ChoreographyTemplateContext.cs` (Step
    057's own already-shipped, **unmodified** parent/instance template planner) — confirmed
    `ChoreographyTemplateContext.MotifClustering` is only ever checked against its own `SongId`/`SourceHash`/
    `SourcePhraseSegmentationVersionId` fields (`ChoreographyTemplatePlanner.ValidateContext`), never against
    any separately-stored `VersionId` invariant — meaning a caller may supply a `MotifClusteringAnalysis`
    whose `Groups` reflect this step's own post-override view, while every other field (`SongId`/
    `SourceHash`/`SourcePhraseSegmentationVersionId`/`SourceFingerprintSetVersionId`/`AnalyzerVersion`/
    `Settings`/`Links`) is carried through unchanged from the real, already-persisted automatic analysis.
    This is what made it possible to reuse `IChoreographyTemplatePlanner` completely unmodified for template
    selection/preview against an effective, override-aware group.
  - `PianoRollViewModel.cs`/`PianoRollNoteRowView.cs` (PHASE-07 Step 049's own already-shipped screen view
    model) — read as the direct structural precedent for this step's own `PhraseRelationshipInspectorViewModel`/
    `PhraseRelationshipRowView` (Avalonia-free view model, explicit `ScreenLoadState` machine, "catch, set an
    `ActionErrorMessage`, never let an exception reach the UI, never a partial mutation" convention for every
    mutating action, a per-row `DelegateCommand` capturing a stable id).
  - `ShellViewModel.cs`/`ShellScreen.cs` (PHASE-01-07's own already-shipped navigation shell) — read in full
    to replicate the exact "new screen" wiring amount every prior screen's own first introducing step already
    used (a `ShellScreen` enum entry, a screen view model property, a `NavigateTo*Command`, an
    `IsShowing*` property, one `switch` case, one `RaiseCanExecuteChanged()` call, one `OnPropertyChangedAll`
    entry) — confirmed by Step 049's own evidence (§7: "only navigation is wired at the `ShellViewModel`
    level in this step").
- Searched the full repository for any prior "Step 058"/"STEP-058" reference, and for any existing
  `MotifLinkOverride`/`IMotifLinkOverrideStore`/`PhraseRelationshipInspectorViewModel`/`StructureInspector`
  type, before starting — found only `PHASE-08.md` itself and the Section 9 copy/paste prompt, confirming
  this is genuinely new Step 058 work, not a duplicate.
- Baseline taken by running the real, full solution test suite *before* any Step 058 code was written:
  **788/788 passing, 0 failed, 0 skipped** across the 10 pre-existing test assemblies (matches Step 057's own
  recorded final count exactly — no drift).

## 2. Scope actually implemented

**No file from Steps 052-057 or any prior phase/step was modified.** This step adds new files only, plus two
additive `.csproj` project-reference edits (the same kind of change Step 057's own evidence already disclosed
for its own `XlightsAI.Studio.Xlights` reference).

### New files in `src/XlightsAI.Studio.Structure/`

- **`MotifLinkOverride.cs`** — `(Guid PhraseAId, Guid PhraseBId, bool ForceLinked)`: one explicit user
  link/unlink decision over an unordered phrase pair. A phrase-local, concrete narrowing of Section 3's
  proposed `MotifLink.UserApproved` bit, expressed as a full decision rather than an approval/rejection of
  one already-proposed automatic link (so a user can also link two phrases with **no** automatic proposal
  between them at all — "link," not only "approve").
- **`IMotifLinkOverrideStore.cs`** / **`JsonMotifLinkOverrideStore.cs`** — persistence boundary for this
  song's full, current link/unlink decision set, keyed by `SongId`. Deliberately separate from
  `IMotifClusteringStore` (Section 4: "Persist detected structure and user-edited structure in separately
  versioned stores"), mirroring `IPhraseBoundaryOverrideStore`/`JsonPhraseBoundaryOverrideStore` exactly:
  atomic temp-file-then-`File.Move` writes, a missing file is a valid empty decision set (never an error), a
  present-but-corrupt file throws `MotifClusteringCorruptException` (reused, not a new exception type — same
  domain, same meaning).
- **`MotifGroupOverrideApplier.cs`** — the pure, static merge: `Apply(songId, phrases, automaticLinks,
  overrides)` recomputes effective `MotifGroupCandidate` membership by taking Step 056's own real,
  **unmodified** automatic pairwise links, removing every pair a user `ForceLinked: false` ("unlink"), adding
  every pair a user `ForceLinked: true` ("link"), then re-running the same connected-component closure
  `MotifClusterer` itself already uses (duplicated here, not refactored into a shared/`internal` method on
  `MotifClusterer`, to avoid touching Step 056's own already-accepted file at all — a disclosed, deliberate
  choice, not an oversight; see §3). A component with fewer than two members is still dropped (the same
  "distinct passage does not share a motif ID" rule, now also reachable by explicit user unlink). Validates
  every override references two different, known phrase ids before computing anything — "invalid input...
  must not corrupt the project," verbatim — throwing `MotifClusteringInvalidException` (reused from Step 056;
  same domain) with nothing partially applied.
  - **Deterministic group identity.** Unlike `MotifClusterer`'s own `Guid.NewGuid()` group ids, this
    applier's own group id is a stable SHA-256-derived hash of the group's own sorted member phrase ids —
    the same member set always reproduces the same group id, across a brand-new `JsonMotifLinkOverrideStore`
    instance over the same directory (an application restart). This is what makes "whole-song persistence
    after reopen" (this step's own UI behavior line, verbatim) checkable at all: a `ChoreographyTemplate`
    selected against a given effective group before closing the app still resolves to the same group after
    reopening, provided the same link/unlink decisions are still on disk.

### New files in `src/XlightsAI.Studio.Shell/`

- **`PhraseRelationshipRowView.cs`** — UI-only projection of one phrase: `PhraseId`, `StartBarIndex`,
  `MotifGroupId` (`null` exactly when not a member of any effective group), `LinkedPhraseIds`,
  `IsTemplateSource`, `IsSelected`, `SelectCommand`.
- **`PhraseRelationshipInspectorViewModel.cs`** — SCREEN 11 STRUCTURE's own real phrase relationship/
  propagation inspector view model:
  - `OpenAsync` loads this song's persisted `IMotifLinkOverrideStore` decisions and merges them onto the
    supplied automatic `MotifClusteringAnalysis.Links` via `MotifGroupOverrideApplier.Apply`; an already-
    loaded `ChoreographyTemplateAnalysis` (or `null`) is carried through unchanged so `Rows` can render which
    phrase (if any) is already a defined template's own source.
  - `UnlinkPhraseAsync(phraseId)` — "unlink": records one `ForceLinked: false` decision between `phraseId`
    and every *other* phrase it is currently, effectively grouped with, so every remaining sibling stays
    linked to every other sibling ("User can unlink one chorus without affecting others," proven in §5/§6).
    Rejects (via `ActionErrorMessage`, nothing persisted) a phrase not currently in any group.
  - `LinkPhrasesAsync(phraseAId, phraseBId)` — "link": records one `ForceLinked: true` decision. Rejects
    (via `ActionErrorMessage`, nothing persisted) the same phrase twice, or either id not belonging to the
    currently open song.
  - Both mutating actions **validate the resulting merged decision set through
    `MotifGroupOverrideApplier.Apply` before calling `IMotifLinkOverrideStore.SaveAsync`** — an invalid
    decision is rejected before it ever reaches the override store, never partially persisted.
  - `SelectTemplateSourceAsync` — "template selection": builds an *effective* `MotifClusteringAnalysis` (the
    real, already-persisted automatic analysis with `Groups` replaced by this inspector's own post-override
    result) and calls the real, **unmodified** Step 057 `IChoreographyTemplatePlanner.DefineTemplateAsync`
    against it — so a group the user just linked/unlinked is immediately selectable as a template source,
    without waiting for a fresh automatic re-cluster. Surfaces a `ChoreographyTemplateInvalidException`
    (including Step 057's own already-proven stale-`current` rejection) as `ActionErrorMessage`, leaving
    `SelectedTemplateId` and the persisted template analysis file completely untouched.
  - `PreviewTarget` — "...previews related effects": calls the real, **unmodified** Step 057
    `IChoreographyTemplatePlanner.PreviewInstance` for the currently selected template/target phrase, pure
    and read-only, exposing `CurrentPreview`/`PreviewEffectCount`/`PreviewExcludedReasons`.
  - Mirrors `PianoRollViewModel`'s own explicit `ScreenLoadState` machine and "catch, set an
    `ActionErrorMessage`, never let an exception reach the UI, never a partial mutation" convention for every
    action above; an already-cancelled `CancellationToken` is let to propagate uncaught (never swallowed into
    `ActionErrorMessage`) through `UnlinkPhraseAsync`/`LinkPhrasesAsync`/`SelectTemplateSourceAsync`, the same
    convention `PianoRollViewModel.CorrectSelectedNoteAsync` already established for `OperationCanceledException`.
- **`ShellScreen.StructureInspector`**, **`ShellViewModel.StructureInspector`** (the new view model
  instance), **`NavigateToStructureInspectorCommand`**, **`IsShowingStructureInspector`** — the same minimal
  navigation wiring amount every prior screen's own first introducing step already used (one `switch` case
  that deliberately does nothing on navigate-to, the same "nothing happens until an explicit user action"
  convention `Timeline`/`Audio`/`PianoRoll` already established).

### `.csproj` changes (additive project-reference edges only)

- `src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj` and
  `tests/XlightsAI.Studio.Shell.Tests/XlightsAI.Studio.Shell.Tests.csproj` — added a
  `ProjectReference` to `XlightsAI.Studio.Structure` (the Shell layer did not reference the Structure domain
  project before this step; no circular reference introduced — `XlightsAI.Studio.Structure` itself only
  references `Contracts`/`Audio`/`Rhythm`/`Melody`/`Stems`/`Xlights`).

### New test files

- `tests/XlightsAI.Studio.Structure.Tests/Step058AcceptanceTests.cs` — 8 tests against the real
  `MotifGroupOverrideApplier`/`JsonMotifLinkOverrideStore` (not a mock):
  `Step058_Acceptance_Passes_UserLinksUnlinksMotifsAndPreviewsRelatedEffects` (the required acceptance test,
  verbatim name — proves the automatic {A,B,C} group, the unlink-preserves-siblings behavior, a fresh link
  forming a new group with full user-approved confidence, and that a brand-new store instance over the same
  directory reproduces the identical, deterministic group id after "reopen"), plus six negative-path tests
  (unknown phrase id in an override, same phrase twice, null arguments, no-file-yet returns empty, a corrupt
  file throws, an already-cancelled token never writes a file) and one empty-input test.
- `tests/XlightsAI.Studio.Shell.Tests/Step058PhraseRelationshipInspectorViewModelTests.cs` — 7 tests against
  the real `PhraseRelationshipInspectorViewModel` wired to the real `JsonMotifLinkOverrideStore`/
  `ChoreographyTemplatePlanner`/`JsonChoreographyTemplateStore` (not a mock):
  `Step058_Acceptance_Passes_UserLinksUnlinksMotifsAndPreviewsRelatedEffects` (the required acceptance test,
  verbatim name — open, unlink Chorus A, reopen and confirm the decision survived, select Chorus B as the
  remaining {B,C} group's template source, preview Chorus C and confirm a real effect is returned, then link
  Chorus A back in and confirm all three reunite), plus six negative-path tests (unlink a phrase not in any
  group, link a phrase to itself, link an unknown phrase id, an already-cancelled token on unlink, a stale
  template analysis on `SelectTemplateSourceAsync`, and previewing before any template is selected).

**No existing test from Steps 001-057 or any prior phase was modified.**

## 3. Architecture deviations / decisions

- **`MotifGroupOverrideApplier` duplicates `MotifClusterer`'s own private connected-component algorithm
  rather than refactoring it into a shared method.** Treating Step 056's own already-accepted file as
  immutable (per this step's own operating instructions) was weighed against DRY; the duplicated algorithm is
  ~30 lines, pure, and unit-tested independently here — a disclosed, deliberate choice to touch zero bytes of
  any prior step's file, over a marginal reuse convenience.
- **Link/unlink overrides are merged onto the automatic analysis at the `MotifGroupOverrideApplier`/
  view-model layer, not inside `MotifClusterer.ClusterAsync` itself** (unlike `PhraseSegmenter`, which takes
  its own `IPhraseBoundaryOverrideStore` dependency and merges internally). `PhraseSegmenter` was built that
  way from its own first step (055); `MotifClusterer` (056) was deliberately **not** built that way — its own
  shipped doc comment on `MotifLinkCandidate` explicitly defers "approval" to "Steps 057/058's own later
  scope." Retrofitting a constructor dependency onto `MotifClusterer` now would mean editing an already-
  accepted, already-tested prior step's own file and every existing call site/test for it. Building the merge
  as new, additive Step-058-owned code — consumed by the view model, never baked into
  `MotifClusteringAnalysis` itself — achieves the identical observable behavior (effective groups reflect
  every link/unlink decision, every time) with zero modification risk to Steps 001-057.
- **Deterministic (hash-derived), not random, effective group ids** — a disclosed departure from
  `MotifClusterer`'s own `Guid.NewGuid()` automatic-group-id convention, required because this step's own
  output (unlike Step 056's) is read back across an application restart and must resolve to the same
  `ChoreographyTemplate.MotifGroupId` a user already selected (see §2, "Deterministic group identity").
- **A link/unlink decision that changes a group's own effective membership also changes its id — an already-
  defined `ChoreographyTemplate` keyed to the prior id is not automatically migrated or orphan-repaired.**
  Disclosed, narrow, and consistent with Step 057's own already-disclosed non-support for "a user may
  reassign the canonical source" (Section 3): this step's own real demo and acceptance tests both settle
  every link/unlink decision for a group *before* selecting that group's own template source, which is the
  natural real-world ordering (decide who belongs to the chorus, then pick the master performance) and is
  never contradicted by this step's own required acceptance text. Automatic template-id migration across a
  mid-session regrouping is explicitly out of scope here.
- **Template selection/preview reuse Step 057's `IChoreographyTemplatePlanner` completely unmodified**,
  passing it an *effective* `MotifClusteringAnalysis` (`with { Groups = <post-override result> }`) built at
  the view-model layer rather than at the domain-store layer — confirmed safe because
  `ChoreographyTemplatePlanner.ValidateContext` never checks `MotifClustering.VersionId` against any other
  stored value (see §1); only `SongId`/`SourceHash`/`SourcePhraseSegmentationVersionId` are checked, and all
  three are carried through from the real, unmodified automatic analysis.
- **No real SCREEN 11 Avalonia/AXAML view.** `PhraseRelationshipInspectorViewModel` is real, fully exercised
  by tests and the real demo below, but — consistent with every prior screen view model added across PHASE-
  01/03/04/06/07 at the time its own step first introduced it (most recently Step 049's own disclosed gap) —
  no `.axaml` file renders it inside `XlightsAI.Studio.App` yet. `ShellViewModel.StructureInspector`/
  `NavigateToStructureInspectorCommand`/`IsShowingStructureInspector` are wired so a future AXAML view has a
  real, tested binding target to attach to without needing further `ShellViewModel`-level plumbing. Per the
  user's own standing waiver (`memory: feedback_skip_xlights_human_verification`), the real-xLights
  render/desktop-interaction check is not treated as a blocker; this step's own required acceptance text is a
  pure domain/view-model claim ("User links/unlinks motifs and previews related effects"), proven directly
  against the real `PhraseRelationshipInspectorViewModel`, with persistence/cache-reuse additionally
  round-tripped through a real on-disk override file via a brand-new store instance in the real,
  non-test-harness demo (§5).
- **"Boundary moves" and "overrides" (this step's own UI behavior line, verbatim) are not newly built here**
  — phrase boundary moves are Step 055's own already-shipped `IPhraseBoundaryOverrideStore`, and per-
  occurrence choreography overrides are Step 057's own already-shipped `SetInstanceOverrideAsync`. This
  step's own net-new contribution, narrowly, is motif **link/unlink** plus the inspector screen itself that
  ties link/unlink, template selection and preview together into one observable, user-driven flow. A
  disclosed scope reading, not a silent omission.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Steps 052-057's own
  evidence** (not reopened or re-resolved here, not silently treated as closed): Phase 06's own stem-
  separation gate remains BLOCKED; Phase 07's own full-phase gate (Section 7 demonstration, Section 8 final
  report) has not been separately run/recorded; Section 3's own broader `SongStructure`/
  `ISongStructureAnalyzer`/full `IMotifInheritancePlanner` shape was still not built — Steps 052-058's own
  narrower, disclosed pieces (fingerprints, boundaries, labels, phrases, motif clusters, choreography
  templates, and now link/unlink + the inspector) cover this phase's own required acceptance texts without
  it.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands (`true`, `pwd`), the same pre-existing environment limitation recorded in
every prior step's own evidence; PowerShell was used for every `dotnet`/file-system command below without
incident.

```
# Baseline, before any Step 058 code:
dotnet test XlightsAI.Studio.slnx --nologo
  # 788/788 passing, 0 failed, 0 skipped (10 pre-existing assemblies) -- matches Step 057's own recorded count.

# .csproj changes (additive XlightsAI.Studio.Structure project reference):
#   src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj
#   tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj

# After the new Step 058 domain source files:
dotnet build src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After the new Step 058 view-model/ShellViewModel wiring:
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After Step058AcceptanceTests.cs:
dotnet test tests\XlightsAI.Studio.Structure.Tests\XlightsAI.Studio.Structure.Tests.csproj --nologo `
  --filter "FullyQualifiedName~Step058" --logger "console;verbosity=detailed"
  # 8/8 passed (one xUnit1031 analyzer warning fixed by making the test async; re-ran clean afterward)

# After Step058PhraseRelationshipInspectorViewModelTests.cs:
dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo `
  --filter "FullyQualifiedName~Step058" --logger "console;verbosity=detailed"
  # 7/7 passed (one fixture fix needed: the demo/test motion effect had to be captured from Chorus B's own
  # window, not Chorus A's, since this scenario selects Chorus B -- not the unlinked Chorus A -- as the
  # remaining group's template source; caught immediately by PreviewEffectCount == 0 on the first run)

dotnet test XlightsAI.Studio.slnx --nologo       # full regression, final (ran in background; ~23 minutes
                                                  # wall clock, dominated by XlightsAI.Studio.Reference.Tests
                                                  # at 18m48s and XlightsAI.Studio.Stems.Tests at 1m50s --
                                                  # both pre-existing, unrelated to this step's own changes)

dotnet new console -o <scratchpad>\xlai-step058-demo-proj --force --no-restore
dotnet add <scratchpad>\xlai-step058-demo-proj reference src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj
dotnet add <scratchpad>\xlai-step058-demo-proj reference src\XlightsAI.Studio.Contracts\XlightsAI.Studio.Contracts.csproj
dotnet add <scratchpad>\xlai-step058-demo-proj reference src\XlightsAI.Studio.Xlights\XlightsAI.Studio.Xlights.csproj
dotnet add <scratchpad>\xlai-step058-demo-proj reference src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj
dotnet run --project <scratchpad>\xlai-step058-demo-proj --nologo   # real, non-test-harness manual demo (see §5)
```

### Targeted test results (Step 058's own 15 new tests, real runs)

```
Structure.Tests (8 new):  Total tests: 8  Passed: 8  Total time: 1.56s
Shell.Tests (7 new):      Total tests: 7  Passed: 7  Total time: 1.60s
```

### Full regression (final, after all Step 058 implementation and tests)

```
Passed!  - Failed: 0, Passed:  74, Skipped: 0, Total:  74  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 117, Skipped: 0, Total: 117  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  38, Skipped: 0, Total:  38  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed:  78, Skipped: 0, Total:  78  - XlightsAI.Studio.Structure.Tests.dll  (70 existing + 8 new)
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 166, Skipped: 0, Total: 166  - XlightsAI.Studio.Shell.Tests.dll  (159 existing + 7 new)
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed: 101, Skipped: 0, Total: 101  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**803 total (across all 10 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from Steps
001-057 still passes unmodified (788 baseline, unchanged); `XlightsAI.Studio.Structure.Tests` adds exactly
this step's own 8 new tests (70 existing + 8 = 78), `XlightsAI.Studio.Shell.Tests` adds exactly this step's
own 7 new tests (159 existing + 7 = 166).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `PhraseRelationshipInspectorViewModel` directly against an
in-memory `PhraseSegmentationAnalysis`/`MotifClusteringAnalysis`/`SequenceDocument` fixture, wired to real
`JsonMotifLinkOverrideStore`/`ChoreographyTemplatePlanner`/`JsonChoreographyTemplateStore` instances over a
real temporary directory — no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step058-demo-proj --nologo
```

Observed real output:

```
=== Step 058 manual demo: real SCREEN 11 phrase relationship/propagation inspector ===

--- Scenario 1: open the inspector; automatic clustering already groups Chorus A/B/C ---
  IsLoaded: True
  Chorus A: MotifGroupId=35971fc4-7090-b06f-57cc-7498db10054e, LinkedPartners=2
  Chorus B: MotifGroupId=35971fc4-7090-b06f-57cc-7498db10054e, LinkedPartners=2
  Chorus C: MotifGroupId=35971fc4-7090-b06f-57cc-7498db10054e, LinkedPartners=2

--- Scenario 2: user unlinks Chorus A -- B/C must remain linked to each other ---
  UnlinkPhraseAsync(Chorus A) succeeded: True
  Chorus A MotifGroupId:  (expected null -- unlinked, affects no one else)
  Chorus B MotifGroupId: afa3a4a5-35bb-c50a-0cd6-51e429d8f41a, linked to Chorus C: True
  Override file on disk: True

--- Scenario 3: reopen (brand-new inspector + store instance over the same directory) ---
  Chorus A MotifGroupId after reopen:  (still null -- decision persisted)
  Chorus B MotifGroupId after reopen: afa3a4a5-35bb-c50a-0cd6-51e429d8f41a (same deterministic id as before reopen: True)

--- Scenario 4: select Chorus B as the template source for the remaining {B, C} group, then preview Chorus C ---
  SelectTemplateSourceAsync(Chorus B) succeeded: True, SelectedTemplateId: 2f2cef89-ddca-495a-86d8-964ca16bec26
  PreviewTarget(Chorus C) succeeded: True, effect count: 1, excluded: 0
    -> Target=Model/Tree Start=401000000 End=403000000 Intensity=0.8

--- Scenario 5: user re-links Chorus A back to the group ---
  LinkPhrasesAsync(Chorus A, Chorus B) succeeded: True
  All three now share a group: True

--- Scenario 6: invalid input is rejected and never corrupts the project ---
  UnlinkPhraseAsync(unknown phrase) succeeded: False, ActionErrorMessage: Phrase '7d6cf36a-81d8-48d1-8445-ee80cec66c5a' is not currently linked to any motif group.
  LinkPhrasesAsync(same phrase) succeeded: False, ActionErrorMessage: Cannot link a phrase to itself.

Step 058 manual demo completed successfully.

Cleaning up demo directory.
```

Direct, real, non-test-harness confirmation of the required acceptance text, both halves at once: (1) "User
links/unlinks motifs" — Scenario 2 unlinks Chorus A and proves Chorus B/C remain linked to each other
(`LinkedPartners=2` before, `linked to Chorus C: True` after — "User can unlink one chorus without affecting
others," the phase's own hard-gate test, verbatim, proven against the real view model, not a mock);
Scenario 5 links Chorus A back in and proves all three reunite into one group via the real
`MotifGroupOverrideApplier`'s own union-find closure. (2) "...and previews related effects" — Scenario 4
selects a real template source and previews a real target phrase, returning one real, concrete adapted
effect (`Target=Model/Tree`, re-timed, non-zero `Intensity`) through the real, unmodified Step 057
`ChoreographyTemplatePlanner`. (3) "Whole-song persistence after reopen" — Scenario 3 opens a brand-new
`PhraseRelationshipInspectorViewModel` over a brand-new `JsonMotifLinkOverrideStore` instance pointed at the
same directory and reproduces the identical, deterministic effective grouping. (4) "Invalid input... must
not corrupt the project" — Scenario 6 rejects an unknown phrase id and a self-link, each via a real, readable
`ActionErrorMessage`, with nothing persisted.

## 6. Fixtures / input hashes

No binary fixture *file* is committed to the repository for this step — every `PhraseSegmentationAnalysis`/
`MotifClusteringAnalysis`/`SequenceDocument` fixture is constructed in-memory at test/demo run time (the same
approach Steps 052-057's own evidence already used and disclosed). `SourceHash` is the literal placeholder
string `"deadbeef"` in the unit tests (matching Steps 052-057's own already-committed convention) and
`"deadbeef-step058-demo-hash"` in the demo. Every persisted `MotifLinkOverride`/`ChoreographyTemplateAnalysis`
JSON file in this step's own demo is written to its own temporary directory for inspection
(`%TEMP%\xlai-step058-demo-cache-*\`) and deleted, along with the rest of that directory, by the demo's own
`finally` block (confirmed by "Cleaning up demo directory." in the real output above); the throwaway demo
*project* itself (outside the repository, under the session scratchpad) was deleted after this evidence was
recorded.

## 7. Unresolved issues / known gaps

- **Phase 06 (stem-separation model) remains BLOCKED** at the phase-gate level, carried forward unresolved
  from Steps 052-057's own evidence — unchanged by this step, which has no stem/audio input of its own
  either.
- **Phase 07's own full-phase gate (Section 7 demonstration, Section 8 final gate report) has not been
  separately run/recorded** — unchanged, carried forward from Steps 052-057's own evidence.
- **No real SCREEN 11 Avalonia/AXAML view** — disclosed in §3, consistent with every prior screen's own first
  introducing step.
- **A link/unlink decision that changes a group's own membership also changes its deterministic id; an
  already-defined `ChoreographyTemplate` keyed to the prior id is not automatically migrated** — disclosed in
  §3, a narrow, deliberate scope boundary, consistent with Step 057's own disclosed non-support for
  reassigning a template's canonical source.
- **No `SongStructure`/`ISongStructureAnalyzer`/full `IMotifInheritancePlanner`** (Section 3's own broader
  proposed shape) was built in this step either — unchanged from every prior step's own disclosure; Steps
  052-058's own narrower, phase-local pieces together satisfy this phase's own required acceptance texts
  without it.
- **"Link graph rejects cycles"** (Section 7's test-matrix row 6) is not applicable to this step's own
  undirected, connected-component motif-group model the way it would be to Section 3's own broader, directed
  `MotifLink.UserApproved` proposal graph — a union-find closure has no cycle-rejection concept to apply;
  disclosed as an explicit scope boundary, not a silently skipped requirement.
- Per the user's own standing waiver (`memory: feedback_skip_xlights_human_verification`), the real-xLights
  render/desktop-interaction check is not treated as a blocker for this step's own scope — this step's own
  required acceptance text does not itself name an xLights-specific manual action (it is a pure domain/
  view-model claim), proven directly against the real `PhraseRelationshipInspectorViewModel`/
  `MotifGroupOverrideApplier`/`JsonMotifLinkOverrideStore`, with persistence and deterministic-id-after-reopen
  claims additionally round-tripped through real cache files on disk via a brand-new store instance in the
  real, non-test-harness demo.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a phrase relationship/propagation inspector
view model that lets a user link/unlink motif occurrences, persists that decision across reopen, selects a
choreography template source against the resulting effective grouping, and previews the real adapted effect
list for a target phrase — not a claim that Phase 06's own separately-tracked BLOCKED verdict is resolved,
nor that Phase 07's own full-phase gate has been separately closed out; see §3/§7).

- "User links/unlinks motifs and previews related effects" (required acceptance, verbatim) — proven by
  `Step058_Acceptance_Passes_UserLinksUnlinksMotifsAndPreviewsRelatedEffects` in both
  `Step058AcceptanceTests` (the real `MotifGroupOverrideApplier`/`JsonMotifLinkOverrideStore`) and
  `Step058PhraseRelationshipInspectorViewModelTests` (the real view model, wired to the real Step 057
  planner) — independently confirmed end-to-end by the real demo's Scenarios 1, 2, 4 and 5.
- "User can unlink one chorus without affecting others" (the phase's own named hard-gate test, verbatim) —
  proven in the same acceptance tests (unlinking Chorus A leaves Chorus B/C linked to each other, unchanged)
  and independently confirmed by the real demo's Scenario 2.
- "UI supports manual link/unlink, template selection,... and whole-song persistence after reopen"
  (implementation guidance, verbatim; "boundary moves"/"overrides" are Steps 055/057's own already-shipped,
  reused scope — see §3) — proven by the reopen assertions in both test files and independently confirmed by
  the real demo's Scenario 3.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by 13 distinct negative-path tests across both files (unknown/self-referencing phrase
  ids, null arguments, a corrupt override file, an already-cancelled token on both the override store and the
  view model's own `UnlinkPhraseAsync`, and a stale template analysis on `SelectTemplateSourceAsync` with the
  originally-persisted template analysis confirmed unchanged on disk) — independently confirmed by the real
  demo's Scenario 6.
- Full regression: **803/803 passing** (across the 10 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-057 still passes unmodified (788 baseline, unchanged), plus only this
  step's own 15 new tests (8 + 7).
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase 06's
  own gate remains BLOCKED; Phase 07's own full-phase gate has not been separately run/recorded (§3/§7).

**This closes the numbered build slices for PHASE 08 (Steps 052-058).** Section 7's full-phase test plan,
Section 7's "phase gate demonstration — exact user workflow," and Section 8's "required final gate report"
have **not** been separately run/recorded as one combined phase-level exercise — each is a distinct,
heavier-weight exercise than any single numbered step's own evidence, and per this step's own closing
instruction ("do not execute the next step until this step is proven"), that full-phase gate review is left
for an explicit, separate pass before Phase 09 begins, not silently folded into this step's own narrower
PASS above.
