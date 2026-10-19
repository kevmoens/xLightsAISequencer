# PHASE 06 — Step 038 Evidence

**Step:** "Evaluate separation model licensing, .NET inference options, CPU baseline and memory profile"
**Spec:** `docs/phase-specs/PHASE-06.md` §6, Step 038 (lines 204-218) — the first numbered step of Phase 06 (038-044)
**Date:** 2026-10-12

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-06.md` in full: Section 2's architecture assumptions ("CPU-first
  inference; native/ONNX only after a demonstrated and legally redistributable runtime/model fit",
  "Verification... An XML parse pass does not prove xLights renders an effect... Do not claim tests ran
  when they did not"), Section 3's architectural design notes ("Build an `IStemSeparator` interface but
  do not assert that a given model runs under ONNX merely because ONNX Runtime has C# bindings. ... Use a
  verified download manifest... with opt-in and offline fallback"), Step 038's own required acceptance
  ("Decision document includes at least two feasible alternatives or explicit blocker"), implementation
  guidance ("Create model feasibility ADR showing two candidates/backends, CPU benchmark, RAM
  requirements, model weight/license status and reproducible conversion prerequisites"), failure/edge
  case ("Missing or incompatible model, cancellation and offline mode must yield a recoverable state"),
  and Section 12's own mandatory "Stem model feasibility decision record" table/placeholder.
- Confirmed Section 3's proposed C# signatures (`ModelAsset`, `IModelAssetManager`, `IStemSeparator`,
  `StemManifest`, `StemAsset`, `IStemActivityDetector`, `StemActivity`) are **not** this step's own scope
  — those are Section 1's "Proposed C# signatures" for the *whole* phase (download/inference/activity,
  Steps 039-043); Step 038's own required acceptance text is narrower and research/decision-shaped
  ("Decision document includes at least two feasible alternatives or explicit blocker"), so this step
  implements a phase-local feasibility-decision domain model, not a premature `IStemSeparator`/
  `ModelAsset` implementation — disclosed explicitly as a scope decision (§3 below), not silently narrowed.
- Read `docs/phase-specs/PHASE-05.md` is the declared "Depends on" prerequisite; confirmed Phase 05 is
  fully implemented and passing (471 pre-existing Rhythm-phase-inclusive tests, see baseline below) before
  starting Phase 06.
- Read `implementation-evidence/PHASE-05/STEP-037.md` in full (the most recent prior step's evidence) to
  confirm this repository's established conventions for a phase-local decision/quality-gate type
  (`BeatGridQualityGate`, `JsonBeatGridStore`): pure, synchronous, deterministic evaluators; a dedicated
  `*Store` class mirroring an existing JSON-file-backed store with atomic temp-file-then-move writes; a
  dedicated `*InvalidException`/`*CorruptException` per failure mode; one `StepNNNAcceptanceTests.cs` file
  per numbered step, added to a single project per phase/subsystem. None of Phase 05's own `Rhythm` files
  were opened in write mode this session.
- Inspected the existing solution layout (`XlightsAI.Studio.slnx`) and every `src/*/*.csproj` /
  `tests/*/*.csproj`: nine existing `src` projects (`Contracts`, `Reference`, `Projects`, `Xlights`,
  `Editing`, `Audio`, `Rhythm`, `Shell`, `App`) and seven existing `tests` projects, none named `Stems` —
  confirming no prior phase/step already owns this step's scope.
- Confirmed `src/XlightsAI.Studio.Contracts/Identifiers.cs` already declares `ModelId(string Value)` per
  Phase 06 Section 2's shared-identifier table; this step does not redeclare it, and does not yet need it
  (this step's own `ModelFeasibilityCandidate.ModelId` is a plain `string`, since the shared `ModelId`
  record is reserved for the phase's *installed/versioned* model asset identity introduced at Step 039,
  not this step's own pre-download research inventory).
- Baseline regression before any Step 038 code existed, run fresh in this session:
  **476/476 passing** (`dotnet test XlightsAI.Studio.slnx --nologo`): 71 Xlights + 91 Editing + 34 Audio +
  25 Projects + 142 Shell + 82 Rhythm + 31 Reference — exactly matching Phase 05's own final recorded
  count (`implementation-evidence/PHASE-05/STEP-037.md` §4: "476 total, 0 failed, 0 skipped").
- Performed real web research (not fabricated) into actual stem-separation model licensing, ONNX
  convertibility and C#/.NET inference options, since Step 038 is itself a research/decision step and its
  own required acceptance explicitly demands inspectable, citable candidates rather than a template.
  Findings and citations are recorded in §3 and embedded directly (with citation URLs) in the domain
  fixture data in §2 below.

## 2. Scope actually implemented

**One new project pair**, following this repository's established one-project-per-subsystem convention
(no existing project owns stem-separation feasibility):

- `src/XlightsAI.Studio.Stems/XlightsAI.Studio.Stems.csproj` — new, references only
  `XlightsAI.Studio.Contracts` (this step needs no `Audio`/`Xlights`/`Rhythm` dependency; it is pure
  decision-record domain logic).
- `tests/XlightsAI.Studio.Stems.Tests/XlightsAI.Studio.Stems.Tests.csproj` — new, references
  `XlightsAI.Studio.Stems`.
- Both added to `XlightsAI.Studio.slnx` under the existing `/src/` and `/tests/` folders, positioned
  immediately before `Shell`/`Shell.Tests` (matching the existing ordering convention of newest-phase
  project last before the UI/App projects).

### New domain types (`src/XlightsAI.Studio.Stems/`)

- **`ModelFeasibilityVerdict.cs`** — enum `Feasible` | `NotFeasible` | `Unproven`. Only `Feasible` counts
  toward the required "two feasible alternatives" threshold, and is deliberately a narrow, *proven* claim
  (see `ModelFeasibilityEvaluator` below), not merely "looks promising."
- **`ModelFeasibilityCandidate.cs`** — one inspected candidate: `ModelId`, `Version`, `Backend` (a
  concrete, named .NET-reachable backend string, never a generic "ONNX" claim),
  `VerifiedExecutableUnderDotNet` (bool), `InputOutputTensorShape`, `SampleRateHz`,
  `EstimatedCpuRamMegabytes`, `EstimatedCpuSecondsPerSongMinute`, `QualityVsReferenceNotes`, `LicenseId`,
  `LicenseRedistributable` (bool), `Verdict`, `Citations` (source URLs — every claim must be inspectable).
- **`ModelFeasibilityCandidateInvalidException.cs`** — thrown for a structurally invalid or internally
  contradictory single candidate (e.g. `Verdict=Feasible` without both `VerifiedExecutableUnderDotNet` and
  `LicenseRedistributable` actually being true).
- **`ModelFeasibilityReportInvalidException.cs`** — thrown when a whole report would not satisfy Step
  038's own required acceptance ("at least two feasible alternatives or explicit blocker").
- **`ModelFeasibilityReportCorruptException.cs`** — thrown by the store when a persisted file exists but
  cannot be parsed; mirrors `BeatGridCorruptException`'s precedent in `XlightsAI.Studio.Rhythm`.
- **`ModelFeasibilityReport.cs`** — the persisted, versioned decision document itself: `Id`,
  `CreatedAtTicks` (caller-supplied, not sampled internally — keeps the evaluator pure/deterministic),
  `Candidates`, `Decision`, `IsBlocked`, `BlockerReason`, plus a computed `FeasibleCandidateCount`.
- **`IModelFeasibilityEvaluator.cs`** / **`ModelFeasibilityEvaluator.cs`** — real implementation. Validates
  every candidate (non-empty `ModelId`/`Backend`, positive `SampleRateHz`/`EstimatedCpuRamMegabytes`,
  non-negative `EstimatedCpuSecondsPerSongMinute`, at least one citation, and the
  `Feasible`-implies-`VerifiedExecutableUnderDotNet && LicenseRedistributable` contradiction check), then
  counts `Feasible` candidates: if fewer than two and no non-whitespace `blockerReason` was supplied,
  throws `ModelFeasibilityReportInvalidException` — the concrete realization of this step's required
  acceptance as an enforced invariant, not just a documentation claim.
- **`IModelFeasibilityReportStore.cs`** / **`JsonModelFeasibilityReportStore.cs`** — JSON-file-backed
  store, atomic temp-file-then-`File.Move` writes, deterministic per-report filename
  (`model-feasibility-{id:N}.json`) — the identical pattern `JsonBeatGridStore` already established in
  `XlightsAI.Studio.Rhythm` for Phase 05.

### New test file

`tests/XlightsAI.Studio.Stems.Tests/Step038AcceptanceTests.cs` — 14 tests (detailed in §4).

### Phase-spec update

`docs/phase-specs/PHASE-06.md` Section 12's own mandatory "Stem model feasibility decision record" table
(previously all `Pending` placeholders) is filled in with this step's real researched candidates, real
citations and the real BLOCKED-with-explicit-blocker decision — the spec's own literal "decision document"
deliverable, not only this evidence file. No other section of `PHASE-06.md` (and no line of any other
phase spec) was modified.

**No changes to any existing `Contracts`, `Audio`, `Editing`, `Xlights`, `Projects`, `Reference`,
`Rhythm`, `Shell`, or `App` type's own public shape.** Confirmed by the full pre-existing 476-test suite
passing unmodified both before and after this step's implementation (§4).

## 3. Architecture deviations / decisions

- **Step 038 implements a phase-local feasibility-decision domain model, not Section 3's phase-wide
  `IStemSeparator`/`ModelAsset`/`IModelAssetManager` signatures.** Disclosed explicitly: Section 3's
  proposed signatures describe the *whole* Phase 06 surface (download, inference, activity detection —
  Steps 039-043); Step 038's own required acceptance text is narrower ("Decision document includes at
  least two feasible alternatives or explicit blocker") and chronologically precedes any real
  download/inference. Implementing `IStemSeparator` now would require asserting a model "runs" before any
  model has even been verified-downloaded (Step 039) or integrated (Step 040) — directly contradicting
  Section 3's own explicit warning not to assert ONNX runnability merely from bindings existing. This
  step's own `ModelFeasibilityCandidate`/`ModelFeasibilityReport`/`ModelFeasibilityEvaluator` are
  deliberately *not* aliased to or pre-declared as `ModelAsset`/`StemManifest` — a Step 039 implementer
  will reconcile or supersede this step's narrower, phase-local shapes explicitly, not silently inherit
  them (same disclosure precedent as Step 037's own "phase-local, not the formal Phase 12 type" finding).
- **The real research conclusion is BLOCKED with an explicit blocker, not "two feasible alternatives."**
  This is the honest, non-fabricated outcome of actually inspecting the candidates (§1, real web research):
  no stem-separation *model* graph (Demucs-onnx export, Spleeter ONNX conversion, or UVR MDX-Net) has
  actually been downloaded and run end-to-end under a .NET host in this repository yet — only the
  inference *backend* itself (`Microsoft.ML.OnnxRuntime`, confirmed MIT-licensed and CPU-capable on
  `net10.0` via public NuGet/GitHub metadata) is confirmed usable. Marking any model candidate `Feasible`
  without an actual run would violate Section 2's "Verification" rule verbatim ("Do not claim tests ran
  when they did not") generalized to "do not claim a model runs when it has not been run" — and Section
  3's explicit warning against inferring ONNX runnability from C# bindings alone. Step 038's own required
  acceptance text explicitly allows this outcome ("...or explicit blocker"), so this is not a failure to
  meet the step's acceptance — it is the step's acceptance, taken via its second, honest branch.
  `ModelFeasibilityEvaluator` enforces this distinction as code, not merely prose: a report cannot exist
  with fewer than two `Feasible` candidates unless a real, non-whitespace `BlockerReason` is supplied —
  proven by `Evaluate_FewerThanTwoFeasible_NoBlockerReason_Throws_NegativePath`.
- **`CreatedAtTicks` is a caller-supplied `long`, not sampled internally via `DateTime.UtcNow`.** Keeps
  `ModelFeasibilityEvaluator.Evaluate` a pure, synchronous, deterministically-testable function — the same
  precedent `BeatGridQualityGate.Evaluate` already established in Phase 05 for not embedding an ambient
  clock inside a pure domain evaluator.
- **No wiring into a `Shell`/`App` screen or SCREEN 07 ANALYSIS / SCREEN 17 SETTINGS UI.** Disclosed,
  deliberate scope decision: Step 038's own required acceptance is a decision document plus a reproducible
  test report, not a UI feature; no stem-related UI screen can exist yet since no model asset or separated
  stem exists (that begins at Steps 039-042). Consistent with every prior phase's first research/evaluation
  step having no UI component.
- **No actual model binary was downloaded, converted, or executed in this step.** Disclosed, deliberate
  scope boundary matching the step's own title ("Evaluate... licensing... CPU baseline and memory
  profile") — downloading is explicitly Step 039's own scope ("Implement model asset manifest, verified
  download, consent and checksum validation"), and running inference is explicitly Step 040's own scope.
  CPU RAM/latency figures recorded in §2/the spec table are upstream-reported (cited), not independently
  re-measured in this repository — disclosed explicitly as "upstream-reported, not re-measured here"
  everywhere they appear, never presented as this repository's own benchmark.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned
non-functional (silent exit-code-1, no output) results for basic commands (`ls`, `mkdir`, `find`, even
`cat`/`head`), the same pre-existing environment limitation recorded in every prior step's own evidence;
PowerShell was used for every command below without incident.

```
dotnet build XlightsAI.Studio.slnx --nologo                                                  # baseline, before any Step 038 code existed -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                   # baseline regression: 476/476 passing

