# PHASE 13 — Step 089: Add render/evaluation gate to refinement and report limitations

**Status: PASS** (automated tests against the real PHASE-12/PHASE-13 production types -- no mocking; no
installed xLights build is available in this environment -- per the standing user waiver recorded in this
project's memory, the real-xLights human verification portion of this step/phase gate is explicitly waived
rather than faked or re-requested; see §4 and §7 for exactly what that waiver does and does not cover).

## 1. Scope actually implemented

Five new files, all additive (no existing PHASE-12/13 file was modified except two `.csproj` files to add a
project reference):

- **`src/XlightsAI.Studio.Refinement/IRenderEvaluationGate.cs`** — new contract: renders a
  `CandidateAttempt`'s gestures against a `SpatialDisplayGraph` layout and a `LoopRun`'s own permitted time
  scope, returning additional `MetricValue` evidence (never a whole new `QualityReport`).
- **`src/XlightsAI.Studio.Refinement/PreviewRenderEvaluationGate.cs`** — default `IRenderEvaluationGate`.
  Compiles every non-`Blackout` `TransitionGesture` into an ephemeral, never-persisted `SequenceDocument`
  tagged with PHASE-13 Step 085's own `LocalGesturePreviewRenderer.PreviewGestureParameterKey` = `Hold`
  hint, renders it through the real `ILocalPreviewRenderer` (Step 085) and real `IDisplayMetricsCalculator`
  (Step 088), and reports:
  - A hard gate, `"RenderPreview"` — `0.0` when any compiled preview effect's own target prop could not be
    resolved against the supplied layout (`PreviewFrame.UnsupportedEffectIds` non-empty for any effect), or
    when the preview window itself is malformed (`RenderPreviewInvalidException`, reported rather than
    thrown); `1.0` otherwise, including the "nothing to render" case (empty gesture list — already covered by
    an unrelated upstream violation, so not double-penalized here).
  - Four soft metrics, `"RenderCoverage"`, `"RenderBrightness"`, `"RenderMotion"`, `"RenderDensity"` —
    `DisplayMetrics` (Step 088) fields, clamped to `[0,1]` (`MetricValue.Validate`'s own required range;
    `SpatialMotion` is otherwise unbounded above) and reported `Weight=0`/"no data" when there is nothing to
    measure (no frames, or no known display points).
- **`src/XlightsAI.Studio.Refinement/RenderGatedQualityEvaluator.cs`** — a pure decorator over
  `IChoreographyQualityEvaluator`: runs the inner evaluator (Step 081's already-accepted
  `GestureQualityEvaluator`, unmodified) first, then `IRenderEvaluationGate`, and merges the render
  `MetricValue`s into the same `QualityReport.Metrics`/`Violations` rather than producing a second report —
  this step's own implementation guidance, verbatim: "Integrate xLights render results as separate hard and
  soft evaluation evidence in loop." A failed render hard gate is always reflected in `Violations`
  (`QualityReport.Validate`'s own already-accepted invariant), with a message naming the exact metric that
  failed.
- **`src/XlightsAI.Studio.Refinement/XlightsAI.Studio.Refinement.csproj`** /
  **`tests/XlightsAI.Studio.Refinement.Tests/XlightsAI.Studio.Refinement.Tests.csproj`** — added
  `ProjectReference`s to `XlightsAI.Studio.Rendering` and `XlightsAI.Studio.Display` (verified neither
  references `Refinement`, so neither is a cyclic reference).
- **`tests/XlightsAI.Studio.Refinement.Tests/Step089AcceptanceTests.cs`** — 7 new tests (see §3).

**`src/XlightsAI.Studio.Refinement/GestureRefinementOrchestrator.cs` was *not* modified.** It is Step 083's
own already-accepted, immutable contract, and it is already constructed against the
`IChoreographyQualityEvaluator` *interface* (dependency injection). Wiring render evidence in is therefore a
pure composition-at-call-site change: a caller constructs `new RenderGatedQualityEvaluator(new
GestureQualityEvaluator(), new PreviewRenderEvaluationGate(...), layout)` and hands that to
`GestureRefinementOrchestrator`'s own unmodified constructor instead of a bare `GestureQualityEvaluator` —
zero change to, and zero migration risk against, Step 083's own already-passing 71 tests (see §6 item 1 for
the full disclosed rationale).

## 2. Commands actually executed

