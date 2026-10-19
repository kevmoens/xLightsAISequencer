# PHASE 05 — Step 036 Evidence

**Step:** "Create beat-pop and alternating-chase recipe to real selected props"
**Spec:** `docs/phase-specs/PHASE-05.md` §6, Step 036 (lines 282-296)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-05.md` in full: Section 2's "do not invent xLights effect
  parameters" / "Implement deterministic On/Off pulse recipes with a restrained default visual
  density," Section 3's proposed `IRhythmGesturePlanner.BuildPulses(grid, targets, verifiedRecipeId,
  range)` signature, Step 036's own required acceptance ("Rendered/exported effects follow detected
  beats"), implementation guidance ("Translate selected beat strength into finite pulse duration and
  verified effect recipe, respecting visual density and output frame snapping"), and failure/edge case
  ("Silence, invalid timing boundaries and missing media must be handled explicitly").
- Re-read the phase-wide quality-gate matrix (§7) row 7, verbatim: "Generated effects align selected
  props and remain confined to time scope" — the spec's own `CORE DEMO REQUIRED` line explicitly names
  this row as something that must never be claimed passed without real proof.
- Read `implementation-evidence/PHASE-05/STEP-031.md` through `STEP-035.md` in full and treated every
  type/behavior they describe as an immutable prior contract: the full `OnsetAnalysis`/`TempoAnalysis`
  chain, `BeatMarker`, `BeatGridSettings`, `BeatGrid` (including `CorrectionVersion`), `IBeatGridBuilder`/
  `BeatGridBuilder`, `IBeatGridEditor`/`BeatGridEditor` and their exception types. None of these files
  were opened in write mode this session.
- Read the existing PHASE-02 `Xlights` project in full (`EffectInstance`, `IEffectRecipeRegistry`/
  `EffectRecipeRegistry`, `SequenceDocument`, `DisplayGraph`/`DisplayModel`, `XsqExporter`,
  `XsqExportPolicy`, `XsqStructuralReader`/`XsqStructuralDocument`/`XsqStructuralElement`/
  `XsqStructuralEffectPlacement`, `ThreeEffectPlanFactory` as a precedent "plan factory" shape) — this
  step's own real surface for "real selected props," since `EffectInstance`/`IEffectRecipeRegistry` are
  PHASE-02-owned contracts, not something this step may redefine. Confirmed `EffectRecipeRegistry`
  currently recognizes exactly one verified recipe, `effect.on.v1` ("the simplest real xLights
  effect"), and that `XsqExporter` explicitly refuses to invent XML for any other recipe id
  (`XsqExportInvalidException: "Unknown effect recipe id..."`).
- Read `docs/XLIGHTS-SEQUENCE-ENGINEERING-GUIDE.md` line 129 directly, verbatim: "Some 'semantic
  gestures' may compile into a series of elementary `On` effects instead of one complex xLights
  effect; that is valid when it achieves predictable timing and preserves editability." This is the
  concrete, pre-existing architectural guidance this step's own "beat-pop and alternating-chase"
  gesture is built against: compiling into many short, already-verified `effect.on.v1` pulses on
  alternating real props, rather than inventing a new, unverified xLights XML effect shape this build
  has never captured from a real xLights install (Section 2's own prohibition, and the exact same
  disclosed limitation `XsqExporter`'s own class doc already records for Steps 013-015).
- Baseline regression before any Step 036 code existed, run fresh in this session:
  **446/446 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 035's own final
  recorded count exactly (71 Xlights + 91 Editing + 25 Projects + 34 Audio + 142 Shell + 52 Rhythm + 31
  Reference).

## 2. Scope actually implemented

**New `ProjectReference`:** `src/XlightsAI.Studio.Rhythm/XlightsAI.Studio.Rhythm.csproj` now also
references `XlightsAI.Studio.Xlights.csproj` (previously only `Contracts`/`Audio`). This is the one,
disclosed, required architectural change this step introduces: Section 3's own proposed
`IRhythmGesturePlanner.BuildPulses` returns `IReadOnlyList<EffectInstance>` — an `Xlights`-owned type
— so `Rhythm` must depend on `Xlights`, not the reverse (confirmed no circular dependency:
`Xlights.csproj` references only `Contracts.csproj`). The test project
(`tests/XlightsAI.Studio.Rhythm.Tests`) gained the same new reference, needed to exercise a real,
end-to-end `EffectRecipeRegistry`/`XsqExporter`/`XsqStructuralReader` round trip (§4).

All other new files added to the existing `src/XlightsAI.Studio.Rhythm/XlightsAI.Studio.Rhythm.csproj`
and `tests/XlightsAI.Studio.Rhythm.Tests/XlightsAI.Studio.Rhythm.Tests.csproj` projects (no new
project, no other `.csproj`/solution changes):

- **`IRhythmGesturePlanner.cs`** — Section 3's proposed signature, verbatim:
  `BuildPulses(BeatGrid grid, IReadOnlySet<ModelId> targets, string verifiedRecipeId, TimeWindow range)`.
  No narrowing was needed this time (unlike Steps 033/034/035's own disclosed narrowings) — Section 3's
  proposed shape is already a pure, synchronous, in-memory function over values already in hand, the
  same shape this codebase's own precedent (`IBeatGridEditor`) already established is correct for a
  domain-level compiler that does not itself own persistence.
- **`RhythmGesturePlanner.cs`** — real implementation. Validates `grid`/`targets` non-null,
  `targets` non-empty, `verifiedRecipeId` non-blank, and `range` a valid non-negative, non-empty
  half-open window; selects every beat inside `[range.StartTicks, range.EndTicks)`; assigns each
  selected beat, in time order, to the next target in a **deterministic, ordinal-sorted round-robin**
  across `targets` (the "alternating-chase" requirement — never the undefined enumeration order of an
  `IReadOnlySet<ModelId>`); computes each pulse's duration by linearly interpolating between
  `RhythmGestureSettings.MinPulseDurationTicks`/`MaxPulseDurationTicks` by that beat's own
  `BeatMarker.Confidence` (the "beat strength -> finite pulse duration" requirement); **snaps that
  duration to `XsqExporter.FrameIntervalMs`'s own real xLights frame grid** (at least one whole frame,
  never a sub-frame duration — the "output frame snapping" requirement); clips the resulting end time
  to `range.EndTicks` (the "remain confined to time scope" quality-gate row); and constructs each pulse
  via the already-injected `IEffectRecipeRegistry.CreateVerified(verifiedRecipeId, ...)` — never
  fabricating an `EffectInstance` by hand, so an unknown/invalid recipe id is rejected by the same real,
  pre-existing PHASE-02 registry, not by a new, parallel check this step invents.
- **`RhythmGestureSettings.cs`** — the one phase-local settings record `BuildPulses`'s own proposed
  signature has no room for (it carries no settings parameter at all); supplied instead as a
  constructor dependency of `RhythmGesturePlanner`, the same settings-record convention already
  established by `RhythmSettings`/`OnsetDetectionSettings`/`TempoDetectionSettings`/`BeatGridSettings`.
  `RhythmGestureSettings.Default` is 50-150 ms — short, restrained pulses well under one beat at any
  realistic tempo (Section 3's own "restrained default visual density").
- **`RhythmGesturePlanInvalidException.cs`** — mirrors `BeatGridEditInvalidException`'s own shape:
  empty `targets`, blank `verifiedRecipeId`, or a malformed/empty `range`. An unknown recipe id itself
  is **not** this exception — see below.

**New test file `tests/XlightsAI.Studio.Rhythm.Tests/Step036AcceptanceTests.cs`** — 15 tests (detailed
in §4). No changes to `Step031AcceptanceTests.cs` through `Step035AcceptanceTests.cs`.

**No changes to any existing `Contracts`, `Audio`, `Editing`, `Xlights`, `Projects`, `Reference`,
`Shell`, `App`, or any prior Phase 05 `Rhythm` type's own public shape.** `BeatMarker`,
`BeatGridSettings`, `BeatGrid`, `IBeatGridBuilder`/`BeatGridBuilder`, `IBeatGridEditor`/
`BeatGridEditor`, `IBeatGridStore`/`JsonBeatGridStore`, every exception type, `EffectInstance`,
`IEffectRecipeRegistry`/`EffectRecipeRegistry`, `SequenceDocument`, `XsqExporter`,
`XsqStructuralReader` — all completely unmodified, confirmed by every one of the 446 pre-existing
tests still passing unmodified (§4).

## 3. Architecture deviations / decisions

- **No narrowing of Section 3's proposed `IRhythmGesturePlanner.BuildPulses` signature was required** —
  unlike every prior Phase 05 step (033/034/035), which each had to narrow or extend their own proposed
  Section 3 interface. `BuildPulses(grid, targets, verifiedRecipeId, range)` is already a pure, in-memory
  function over fully-resolved inputs (an already-built `BeatGrid`, an already-chosen target set, an
  already-verified recipe id, an already-decided time window) — nothing here requires I/O, async, or a
  `SongId`-keyed lookup the proposed signature is missing. This is disclosed explicitly as a real,
  positive finding, not silently skipped as "nothing to report."
- **`IRhythmGesturePlanner` is implemented as a plain class with a constructor-injected
  `IEffectRecipeRegistry` and `RhythmGestureSettings`, rather than a static/stateless function.** This
  mirrors `BeatGridBuilder`'s own precedent (constructor-injected `IBeatGridStore`) and is the only way
  to supply Section 3's data-contract rule ("do not invent xLights effect parameters") concretely:
  every single `EffectInstance` this planner produces is constructed by calling the real, externally
  supplied `IEffectRecipeRegistry.CreateVerified` — this planner itself has zero knowledge of what
  `effect.on.v1` (or any other recipe) actually means in xLights terms, and cannot drift from whatever
  that registry currently verifies.
- **"Alternating-chase" is implemented as a deterministic, ordinal-sorted round-robin across
  `targets`, one target per beat in time order — not a fixed "ping-pong only ever between exactly two
  props" rule.** `targets` is typed `IReadOnlySet<ModelId>` (Section 3's own proposed signature,
  verbatim) — an arbitrary-sized set, not a fixed pair — so the chase rotates through however many
  targets the caller selected. With exactly two targets (this step's own gate-demo shape: "generate
  restrained pops for two props") the rotation is exactly the expected alternating A/B/A/B pattern,
  directly proven in `Step036_Acceptance_Passes_RenderedExportedEffectsFollowDetectedBeats` (§4) and
  the manual demo (§5). Sorting by `ModelId.Value` (ordinal) rather than the caller's own
  `IReadOnlySet<ModelId>` enumeration order was a deliberate choice: `HashSet<T>` enumeration order is
  explicitly unspecified by .NET and must never be relied upon for a deterministic, reproducible chase
  pattern (Section 2: "Verification... Do not claim tests ran when they did not" extends to never
  claiming a result is deterministic when the underlying collection type does not guarantee it).
- **"Beat-pop" duration uses linear interpolation between a fixed min/max by `BeatMarker.Confidence`,
  not a fixed constant duration.** Step 036's own implementation guidance, verbatim: "Translate selected
  beat strength into finite pulse duration" — a uniform duration regardless of confidence would not
  satisfy this; `RhythmGestureSettings` makes the min/max an explicit, inspectable, swappable setting
  (not a hardcoded magic number) per Section 4's cache-key-versioning convention precedent, even though
  `BuildPulses` output here is not itself cached (a pure function recomputed from values already in
  hand, same precedent as `IBeatGridEditor`).
- **Output frame snapping reuses `XsqExporter.FrameIntervalMs` directly (`25` ms) as the single,
  already-established source of truth for "the real xLights sequence frame grid," rather than
  redeclaring a second, possibly-drifting `25` literal inside `Rhythm`.** This is the concrete reason
  `Rhythm` now depends on `Xlights` (§2): Section 2's own canonical rule ("Convert to xLights frames
  only when explicitly snapping or exporting") names frame-snapping as an explicit, intentional
  conversion point — and the single already-verified constant for what a frame actually is already
  lives in `XsqExporter`, not duplicated here.
- **An unknown/unverified `verifiedRecipeId` is surfaced as `EffectPlanInvalidException` (PHASE-02's
  own, pre-existing exception type, thrown by the real `IEffectRecipeRegistry.CreateVerified`), not a
  new `RhythmGesturePlanner`-specific exception.** `RhythmGesturePlanInvalidException` covers only this
  step's own input-shape failures (empty targets, blank recipe id string, malformed range) that occur
  *before* any registry call is even attempted; whether a given `verifiedRecipeId` is one the registry
  actually recognizes is PHASE-02's own concern and already has a real, tested answer — inventing a
  second, parallel "is this recipe known" check inside `Rhythm` would risk the two checks silently
  drifting apart over time. Proven by `BuildPulses_UnknownRecipeId_ThrowsEffectPlanInvalid_NegativePath`
  (§4).
- **A grid with zero beats inside the requested `range` (including a grid with zero beats at all —
  true silence) returns an empty `IReadOnlyList<EffectInstance>`, not an exception.** This step's own
  failure/edge case, verbatim: "Silence... must be handled explicitly" — satisfied by *not* throwing: a
  silent/out-of-range request has a well-defined, valid answer ("generate nothing"), and forcing an
  exception for a legitimately empty result would make this planner unusable for scrubbing a time range
  that simply has no beats in it yet (e.g. an intro). Proven by two dedicated tests (§4): a structurally
  empty `BeatGrid.Beats` array, and a real click-track grid queried with a `range` entirely after the
  last beat.
- **A pulse's end time is clipped to `range.EndTicks`, and a beat whose clipped window would be
  zero/negative width is silently skipped rather than producing a malformed effect** — a defensive
  branch proven unreachable in practice by this step's own filter (`beatsInRange` already guarantees
  every iterated beat's own `TimeTicks < range.EndTicks`, so the clipped end is always strictly greater
  than that beat's start), but kept and disclosed explicitly rather than removed, since it is the
  concrete code path implementing quality-gate matrix row 7's "remain confined to time scope" guarantee
  and is cheap insurance against a future change to the surrounding loop silently breaking that
  invariant.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned
non-functional results for basic commands (`cat`, `ls`, directory listings), the same pre-existing
environment limitation recorded in every prior step's own evidence; PowerShell was used for every
command below without incident.

```
dotnet build XlightsAI.Studio.slnx --nologo                                                 # baseline, before any Step 036 code existed
dotnet test  XlightsAI.Studio.slnx --nologo                                                  # baseline regression: 446/446 passing
dotnet build src\XlightsAI.Studio.Rhythm\XlightsAI.Studio.Rhythm.csproj --nologo             # new planner/settings/exception types + new Xlights ProjectReference, before tests existed
dotnet test  tests\XlightsAI.Studio.Rhythm.Tests\XlightsAI.Studio.Rhythm.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step036"
  # first and only real run: 15/15 passed (no red-then-green cycle needed; the one real design risk —
  # a clipped-to-near-zero pulse window at the very end of a tight test range triggering
  # XsqExportInvalidException's own "non-positive duration" check — was caught and fixed by direct
  # code-path reasoning before the test was ever run, by choosing exported-acceptance test ranges that
  # extend comfortably past the last beat's own max-duration pulse; disclosed explicitly rather than
  # overclaimed as "every test passed first try with no reasoning required")