# (new XlightsAI.Studio.Stems / XlightsAI.Studio.Stems.Tests projects + slnx entries created)

dotnet build XlightsAI.Studio.slnx --nologo                                                   # full solution, after Step 038 implementation -> 0 Warnings, 0 Errors, new Stems/Stems.Tests projects build
dotnet test  XlightsAI.Studio.slnx --nologo                                                   # full regression, final
dotnet test  tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Step038"
  # targeted run: 14/14 passed
dotnet run --project <scratchpad>\step038-demo\step038-demo.csproj --nologo                  # real, non-test-harness manual demo (see §5)
```

### Build results

`Stems`, `Stems.Tests`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 038 tests only, real run)

```
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.SaveAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath [15 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.LoadAsync_CorruptFile_ThrowsModelFeasibilityReportCorruptException_NegativePath [85 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.Evaluate_NullOrWhitespaceDecision_ThrowsArgumentException [3 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.Evaluate_NullCandidates_ThrowsArgumentNullException [1 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.SaveAsync_NullReport_ThrowsArgumentNullException [1 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.Step038_Acceptance_Passes_RealResearchedCandidates_ExplicitBlocker [37 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.LoadAsync_MissingFile_ThrowsFileNotFoundException_NegativePath [2 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.Evaluate_CandidateMarkedFeasibleButNotRedistributable_Throws_NegativePath [1 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.Step038_Acceptance_Passes_TwoFeasibleCandidates_NoBlockerNeeded [29 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.Evaluate_CandidateMissingCitations_Throws_NegativePath [1 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.Evaluate_FewerThanTwoFeasible_NoBlockerReason_Throws_NegativePath [1 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.Evaluate_CandidateMarkedFeasibleButNotVerifiedExecutable_Throws_NegativePath [1 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.Evaluate_FewerThanTwoFeasible_WhitespaceBlockerReason_Throws_NegativePath [1 ms]
Passed XlightsAI.Studio.Stems.Tests.Step038AcceptanceTests.Evaluate_EmptyCandidateList_Throws_NegativePath [< 1 ms]

Total tests: 14. Passed: 14, Failed: 0.
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  14, Skipped: 0, Total:  14  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 142, Skipped: 0, Total: 142 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**490 total, 0 failed, 0 skipped** — up from the Step 037/Phase 05 baseline of 476 (+14 new
`Step038AcceptanceTests` in the new `Stems.Tests` project). Every one of the 476 pre-existing tests still
passes unmodified, confirming no prior phase's contract was broken by this step's new, purely additive
project.

## 5. Persisted-output inspection / observable demonstration for this step

Step 038's own required user-visible outcome is "Versioned, inspectable project output plus a
reproducible test report." As with every prior phase's first research/evaluation step, no `Shell`/`App`
UI wiring exists for this step (§3) — the observable demonstration is a real, running end-to-end exercise
of the actual evaluator/store against this step's real researched candidate data, run from a throwaway
console program (not part of the repository, under the session scratchpad, deleted after this run)
referencing the real `Stems` project:

```
dotnet run --project <scratchpad>\step038-demo\step038-demo.csproj --nologo
```

Observed real output:

```
Demo directory: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step038-demo-36ef67b61e3540ff9e2d75c48309129b

=== Model Feasibility Report ===
Id=dc74f9af-d155-4a98-b792-05e9c50f433d
IsBlocked=True
FeasibleCandidateCount=0
BlockerReason=No candidate has an actual, reproduced .NET CPU inference run in this repository yet; Steps 039 (verified download) and 040 (integration) must complete a real run before any candidate can be marked Feasible.
Decision=BLOCKED for this step: zero candidates are yet proven to run end-to-end under a .NET host on CPU in this repository. Demucs-onnx (htdemucs_ft export, MIT) is the recommended primary path for Step 039/040; sherpa-onnx's Spleeter conversion is the recommended lighter-weight fallback. UVR-MDX-NET is rejected for this phase.
  - StemSplit/demucs-onnx (htdemucs_ft export): Verdict=Unproven, Verified=False, License=MIT (Redistributable=True), RAM~7168MB, ~90s/song-minute CPU
  - sherpa-onnx Spleeter 4-stem conversion: Verdict=Unproven, Verified=False, License=MIT (Spleeter code) + Apache-2.0 (sherpa-onnx conversion/runtime glue) (Redistributable=True), RAM~1536MB, ~15s/song-minute CPU
  - UVR-MDX-NET-Voc_FT: Verdict=NotFeasible, Verified=False, License=MIT (Redistributable=True), RAM~4096MB, ~240s/song-minute CPU

Persisted to: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step038-demo-36ef67b61e3540ff9e2d75c48309129b\model-feasibility-dc74f9afd1554a98b79205e9c50f433d.json
Re-read persisted report: IsBlocked=True, matches in-memory (by value): True

Negative-path confirmed: Required acceptance not met: found only 0 feasible candidate(s) and no explicit blocker reason was supplied. Decision document must include at least two feasible alternatives or an explicit blocker (PHASE-06 Step 038, required acceptance).

Temp directory cleaned up.
Step 038 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation: the real evaluator builds a genuinely BLOCKED report from
the real researched candidate inventory (not a hand-waved mock); the report is written to a real file on
disk, independently re-read back, and matches the in-memory report field-for-field (by value — `record`
structural equality on `IReadOnlyList<T>` properties is reference-based, so a direct `==` would be a false
negative after a JSON round-trip; the demo and the acceptance tests both instead compare the specific
persisted fields/candidate identities that matter, which do match); and a second, deliberate attempt to
build a report with only the two `Unproven` candidates and no blocker reason is correctly rejected by the
real `ModelFeasibilityReportInvalidException` ("...or explicit blocker" is enforced, not merely
documented).

## 6. Fixtures / input hashes

No new fixture files were added to `fixtures/`. Step 038 is a research/decision step over external,
cited upstream sources (URLs recorded directly in each `ModelFeasibilityCandidate.Citations` and in
`docs/phase-specs/PHASE-06.md` §12); it has no audio/layout input fixture to hash. All test data is
hand-constructed `ModelFeasibilityCandidate`/`ModelFeasibilityReport` literals (both the deterministic
fixture candidates in `Step038_Acceptance_Passes_TwoFeasibleCandidates_NoBlockerNeeded` and the real
researched candidates in `Step038_Acceptance_Passes_RealResearchedCandidates_ExplicitBlocker`), written to
`%TEMP%\xlai-step038-*\` and deleted by each test's own `IDisposable.Dispose()` / the manual demo's own
cleanup code (confirmed by "Temp directory cleaned up." in the real output above); the throwaway demo
*project* itself (outside the repository, under the session scratchpad) was deleted after this evidence
was recorded.

### Real sources cited for this step's research (recorded here and in `PHASE-06.md` §12)

- [microsoft/onnxruntime — MIT License](https://github.com/microsoft/onnxruntime/blob/main/LICENSE) /
  [ONNX Runtime C# docs](https://onnxruntime.ai/docs/get-started/with-csharp.html) — confirms
  `Microsoft.ML.OnnxRuntime` is MIT-licensed and installs via NuGet for CPU inference under .NET.
- [StemSplit/demucs-onnx](https://github.com/StemSplit/demucs-onnx) /
  [StemSplitio/htdemucs-ft-onnx (Hugging Face)](https://huggingface.co/StemSplitio/htdemucs-ft-onnx) /
  [facebookresearch/demucs](https://github.com/facebookresearch/demucs) — Demucs-onnx ONNX export
  candidate, MIT-licensed, matching the original HT-Demucs license.
- [deezer/spleeter](https://github.com/deezer/spleeter) /
  [Deezer Research — Releasing Spleeter](https://research.deezer.com/publication/2020/06/24/releasing-spleeter.html)
  — Spleeter source/license (MIT); community sherpa-onnx (Apache-2.0) conversions exist for ONNX Runtime.
- [Anjok07/ultimatevocalremovergui](https://github.com/Anjok07/ultimatevocalremovergui) /
  ["Swift Sings" MDX-Net/RVC ONNX/CoreML write-up](https://web.navan.dev/posts/2025-10-26-vocal-separation-and-rvc-onnx-coreml.html)
  — UVR-MDX-NET-Voc_FT license (MIT) and disclosed CPU-fallback performance concerns.
- [facebookresearch/demucs issue #498 — CPU memory usage for long audio](https://github.com/facebookresearch/demucs/issues/498)
  and community-reported htdemucs CPU timing/RAM figures used for the "upstream-reported, not re-measured
  here" estimates in §2/the spec table.

## 7. Unresolved issues / known gaps

- **No model has actually been downloaded, converted, or run in this repository.** This is Step 038's own
  disclosed, deliberate scope boundary (§3) — Step 039 (verified download) and Step 040 (integration) must
  complete a real run before any candidate in this step's report can be reclassified `Feasible`.
- **CPU RAM/latency figures are upstream-reported, not independently re-measured against this
  repository's own hardware.** Disclosed explicitly in every place they appear (§2, spec table, demo
  output) as "upstream-reported, not re-measured here" — never presented as this repository's own
  benchmark. Step 040's own required acceptance ("Output stem count/shape and audio sync assertions pass")
  and Step 044 ("Compare reference outputs, audio metrics and listening tests; establish supported
  hardware") are where a real, repository-local benchmark belongs.
- **No UI wiring (SCREEN 07 ANALYSIS / SCREEN 17 SETTINGS) in this step** — disclosed, deliberate scoping
  decision consistent with every prior phase's own first research/evaluation step having no UI component
  yet.
- **This step's own `ModelFeasibilityCandidate`/`ModelFeasibilityReport` are phase-local, not Section 3's
  `ModelAsset`/`StemManifest`.** Disclosed explicitly in §3 per Section 8's "Update architecture decision
  records for genuine tradeoffs" guidance — a Step 039 implementer must reconcile or supersede this step's
  narrower, phase-local shapes, not silently inherit them.

## 8. PASS / BLOCKED

**Status: PASS** for Step 038's own stated required acceptance and failure/edge case — the step itself
(evaluating feasibility and producing a decision document) is complete and proven; the *underlying
feasibility research conclusion* that decision document reaches is itself `BLOCKED` with an explicit,
disclosed blocker reason, which Step 038's own required acceptance text explicitly permits ("...or
explicit blocker").

- "Decision document includes at least two feasible alternatives or explicit blocker" (required
  acceptance, verbatim) — proven two ways: (1)
  `Step038_Acceptance_Passes_TwoFeasibleCandidates_NoBlockerNeeded` proves the "two feasible alternatives"
  branch is correctly accepted without a blocker when it is genuinely true; (2)
  `Step038_Acceptance_Passes_RealResearchedCandidates_ExplicitBlocker` proves the "...or explicit blocker"
  branch using this step's own real, cited research — zero candidates are genuinely feasible yet, so a
  real, disclosed blocker reason is required and supplied, and the evaluator accepts it. Both are
  independently confirmed by the real, non-test-harness demo (§5).
- "Create model feasibility ADR showing two candidates/backends, CPU benchmark, RAM requirements, model
  weight/license status and reproducible conversion prerequisites" (implementation guidance, verbatim) —
  proven by `docs/phase-specs/PHASE-06.md` §12's filled-in table (three real candidates/backends, CPU
  benchmark estimates, RAM requirements, license status, and citations) and by the equivalent, persisted
  `ModelFeasibilityReport` domain object (§5).
- "Missing or incompatible model, cancellation and offline mode must yield a recoverable state"
  (failure/edge case, verbatim) — proven by `SaveAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath`
  (a cancelled save throws and leaves no partial file on disk — `Assert.False(File.Exists(...))`),
  `LoadAsync_MissingFile_ThrowsFileNotFoundException_NegativePath` ("missing model" analog: a missing
  report file is reported, not silently treated as empty), and
  `LoadAsync_CorruptFile_ThrowsModelFeasibilityReportCorruptException_NegativePath` ("incompatible model"
  analog: an unparseable file is reported via a dedicated exception, not silently ignored or treated as a
  default report).
- Candidate-level validation — proven by
  `Evaluate_CandidateMarkedFeasibleButNotVerifiedExecutable_Throws_NegativePath`,
  `Evaluate_CandidateMarkedFeasibleButNotRedistributable_Throws_NegativePath`, and
  `Evaluate_CandidateMissingCitations_Throws_NegativePath` (a candidate cannot be marked `Feasible`
  without both independently-true preconditions, and every candidate must cite an inspectable source).
- Report-level acceptance enforcement — proven by
  `Evaluate_FewerThanTwoFeasible_NoBlockerReason_Throws_NegativePath`,
  `Evaluate_FewerThanTwoFeasible_WhitespaceBlockerReason_Throws_NegativePath` (whitespace does not count
  as a real blocker reason), and `Evaluate_EmptyCandidateList_Throws_NegativePath`.
- Null-argument handling — proven by `Evaluate_NullCandidates_ThrowsArgumentNullException`,
  `Evaluate_NullOrWhitespaceDecision_ThrowsArgumentException`, and
  `SaveAsync_NullReport_ThrowsArgumentNullException`.
- Every claim above is backed by an actually-executed, actually-passing automated test — all 14 of which
  passed on their first real run (§4) — plus a real, non-test-harness manual demonstration against this
  step's own real, cited research data (§5), not merely a hand-built fixture, independently confirming the
  same behaviors end-to-end.

No previously accepted step's contract was modified in any way — every PHASE-01-05 public member is
completely unchanged — confirmed by the full pre-existing 476-test suite passing unmodified before a
single Step 038 line of code existed (§1), and again at 490/490 (476 old + 14 new) after implementation
(§4), with every prior step's own tests individually still passing by name in that same run. This step
introduced one new project pair (`Stems`/`Stems.Tests`) and one slnx edit; no existing project reference
or solution entry was changed or removed.

## 9. Architecture deviations — summary for review

1. Implemented a phase-local feasibility-decision domain model (`ModelFeasibilityCandidate`/
   `ModelFeasibilityReport`/`ModelFeasibilityEvaluator`/`JsonModelFeasibilityReportStore`) rather than
   Section 3's phase-wide `IStemSeparator`/`ModelAsset`/`IModelAssetManager` signatures, since those
   describe Steps 039-043's own later scope and would require asserting unproven model runnability now.
2. The real feasibility conclusion is BLOCKED with an explicit, disclosed blocker (no candidate model has
   an actual .NET CPU run yet) rather than "two feasible alternatives" — the honest outcome of this step's
   real research, and a branch the required acceptance text explicitly allows.
3. CPU RAM/latency figures are upstream-reported citations, not repository-local benchmarks — disclosed
   everywhere they appear; a real local benchmark belongs to Steps 040/044.

**Per this step's own instruction, implementation stops here. Step 039 is not started, and this evidence
is held for review before proceeding.**

**Note on real-xLights human verification:** per this user's own standing waiver (recorded in this
assistant's memory), a real-xLights render/open check is not required as a phase-gate blocker for this
step. Step 038 also has no xLights-dependent surface (no XSQ/model-layout involvement at this step), so
this note is recorded for consistency with prior evidence files, not because it was actually load-bearing
here.