```
dotnet build XlightsAI.Studio.slnx --nologo
                                                                       # Build succeeded, 0 warnings, 0 errors
dotnet test tests/XlightsAI.Studio.Refinement.Tests/XlightsAI.Studio.Refinement.Tests.csproj --nologo
dotnet test XlightsAI.Studio.slnx --nologo                            # full repository regression
dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"   # excludes real-audio-hardware tests
```

Run from `C:\CODE\xLightsAISequencer` via the PowerShell tool (this session's Bash/git-bash tool again
returned a bare exit code 1 with no captured output on every invocation, the same pre-existing local
shell-launch issue every prior PHASE-13 step's evidence already disclosed — not a project/test failure; all
commands below ran successfully via PowerShell).

## 3. Actual results

### 3.1 New-project test run

```
Passed!  - Failed: 0, Passed: 78, Skipped: 0, Total: 78, Duration: 1 s - XlightsAI.Studio.Refinement.Tests.dll (net10.0)
```

(71 pre-existing Step 079–084 tests + 7 new Step 089 tests, all passing, same assembly.)

The 7 new tests:

| Test | Proves |
|---|---|
| `Step089_Acceptance_Passes` (required name) | Required acceptance, verbatim: "Broken/unrenderable proposal cannot auto-apply; clear diagnostics." A run whose permitted scope names a prop absent from the real display layout runs to exhaustion with `BestCandidateId` **never** set (nothing was ever auto-applied) and lands on `Blocked` (never `AwaitingApproval`, never `Accepted`). Re-evaluating the first recorded attempt directly shows the merged `QualityReport`'s own `"RenderPreview"` metric at `Value=0.0` with an explanation naming the real reason ("...could not be resolved against the display layout") and a matching `Violations` entry — a clear, non-generic diagnostic. |
| `Step089_RenderEvidence_IsMergedAlongsideExistingQualityMetrics_WhenRenderable` | Implementation guidance, verbatim: "Integrate xLights render results as separate hard and soft evaluation evidence in loop." A renderable candidate's merged report carries **both** `GestureQualityEvaluator`'s own untouched metrics (`LegalEffects`, `Density`, ...) **and** all five render metrics, with exact grounded values (fully-lit window on two permitted props → `RenderCoverage`/`RenderDensity`/`RenderBrightness` all `1.0`), and `EvaluatorVersion` is unchanged (still comparable across attempts). |
| `Step089_Blackout_CompilesToNoPreviewEffect_NeverReportedAsUnrenderable` | A `TransitionKind.Blackout` gesture (PHASE-10's own "intentional darkening") is never mistaken for an unresolved/broken effect — `RenderPreview` still passes, and the soft metrics correctly report zero (nothing was ever lit), not an error. |
| `Step089_Acceptance_NeverAutoCommits_EvenWhenRenderableAndImproving_OnlyExplicitAcceptReaches_Accepted` | Implementation guidance, verbatim: "require user acceptance even when score improves." A fully-renderable, best-scoring run still only ever reaches `AwaitingApproval`; only an explicit, separate `GestureCandidateCommitService.AcceptAsync` call reaches `Accepted`. |
| `Step089_NegativePath_NullArguments_ThrowWithoutProducingEvidence` (required negative path) | `IRenderEvaluationGate`/`RenderGatedQualityEvaluator` throw `ArgumentNullException` for every null `run`/`candidate`/`layout`/`context` — no partial/invalid evidence is ever constructed first. |
| `Step089_NegativePath_CancellationRequested_ThrowsWithoutProducingEvidence` (required failure/edge case, verbatim: "...cancellation... must not corrupt the project") | A pre-cancelled token throws `OperationCanceledException` from both the gate and the decorator before any render/evidence work happens — nothing partial is ever persisted (neither type persists anything itself; both are pure functions of their inputs). |
| `Step089_NegativePath_NoGesturesToRender_PassesTrivially_NeverFalselyReportedAsBroken` | An already-rejected, empty-gesture upstream generation (e.g. an empty edit scope) is never *also* reported as a render failure — there is nothing broken to render; the pre-existing upstream violation already blocks it. |

### 3.2 Full-repository regression (`dotnet test XlightsAI.Studio.slnx`)

| Project | Result |
|---|---|
| XlightsAI.Studio.Xlights.Tests | Passed 74/74 |
| XlightsAI.Studio.Editing.Tests | Passed 91/91 |
| XlightsAI.Studio.Projects.Tests | Passed 25/25 |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.Display.Tests | Passed 56/56 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.Composition.Tests | Passed 78/78 |
| XlightsAI.Studio.AiEdits.Tests | Passed 79/79 |
| **XlightsAI.Studio.Refinement.Tests (+7 new Step 089 tests)** | **Passed 78/78** |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| **XlightsAI.Studio.Rendering.Tests** | **Passed 31/31 (unchanged — this step added no new file here)** |
| XlightsAI.Studio.Reference.Tests | Passed 31/31 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |
| XlightsAI.Studio.Audio.Tests | **Flaky across runs: 23/23 in one run, 28/38 (10 failed) in another, with no source change between runs** — real `WaveOutAudioTransport` Windows audio-hardware-backed tests; not touched by this step. |
| XlightsAI.Studio.Shell.Tests | **272 total; 257/272 passed, 15 failed in a full run; 199/199 passed with `--filter "Category!=Integration"`** — the 15 failures are exclusively in `[Trait("Category","Integration")]`/`[SupportedOSPlatform("windows")]` tests (`Step028ShellWiringTests`, `Step029ShellWiringTests`) that construct a real `WaveOutAudioTransport` against this machine's actual Windows audio device; none of their stack traces touch any file this step added or modified. |

**No test in `XlightsAI.Studio.Refinement.Tests`, `XlightsAI.Studio.Rendering.Tests`, or any of the other 11
fully-green projects regressed.** The Audio/Shell-Integration failures are a pre-existing, disclosed,
environment-dependent flake (no reliable audio output device in this sandbox) — confirmed unrelated to this
step because (a) this step touched no file under `src/XlightsAI.Studio.Audio` or
`src/XlightsAI.Studio.Shell`, and (b) the exact same test subset's pass/fail count changes between otherwise
identical consecutive runs with zero source changes in between, which is the signature of hardware-dependent
flakiness, not a deterministic regression. Running with `--filter "Category!=Integration"` (which this
repository's own Step 028/029 tests already opt into via `[Trait("Category","Integration")]`) shows
`XlightsAI.Studio.Shell.Tests` fully green (199/199).

## 4. Observable result / "manual demonstration"

Step 089's own phase-spec "User-visible outcome" field reads "Versioned, inspectable project output plus a
reproducible test report" — the same honestly-scoped wording Step 088 already used for a domain-integration
step with no bindable screen of its own (no `RenderPreviewViewModel`/Avalonia view exists yet for any
Rendering-tier concept in this repository, per Steps 086–088's own precedent). The observable proof is
`Step089_Acceptance_Passes` and its five sibling tests: a reproducible test report showing (a) a candidate
targeting a prop absent from the real display layout is never auto-applied and reports a clear, named
diagnostic; (b) a renderable candidate's report carries both evaluation tiers' evidence with exact grounded
values; and (c) an explicit human `AcceptAsync` call remains the only path to `Accepted` regardless of score.

No installed xLights executable exists on this machine (same disclosed gap as every prior PHASE-02/13 step).
Per this project's own standing memory record (waiver: "user waives the real-xLights human verification check
as a phase-gate blocker; record waiver, don't fake or re-ask"), this step's render gate has never been
exercised against a real `IRenderAdapter`/installed-xLights render — only against the fast, local
`ILocalPreviewRenderer` tier (Step 085), which is this step's own disclosed, deliberate design choice (see §6
item 2), not a shortcut taken to dodge the waiver. The authoritative xLights render/compare demonstration
required by the overall PHASE-13 gate (Section 1: "export a temp XSQ and render it using a verified installed
xLights mode... compare a frame/time metric with the local approximation") remains correctly deferred, not
faked here, and not re-requested — recorded as **waived**, now carried forward against the final PHASE-13 gate
report with every numbered step (085–089) complete.

## 5. Fixtures

No binary fixture files were added under `fixtures/`. Every `SpatialDisplayGraph`/`LoopRun`/`CandidateAttempt`
in `Step089AcceptanceTests` is built directly in the test file via real production constructors (no mocking),
the same convention every prior PHASE-12/13 step's own tests already established — there is nothing to
serialize to disk as a fixture at this step either.

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **Composition (a decorator + call-site wiring) instead of modifying `GestureRefinementOrchestrator`
   (disclosed, the step's own central design decision).** PHASE-13 Section 3 proposes no signature for a
   render/evaluation gate at all (it stops at `RenderArtifact`/`IRenderAdapter`/`ILocalPreviewRenderer`); this
   step's own new `IRenderEvaluationGate`/`PreviewRenderEvaluationGate`/`RenderGatedQualityEvaluator` types are
   therefore additive, not a deviation from a verbatim signature. The deliberate choice is architectural: Step
   083's own already-accepted `GestureRefinementOrchestrator` takes an `IChoreographyQualityEvaluator` by
   constructor injection, so wrapping `GestureQualityEvaluator` in `RenderGatedQualityEvaluator` integrates
   render evidence into every existing and future loop with **zero lines changed** in
   `GestureRefinementOrchestrator.cs` itself — honoring this task's own instruction to "treat all preceding
   completed step contracts as immutable unless the current step has an accepted migration plan." No migration
   plan was needed because none was required.
2. **The fast preview tier (`ILocalPreviewRenderer`), not the authoritative `IRenderAdapter`, drives in-loop
   evidence (disclosed, see `IRenderEvaluationGate`'s own XML doc remarks for the full rationale).** A bounded
   refinement loop may run dozens of attempts (PHASE-12 Section 3); gating every one of them on a real,
   external xLights process launch would make the loop as slow as a full external render per attempt and would
   make the loop itself unable to run at all on a machine without a compatible installed xLights build
   (Section 2: "no assumption every installed release supports... headless"). The authoritative
   `IRenderAdapter` render remains the separate, explicit, human-triggered verification PHASE-13 Section 1's
   own user story describes ("...then run xLights itself to confirm"), performed once against the single
   candidate about to be accepted — not automatically, once per loop attempt, inside this step.
3. **`TransitionGesture` → preview-effect mapping: every non-`Blackout` gesture becomes a `Hold` preview
   effect for its own window (disclosed).** PHASE-10's own `TransitionGesture` (an intensity ramp over a
   window across one or more target props) carries no step/hold/pop distinction — `LocalGesturePreviewRenderer`'s
   own three-gesture vocabulary (Step 085) was designed against a different, not-yet-compiled effect shape, and
   none of PHASE-10/12's own gesture compilers (`ChoreographyCompiler`, `GestureCandidateCommitService`) ever
   tag a compiled `EffectInstance` with Step 085's own preview hint at all. `Hold` is the only one of the three
   vocabulary members requiring no further parameter this record cannot supply (`Step` needs a `PathId` no
   `TransitionGesture` carries) and the most faithful reading of "a prop is lit for this gesture's own
   duration," true of every `TransitionKind` except `Blackout`. A `Blackout` gesture is deliberately compiled to
   *no* preview effect at all, since it is an intentional, legal darkening (PHASE-10's own required acceptance),
   never a renderer failure.
4. **`SpatialMotion` is clamped to `[0,1]` for `MetricValue` (disclosed, carried forward from Step 088's own
   §7-disclosed risk).** `DisplayMetrics.SpatialMotion` is documented as "unbounded above" in normalized
   preview-coordinate units; `MetricValue.Validate` requires every non-hard-gate value to lie in `[0,1]`.
   `Math.Clamp` is applied with the raw (pre-clamp) value still named in the metric's own `Explanation` text,
   so no information is silently discarded, only re-ranged to fit this report shape's own already-accepted
   contract.
5. **Known, disclosed limitation — render evidence is not re-checked inside `GestureCandidateCommitService.AcceptAsync`
   (this step's own title: "...and report limitations").** `BestCandidateSelector.SelectBest` already checks
   both `CandidateAttempt.Violations` *and* `QualityReport.Violations` (the merged report, including this
   step's own render hard gate) before a candidate can ever become `LoopRun.BestCandidateId` — this is the
   mechanism that makes "broken/unrenderable proposal cannot auto-apply" true for the automatic loop path, and
   is directly exercised by `Step089_Acceptance_Passes`. However, `GestureCandidateCommitService.AcceptAsync`
   (Step 082/083's own already-accepted human-approval commit path) takes an arbitrary `candidateId` parameter
   and only re-checks `CandidateAttempt.Violations` plus a live scope/lock re-check — by its own already-accepted
   design (its own doc comment: "defense-in-depth re-check of exactly the two scope rules"), it does **not**
   re-run `IChoreographyQualityEvaluator`/`QualityReport` at all, render-gated or otherwise, and this was
   already true before this step for the "legal effects" hard gate too. A human operator who deliberately
   passes a non-best, render-broken candidate's own id directly to `AcceptAsync` is therefore not blocked by
   render evidence there today. This is a pre-existing architectural characteristic of the already-accepted
   commit path (not introduced or worsened by this step) left unresolved here rather than broadening this
   step's own scope into modifying Step 082/083's own immutable `AcceptAsync` contract without an accepted
   migration plan — explicitly disclosed per this step's own title, not silently left out.
6. **No new `ShellScreen`/Avalonia view.** Consistent with Steps 086–088's own precedent; this step's "User
   visible outcome" text (see §4) does not ask for one.

## 7. Known risks / unresolved issues

- **Render evidence has never been computed over a real, authoritative xLights-rendered artifact** — see §6
  item 2 (by design, the fast preview tier drives the in-loop gate) and §4 (the authoritative
  `IRenderAdapter` demonstration itself remains waived, consistent with every PHASE-02/13 step before it, since
  no xLights executable exists in this environment).
- **§6 item 5's disclosed `AcceptAsync` gap** — a human operator bypassing the retained-best candidate and
  directly accepting a different, render-broken attempt id is not blocked by render evidence at the commit
  boundary today. Closing this would require either (a) persisting `QualityReport.Violations` onto
  `CandidateAttempt.Violations` at save time (a change to the orchestrator's own already-accepted
  attempt-finalization shape, `GestureRefinementOrchestrator.ExecuteAsync`'s `finalizedAttempt` construction),
  or (b) having `AcceptAsync` itself re-run the full render-gated evaluator before committing. Either is a
  legitimate, scoped follow-up; neither was done here to avoid an undisclosed migration against Step 082/083's
  immutable contracts within this one step's own boundary.
- **`Hold`-only gesture mapping (§6 item 3) is approximate by construction** — `TransitionGesture`'s own
  intensity ramp (`StartIntensity`/`EndIntensity`) is not reflected in the binary-lit preview at all; a `Build`
  and a `Release` gesture targeting the same prop over the same window render identically under this gate. This
  is the same "fast, visually-approximate... no color/brightness fidelity claim" scope `LocalGesturePreviewRenderer`
  itself already discloses (Step 085), applied here to a gesture vocabulary it was not originally designed
  against; a future step that teaches the preview renderer to read intensity directly would let this gate
  reflect it without any change to this step's own merge logic.

## 8. Required final checklist (Step 089 only)

- [x] Implementation / tests / observable demo (automated, real production types, no mocking; see §4 for the
      disclosed real-xLights waiver) / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 errors).
- [x] Full existing regression suite re-run; every project this step could affect (`Refinement`, `Rendering`,
      and all 11 other non-audio-hardware projects) passes with 0 failures. The only failures observed
      (`Audio.Tests`/`Shell.Tests` `Category=Integration`) are pre-existing, environment-dependent
      (real-audio-hardware) flakiness unrelated to any file this step touched (see §3.2).
- [x] "Manual" demonstration performed against reproducible, real-production-type test data; the real-xLights
      portion is explicitly waived per this project's own standing memory record, not faked.
- [x] No previously accepted sequence/project file modified; only five new files under
      `src/XlightsAI.Studio.Refinement/` and `tests/XlightsAI.Studio.Refinement.Tests/`, plus two `.csproj`
      files updated to add a project reference each. No `GestureRefinementOrchestrator.cs` or other prior-step
      source file changed.
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7) — including this step's
      own required "report limitations" framing (§6 item 5, §7).

**Gate for Step 089: PASS.** Per the standing user waiver, the real-xLights/manual human verification portion
of the overall PHASE-13 gate (the final demonstration across Steps 086–089, exporting a real temp XSQ and
rendering it with an installed xLights build) remains correctly deferred and is not claimed as independently
proven here; Step 089's own numbered-step scope — integrate render results as separate hard/soft evidence into
the refinement loop, ensure a broken/unrenderable proposal cannot auto-apply with clear diagnostics, confirm
user acceptance is still always required, and disclose the resulting limitations — is fully implemented and
tested. **All five PHASE-13 numbered steps (085–089) are now complete**, each with its own PASS evidence file
under `implementation-evidence/PHASE-13/`.
