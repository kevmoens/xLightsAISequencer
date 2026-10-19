# PHASE 13 — Step 085: Build fast lightweight internal preview for supported primitive gestures

**Status: PASS** (automated tests + ViewModel-level "observable UI" demo; real xLights process launch is explicitly out of scope for Step 085 — that is Step 086's own later, not-yet-implemented scope).

## 1. Scope actually implemented

- New project `src/XlightsAI.Studio.Rendering` (`XlightsAI.Studio.Rendering.csproj`, net10.0, referencing `Contracts`, `Xlights`, `Display`):
  - `PixelPoint.cs` — `PixelPoint(ModelId Model, double X, double Y, byte R, byte G, byte B)`, exactly the PHASE-13 Section 3 proposed signature.
  - `PreviewFrame.cs` — `PreviewFrame(long TimeTicks, IReadOnlyList<PixelPoint> Illuminated, IReadOnlyList<Guid> UnsupportedEffectIds)`. Additive third field beyond the spec's bare two-field signature; see §3 "Architecture deviations" below.
  - `ILocalPreviewRenderer.cs` — exactly the PHASE-13 Section 3 proposed signature (`PreviewAsync(SequenceDocument, SpatialDisplayGraph, TimeWindow, CancellationToken) : IAsyncEnumerable<PreviewFrame>`).
  - `RenderPreviewInvalidException.cs` — thrown before any frame is produced for a malformed (non half-open, non-empty) window, mirroring `Xlights.EffectPlanInvalidException`'s convention.
  - `LocalGesturePreviewRenderer.cs` — default `ILocalPreviewRenderer`. Pure function (no I/O, no persistence, no mutation of any project state). Recognizes exactly three preview gestures (`Hold`, `Pop`, `Step`) via an additive, preview-only `EffectInstance.Parameters["PreviewGesture"]` key (see ADR below); any other/absent value is reported via `PreviewFrame.UnsupportedEffectIds`, never guessed at.
- New test project `tests/XlightsAI.Studio.Rendering.Tests` (xUnit, real objects, no mocking):
  - `Step085AcceptanceTests.cs` — 5 tests: `Step085_Acceptance_Passes` (required test name, verbatim), a Pop-flash test, an unsupported-gesture-flagged test, and two required negative-path tests (invalid window throws before any frame; mid-enumeration cancellation stops cleanly with no further frames produced).
- Shell wiring (the "observable UI" slice, following the pre-existing project convention that screen view models — not Avalonia views — are the tested UI boundary; see §4):
  - `src/XlightsAI.Studio.Shell/RenderPreviewViewModel.cs` — bindable `LoadState`/`Frames`/`HasApproximateEffects`/`ErrorMessage` projection over `ILocalPreviewRenderer`, following `CandidateCompareViewModel`'s exact pattern (no Avalonia types, `ObservableObject` base, `ScreenLoadState` reuse).
  - `tests/XlightsAI.Studio.Shell.Tests/Step085RenderPreviewViewModelTests.cs` — 2 tests proving the required acceptance ("Manual known fixtures match approximate rendered path") and the required negative path (invalid window rejected without partial state) at the bindable view-model layer.
- `XlightsAI.Studio.slnx` updated to register both new projects under `/src/` and `/tests/`.

Out of scope for this step (left for Step 086+ per the phase spec's own numbered-step boundaries): `IRenderCapabilityProbe`, `IRenderAdapter`, `RenderRequest`/`RenderArtifact`/`RenderAuthority`, any real xLights process launch, FSEQ parsing, and any new `ShellScreen` enum entry/AXAML view (no prior step in this repository wires a new `ShellScreen` value at the same time its view model is introduced — PHASE-12 Step 084's `CandidateCompareViewModel` is the most recent precedent and also has no `ShellScreen` entry yet).

## 2. Commands actually executed

```
dotnet build XlightsAI.Studio.slnx --nologo            # baseline, before any Step 085 change: Build succeeded, 0 warnings, 0 errors
dotnet build XlightsAI.Studio.slnx --nologo            # after Step 085 implementation: Build succeeded, 0 errors
dotnet test tests/XlightsAI.Studio.Rendering.Tests/XlightsAI.Studio.Rendering.Tests.csproj --nologo
dotnet test XlightsAI.Studio.slnx --nologo             # full repository regression
dotnet --version                                        # 10.0.302
```

## 3. Actual results

### 3.1 New project test run

```
Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 142 ms - XlightsAI.Studio.Rendering.Tests.dll (net10.0)
```

Tests: `Step085_Acceptance_Passes`, `Step085_Acceptance_PopFlashesThenGoesDark`, `Step085_Acceptance_UnsupportedGestureIsFlaggedApproximate`, `Step085_NegativePath_InvalidWindow_ThrowsWithoutYieldingAnyFrame`, `Step085_NegativePath_Cancellation_StopsWithoutCorruptingOutput`.

### 3.2 Full-repository regression (`dotnet test XlightsAI.Studio.slnx`)

Every test project in the solution passed, 0 failures, 0 skipped:

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 31/31 |
| XlightsAI.Studio.Xlights.Tests | Passed 74/74 |
| XlightsAI.Studio.Editing.Tests | Passed 91/91 |
| XlightsAI.Studio.Display.Tests | Passed 56/56 |
| **XlightsAI.Studio.Rendering.Tests (new)** | **Passed 5/5** |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.Composition.Tests | Passed 78/78 |
| XlightsAI.Studio.AiEdits.Tests | Passed 79/79 |
| XlightsAI.Studio.Projects.Tests | Passed 25/25 |
| XlightsAI.Studio.Refinement.Tests | Passed 71/71 |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Audio.Tests | Passed 38/38 |
| **XlightsAI.Studio.Shell.Tests (incl. 2 new Step085 tests)** | **Passed 272/272** |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |

No pre-existing test regressed. No previously accepted source file outside `src/XlightsAI.Studio.Rendering`, `src/XlightsAI.Studio.Shell/RenderPreviewViewModel.cs`, `XlightsAI.Studio.Shell.csproj`, `XlightsAI.Studio.slnx`, and the two new test files was modified.

## 4. Observable result / "UI" demonstration

This repository's own pre-existing, consistent convention (verified directly in `CandidateCompareViewModel`/`Step084CandidateCompareViewModelTests` — PHASE-12's own closest sibling step) is that no literal Avalonia `.axaml` view exists yet for a new screen's view model; the required "Avalonia screenshot or repeatable desktop interaction recording with saved-state verification" outcome is instead proven at the bindable view-model layer a real view would bind to. Step 085 follows that same, already-accepted pattern:

