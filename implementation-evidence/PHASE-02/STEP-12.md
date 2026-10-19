# PHASE 02 — Step 012 Evidence

**Step:** Create effect plan containing three hardcoded simple effects on selected real props.
**Spec:** [`docs/phase-specs/PHASE-02.md`](../../docs/phase-specs/PHASE-02.md), Section 6, Step 012.
**Date:** 2026-10-10
**Status: PASS**

> Filename note: continuing the PHASE-01/PHASE-02 precedent (`STEP-02.md`...`STEP-11.md`), this file
> uses `STEP-12.md` rather than the phase spec's own `STEP-012.md` form, matching that same precedent.

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting (PHASE-01 Steps 001-008 + PHASE-02 Steps 009-011, the
only prior PHASE-02 steps):

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 33
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 34
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# 123/123, exactly matching Step 011's own recorded final total.
```

No Step 009/010/011 (or PHASE-01) production `src/**` type's existing shape was changed or removed —
this step only **adds** new files (`EffectInstance.cs`, `SequenceDocument.cs`,
`EffectPlanInvalidException.cs`, `IEffectRecipeRegistry.cs`, `EffectRecipeRegistry.cs`,
`IThreeEffectPlanFactory.cs`, `ThreeEffectPlanFactory.cs`, `ISequenceDocumentStore.cs`,
`SequenceDocumentCorruptException.cs`, `JsonSequenceDocumentStore.cs`, `NullSequenceDocumentStore.cs`)
plus additive extensions to `ShowImportViewModel.cs`/`ShellViewModel.cs`/`App.axaml.cs`/`Program.cs`/
`MainWindow.axaml` (new optional constructor parameters with safe defaults, new bindable
properties/commands, new UI sections) — proven non-breaking by the unmodified Step 009/010/011 test
suites still passing in full below (§3).

**Real xLights installation:** still none found on this machine (carried forward from Step 010/011's
own §1 disclosure). Step 012 itself creates and persists a plan as plain JSON — it never touches XSQ
or xLights at all (Section 3: "Store recipe identity independent of final xLights XML settings
representation"; that translation is explicitly Step 013's own scope) — so this gap does not block
this step's own required acceptance. It remains Step 014's blocking dependency.

## 2. What was built

Per PHASE-02 Section 6 Step 012 ("Create sequence with known song duration and three deterministic
effect times. Store recipe identity independent of final xLights XML settings representation.") and
the required acceptance ("Plan is serialized and inspectable independently of XSQ."):

- **`src/XlightsAI.Studio.Xlights/`** (extends the Step 009-011 project; no new project — Section 3's
  own proposed `EffectInstance`/`SequenceDocument`/`IEffectRecipeRegistry` signatures live in this
  same `XlightsAI.Studio.Xlights` namespace, consistent with `DisplayGraph`/`IShowLayoutImporter`):
  - `EffectInstance.cs` (new) — matches PHASE-02 Section 3's proposed signature exactly: `Id`,
    `Target` (`ModelId`), `Layer`, `StartTicks`/`EndTicks` (half-open `[start, end)`), `RecipeId`,
    `RecipeVersion`, `Parameters` (the recipe's own named inputs, never a final xLights XML settings
    string).
  - `SequenceDocument.cs` (new) — matches Section 3's proposed signature: `Id`, `Revision`, `Effects`.
    A plain record with no XML/XSQ dependency anywhere in its graph — the mechanism by which this
    step's required acceptance ("inspectable independently of XSQ") is actually true, not just
    asserted.
  - `EffectPlanInvalidException.cs` (new) — the one exception type for every rejected-plan-request
    path (wrong target count, duplicate target, unresolved target, non-positive duration, unknown
    recipe id, empty/negative time window). Always thrown **before** any `SequenceDocument` is
    constructed — this step's required failure path: "Malformed proposal... must not auto-commit."
  - `IEffectRecipeRegistry.cs` / `EffectRecipeRegistry.cs` (new) — the "known-good effect recipes"
    registry (Section 3's proposed `CreateVerified(recipeId, target, startTicks, endTicks)` shape).
    Ships exactly one recipe this build, `effect.on.v1` (version `"1"`) — the simplest real xLights
    effect, matching PHASE-02 Section 12 appendix's own illustrative `<Effect name="On" .../>` shape.
    Validates target/time-window/recipe-id before constructing anything; unknown recipe ids are
    rejected by name (not silently accepted).
  - `IThreeEffectPlanFactory.cs` / `ThreeEffectPlanFactory.cs` (new) — the actual "three hardcoded
    effects on selected real props" composition: validates exactly three distinct target names, that
    every one resolves against the real, already-imported `DisplayGraph` (a `DisplayModel.XlightsName`
    **or** a `DisplayModelGroup.Name` — Step 011's own "a group is itself a valid effect target"
    contract, reused rather than re-invented), and that the song duration is positive — all **before**
    calling `IEffectRecipeRegistry.CreateVerified` three times. Splits the duration into three
    contiguous, half-open windows (the last absorbing any remainder) so the three effects tile
    `[0, duration)` exactly: no gap, no overlap, no overrun.
  - `ISequenceDocumentStore.cs` / `JsonSequenceDocumentStore.cs` / `SequenceDocumentCorruptException.cs`
    / `NullSequenceDocumentStore.cs` (new) — Section 4's "Save SequenceDocument in versioned
    JSON/SQLite" persistence boundary. `JsonSequenceDocumentStore` reuses `JsonProjectStore`/
    `SettingsStore`'s own established atomic-write pattern (same-directory temp file +
    `File.Move(overwrite: true)`) and writes each plan to its own `sequence-plan-{Id:N}.json` file —
    concurrent plans never share a path. `NullSequenceDocumentStore` follows the
    `NullProjectService`/`NullShowDirectoryDiscoveryService` throwing-stub pattern (needs a real
    configured output directory, unlike the two stateless domain services above).
- **`src/XlightsAI.Studio.Shell/ShowImportViewModel.cs`** (modified, additive): new bindable
  `EffectTargetNamesCsv`/`SongDurationTicks`/`IsPlanBusy`/`PlanErrorMessage`/`Plan`/`PlanSavedPath`/
  `HasPlan` properties and a new `GenerateEffectPlanAsync` method — same busy-guard/try-catch-sets-
  error-state shape as the existing `DiscoverAsync`, kept as entirely separate state (a failed plan
  attempt never clobbers `Layout`, and generating a plan never re-runs Discover).
- **`src/XlightsAI.Studio.Shell/ShellViewModel.cs`** (modified, additive): three new optional
  constructor parameters (`IThreeEffectPlanFactory`, `IEffectRecipeRegistry`, `ISequenceDocumentStore`)
  — the first two default to real instances (same "pure, stateless, nothing to configure" reasoning
  Step 010 already used for `IShowLayoutImporter`), the third defaults to `NullSequenceDocumentStore`
  (same "needs a real directory" reasoning as `IProjectService`/`IShowDirectoryDiscoveryService`) — and
  a new `GenerateEffectPlanCommand`/`LastGenerateEffectPlanCommandTask`, wired exactly like
  `DiscoverShowDirectoryCommand`/`LastShowImportCommandTask`.
- **`src/XlightsAI.Studio.App/Program.cs` / `App.axaml.cs`** (modified, additive): real composition
  wires a real `JsonSequenceDocumentStore` rooted at `%AppData%\XlightsAI.Studio\exports` (sibling to
  the existing `logs`/project directories, never under any xLights show directory).
- **`src/XlightsAI.Studio.App/Views/MainWindow.axaml`** (modified): new "Generate Effect Plan" section
  on the existing Show Import screen (target-names textbox, duration textbox, Generate 3 Effects
  button, busy/error text, and the resulting plan's revision + effect list) — no existing markup
  removed. **Also wraps the existing screen content `Grid` (Row 1) in a `ScrollViewer`** — a real,
  disclosed layout fix (§9) found and fixed during this step's own manual UI verification, not a
  pre-planned change.
- **Tests** (new, no existing test file's assertions touched):
  - `tests/XlightsAI.Studio.Xlights.Tests/Step012AcceptanceTests.cs` — 12 tests against the real
    `ThreeEffectPlanFactory`/`EffectRecipeRegistry`/`JsonSequenceDocumentStore`, real file-system I/O,
    no mocks.
  - `tests/XlightsAI.Studio.Shell.Tests/Step012ShellWiringTests.cs` — 4 tests proving the real
    `ShellViewModel.GenerateEffectPlanCommand` end-to-end pathway.

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests\XlightsAI.Studio.Xlights.Tests\XlightsAI.Studio.Xlights.Tests.csproj --filter "FullyQualifiedName~Step012" --logger "console;verbosity=normal"
Total tests: 12. Passed: 12.

$ dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --filter "FullyQualifiedName~Step012" --logger "console;verbosity=normal"
Total tests: 4. Passed: 4.

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 45   (33 baseline + 12 new Step 012 domain tests)
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 38   (34 baseline + 4 new Step 012 shell-wiring tests)
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25   (untouched)
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31   (untouched)
# Full, final regression: 139/139 passing (123 baseline + 16 new), 0 regressions.

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes
(no output; exit code 0)
```

All commands were run via PowerShell; the Bash tool continues to fail on every invocation in this
environment (exit 1 / no output, including on a bare `ls`/`find`), consistent with every prior step's
evidence (PHASE-01 Steps 001-008, PHASE-02 Steps 009-011).

## 4. Required acceptance: "Plan is serialized and inspectable independently of XSQ"

`Step012_Acceptance_Passes_PlanIsSerializedAndInspectableIndependentlyOfXsq`
(`tests/XlightsAI.Studio.Xlights.Tests/Step012AcceptanceTests.cs`) builds an in-memory `DisplayGraph`
using the phase gate's own exact prop names ("Roofline, Arch 1 and Arch 2", PHASE-02 Section 1), calls
the real `ThreeEffectPlanFactory.CreatePlan`, and asserts against the real returned `SequenceDocument`:

- Exactly 3 effects, targeting `Roofline`/`Arch 1`/`Arch 2` in that order, each using the registry's
  one known recipe (`effect.on.v1`, version `"1"`).
- The three `[start, end)` windows tile `[0, 300_000_000)` ticks (30s) exactly: effect 0 starts at 0,
  each next effect's start equals the previous effect's end, and the last effect ends exactly at the
  requested duration — no gap, no overlap, no overrun.
- **The required acceptance itself**: the plan is saved via the real `JsonSequenceDocumentStore` to a
  real file on disk; the raw file content contains `"RecipeId"` and **no XML angle bracket anywhere**
  and no case-insensitive `"xsq"` substring — proving it is plain, inspectable JSON, not a disguised or
  partial XSQ representation. The file is then independently re-loaded by a **fresh** store instance
  and every field of every effect is field-compared equal to the original (full round-trip).

`Plan_MayTargetARealModelGroup_NotOnlyIndividualModels` proves the complementary case: a plan may
equally target `Front-Arches` (a real `DisplayModelGroup`, Step 011's own "a group is itself a valid
effect target" contract) in one of its three slots, not only individual models — "selected real props"
is honored for both entity kinds this phase recognizes as targetable, and that plan also round-trips
through the real JSON store correctly.

## 5. Automated test results — detail

### `tests/XlightsAI.Studio.Xlights.Tests/Step012AcceptanceTests.cs` (12 new tests, real file-system I/O, no mocks)

```text
Passed Step012_Acceptance_Passes_PlanIsSerializedAndInspectableIndependentlyOfXsq
Passed Plan_MayTargetARealModelGroup_NotOnlyIndividualModels
Passed UnknownTargetName_FailsSafely_DoesNotCreateAPlan_NegativePath
Passed WrongNumberOfTargets_FailsSafely_NegativePath
Passed DuplicateTargetName_FailsSafely_NegativePath
Passed NonPositiveSongDuration_FailsSafely_NegativePath(invalidDuration: 0)
Passed NonPositiveSongDuration_FailsSafely_NegativePath(invalidDuration: -1)
Passed UnknownRecipeId_FailsSafely_NegativePath
Passed InvalidEffectTimeWindow_FailsSafely_NegativePath(start: 100, end: 100)
Passed InvalidEffectTimeWindow_FailsSafely_NegativePath(start: 100, end: 50)
Passed LoadingMissingPlanFile_FailsSafely_NegativePath
Passed LoadingCorruptPlanFile_FailsSafely_NegativePath

Total: 12. Passed: 12.
```

- **Required negative-path assertion** ("Malformed proposal... must not auto-commit"):
  `UnknownTargetName_FailsSafely_DoesNotCreateAPlan_NegativePath` proves a target naming a prop absent
  from the real imported layout (`'Ghost-Prop'`) throws `EffectPlanInvalidException` naming the bad
  target, with no `SequenceDocument` ever constructed — plus five more negative-path tests covering
  every other rejected-request shape this step's own validation recognizes (wrong count, duplicate,
  non-positive duration, unknown recipe id, invalid time window), and two more covering the persistence
  layer's own failure paths (missing file, corrupt file) never silently returning an empty/null
  document.

### `tests/XlightsAI.Studio.Shell.Tests/Step012ShellWiringTests.cs` (4 new tests, real file-system I/O, no mocks)

```text
Passed Step012_Acceptance_Passes_GenerateThreeEffectsEndToEnd
Passed ShellViewModel_WithoutExplicitSequenceDocumentStore_FailsSafely_BackwardCompatible
Passed GenerateWithoutALayoutYet_SetsScreenError_NegativePath
Passed GenerateWithUnknownTarget_SetsScreenError_SavesNothing_NegativePath

Total: 4. Passed: 4.
```

- `Step012_Acceptance_Passes_GenerateThreeEffectsEndToEnd` runs the **exact phase gate demo shape**
  through the real `ShellViewModel`: Discover a real `xlights_rgbeffects.xml` with `Roofline`/`Arch 1`/
  `Arch 2`, then execute `GenerateEffectPlanCommand` — asserts the resulting `Plan` has 3 effects on
  those exact targets in order, and that `PlanSavedPath` is a real file that exists under the
  configured exports directory.
- `GenerateWithoutALayoutYet_SetsScreenError_NegativePath` proves generating a plan before Discover has
  ever populated `Layout` fails safely with an explicit screen error, not an unhandled exception or a
  guessed/invented layout.
- `GenerateWithUnknownTarget_SetsScreenError_SavesNothing_NegativePath` proves a rejected plan request
  never creates the exports directory at all (nothing is ever written on a failed attempt).
- `ShellViewModel_WithoutExplicitSequenceDocumentStore_FailsSafely_BackwardCompatible` proves every
  earlier step's existing `ShellViewModel` call-site shape (no explicit store argument) still compiles
  and now fails safely (rather than silently "succeeding" with a fake save) when a caller actually
  tries to generate a plan without configuring a real store.

### Pre-existing regression (unmodified acceptance text)

```text
XlightsAI.Studio.Xlights.Tests: 33/33 pre-existing Step009/010/011 tests passing, unmodified.
XlightsAI.Studio.Shell.Tests:   34/34 pre-existing tests (Step 004-011) passing, unmodified.
XlightsAI.Studio.Projects.Tests: 25/25 passing, untouched by this step.
XlightsAI.Studio.Reference.Tests: 31/31 passing, untouched by this step.
# Full regression with this step's new tests included: 139/139, 0 regressions.
```

No PHASE-01 or Step 009/010/011 test file's assertions were edited. No prior-step production type's
existing public shape was changed or removed; `ShowImportViewModel`/`ShellViewModel` only gained new
members, and `App.axaml.cs`/`Program.cs`/`MainWindow.axaml` only gained new optional/additive wiring
(§3a evidence above is the proof: every prior step's own test suite still passes byte-for-byte against
its original assertions).

## 6. Manual / desktop UI verification — real Avalonia app, real file system

Per the task's "Build the minimum complete UI -> application -> domain -> data/adapter slice" and "run
... manual tests when this step requires them" instructions, the real compiled desktop app
(`src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe`, started via `Start-Process` — the
exact same executable a user double-clicks) was launched for real and driven via Windows UI Automation
(`System.Windows.Automation`, real `InvokePattern`/`ValuePattern`/`ScrollPattern` calls against the real
running window — not simulated input, not a test harness substitute). No xLights installation is
relevant to this step (Step 012 only creates and persists a JSON plan; nothing is exported/opened in
xLights yet — that remains Step 013/014's scope). A fixture show directory was created directly under
`C:\temp\xlai-step012-demo-show` (outside the repository, at the same "locally copied files" convention
Step 011 established) using the phase gate's own exact prop names:

```xml
<xrgb FormatVersion="2" BaseChannel="0">
  <models>
    <model name="Roofline" DisplayAs="Star" WorldPosX="0" WorldPosY="5" />
    <model name="Arch 1" DisplayAs="Arches" WorldPosX="-3" WorldPosY="0" />
    <model name="Arch 2" DisplayAs="Arches" WorldPosX="3" WorldPosY="0" />
  </models>
  <modelGroups>
    <modelGroup name="Front-Arches" models="Roofline,Arch 1,Arch 2" />
  </modelGroups>
</xrgb>
```

SHA256 of `C:\temp\xlai-step012-demo-show\xlights_rgbeffects.xml`:
`792C35E91EFBB345D5C7A7D37A4651D19DC4FF1402BCEC8E2DCDE56820971937`.

1. **Success path** —
   `implementation-evidence/PHASE-02/step-012-generate-three-effects-success.png`: navigated to Show
   Import via a real `InvokePattern.Invoke()`, set the real show-directory textbox to the fixture above,
   clicked the real Discover button (shows the real parsed `Roofline`/`Arch 1`/`Arch 2` models and
   `Front-Arches` group, real computed `Layout hash: 792C35E9...`). Then set the real "Three real props"
   textbox to `Roofline, Arch 1, Arch 2` and clicked the real **Generate 3 Effects** button. The screen
   shows a real saved-path line (`Saved plan: C:\Users\...\AppData\Roaming\XlightsAI.Studio\exports\
   sequence-plan-32095dd4a03c4dd19fb7e46c8abcf26e.json`), `Revision: 1`, and all three real
   `EffectInstance` entries (`Target = ModelId { Value = Roofline }` / `Arch 1` / `Arch 2`,
   `StartTicks`/`EndTicks` visibly tiling the 300,000,000-tick duration) — real, end-to-end UI evidence
   of the full UI → application → domain → persistence slice, not a blank screen or a crash.
   The real saved file's content was independently inspected directly on disk:
   ```json
   {"Id":"32095dd4-a03c-4dd1-9fb7-e46c8abcf26e","Revision":1,"Effects":[
     {"Id":"80a6bfcf-9d65-4be4-a9ee-cd6933636c6f","Target":{"Value":"Roofline"},"Layer":0,"StartTicks":0,"EndTicks":100000000,"RecipeId":"effect.on.v1","RecipeVersion":"1","Parameters":{"EffectFamily":"On"}},
     {"Id":"83785fd9-4c46-40b1-86b4-e59d05c878db","Target":{"Value":"Arch 1"},"Layer":0,"StartTicks":100000000,"EndTicks":200000000,"RecipeId":"effect.on.v1","RecipeVersion":"1","Parameters":{"EffectFamily":"On"}},
     {"Id":"05eb05a8-b129-446e-8a14-103142627d9a","Target":{"Value":"Arch 2"},"Layer":0,"StartTicks":200000000,"EndTicks":300000000,"RecipeId":"effect.on.v1","RecipeVersion":"1","Parameters":{"EffectFamily":"On"}}
   ]}
   ```
   SHA256 of this real saved file: `91153B6EC6CBF331D8F6E3A10FDF99B731165C36C1ADB415AECB647BD5B6C257`.
   Plain JSON, no XML/XSQ anywhere — the required acceptance, demonstrated against the real running app,
   not only the automated test in §4.
2. **Negative path** — `implementation-evidence/PHASE-02/step-012-generate-unknown-target-error.png`:
   with the same already-discovered layout, the "Three real props" textbox was set to
   `Roofline, Arch 1, Ghost-Prop` and Generate 3 Effects clicked again. The screen shows the real red
   error line `'Ghost-Prop' is not a real model or model group name in the imported layout; no effect
   plan was created.` — not a crash, not a silently-accepted invalid plan, and (per §5's
   `GenerateWithUnknownTarget_SetsScreenError_SavesNothing_NegativePath`) nothing new was written to the
   exports directory for this attempt.

**Disclosed side effect**: this manual verification wrote real files under this machine's own
`%AppData%\XlightsAI.Studio\exports\` (three `sequence-plan-*.json` files accumulated across repeated
manual runs during this step's own development/verification, one of which is the file inspected above)
— the same "runs against the real local AppData location" precedent every prior step's manual
verification already established for `settings.json`/logs. No project file, show file, or test fixture
under `tests/.../Fixtures/` was touched by this step's manual verification.

## 7. Fixture hashes

```text
C:\temp\xlai-step012-demo-show\xlights_rgbeffects.xml (manual-demo fixture, outside the repository)
  SHA256=792C35E91EFBB345D5C7A7D37A4651D19DC4FF1402BCEC8E2DCDE56820971937

Saved plan JSON (real app output, %AppData%\XlightsAI.Studio\exports\sequence-plan-32095dd4a03c4dd19fb7e46c8abcf26e.json)
  SHA256=91153B6EC6CBF331D8F6E3A10FDF99B731165C36C1ADB415AECB647BD5B6C257
```

This step's automated tests (§4/§5) build their `DisplayGraph` fixture in-memory (no new checked-in XML
fixture file was needed — Step 012 does not add any new XML parsing; it consumes an already-parsed
`DisplayGraph`), so there is no new `tests/.../Fixtures/*.xml` hash table analogous to Step 010/011's.

## 8. Changed/added paths

```
src/XlightsAI.Studio.Xlights/EffectInstance.cs                                   (new)
src/XlightsAI.Studio.Xlights/SequenceDocument.cs                                 (new)
src/XlightsAI.Studio.Xlights/EffectPlanInvalidException.cs                       (new)
src/XlightsAI.Studio.Xlights/IEffectRecipeRegistry.cs                            (new)
src/XlightsAI.Studio.Xlights/EffectRecipeRegistry.cs                             (new)
src/XlightsAI.Studio.Xlights/IThreeEffectPlanFactory.cs                          (new)
src/XlightsAI.Studio.Xlights/ThreeEffectPlanFactory.cs                           (new)
src/XlightsAI.Studio.Xlights/ISequenceDocumentStore.cs                           (new)
src/XlightsAI.Studio.Xlights/SequenceDocumentCorruptException.cs                 (new)
src/XlightsAI.Studio.Xlights/JsonSequenceDocumentStore.cs                        (new)
src/XlightsAI.Studio.Xlights/NullSequenceDocumentStore.cs                        (new)
src/XlightsAI.Studio.Shell/ShowImportViewModel.cs                                (modified: +EffectTargetNamesCsv/+SongDurationTicks/+IsPlanBusy/+PlanErrorMessage/+Plan/+PlanSavedPath/+HasPlan/+GenerateEffectPlanAsync)
src/XlightsAI.Studio.Shell/ShellViewModel.cs                                     (modified: +3 optional ctor params, +GenerateEffectPlanCommand, +LastGenerateEffectPlanCommandTask)
src/XlightsAI.Studio.App/Program.cs                                              (modified: +sequenceDocumentStore composition)
src/XlightsAI.Studio.App/App.axaml.cs                                            (modified: +SequenceDocumentStore hand-off property/default)
src/XlightsAI.Studio.App/Views/MainWindow.axaml                                  (modified: +Generate Effect Plan section; +ScrollViewer around screen content, §9)
tests/XlightsAI.Studio.Xlights.Tests/Step012AcceptanceTests.cs                   (new; 12 tests)
tests/XlightsAI.Studio.Shell.Tests/Step012ShellWiringTests.cs                    (new; 4 tests)
implementation-evidence/PHASE-02/STEP-12.md                                      (new, this file)
implementation-evidence/PHASE-02/step-012-generate-three-effects-success.png     (new)
implementation-evidence/PHASE-02/step-012-generate-unknown-target-error.png      (new)
```

No PHASE-01 file, no Step 009/010/011 production file's existing members, and no Step 009/010/011 test
file's assertions were edited or removed.

## 9. Architecture deviations

- **Found and fixed during this step's own manual UI verification (not pre-planned): the Show Import
  screen's content had no scroll container at all.** `MainWindow.axaml`'s screen content `Grid` (Row 1)
  sat directly inside the fixed-size `Window` (900×600, `MinHeight="400"`) with no `ScrollViewer`; once
  this step added the new "Generate Effect Plan" section below the already-existing Models/Groups/
  Warnings lists, the combined content for a populated screen exceeded the window's height and the
  overflow was silently **clipped** (not scrollable) — the Generate button and its result were
  physically unreachable/invisible without resizing the window. This was discovered the first time this
  step's own manual demo was attempted (not by an automated test, since Avalonia's headless/unit layer
  does not render real pixels) and is a pre-existing layout gap that Step 012 was simply the first step
  to expose (every prior screen's content happened to fit inside the default 600px height). Fixed by
  wrapping the existing content `Grid` in a `ScrollViewer` (`VerticalScrollBarVisibility="Auto"`) —
  purely additive, no existing `IsVisible`/binding markup changed, and confirmed non-breaking by the
  full regression suite (§3) still passing unmodified (none of the existing tests assert anything about
  the `Window`/layout tree). Disclosed here per this task's explicit "Show me any architecture
  deviations" instruction, since it touches a shared (`MainWindow.axaml`) file outside this step's own
  narrow Xlights/Shell scope, even though the fix itself does not alter any prior step's bindings or
  screen logic.
- **A plan's "selected real props" may be either an individual `DisplayModel` or a `DisplayModelGroup`
  name**, not only individual models. The step text says "selected real props," and Section 3 already
  establishes (via Step 011's own evidence) that a model group is itself a valid, independently
  targetable xLights effect entity, distinct from its members — restricting Step 012 to models only
  would have been an arbitrary, unstated narrowing of "real props." `ThreeEffectPlanFactory` validates
  a target name against **either** set (`DisplayGraph.Models` by `XlightsName` or `DisplayGraph.Groups`
  by `Name`), never inventing a third kind of target.
- **Exactly three *distinct* targets are required**, not merely "three effects" (which could otherwise
  be read as allowing the same prop three times). The phase gate's own demo text ("Roofline, Arch 1 and
  Arch 2" — three distinct named props) and the step's own "selected real props" (plural, distinct
  props) were read together as requiring distinctness; `DuplicateTargetName_FailsSafely_NegativePath`
  proves the same prop cannot be selected twice. No requirement text explicitly says "distinct," so this
  is recorded here as an interpretation decision, not a verified fact.
- **Song duration is a caller-supplied value for this step, not yet a real `SongAsset`.** Phase 04 (per
  Section 2's cross-cutting table) is the first phase to introduce `SongAsset`/real audio timing; this
  step's own implementation guidance only says "known song duration," so `ShowImportViewModel` exposes a
  plain bindable `SongDurationTicks` (defaulting to 300,000,000 ticks / 30s) rather than fabricating an
  early, unverified `SongAsset`-shaped dependency three phases ahead of its own owning phase.
- **No `IEffectRecipeRegistry`/`IThreeEffectPlanFactory` Null-object fallback.** Unlike
  `IProjectService`/`IShowDirectoryDiscoveryService`/`ISequenceDocumentStore` (each needs real external
  configuration — a workspace path, a show path, an output directory), both of these new collaborators
  are pure, stateless, parameterless domain logic with nothing to configure, so `ShellViewModel` defaults
  them to real instances directly — the exact same reasoning Step 010 already recorded for
  `IShowLayoutImporter` (see that step's own constructor comment), applied consistently here rather than
  introducing a third fallback convention.

## 10. Unresolved issues / risks carried forward

1. **No installed, runnable xLights executable was available on this machine** (carried forward from
   Step 009/010/011's own §1) — still Step 014's own blocking dependency, not this step's; Step 012
   never touches XSQ/xLights at all.
2. **`xlights_networks.xml` is still not parsed** (carried forward from Step 011's own §10 item 2) —
   unaffected by this step's own scope.
3. **Submodel references are still only half-verified; no nested-group cycle detection** (carried
   forward from Step 011's own §10 items 3/4) — unaffected by this step's own scope. A plan may
   currently target a submodel-shaped name only if it independently matches a real model or group name
   outright (submodel names themselves, e.g. `Arch 1/Segment-1`, are not accepted as a distinct
   targetable entity by `ThreeEffectPlanFactory` — only `DisplayGraph.Models`/`Groups` names are
   checked); this is a narrower, disclosed limitation, not a silent gap, and no requirement text in this
   step names submodels as an in-scope target shape.
4. **This step's manual verification used the real local `%AppData%\XlightsAI.Studio\exports\`
   directory** (§6) — same precedent as every prior step's own settings/logs disclosure, carried forward
   again here; three real `sequence-plan-*.json` files now exist there from this step's own repeated
   manual demo runs (none are part of the committed repository).
5. **The new `ScrollViewer` (§9) was verified only by the full automated regression suite (no layout-
   tree assertions exist in this codebase for any screen) plus this step's own two manual screenshots**
   — no dedicated automated test asserts the scroll behavior itself, since this repository's existing
   Shell tests are headless view-model tests with no real Avalonia rendering/layout pass. Recorded as an
   open item for a future step that wants dedicated UI-layout test coverage, not something this step's
   own required acceptance calls for.
6. Git Bash remains non-functional in this environment (carried forward from every prior step); every
   command in this evidence was run via PowerShell.
7. **Step 013 (minimal editable XSQ writer) is the natural next consumer of this step's validated
   `SequenceDocument`** — not started by this step, per its own narrow scope.

## 11. Gate checklist (this step only)

- [x] Creates a sequence with a known song duration and three deterministic effect times (§2, §4, §5).
- [x] Targets exactly three real, distinct, already-imported props (model or model group) — no
      guessed/invented target is ever accepted (§2, §4, §5, §9).
- [x] Stores recipe identity (`RecipeId`/`RecipeVersion`) independent of any final xLights XML settings
      representation — the plan contains no XML/XSQ type anywhere (§2, §4).
- [x] Required acceptance — "Plan is serialized and inspectable independently of XSQ" — proven exactly,
      both by an automated round-trip test and a real UI demonstration with independent on-disk file
      inspection (§4, §5, §6).
- [x] Required failure path — "Malformed proposal... must not auto-commit" — proven by 8 distinct
      negative-path tests plus a real UI negative-path demonstration; no partial/invalid
      `SequenceDocument` is ever constructed or persisted (§4, §5, §6).
- [x] Full regression suite re-run, final: 139/139 passing (123 PHASE-01/Step009-011 baseline + 16 new
      Step 012 tests), zero regressions (§3, §5).
- [x] `dotnet format --verify-no-changes` passes cleanly against every new and modified file (§3).
- [x] No original/show file or PHASE-01 file touched; no Step 009/010/011 test *assertion* touched —
      every prior-step production file edited gained only new, additive members (§5, §8).
- [x] Architecture deviations disclosed in full (§9), per this task's explicit instruction, including a
      real layout defect found (not planned) during this step's own manual verification and fixed with a
      minimal, non-breaking change.
- [x] Real UI demonstration performed (not simulated/mocked) — two screenshots via real Windows UI
      Automation against the real running app: the phase gate's own exact success-path prop names, and
      an explicit unknown-target negative path (§6). No installed xLights executable was required or
      available for this step's own scope (Step 012 never produces or opens an XSQ file) — Step 014
      remains the step that owns the real-xLights-render gate.

**Step 012 status: PASS.** Steps 013-015 remain unimplemented, as instructed ("do not execute the next
step until this step is proven").
