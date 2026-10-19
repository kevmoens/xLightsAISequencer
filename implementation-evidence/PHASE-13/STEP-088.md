# PHASE 13 — Step 088: Compute display-level metrics for brightness, coverage, spatial motion and density

**Status: PASS** (automated tests against hand-designed `PreviewFrame` fixtures with reasoned, checkable
expected values; no installed xLights build is available in this environment — per the standing user waiver
recorded in this project's memory, the real-xLights human verification portion of this step/phase gate is
explicitly waived rather than faked or re-requested).

## 1. Scope actually implemented

New types in `src/XlightsAI.Studio.Rendering` (same project Steps 085–087 created). The PHASE-13 Section 3
proposed signatures stop at Step 086's `RenderArtifact`/`IRenderAdapter` and Step 087 added its own
decoded-frame shapes on top; neither defines a metrics type, so this step's own new contracts are additive:

- `DisplayMetrics.cs` — `DisplayMetrics(double MeanBrightness, double CoverageRatio, double SpatialMotion, double DensityRatio)`.
  Four grounded, documented measurements (Section 3: "choose properties that can be grounded... not an
  unjustified scalar 'beauty' claim"), each a plain reduction over a window — never a subjective score.
- `IDisplayMetricsCalculator.cs` — `Compute(IReadOnlyList<PreviewFrame> frames, int totalDisplayPointCount) : DisplayMetrics`,
  throwing `DisplayMetricsInvalidException` on any malformed input.
- `DisplayMetricsInvalidException.cs` — mirrors `RenderPreviewInvalidException`'s "no partial/invalid output
  is ever produced" convention.
- `DisplayMetricsCalculator.cs` — default `IDisplayMetricsCalculator`. Pure, synchronous, deterministic.

**Metric definitions (documented in full in `DisplayMetrics.cs`'s XML doc comments):**

| Metric | Definition | Range |
|---|---|---|
| `MeanBrightness` | Mean normalized ITU-R BT.601 luma (`0.299R + 0.587G + 0.114B`, ÷255) across every illuminated point in every frame of the window | `[0, 1]` |
| `CoverageRatio` | Distinct `PixelPoint.Model` ids illuminated at least once anywhere in the window, ÷ the window's known total addressable point count. **Cumulative** — a point lit once counts the same as one lit every frame | `[0, 1]` |
| `SpatialMotion` | Mean Euclidean distance (normalized `[0,1]` preview-coordinate units) between the illuminated centroid of each consecutive pair of non-empty frames | `≥ 0`, unbounded above (clamped in practice by the `[0,1]` coordinate space) |
| `DensityRatio` | Mean, across every frame, of that frame's illuminated point count ÷ the window's total addressable point count. **Instantaneous** — unlike `CoverageRatio`, a point lit every frame counts every time | `[0, 1]` |

New test file, `tests/XlightsAI.Studio.Rendering.Tests/Step088AcceptanceTests.cs` (8 tests, every fixture a
hand-built `PreviewFrame[]` window with reasoned expected values — no mocking of `IDisplayMetricsCalculator`
itself):

| Test | Proves |
|---|---|
| `Step088_Acceptance_Passes` (required name) | Required acceptance, verbatim ("Designed render samples produce sensible metric order"): a bright/dense/covering/moving 3-frame window scores strictly higher than a dim/sparse/static 3-frame window on **all four** metrics, plus exact grounded absolute values for both (e.g. bright window's `MeanBrightness == 1.0`, `CoverageRatio == 1.0`, `DensityRatio == 0.5`; dim window's `MeanBrightness == 20/255`, `SpatialMotion == 0`). |
| `Step088_Acceptance_CoverageIsCumulativeButDensityIsInstantaneous` | The two ratio metrics are genuinely distinct measures, not aliases: 4 distinct points lit one-at-a-time across 4 frames yield full `CoverageRatio` (1.0, everything was eventually lit) but low `DensityRatio` (0.25, only one point lit at any instant). |
| `Step088_Acceptance_EmptyWindow_YieldsZeroMetricsWithoutThrowing` | An empty (but validly-shaped) window is a legitimate "nothing to measure yet" case, not an error. |
| `Step088_Acceptance_AllFramesDark_YieldsZeroMetricsWithoutThrowing` | Frames with zero illuminated points every instant also yield all-zero metrics rather than `NaN`/division artifacts. |
| `NegativePath_NullFrameWindow_ThrowsWithoutProducingMetrics` | Required failure case ("Invalid input... must not corrupt the project"): a null window throws before any `DisplayMetrics` is constructed. |
| `NegativePath_NullFrameInsideWindow_ThrowsRatherThanSkippingIt` | A window containing one null frame is rejected outright (with its index named in the message), not silently skipped — silently skipping a corrupt entry would under-measure without telling the caller. |
| `NegativePath_NonPositiveTotalPointCount_ThrowsRatherThanDividingByZero` (`[Theory]`, 0 and -1) | A non-positive total point count is rejected rather than producing `Infinity`/`NaN` ratios or a silently wrong divide. |

`tests/XlightsAI.Studio.Rendering.Tests.csproj` — **no changes**; this step needed no new package reference
(no binary file format, no compression, pure LINQ-free arithmetic over the existing `PreviewFrame`/`PixelPoint`
records already referenced via the production project).

## 2. Commands actually executed

```
dotnet build XlightsAI.Studio.slnx --nologo
                                                              # Build succeeded, 0 warnings, 0 errors
dotnet test tests/XlightsAI.Studio.Rendering.Tests/XlightsAI.Studio.Rendering.Tests.csproj --nologo
dotnet test XlightsAI.Studio.slnx --nologo                   # full repository regression
```

(Run from `C:\CODE\xLightsAISequencer` via the PowerShell tool; this session's Bash/git-bash tool returned a
bare exit code 1 on every invocation, including a plain `echo hello`, with no stdout/stderr captured — a
local shell-launch issue in this environment, not a project/test failure. All commands below were executed
successfully via PowerShell instead.)

## 3. Actual results

### 3.1 New-project test run

```
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 3 s - XlightsAI.Studio.Rendering.Tests.dll (net10.0)
```

(23 pre-existing Step 085/086/087 tests + 8 new Step 088 tests, all passing, same assembly.)

### 3.2 Full-repository regression (`dotnet test XlightsAI.Studio.slnx`)

Every test project passed, 0 failures, 0 skipped:

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 31/31 |
| XlightsAI.Studio.Xlights.Tests | Passed 74/74 |
| XlightsAI.Studio.Editing.Tests | Passed 91/91 |
| XlightsAI.Studio.Display.Tests | Passed 56/56 |
| **XlightsAI.Studio.Rendering.Tests (+8 new Step 088 tests)** | **Passed 31/31** |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.Composition.Tests | Passed 78/78 |
| XlightsAI.Studio.AiEdits.Tests | Passed 79/79 |
| XlightsAI.Studio.Projects.Tests | Passed 25/25 |
| XlightsAI.Studio.Refinement.Tests | Passed 71/71 |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Audio.Tests | Passed 38/38 |
| XlightsAI.Studio.Shell.Tests | Passed 272/272 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |

Total: 890 tests (882 from before Step 088 + 8 new), 0 failures. No pre-existing test regressed. No
previously accepted source file outside the four new files under `src/XlightsAI.Studio.Rendering/` and the
one new test file under `tests/XlightsAI.Studio.Rendering.Tests/` was modified. No `.csproj` was changed.

## 4. Observable result / "manual demonstration"

Step 088's own phase-spec "User-visible outcome" field reads "Versioned, inspectable project output plus a
reproducible test report" — unlike Steps 085/087's "Avalonia screenshot" wording, this is already honestly
scoped to a domain computation with no bindable screen of its own (consistent with Step 086's own precedent:
no `ShellScreen`/Avalonia view exists yet for any Rendering-tier concept in this repository). The observable
proof is `Step088_Acceptance_Passes`: a reproducible test report showing a hand-designed bright/moving sample
scoring higher than a hand-designed dim/static sample on all four metrics, with exact grounded absolute
values asserted for both (not just a pass/fail relative comparison) — the literal "reproducible test report"
the spec asks for at this step.

No installed xLights executable exists on this machine (same disclosed gap as every prior PHASE-02/13 step).
Per this project's own standing memory record (waiver: "user waives the real-xLights human verification check
as a phase-gate blocker; record waiver, don't fake or re-ask"), this step's metrics have never been computed
over a real xLights-rendered artifact (only over Step 085's approximate preview shape, by design — see §6).
The real-build manual demonstration required by the overall phase gate remains correctly deferred, not faked
here, and not re-requested — recorded as **waived**, carried forward against the final PHASE-13 gate report
once Step 089 is also complete.

## 5. Fixtures

No binary fixture files were added under `fixtures/`. Every window in `Step088AcceptanceTests` is a
hand-constructed `PreviewFrame[]` literal built directly in the test file with explicit, reasoned-about
`PixelPoint` values (e.g. "2 of 4 known points lit at full white, centroid moves 0.0→0.5→1.0") — there is no
binary format to decode at this step, so there is nothing to serialize to disk as a fixture; the test code
itself is the fixture, consistent with Step 085/086's own precedent for synthetic domain-level fixtures.

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **New contracts not present in Section 3 (disclosed, additive).** Section 3's proposed signatures stop at
   `RenderArtifact`/`IRenderAdapter`/`ILocalPreviewRenderer`/`PreviewFrame`/`PixelPoint` and define no
   metrics shape. `DisplayMetrics`/`IDisplayMetricsCalculator`/`DisplayMetricsCalculator` are new types this
   step introduces to fill that gap, following the same naming/record-shape conventions every prior
   `Rendering` contract in this repository already uses.
2. **Metrics operate on `PreviewFrame` windows, not Step 087's `DecodedRenderOutput` (the step's single most
   significant deviation, directly carried forward from a risk Step 087 already disclosed).** Step 088 asks
   for "display-level metrics for... spatial motion and density," which require per-point `X`/`Y` position
   data to mean anything spatially. Step 087's own evidence (§6 item 2 of `STEP-087.md`) already recorded
   that `DecodedRenderOutput` is **channel-indexed, not model-indexed**, because no step through PHASE-13
   Step 087 imports the `xlights_networks.xml` channel-offset data needed to map a raw channel byte back to
   a model's `X`/`Y` position — and inventing that mapping would violate Section 2's "do not invent xLights
   effect parameters" rule. `PreviewFrame`/`PixelPoint` (Step 085's approximate-preview shape) is the
   **only** contract in this repository through Step 088 that actually carries spatial position alongside
   color, so `IDisplayMetricsCalculator.Compute` is typed against `IReadOnlyList<PreviewFrame>`, not
   `DecodedRenderOutput`. This is honestly scoped to what prior steps have actually imported, not a gap
   introduced by this step: a future step that adds real channel-to-model mapping on top of Step 087's
   decoder can produce `PreviewFrame`-shaped output from real rendered data, and this same calculator will
   consume it unchanged — no rewrite required. Until then, Step 089's render/evaluation gate can apply these
   metrics to Step 085's approximate preview on both sides of an A/B comparison (candidate vs. baseline),
   which is still a grounded, consistent comparison even though neither side is yet an authoritative render.
3. **`totalDisplayPointCount` is a required caller-supplied parameter, not derived from the frame window
   itself (disclosed).** A window of `PreviewFrame`s only ever lists points that were *actually illuminated*
   at some sampled instant — it has no way to know how many addressable points exist on the real display
   that were simply never lit. Without this parameter, `CoverageRatio`/`DensityRatio` could only be computed
   relative to "every point this window happened to light," which would always report coverage as 100% by
   construction (a dishonest, self-fulfilling ratio) rather than a real fraction of a known, independently
   authoritative display. Callers are expected to supply the model/point count from `SpatialDisplayGraph`
   (Phase 09, already pinned as this repository's own authoritative display-geometry source) — this step
   does not import or depend on that type directly, keeping `IDisplayMetricsCalculator` decoupled from the
   `Display` project's own evolution.
4. **Synchronous, non-async `Compute(...)` (disclosed, consistent with sibling readers).** Pure in-memory
   arithmetic over an already-fully-materialized window, the same synchronous shape `IRenderedFrameDecoder.Decode`
   already uses. No `CancellationToken` parameter exists for the same reason Step 087 recorded: there is no
   partial/mutable state a cancellation could leave behind — one call either returns one fully-constructed
   `DisplayMetrics` or throws before returning anything.
5. **No new `ShellScreen`/Avalonia view.** Consistent with Steps 085–087's own precedent; this step's "User
   visible outcome" text itself (see §4) does not ask for one, unlike Steps 085/087's wireframe-screenshot
   wording.

## 7. Known risks / unresolved issues

- **Metrics have never been computed over a real xLights-rendered artifact** (see §6 item 2) — only over
  Step 085's approximate preview shape, which is the intended, honestly-scoped target for this step given
  what prior steps have actually imported. A future channel-to-model mapping step is required before these
  same metrics can run against real `.fseq` render data without first being converted to the `PreviewFrame`
  shape.
- **`SpatialMotion`'s centroid-distance definition is one reasonable choice among several**, not the only
  possible "movement trajectory" measure (e.g. per-point trajectory tracking by `ModelId` across frames,
  rather than a single aggregate centroid per frame, would also be defensible and more sensitive to multiple
  independently-moving groups). The centroid measure was chosen because it requires no point-identity
  tracking across frames (robust to points appearing/disappearing between frames, e.g. effects that pop
  different models in and out) and is simple to reason about and test; documented as a specific, disclosed
  definition in `DisplayMetrics.cs`'s XML doc comments rather than an implicit assumption.
- **No `QualityReport`/`MetricValue` (Phase 12) wiring yet.** Phase 13 Section 1's phase gate and Step 089's
  own guidance ("Integrate xLights render results as separate hard and soft evaluation evidence in loop")
  suggest these metrics will eventually feed a `MetricValue` entry for loop evaluation, but Step 088's own
  required acceptance text only asks for "sensible metric order" from "designed render samples" — no direct
  `QualityReport` consumer is specified at this step boundary, so none was added here (avoiding inventing an
  integration point one step early, per this project's standing "don't design for hypothetical future
  requirements" convention). Step 089 is the correct place to wire `DisplayMetrics` into any loop-facing
  evidence shape.

## 8. Required final checklist (Step 088 only)

- [x] Implementation / tests / observable demo (automated, hand-designed `PreviewFrame` fixtures with
      reasoned expected values; see §4 for the disclosed real-xLights waiver) / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 errors).
- [x] Full existing regression suite re-run and passing (890 total tests across all 15 test projects, 0
      failures).
- [x] "Manual" demonstration performed against reproducible, hand-designed test data; the real-xLights
      portion is explicitly waived per this project's own standing memory record, not faked.
- [x] No previously accepted sequence/project file modified; only four new files under
      `src/XlightsAI.Studio.Rendering/` and one new test file under
      `tests/XlightsAI.Studio.Rendering.Tests/`. No `.csproj` changed.
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).

**Gate for Step 088: PASS.** Per the standing user waiver, the real-xLights/manual human verification
portion of the overall PHASE-13 gate (final demonstration across Steps 086–089) remains correctly deferred
and is not claimed as independently proven here; Step 088's own numbered-step scope (compute grounded
brightness/coverage/spatial-motion/density metrics over a `PreviewFrame` window, with a hard failure gate on
any malformed input) is fully implemented and tested.