`Step085RenderPreviewViewModelTests.Step085_Acceptance_LoadAsyncPopulatesFramesFromKnownFixture` drives `RenderPreviewViewModel.LoadAsync` end-to-end against a real `LocalGesturePreviewRenderer` and a known fixture (one `Hold` effect on a single-node layout over a 2-frame window), and asserts the bindable `LoadState`/`Frames`/`HasApproximateEffects` properties transition exactly as a bound SCREEN 16/15 view would observe: `Empty` → `Loaded`, 2 populated frames, the known node illuminated in both, `HasApproximateEffects == false`. `Step085_NegativePath_InvalidWindowRejectedWithoutPartialState` proves the companion `Empty`/`Error` transition with `Frames` left empty.

No real desktop screenshot was captured (no `.axaml` view exists for this screen in this repository yet, consistent with every other screen view model introduced since Step 084). **This is the one disclosed, explicit gap in this step's evidence** — if a literal pixel screenshot is required rather than the bindable-view-model proof, that is additional, not-yet-scoped UI work.

## 5. Fixtures

No on-disk fixture files were needed or added. Every test fixture (`SpatialDisplayGraph`/`DisplayNode`/`SpatialPath`/`SequenceDocument`/`EffectInstance`) is built entirely programmatically inside the test files, the same convention `Step084AcceptanceTests`/`Step084CandidateCompareViewModelTests` already established for data-only object-graph fixtures (no XML/JSON file round-trip is exercised by this step). Fixture object identities used:
- Layout: 3-node arch (`Node-A`, `Node-B`, `Node-C`) with one `SpatialPath` (`Id = 11111111-1111-1111-1111-111111111111`) walking them in order.
- Effects: `Hold` on `Node-A`, `Step` along the arch path, `Pop` on `Node-C`, and one deliberately unrecognized-gesture effect.

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **Gesture vocabulary source (new, disclosed).** The phase spec's Section 3 signatures give no field on `EffectInstance`/`SequenceDocument` that already distinguishes "step," "hold," or "pop." The only xLights recipe `EffectRecipeRegistry` currently emits is `effect.on.v1` (`EffectFamily = "On"`), carrying no such distinction. Rather than inventing new real xLights effect recipes/parameters (explicitly forbidden by Section 2: "do not invent xLights effect parameters"), `LocalGesturePreviewRenderer` reads its own additive, non-xLights parameter key, `PreviewGestureParameterKey = "PreviewGesture"` (values `"Hold"`/`"Pop"`/`"Step"`, plus `"PathId"` for `Step`), directly off `EffectInstance.Parameters`. This key is never serialized into xLights XML and is never claimed as a real xLights effect-settings requirement — it exists solely for this fast preview tier. One consequence, by design: every currently-compiled `effect.on.v1` instance (which never sets this key) is reported as **unsupported/approximate** by this fast tier until a future step (Composition/AiEdits) starts setting it. This is the correct, honest outcome per Section 2's own rule ("An XML parse pass does not prove xLights renders an effect") rather than guessing a gesture from an xLights recipe id that carries no such information today.
2. **`PreviewFrame.UnsupportedEffectIds` (additive field, disclosed).** The spec's literal `PreviewFrame(TimeTicks, Illuminated)` signature has no field to carry Step 085's own required implementation guidance: "show an unmistakable 'approximate' indication for unsupported effects." A view cannot render a visual gesture for an effect this tier could not resolve at all. Recording the unresolved `EffectInstance.Id` values active at each frame lets `RenderPreviewViewModel.HasApproximateEffects` and a future real view surface that indication, instead of silently under-rendering with no signal.
3. **Deterministic integer-tick stepping.** `Step` gesture node-index selection uses integer tick arithmetic (`elapsedTicks * nodeCount / durationTicks`) rather than `double` progress multiplication, to avoid floating-point truncation (`2.0/3.0 * 3` can round down to `1.999...` in IEEE double, landing one path node short of the correct index). Verified directly by `Step085_Acceptance_Passes`, which asserts the exact expected node at each of 3 sampled frames across a path of 3 nodes.
4. **No persistence/caching layer added.** Section 4 allows ("Keep preview caches separate from authoritative render evidence") but does not require a cache for the fast tier specifically. `ILocalPreviewRenderer` is a pure function with no store, which trivially satisfies this step's required failure/edge case ("Invalid input, cancellation or stale persisted state must not corrupt the project") — there is no persisted state for a pure, non-mutating preview to corrupt. A caching layer can be added later without changing this interface if profiling shows it is needed.
5. **No new `ShellScreen` enum value / `.axaml` view.** Consistent with the pre-existing precedent set by PHASE-12 Step 084 (`CandidateCompareViewModel` also has no `ShellScreen` entry yet) — adding a new screen's full navigation wiring and visual layout is left to a later, dedicated UI-integration step rather than bundled into this domain/rendering step.