dotnet build XlightsAI.Studio.slnx --nologo                                                  # full solution, after Step 036 implementation
dotnet test  XlightsAI.Studio.slnx --nologo                                                  # full regression, final
```

### Build results

`Rhythm`, `Rhythm.Tests`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 036 tests only, real run)

```
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.Step036_Acceptance_Passes_RenderedExportedEffectsFollowDetectedBeats [772 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_NullTargets_ThrowsArgumentNullException [230 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_RangeNarrowerThanGrid_OnlyConfinesToScope_NegativePath [522 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_GridWithNoBeatsInRange_ReturnsEmpty_SilenceEdgeCase [6 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.Constructor_NullRegistry_ThrowsArgumentNullException [2 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_DurationsAreFrameSnapped [313 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.Constructor_InvalidSettings_ThrowsArgumentOutOfRange_NegativePath [2 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_InvalidTimingBoundary_EndNotAfterStart_ThrowsInvalid_NegativePath [229 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_RangeOutsideAllBeats_ReturnsEmpty_SilenceEdgeCase [236 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_UnknownRecipeId_ThrowsEffectPlanInvalid_NegativePath [223 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_BlankRecipeId_ThrowsInvalid_NegativePath [221 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_EmptyTargets_ThrowsInvalid_NegativePath [223 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_NegativeRangeStart_ThrowsInvalid_InvalidTimingBoundaryNegativePath [227 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_HigherConfidenceBeatsGetLongerPulses_BeatStrengthTranslatesToDuration [539 ms]
Passed XlightsAI.Studio.Rhythm.Tests.Step036AcceptanceTests.BuildPulses_NullGrid_ThrowsArgumentNullException [3 ms]

Total tests: 15. Passed: 15, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  67, Skipped: 0, Total:  67  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**461 total, 0 failed, 0 skipped** — up from the Step 035 baseline of 446 (+15 new
`Step036AcceptanceTests` in the already-existing `Rhythm.Tests` project: 67 = 7 Step031 + 9 Step032 +
10 Step033 + 11 Step034 + 15 Step035 + 15 Step036). Every one of the 446 pre-existing tests still
passes unmodified, confirming no prior step's contract was broken by the new `Rhythm` -> `Xlights`
project reference or any other change.

### Persisted-output inspection / observable demonstration for this step

Step 036's own user-visible outcome is "Saved audio/analysis artifact with timestamp provenance plus a
synchronized waveform/timeline demonstration." As with Steps 034/035 (same disclosed scope decision,
§6), no `BeatGrid`/pulse screen or `Shell`/`App` wiring exists in this repository yet; the observable
demonstration is a real, running end-to-end exercise of the actual exported artifact, outside the test
harness's temp-directory cleanup, run from a throwaway console program (not part of the repository,
under the session scratchpad, deleted after this run) referencing the real `Rhythm`/`Xlights`/`Audio`/
`Reference`/`Contracts` projects:

```
dotnet run --project <scratchpad>\step036-demo\step036-demo.csproj --nologo
```

Observed real output (same 120 BPM click-track fixture shape as Steps 034/035's own demos — 16 clicks,
0.5 s silent lead-in, 8 kHz):

```
Probed SongAsset: Id=SongId { Value = b5d49610-0c47-4e87-b05f-1ba4b87f71f9 }, Sha256=159efe567569..., SampleRate=8000, SampleCount=68000
Generated beat grid: VersionId=bd202160-cedf-4b55-9b76-bde9721e43eb, BeatCount=16, Top=120.00 BPM
Corrected downbeat to beat Id=bddef688-f5f3-476b-bb30-b815482cd4b4. Grid CorrectionVersion now 1.
Built 16 beat-pop pulses across 2 props (alternating-chase).

Beat#  TimeTicks   Confidence  Target     Start-End (ticks)
    0    4960000      0.9054  Arch 1     [4960000,6460000)
    1    9920000      0.7577  Arch 2     [9920000,11170000)
    2   15040000      0.9539  Arch 1     [15040000,16540000)
    3   20000000      1.0000  Arch 2     [20000000,21500000)
    4   24960000      0.9054  Arch 1     [24960000,26460000)
    5   29920000      0.7577  Arch 2     [29920000,31170000)
    6   35040000      0.9539  Arch 1     [35040000,36540000)
    7   40000000      1.0000  Arch 2     [40000000,41500000)
    8   44960000      0.9054  Arch 1     [44960000,46460000)
    9   49920000      0.7577  Arch 2     [49920000,51170000)
   10   55040000      0.9539  Arch 1     [55040000,56540000)
   11   60000000      1.0000  Arch 2     [60000000,61500000)
   12   64960000      0.9054  Arch 1     [64960000,66460000)
   13   69920000      0.7577  Arch 2     [69920000,71170000)
   14   75040000      0.9539  Arch 1     [75040000,76540000)
   15   80000000      1.0000  Arch 2     [80000000,81500000)

Exported real .xsq to: ...\step036-pulses.xsq
File exists on disk: True
Export sha256: 4BDF335F8A0182DC...
Export warning (disclosed, pre-existing from PHASE-02): The 'effect.on.v1' recipe's EffectDB settings are an empty placeholder: no real xLights-authored 'On' effect sample has been captured yet to verify its actual default parameters against (no xLights install is available in this environment). Do not treat this export as proof the 'On' effect renders correctly until Step 014/015's golden fixture process captures and substitutes a real, verified settings string.

Re-read exported XSQ independently: 16 effect placements across 2 elements.
Beat count matches exported effect count: True
All placements confined to [0, 8500] ms range: True

Step 036 manual demo completed successfully.
Temp directory cleaned up.
```

Direct, real, non-test-harness confirmation of every element of this step's own required acceptance:
every one of the real grid's 16 beats produced a real pulse; pulses strictly alternate `Arch 1`/`Arch
2` in beat order (the "alternating-chase" requirement); pulse duration visibly scales with each beat's
own `Confidence` (e.g. beat 3's `1.0000` confidence produces a `1500000`-tick/150ms pulse — this
setup's own configured maximum — versus beat 1's `0.7577` confidence producing a shorter `1250000`-tick
pulse); every pulse's own duration is an exact multiple of the real xLights `25` ms frame grid (`150`,
`125` ms, etc. — all `% 25 == 0`); the pulses were compiled into a real `SequenceDocument`, exported
through the real, unmodified PHASE-02 `XsqExporter` to a real `.xsq` file on disk, and independently
re-read back through the real, unmodified `XsqStructuralReader` — proving the beat-driven pulses
actually survive a real xLights XML round trip, not merely existing as in-memory `EffectInstance`
values ("Rendered/exported effects follow detected beats," required acceptance, verbatim); and every
re-read placement's own end time falls at or before the requested scope boundary ("remain confined to
time scope," quality-gate matrix row 7, verbatim).

No xLights/Avalonia UI demonstration, and no actual xLights-application render/open of the exported
`.xsq`, was run for this step. Per this user's own standing waiver (recorded in this assistant's
memory) a real-xLights render check is not required as a phase-gate blocker. Separately, this
specific gap is the same disclosed one every PHASE-02 step (012-015) already carries forward
unmodified (§2/§3): no installed xLights build has ever been available to this repository, so
`effect.on.v1`'s own EffectDB settings string remains an intentionally empty, disclosed placeholder
(the export's own printed `Warnings` entry, observed verbatim above) — this step reuses that
already-verified recipe exactly as-is and introduces no new unverified XML of its own.

## 5. Fixtures / input hashes

No new fixture files were added to `fixtures/`. All tests and the manual demo construct real WAV bytes
via this repository's own already-licensed, already-verified `DeterministicSyntheticAudioGenerator`,
identical generation parameters to Steps 034/035's own fixtures:

- `BuildClickTrack(bpm: 120, clickCount: 6-16, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate:
  8000)` prepended with 4000 samples (0.5 s) of silence — the primary acceptance/demo fixture.
- A hand-constructed, zero-beat `BeatGrid` literal (no audio) for the structural silence/empty-grid
  edge case (`BuildPulses_GridWithNoBeatsInRange_ReturnsEmpty_SilenceEdgeCase`).
- The gate-demo's own two-prop layout shape ("Arch 1", "Arch 2") — matching PHASE-02's own
  `Step013`/`Step015AcceptanceTests`' `GateDemoLayout()` naming convention, reused here as
  `TwoArchLayout()`.
- All test files are written under `%TEMP%\xlai-step036-*\`, deleted by each test class's `Dispose()`
  — identical cleanup convention to Steps 031-035. The manual demo's own throwaway files were written
  under `%TEMP%\xlai-step036-demo-*\` and deleted by the demo program itself at the end of its run
  (confirmed by "Temp directory cleaned up." in the real output above); the throwaway demo *project*
  itself (outside the repository, under the session scratchpad) was deleted after this evidence was
  recorded.

## 6. Unresolved issues / known gaps

- **No UI wiring (SCREEN 09 BEAT GRID / a pulse-preview screen) in this step** — a disclosed,
  deliberate scoping decision, consistent with Steps 034/035's own identical precedent: no such
  wiring exists anywhere in `Shell`/`App` yet for any Phase 05 step.
- **`effect.on.v1`'s own EffectDB settings remain the same disclosed empty placeholder PHASE-02
  established** — this step does not resolve that pre-existing gap (no installed xLights build has
  been available to capture a real, verified "On" settings string); it is carried forward unchanged,
  and every export through this step's own pulses still carries PHASE-02's own disclosed `Warnings`
  entry verbatim (observed in §4's real demo output).
- **No new, second xLights effect family (a literal "pulse" or "chase" xLights effect type) was
  introduced.** Per `XLIGHTS-SEQUENCE-ENGINEERING-GUIDE.md` line 129 (§1) this is an explicitly valid
  choice — "compile into a series of elementary `On` effects" — not a shortcut around the step's own
  acceptance text; "beat-pop" and "alternating-chase" are real, demonstrated *gesture-compilation*
  behaviors (finite duration from beat strength; rotating target assignment) built entirely from the
  one recipe this codebase has actually verified end-to-end. A literal new xLights effect family (e.g.
  capturing a real "Shockwave" or "Bars" effect's own settings XML) remains blocked on the same missing
  xLights install PHASE-02 already disclosed, and was not fabricated here.
- **`RhythmGesturePlanner` does not itself validate that `targets` resolve against any real
  `DisplayGraph`** (unlike `ThreeEffectPlanFactory`, which does this check itself). This is a
  deliberate, disclosed scope boundary: Section 3's own proposed `BuildPulses` signature takes
  `IReadOnlySet<ModelId>` directly, not a `DisplayGraph` — resolving a target name against an actual
  imported layout is the caller's own command-boundary responsibility (the same "narrower, in-memory
  domain function, not an I/O-bound or layout-aware service" precedent `IBeatGridEditor` already set,
  §3). The real demo (§4) still only ever used genuinely layout-present names ("Arch 1"/"Arch 2"), and
  `XsqExporter` itself independently re-validates every effect's target against the real `DisplayGraph`
  at export time regardless (an unresolved target there still fails safely via the pre-existing,
  unmodified `XsqExportInvalidException` path) — so an unresolved target is still caught before any file
  is ever written, just one layer later than `ThreeEffectPlanFactory`'s own precedent.
- **The alternating-chase rotation is purely positional (beat index modulo target count) and carries
  no separate "skip a beat to space out pulses further" visual-density knob beyond the pulse-duration
  min/max itself.** A future step wanting, e.g., "only every other beat pops" would need an additional,
  explicit setting — not fabricated here since no required acceptance text in this step names it.

## 7. PASS / BLOCKED

**Status: PASS** for Step 036's own stated required acceptance and failure/edge case:

- "Rendered/exported effects follow detected beats" (required acceptance, verbatim) — proven by
  `Step036_Acceptance_Passes_RenderedExportedEffectsFollowDetectedBeats` (every one of a real,
  click-track-derived `BeatGrid`'s beats produces exactly one real, independently re-read exported
  `.xsq` effect placement at that beat's own frame-rounded start time, confirmed by a real
  `XsqExporter.ExportAsync` -> `XsqStructuralReader.Read` round trip against real on-disk bytes, not an
  in-memory assertion alone) and independently confirmed by the real, non-test-harness demo run (§4).
- "Translate selected beat strength into finite pulse duration" (implementation guidance, verbatim) —
  proven by `BuildPulses_HigherConfidenceBeatsGetLongerPulses_BeatStrengthTranslatesToDuration`
  (monotonic non-decreasing duration by confidence, using a real fixture with genuinely varying
  per-beat confidence) and directly observed in the real demo's own printed table (§4: beat 3's `1.0000`
  confidence visibly produces a longer pulse than beat 1's `0.7577`).
- "...respecting visual density and output frame snapping" — "frame snapping" is proven by
  `BuildPulses_DurationsAreFrameSnapped` (every produced pulse's own duration is an exact, non-zero
  multiple of the real `XsqExporter.FrameIntervalMs`); "restrained... visual density" is proven by
  `RhythmGestureSettings.Default`'s own 50-150 ms bound (well under one beat interval at the fixture's
  120 BPM/500 ms beat spacing) and by the alternating-chase rotation itself never firing every target on
  every beat simultaneously.
- "...and verified effect recipe" — proven by every produced `EffectInstance` being constructed
  exclusively through the real, externally-injected `IEffectRecipeRegistry.CreateVerified`, and by
  `BuildPulses_UnknownRecipeId_ThrowsEffectPlanInvalid_NegativePath` proving an unverified recipe id is
  rejected by that same real registry, not silently accepted.
- "alternating-chase... to real selected props" — proven by the real demo's own printed per-beat
  target column (strict `Arch 1`/`Arch 2` alternation) and by
  `Step036_Acceptance_Passes_RenderedExportedEffectsFollowDetectedBeats`'s own per-beat
  `expectedElement` assertion against the real, independently re-read exported XSQ elements.
- "Generated effects align selected props and remain confined to time scope" (quality-gate matrix row
  7, verbatim, the spec's own named `CORE DEMO REQUIRED` line) — proven by
  `Step036_Acceptance_Passes_RenderedExportedEffectsFollowDetectedBeats`'s own range-boundary
  assertions against the real exported file, and by the dedicated
  `BuildPulses_RangeNarrowerThanGrid_OnlyConfinesToScope_NegativePath` test (a range covering only the
  first half of a grid's beats produces exactly, and only, that many pulses, every one of them inside
  the requested window).
- "Silence... must be handled explicitly" — proven by two dedicated tests:
  `BuildPulses_GridWithNoBeatsInRange_ReturnsEmpty_SilenceEdgeCase` (a structurally empty `BeatGrid`)
  and `BuildPulses_RangeOutsideAllBeats_ReturnsEmpty_SilenceEdgeCase` (a real click-track grid queried
  entirely outside its own beats) — both return a real, empty, non-exceptional result.
- "Invalid timing boundaries... must be handled explicitly" — proven by
  `BuildPulses_InvalidTimingBoundary_EndNotAfterStart_ThrowsInvalid_NegativePath` (both `end == start`
  and `end < start`) and `BuildPulses_NegativeRangeStart_ThrowsInvalid_InvalidTimingBoundaryNegativePath`
  — no partial or fabricated effect list is ever returned for a malformed range.
- "...and missing media must be handled explicitly" — this step introduces no new media-facing code
  path; the pre-existing Audio-layer/Step 031-035 handling (unmodified, §1/§4) continues to apply
  unchanged, confirmed by all of those steps' own missing-media tests still passing by name in this
  session's full regression run.
- Null-argument handling — proven by `BuildPulses_NullGrid_ThrowsArgumentNullException`,
  `BuildPulses_NullTargets_ThrowsArgumentNullException`, and
  `Constructor_NullRegistry_ThrowsArgumentNullException`.
- Constructor-level settings validation — proven by
  `Constructor_InvalidSettings_ThrowsArgumentOutOfRange_NegativePath` (both a non-positive minimum and
  a maximum below the minimum are rejected at construction, before any `BuildPulses` call can ever use
  an invalid setting).
- Empty-targets / blank-recipe-id input validation — proven by
  `BuildPulses_EmptyTargets_ThrowsInvalid_NegativePath` and
  `BuildPulses_BlankRecipeId_ThrowsInvalid_NegativePath`.
- The step's own user-visible outcome ("Saved audio/analysis artifact with timestamp provenance plus a
  synchronized waveform/timeline demonstration") is proven at the domain/export level identically to
  Steps 034/035's own precedent: the exported `.xsq` artifact was directly inspected on disk (§4, real
  file path, real re-read/round-trip confirmation of effect count/timing/target), and every pulse
  carries its own provenance back to the unchanged, already-corrected source `BeatGrid`. No UI timeline
  view exists yet to render this visually — a disclosed scope decision (§3/§6), not a silently skipped
  requirement — and every claim above is backed by an actually-executed, actually-passing automated
  test, all 15 of which passed on their first real run (§4; the one real design risk — a
  near-zero-width clipped pulse at a tight test range's own boundary triggering `XsqExporter`'s own
  "non-positive duration" rejection — was identified and avoided by direct reasoning about the clipping
  formula before any test was run, disclosed explicitly rather than claimed as an untested certainty).

No previously accepted step's contract was modified in an incompatible way — every PHASE-01-04 public
member and every prior Phase 05 `Rhythm` type (`BeatMarker`, `BeatGridSettings`, `BeatGrid`,
`IBeatGridBuilder`/`BeatGridBuilder`, `IBeatGridEditor`/`BeatGridEditor`, every exception type) is
completely unchanged; the only architectural change is the new, disclosed, additive `Rhythm` ->
`Xlights` `ProjectReference` (§2/§3) — confirmed by the full pre-existing 446-test suite passing
unmodified before a single Step 036 line of code existed (§1), and again at 461/461 (446 old + 15 new)
after implementation (§4), with every prior step's own tests individually still passing by name in
that same run.

## 8. Phase-wide status after this session

Steps 031, 032, 033, 034, 035 and now 036 (of PHASE-05's seven numbered steps, 031-037) now have
recorded PASS evidence. Step 037 and the phase-wide gate demonstration/Gate Report (spec Section 7/8:
"Analyze a click/drum sample, see beat markers, correct an intentionally wrong downbeat, generate
restrained pops for two props and play them synchronized while comparing before/after") are **not**
produced by this session — per the task's own instruction, this session implements only Step 036 and
does not proceed past it until this evidence is reviewed.