## 7. Known risks / unresolved issues

- Preview color is fixed white (`255,255,255`) for every illuminated pixel — this step makes no claim about color/brightness fidelity to a real xLights effect's own palette (Section 2: "fast lightweight internal preview," not a color-accurate renderer). A future step compiling real color parameters into `EffectInstance.Parameters` would need a corresponding renderer update; tracked as technical debt, not a blocker for this step's own scope.
- No literal Avalonia screenshot exists (see §4) — disclosed, not hidden, and consistent with the rest of this codebase's current state.
- `Pop`'s flash-then-dark curve (`PopFlashFraction = 0.25`, linear on/off) is a deliberately simple approximation, not a claim about any specific xLights "pop" effect's real intensity curve.

## 8. Required final checklist (Step 085 only)

- [x] Implementation / tests / observable demo / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 errors).
- [x] Full existing regression suite re-run and passing (864 total tests across all projects, 0 failures).
- [x] Desktop interaction exercised at the bindable view-model layer (no literal Avalonia screenshot — disclosed gap, §4).
- [x] No previously accepted sequence/project file modified; only new files + two additive, backward-compatible project-reference lines added to `XlightsAI.Studio.Shell.csproj` and `XlightsAI.Studio.slnx`.
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).

**Gate for Step 085: PASS.** Per standing user waiver (real-xLights/manual human verification waived for this project), the xLights-specific render/manual-demo portions of the Phase 13 gate (Steps 086–089) remain unimplemented and are correctly out of scope for this step; they are not claimed as done here.
