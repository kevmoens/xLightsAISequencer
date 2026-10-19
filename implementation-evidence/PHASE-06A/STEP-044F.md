# PHASE 06A — Step 044F Evidence

**Step:** "Revise the feasibility report, disclose the license risk, and update Phase 06's carried-forward
gate status"
**Spec:** `docs/phase-specs/PHASE-06A.md` §6, Step 044F — sixth and final numbered step of this addendum
(044A-044F), inserted between Phase 06's Step 044 and Phase 07's Step 045
**Date:** 2026-10-14

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-06A.md` in full before writing any code, in particular §3's
  `ModelFeasibilityVerdict.FeasibleWithDisclosedLicenseRisk` proposed addition (its own doc comment's
  explicit "appended last, not inserted mid-sequence" ordinal-safety rule) and §6 Step 044F's own required
  acceptance ("A new, persisted `ModelFeasibilityReport` revision exists, distinct from Step 038's original
  ... the sherpa-onnx Spleeter candidate's `VerifiedExecutableUnderDotNet` is `true`; its
  `LicenseRiskDisclosure` is non-empty and accurately describes the real, disclosed Spleeter-weights license
  ambiguity; and this step's own gate-report text explicitly distinguishes 'Phase 06's real-model demo can
  now run' from 'Step 038's own research-threshold is still formally unmet,' never conflating the two"),
  implementation guidance (the candidate-identity disclosure choice between narrowing Step 038's own record
  vs. adding a distinct new candidate; the explicit instruction not to assert `LicenseRedistributable=true`
  absent a named waiver; the requirement to extend `ModelFeasibilityCandidate` additively with
  `LicenseRiskDisclosure`), and failure/edge case ("Attempting to construct a report that marks the
  candidate `Feasible` (not `FeasibleWithDisclosedLicenseRisk`) while `LicenseRedistributable` is
  `false`/contested must still throw via the evaluator's own unmodified invariant").
- Read `implementation-evidence/PHASE-06/STEP-038.md` in full (the step whose own original persisted report
  this step must never mutate) and `tests/XlightsAI.Studio.Stems.Tests/Step038AcceptanceTests.cs` in full,
  confirming the real candidate B this addendum's own spec text refers to is `ModelId: "sherpa-onnx Spleeter
  4-stem conversion"` (not "2/4/5-stem" — that phrase appears only inside that candidate's own
  `InputOutputTensorShape` prose field, describing what upstream documentation claims, not the `ModelId`
  itself), `Verdict: Unproven`, `VerifiedExecutableUnderDotNet: false`, `EstimatedCpuRamMegabytes: 1536`,
  `EstimatedCpuSecondsPerSongMinute: 15.0` — and that **no committed JSON file anywhere in this repository
  persists Step 038's report**; every `ModelFeasibilityReport` Step 038 itself produced was written to a
  per-test `%TEMP%` directory and deleted by that test's own `IDisposable.Dispose()` (confirmed by
  `Step038AcceptanceTests`'s own `_tempDirectory`/`Dispose()` pattern and by `implementation-evidence/
  PHASE-06/STEP-038.md` §6: "Temp directory cleaned up... the throwaway demo project itself... was deleted
  after this evidence was recorded"). "Never mutate Step 038's own original persisted report" is therefore
  honored in this step by reconstructing that exact candidate data byte-for-byte and persisting it
  (unmutated) to its own file in this step's own test, before building and separately persisting the new
  revision — not by locating and avoiding editing some pre-existing committed file, since none exists.
- Read `implementation-evidence/PHASE-06A/STEP-044D.md` and `STEP-044E.md` in full: confirmed Steps
  044A-044E are all **PASS**; confirmed the real, pinned `sherpa-onnx-spleeter-2stems` model has already been
  downloaded, hash-verified (SHA-256 `e26401d9c1801f43c0229731d78d32c2e80085e3aceedeb25f29d2de5fa68ca2`),
  extracted, and run to completion (byte-for-byte deterministic across two runs — Step 044D) and that a real
  `MixReconstructionSelfConsistencyReport`/`HardwareProfile` measurement mechanism already exists and is
  proven (Step 044E) — this step is **not** BLOCKED on "no real model/measurement mechanism available"; it
  cites and reuses both directly.
- Inspected every relevant existing file in `src/XlightsAI.Studio.Stems/` before writing anything:
  `ModelFeasibilityVerdict.cs` (confirmed the real, committed enum is exactly `{ Feasible, NotFeasible,
  Unproven }`, ordinals 0/1/2, matching the spec's own stated assumption precisely), `ModelFeasibilityCandidate.cs`,
  `ModelFeasibilityReport.cs`, `ModelFeasibilityEvaluator.cs` (confirmed its own existing
  `ValidateCandidate` invariant — `Verdict == Feasible` implies `VerifiedExecutableUnderDotNet &&
  LicenseRedistributable`, else throws `ModelFeasibilityCandidateInvalidException` — requires **no code
  change** to correctly reject a misconstructed `Feasible` candidate with a contested license; the spec's own
  required failure/edge case is a new *test* proving this existing invariant still fires for this new
  scenario, not a new invariant to write), `JsonModelFeasibilityReportStore.cs` (confirmed it serializes via
  plain `JsonSerializer.Serialize`/`DeserializeAsync` with no `JsonStringEnumConverter` anywhere for this
  type — i.e. as a raw integer, confirming the spec's own "append last, never insert mid-sequence" ordinal
  concern is real and must be honored), `SherpaOnnxSpleeterModelAsset.cs`, `HardwareProfile.cs`,
  `MixReconstructionSelfConsistencyReport.cs`/`MixReconstructionSelfConsistencyEvaluator.cs`
  (Step 044E's own real measurement mechanism, reused unmodified to obtain real, fresh RAM/CPU figures for
  the new candidate rather than fabricating or copying estimated numbers).
- Confirmed via `Grep`/`Glob` that no file anywhere in the repository already declares
  `ModelFeasibilityVerdict.FeasibleWithDisclosedLicenseRisk`, a `LicenseRiskDisclosure` field, or a
  `Step044F*Tests` file.
- Baseline regression before any Step 044F file existed (`dotnet build XlightsAI.Studio.slnx --nologo` then
  `dotnet test XlightsAI.Studio.slnx --nologo`, both run fresh in this session before any new code):
  **Build succeeded, 0 Warnings, 0 Errors.** Full solution: **682/682 passing, 0 failed** (71 Xlights + 91
  Editing + 86 Melody + 25 Projects + 38 Audio + 159 Shell + 82 Rhythm + 31 Reference + 99 Stems), matching
  Step 044E's own final recorded baseline exactly.

## 2. Scope actually implemented

### Modified files (additive only)

- **`src/XlightsAI.Studio.Stems/ModelFeasibilityVerdict.cs`** — appended one new enum member,
  `FeasibleWithDisclosedLicenseRisk`, strictly *after* the existing `{ Feasible, NotFeasible, Unproven }`
  (ordinals 0/1/2 unchanged; new member is ordinal 3) — per the spec's own explicit instruction, this
  preserves the meaning of any already-persisted raw-integer-serialized `Unproven` (`2`) value. Full XML doc
  comment carried over from the spec's own proposed signature, naming both the semantic meaning (proven
  executable, contested license) and the ordinal-safety rationale for appending rather than inserting.
- **`src/XlightsAI.Studio.Stems/ModelFeasibilityCandidate.cs`** — appended one new, optional, trailing
  parameter, `string LicenseRiskDisclosure = ""`, after the existing `Citations` parameter. Purely additive:
  every existing call site in `Step038AcceptanceTests.cs` (and this repository's own
  `ModelFeasibilityEvaluator.cs`/`JsonModelFeasibilityReportStore.cs`) uses named arguments in the existing
  declared order and does not supply this new trailing parameter, so the C# compiler supplies its default
  (`""`) with no source change required at any existing call site — confirmed by the full, unmodified
  pre-existing test suite (`Step038AcceptanceTests`, `Step044*AcceptanceTests`) continuing to compile and
  pass without a single edit (§4).

**No changes to `ModelFeasibilityEvaluator.cs`, `ModelFeasibilityReportInvalidException.cs`,
`ModelFeasibilityCandidateInvalidException.cs`, `ModelFeasibilityReportCorruptException.cs`,
`IModelFeasibilityEvaluator.cs`, `IModelFeasibilityReportStore.cs`, `JsonModelFeasibilityReportStore.cs`,
`ModelFeasibilityReport.cs`, or any Step 038-044E file.** Confirmed by this session making no `Edit` calls
against any of them — only the two additive edits listed above, plus one new test file.

### New test file

- **`tests/XlightsAI.Studio.Stems.Tests/Step044FAcceptanceTests.cs`** — 2 tests (full list/results in §6).

## 3. Candidate identity disclosure (required by this step's own implementation guidance)

**Decision: option (b) — a new, distinct candidate entry, not a narrowing of Step 038's original record.**

Step 038's own original "Candidate B" (`ModelId: "sherpa-onnx Spleeter 4-stem conversion"`) is reconstructed
**byte-for-byte identical** to what `Step038AcceptanceTests.Step038_Acceptance_Passes_RealResearchedCandidates_ExplicitBlocker`
actually declared — same `ModelId`, `Version`, `Backend`, `VerifiedExecutableUnderDotNet=false`,
`InputOutputTensorShape` prose (still describing the generic, never-independently-re-inspected "2/4/5-stem"
upstream claim), `EstimatedCpuRamMegabytes=1536`, `EstimatedCpuSecondsPerSongMinute=15.0`, `Verdict=Unproven`,
and `Citations` — and persisted, unmutated, to its own report file
(`Step044F_Acceptance_Passes_RevisedReportDistinctFromStep038OriginalWithHonestLicenseRiskVerdict`'s own
`originalStoreDirectory`), independently re-loadable and asserted field-for-field against those exact
original values (§6, assertions 6).

The real, downloaded, hash-verified `sherpa-onnx-spleeter-2stems` model this addendum actually ran (Steps
044D/044E) is recorded as a **fourth, distinct** candidate
(`ModelId: "sherpa-onnx Spleeter 2-stem (sherpa-onnx-spleeter-2stems, real downloaded/verified model)"`) in
the new revision, appended alongside Step 038's three original (untouched) candidates — never substituted
for or merged into the original "4-stem conversion" entry. This choice was made (over narrowing the original
record in place) because:

1. The original entry's own `InputOutputTensorShape` text is itself a direct, disclosed quote of what
   upstream documentation claims generically ("2/4/5-stem U-Net spectrogram masking model"); the real asset
   this addendum downloaded and ran is specifically and only the 2-stem variant. Narrowing the original
   record in place would either (a) silently discard the fact that Step 038 itself never independently
   inspected the real tensor shape, or (b) require fabricating a claim that Step 038's own original inspection
   covered the 2-stem variant specifically, which it did not — it was researched generically, pre-download.
2. A new, additively-appended candidate keeps Step 038's own original record exactly as that step produced
   it — inspectable, auditable, and provably never retroactively "improved" to make a later step's real
   measurement look like it was always known — consistent with Section 2's "Must prove, don't infer" rule
   and this addendum's own repeatedly-stated "versioned revision, not destructive replacement" principle
   (§4 of the spec, citing `ChoreographyPlan` vs `SequenceDocument`).
3. Both options are explicitly permitted by the spec's own text ("Either (a)... or (b)... Either choice must
   be an explicit, disclosed decision..., not a silent substitution") — this is that explicit, disclosed
   decision for this step.

## 4. The new candidate's real, measured fields (never fabricated)

Per the spec's own implementation guidance ("replace the estimated RAM/CPU figures with Step 044E's own
real, measured `HardwareProfile` numbers"), this step's own acceptance test performs one additional real,
fresh `MixReconstructionSelfConsistencyEvaluator.EvaluateAsync` run (Step 044E's own real, unmodified
mechanism, reusing the already-downloaded/verified model cache established by Steps 044D/044E — no re-download
occurred in this session) against a real stereo fixture, and copies the resulting real
`HardwareProfile.PeakWorkingSetBytes` (converted to megabytes) and real
`HardwareProfile.MeasuredWallClockSecondsPerSongMinute` directly into the new candidate's
`EstimatedCpuRamMegabytes`/`EstimatedCpuSecondsPerSongMinute` fields — real, this-machine, this-session
numbers, never copied from any prior step's estimate or fabricated to look plausible. The new candidate's
`QualityVsReferenceNotes` field embeds the real, measured `SignalToDistortionRatioDb` from that same run,
and explicitly states this is a self-consistency proxy, never a separation-quality claim (consistent with
Step 044E's own disclosed constraint, reiterated here rather than silently dropped when the number moved
into a different document).

`VerifiedExecutableUnderDotNet` is flipped to `true` for this new candidate only — genuinely proven by Step
044D's own real, byte-for-byte-deterministic, twice-run separation (cited directly as a citation path,
`implementation-evidence/PHASE-06A/STEP-044D.md`), not inferred from "ONNX Runtime has C# bindings."

`LicenseRedistributable` is **not** asserted `true`. Per the spec's own explicit instruction, no named,
user-granted waiver of the Spleeter pretrained-weights redistribution-license risk exists in this
addendum's own record (unlike this user's own standing, separately-recorded waiver of the real-xLights
human-verification check — a different, already-granted waiver, not reused here as if it covered this
unrelated question). `LicenseRiskDisclosure` is populated with a non-empty, honest statement: Spleeter's own
README states only the *code* is MIT; Deezer has made no explicit statement extending that to the released
pretrained weights; the one public GitHub discussion asking Deezer to clarify
(`https://github.com/deezer/spleeter/issues/898`) has no maintainer response; this is a real, unresolved
question with no existing official answer to cite, not a settled policy either way. `Verdict` is therefore
`FeasibleWithDisclosedLicenseRisk`, never `Feasible` — proven both by the real acceptance test's own
assertions (§6) and by the dedicated negative-path test proving the evaluator's own existing invariant still
rejects any attempt to mark an equivalent contested-license candidate fully `Feasible` (§6).

## 5. The revised report's own gate-status language (required disclosure)

Per the spec's own required acceptance text, the revised report's `Decision` explicitly distinguishes two
separate facts, in its own words, never conflated:

- **"Phase 06's own real-model separation DEMONSTRATION can now run end-to-end"** against the real,
  downloaded, hash-verified `sherpa-onnx-spleeter-2stems` model (citing `STEP-044D.md`/`STEP-044E.md`
  directly) — this is **true** and newly proven by this addendum.
- **Step 038's own formal "at least two feasible alternatives" research-threshold acceptance criterion
  "remains HONESTLY UNMET"** — the real, verified candidate is `FeasibleWithDisclosedLicenseRisk`, which by
  design never counts toward that threshold; Candidate A (Demucs-onnx) remains `Unproven` (never
  downloaded/run — explicitly out of this addendum's own scope per PHASE-06A.md §1/§8); Candidate C
  (UVR-MDX-NET) remains rejected. `FeasibleCandidateCount` on the revised report is therefore still `0`,
  `IsBlocked` is still `true`, and a real, honest `BlockerReason` is supplied (required by
  `ModelFeasibilityEvaluator`'s own unmodified acceptance rule, since `FeasibleCandidateCount < 2`).

Both of these distinct statements are directly asserted as present in the revised report's own persisted
`Decision` text by the acceptance test (`Assert.Contains("can now run", ...)`,
`Assert.Contains("HONESTLY UNMET", ...)`, `Assert.Contains("two feasible alternatives", ...)`), not merely
claimed true in this evidence file's own prose.

## 6. Commands actually executed, with real observed output

All commands run via the PowerShell tool in this session (the sandboxed POSIX Bash tool again returned a
bare `exit code 1` with no output for plain commands — the same pre-existing environment limitation
recorded in every prior addendum step's own evidence).

```powershell
# Baseline, before any Step 044F file existed:
dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet test XlightsAI.Studio.slnx --nologo
  # 682/682 passing, 0 failed (71 Xlights, 91 Editing, 86 Melody, 25 Projects, 38 Audio, 159 Shell,
  #  82 Rhythm, 31 Reference, 99 Stems)

# Implementation, incremental build verification:
dotnet build src\XlightsAI.Studio.Stems\XlightsAI.Studio.Stems.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet build tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s). (confirms every existing Step 038-044E call site still
  #  compiles unmodified against the new, additive ModelFeasibilityCandidate/ModelFeasibilityVerdict shapes)

# Targeted Step 044F test runs:
dotnet test tests\XlightsAI.Studio.Stems.Tests\... --filter "FullyQualifiedName~Step044F&FullyQualifiedName!~RevisedReportDistinctFromStep038"
  # Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 22 ms (negative-path invariant test)

dotnet test tests\XlightsAI.Studio.Stems.Tests\... --filter "FullyQualifiedName~RevisedReportDistinctFromStep038" --logger "console;verbosity=detailed"
  # Passed XlightsAI.Studio.Stems.Tests.Step044FAcceptanceTests.Step044F_Acceptance_Passes_RevisedReportDistinctFromStep038OriginalWithHonestLicenseRiskVerdict [38 s]
  # Test Run Successful. Total tests: 1, Passed: 1 (real separation + real hardware re-measurement + real
  #  JSON persist/reload of two distinct reports, reusing the already-downloaded/verified model cache)

dotnet test tests\XlightsAI.Studio.Stems.Tests\XlightsAI.Studio.Stems.Tests.csproj --nologo
  # Passed! - Failed: 0, Passed: 101, Skipped: 0, Total: 101, Duration: 1 m 50 s
  #  (99 pre-existing Step 038-044E tests + 2 new Step 044F tests)

dotnet build XlightsAI.Studio.slnx --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test XlightsAI.Studio.slnx --nologo
  # Passed! 71  - Xlights.Tests
  # Passed! 91  - Editing.Tests
  # Passed! 86  - Melody.Tests
  # Passed! 25  - Projects.Tests
  # Passed! 38  - Audio.Tests
  # Passed! 159 - Shell.Tests
  # Passed! 82  - Rhythm.Tests
  # Passed! 31  - Reference.Tests
  # Passed! 101 - Stems.Tests
  # TOTAL: 684/684 passing, 0 failed (682 pre-existing + 2 new Step 044F tests)
```

### Targeted Step 044F test results (final run, full list)

```
Passed Step044FAcceptanceTests.Step044F_Acceptance_Passes_RevisedReportDistinctFromStep038OriginalWithHonestLicenseRiskVerdict [38 s]
Passed Step044FAcceptanceTests.Evaluate_CandidateMarkedFeasibleWithContestedLicense_StillThrowsViaExistingUnmodifiedInvariant_NegativePath [22 ms]
```

## 7. Persisted-output inspection

Real, directly observed, non-fabricated results from this step's own real end-to-end run:

- Step 038's own original report (3 candidates, `IsBlocked=true`) was persisted to its own JSON file,
  independently reloaded, and its own reconstructed "sherpa-onnx Spleeter 4-stem conversion" candidate was
  directly asserted, post-round-trip, to still carry `Verdict=Unproven`,
  `VerifiedExecutableUnderDotNet=false`, `EstimatedCpuRamMegabytes=1536`,
  `EstimatedCpuSecondsPerSongMinute=15.0`, and an empty `LicenseRiskDisclosure` — unmutated.
- The revised report (4 candidates — the same 3 plus the new, real, verified candidate) was persisted to a
  **separate** JSON file with a **different** `Id`, independently reloaded, and directly asserted to have:
  the new candidate's `VerifiedExecutableUnderDotNet=true`; a non-empty `LicenseRiskDisclosure` containing
  both "Deezer" and "pretrained" (real, substantive disclosure text, not a placeholder); the new candidate's
  `Verdict=FeasibleWithDisclosedLicenseRisk` and `LicenseRedistributable=false`; the whole report's own
  `FeasibleCandidateCount=0` (confirming `FeasibleWithDisclosedLicenseRisk` never counts toward the "two
  feasible" threshold); `IsBlocked=true` with a real, non-empty `BlockerReason`; and a `Decision` string
  containing all three required distinguishing phrases ("can now run", "HONESTLY UNMET", "two feasible
  alternatives").
- The new candidate's own `EstimatedCpuRamMegabytes`/`EstimatedCpuSecondsPerSongMinute` were real, freshly
  measured positive values taken from a real `MixReconstructionSelfConsistencyEvaluator.EvaluateAsync` run
  against the real downloaded model in this same test execution (not copied from any estimate) — directly
  asserted `> 0` / `>= 0` post-round-trip.
- The dedicated negative-path test constructed a candidate marked `Verdict=Feasible` with
  `LicenseRedistributable=false` and confirmed `ModelFeasibilityEvaluator.Evaluate` still throws
  `ModelFeasibilityCandidateInvalidException` with the existing, unmodified message text ("not both proven
  executable") — proving this step's own new enum member and field did not quietly weaken or bypass that
  pre-existing invariant.

## 8. Fixtures / input hashes

| Artifact | Value |
|---|---|
| Real sherpa-onnx-spleeter-2stems archive (reused, not re-downloaded this run) | SHA-256 `e26401d9c1801f43c0229731d78d32c2e80085e3aceedeb25f29d2de5fa68ca2`, 74,682,545 bytes — identical to Steps 044D/044E's own pinned value, unchanged |
| Real stereo WAV test fixture | Synthesized in-memory via `DeterministicSyntheticAudioGenerator.BuildHarmonicPhrase`/`EncodeStereoWav` (220/330 Hz left, 440/550 Hz right, 1.5 s each, 44,100 Hz) — identical fixture shape to Steps 044D/044E's own established pattern |
| Step 038 "original" `ModelFeasibilityReport` JSON (this step's own reconstruction) | Written to `%TEMP%\xlai-step044f-{guid}\step038-original\model-feasibility-{id}.json`, deleted on test `Dispose()` — not a stable golden file (re-created fresh, byte-identical in *content*, every test run) |
| Step 044F revised `ModelFeasibilityReport` JSON | Written to `%TEMP%\xlai-step044f-{guid}\step044f-revision\model-feasibility-{id}.json`, deleted on test `Dispose()` |

No new external upstream research was required for this step beyond re-citing Steps 044D/044E's own
already-verified findings and the Deezer/Spleeter license sources Step 038 and the spec itself already
name (`https://github.com/deezer/spleeter`, `https://github.com/deezer/spleeter/issues/898`).

## 9. Unresolved issues / known gaps

- **The Spleeter pretrained-weights redistribution-license question remains genuinely, formally open** —
  this step's own entire purpose is to disclose that honestly, not resolve it. No code or document in this
  repository asserts a settled answer.
- **No named, user-granted waiver upgrading the candidate to `Feasible` exists.** Per the spec's own explicit
  instruction, this is correctly left as a future, explicit maintainer decision point (§8 of the spec), not
  something this step resolves on its own authority. (This is distinct from, and not covered by, this user's
  own separately-recorded standing waiver of the real-xLights human-verification check — that waiver answers
  a different question and is not treated here as if it extended to this license question.)
- **Step 038's own "two feasible alternatives" formal research-threshold remains unmet.** Disclosed
  explicitly and repeatedly in this evidence file, the revised report's own `Decision` text, and §10 below —
  never silently presented as resolved.
- **No UI wiring.** Consistent with every prior Phase 06/06A step's own identical precedent — this step's
  real persisted JSON reports (§6/§7) stand in for an Avalonia/xLights visual demonstration; no
  `ModelFeasibilityReport` viewer screen exists yet in `Shell`/`App`.
- **`ModelFeasibilityCandidate.LicenseRiskDisclosure` is a free-text field**, not a structured/typed
  disclosure — sufficient for this step's own required acceptance (a non-empty, accurate prose disclosure),
  but a future phase wiring this report into a UI screen (§5 of the spec: "SCREEN 17 SETTINGS... must surface
  this addendum's own disclosed license-risk statement before a user consents") will need to decide how to
  render this prose, not merely check its non-emptiness.
- **The UVR-MDX-NET re-evaluation question and the Demucs-onnx candidate remain exactly as unresolved as
  Step 038/this addendum's own §8 already disclosed** — neither is investigated or decided by this step,
  consistent with the spec's own explicit scope boundary.

## 10. PASS / BLOCKED — this step's own gate (Step 044F only)

**Status: PASS**

- "A new, persisted `ModelFeasibilityReport` revision exists, distinct from Step 038's original (both
  independently loadable and inspectable)" (required acceptance, verbatim) — proven: `originalReport.Id !=
  revisedReport.Id`, both persisted to separate files via separate `JsonModelFeasibilityReportStore`
  instances, both independently reloaded and field-asserted.
- "the sherpa-onnx Spleeter candidate's `VerifiedExecutableUnderDotNet` is `true`" (required acceptance,
  verbatim) — proven: `reloadedRealCandidate.VerifiedExecutableUnderDotNet == true`, asserted post-JSON-round-trip.
- "its `LicenseRiskDisclosure` is non-empty and accurately describes the real, disclosed Spleeter-weights
  license ambiguity" (required acceptance, verbatim) — proven: non-empty, contains "Deezer" and "pretrained",
  matching the real disclosed facts recorded in §4/§5 above and cited directly to
  `https://github.com/deezer/spleeter/issues/898`.
- "this step's own gate-report text explicitly distinguishes 'Phase 06's real-model demo can now run' from
  'Step 038's own research-threshold is still formally unmet,' never conflating the two" (required
  acceptance, verbatim) — proven: the revised report's own `Decision` text contains all three required
  distinguishing phrases, directly asserted post-round-trip.
- "Attempting to construct a report that marks the candidate `Feasible` (not `FeasibleWithDisclosedLicenseRisk`)
  while `LicenseRedistributable` is `false`/contested must still throw via the evaluator's own unmodified
  invariant" (failure/edge case, verbatim) — proven by
  `Evaluate_CandidateMarkedFeasibleWithContestedLicense_StillThrowsViaExistingUnmodifiedInvariant_NegativePath`:
  `ModelFeasibilityCandidateInvalidException` is thrown by the real, completely unmodified
  `ModelFeasibilityEvaluator.ValidateCandidate` code path — no new code in this step intercepts or
  special-cases this scenario.
- Full targeted suite: **2/2 passing**, 0 failed. Full `Stems.Tests` assembly: **101/101 passing** (99
  pre-existing Step 038-044E tests + 2 new Step 044F tests), 0 failed — every prior Phase 06/06A Stems test
  still passes unmodified, confirming the two additive source changes (§2) broke nothing. Full solution
  regression: **684/684 passing, 0 failed** (682 pre-existing + 2 new), including every pre-existing Phase
  06/07 test.
- No carried-forward blocker specific to this step's own scope. This step's own candidate-identity
  (option (b)) and ordinal-safe-enum-append decisions are named architecture decisions (§3), not deviations
  requiring a waiver.

**Note on real-xLights human verification:** per this user's own standing waiver (recorded in this
assistant's memory), a real-xLights render/open check is not required as a phase-gate blocker for this step.
Step 044F has no xLights-dependent surface (no XSQ/model-layout involvement — it is a feasibility-decision
document revision), so this note is recorded for consistency with prior evidence files, not because it was
actually load-bearing here.

## 11. Architecture deviations — summary for review

1. `ModelFeasibilityVerdict` gained one new member, `FeasibleWithDisclosedLicenseRisk`, appended strictly
   after `Unproven` (ordinal 3) to preserve the meaning of any already-serialized raw-integer `Unproven`
   value (`JsonModelFeasibilityReportStore` has no `JsonStringEnumConverter` for this type).
2. `ModelFeasibilityCandidate` gained one new, optional, trailing field, `LicenseRiskDisclosure` (default
   `""`) — purely additive; every existing call site compiles and passes unmodified.
3. The real, verified `sherpa-onnx-spleeter-2stems` model is recorded as a **new, distinct** fourth
   candidate in the revised report, not a narrowing of Step 038's own original "sherpa-onnx Spleeter 4-stem
   conversion" placeholder record — an explicit, disclosed choice between the spec's own two permitted
   options (§3 above).
4. `ModelFeasibilityEvaluator.cs` itself was **not** modified — the required failure/edge-case assertion is
   satisfied entirely by that file's own pre-existing, unmodified invariant.
5. No real new model download, extraction, or inference was required to satisfy this step's own literal
   required acceptance (the report revision itself); one additional real
   `MixReconstructionSelfConsistencyEvaluator.EvaluateAsync` run was nonetheless performed (reusing the
   already-verified cache) so the new candidate's own RAM/CPU fields are genuinely, freshly measured rather
   than copied from Step 044E's own prior run or estimated.

---

## 12. Required final gate report for the whole PHASE-06A addendum (Section 8 checklist)

Per PHASE-06A.md §8 ("Required final gate report") and the task instruction that this is the addendum's
last step, the following checklist is completed against the **whole addendum (Steps 044A-044F)**, not only
this step:

- [x] **All named numbered steps (044A-044F) show independently recorded evidence.**
      `implementation-evidence/PHASE-06A/STEP-044A.md` through `STEP-044F.md` (this file) all exist, each
      independently reviewed in this session's §1 inspection, each recording its own PASS status.
- [x] **Compiles; full unit/integration/golden regression run records actual commands, including every
      pre-existing Phase 06/07 test passing unmodified.** `dotnet build XlightsAI.Studio.slnx --nologo` →
      0 Warnings, 0 Errors (final run, §6 above). `dotnet test XlightsAI.Studio.slnx --nologo` → **684/684
      passing, 0 failed** (final run, §6 above): 71 Xlights + 91 Editing + 86 Melody + 25 Projects + 38
      Audio + 159 Shell + 82 Rhythm + 31 Reference + 101 Stems. Every one of Phase 06's own 93
      pre-Step-044D Stems tests and every Phase 07 Melody test (86) is included in this same run and passes
      unmodified.
- [x] **Desktop interaction was exercised or explicitly reported blocked.** No Avalonia `Shell`/`App` UI
      exists yet for any stem-separation surface (waveform-domain or spectrogram-domain) — explicitly
      reported, not silently omitted, in every one of Steps 044A-044F's own evidence files' "Unresolved
      issues" sections. Real, persisted, independently-hashed/round-tripped output artifacts (stem WAV
      files, `MixReconstructionSelfConsistencyReport`/`ModelFeasibilityReport` JSON) stand in for a visual
      desktop demonstration, per this addendum's own repeated, disclosed precedent.
- [x] **Required xLights/manual/inference test ran against a recorded version, if applicable.** This
      addendum has no xLights-XML/layout surface (per this user's own standing waiver, reiterated in §10
      above and in every prior step's evidence). The real, applicable inference test — real ONNX Runtime CPU
      inference against the real, downloaded `sherpa-onnx-spleeter-2stems` model — ran and is recorded in
      Step 044D's evidence (real run, twice, byte-for-byte deterministic) and reused by Steps 044E/044F.
- [x] **Missing assumptions, compatibility limits, technical debt and migration impacts recorded** — in
      particular, the two unresolved pretrained-weight license questions and the UVR-MDX-NET re-evaluation
      deferral. Both are disclosed explicitly in this file's §9 and in `docs/phase-specs/PHASE-06A.md` §8's
      own "Explicit ADR questions to resolve during this phase" (unmodified by this step — that section
      remains the addendum's own open-question record, not resolved here).
- [x] **Previous accepted sequences/project files are unchanged except approved migrations/edits.** No
      `.xsq`/`xlights_rgbeffects.xml`/`xlights_networks.xml` file exists or was touched anywhere in this
      addendum. The only source edits across the whole addendum are additive (new files, or additive
      fields/enum members disclosed in each step's own evidence) — confirmed cumulatively by the full
      684/684 regression above, which includes every pre-addendum Phase 01-07 test passing unmodified.
- [x] **Step 038's original `ModelFeasibilityReport` is unmodified; a new revision exists alongside it.**
      Proven directly in this step's own acceptance test (§6/§7 above): Step 038's own original candidate
      data is reconstructed byte-for-byte, persisted to its own file, independently reloaded, and asserted
      field-for-field unchanged; the revised report is a separate, independently-persisted, independently-loadable
      document with a different `Id`.
- [x] **Gate result = PASS or BLOCKED for this addendum's own steps, and a separate, explicit statement of
      Phase 06's carried-forward gate status.** See §13 immediately below.

### 13. Final addendum gate result

**This addendum's own gate (Steps 044A-044F): PASS.**

Every numbered step (044A stereo decode, 044B STFT/ISTFT, 044C masking/reconstruction math, 044D real
model declaration/extraction/end-to-end run, 044E real quality/hardware re-measurement proxy, 044F this
feasibility report revision) independently proved its own required acceptance and failure/edge case via a
real, executed, passing automated test — not a mock or a hand-waved claim — and is independently recorded
PASS in its own evidence file. The addendum's own blocking phase gate text (PHASE-06A.md §1: "decode a real
multi-second stereo audio sample, separate it into vocals and accompaniment stem WAV files via the real
dual-ONNX-session spectrogram pipeline, run the separation twice and confirm byte-for-byte determinism
across both runs, review the real (proxy, disclosed-as-such) quality/hardware re-measurement, and review the
revised `ModelFeasibilityReport`'s disclosed verdict and license-risk statement") is satisfied in full:
Step 044D proves the real separation + determinism; Step 044E proves the real proxy quality/hardware
re-measurement; Step 044F (this step) proves the real, revised, reviewable feasibility report with its
disclosed verdict and license-risk statement.

**Phase 06's own carried-forward gate status: still formally `BLOCKED`, resolved pending an explicit
license-risk waiver — not silently resolved, not conflated with this addendum's own PASS above.**

Specifically, and separately:

- **What is now true and newly proven (this addendum's own contribution):** Phase 06's own user story — "I
  can trust that the drum/bass/vocal separation mechanism Phase 06 built actually produces real separated
  audio from a real, license-disclosed model" — **can now be demonstrated**: a real, downloaded,
  hash-verified model genuinely separates a real stereo audio sample into real vocals/accompaniment stems,
  deterministically, with a real (disclosed-as-proxy) quality/hardware measurement and a real, reviewable
  feasibility verdict. This was **not** true before this addendum (Phase 06's Step 044 proved the mechanism
  only against a 307-byte synthetic fixture).
- **What remains formally unmet:** Step 038's own required acceptance text, verbatim, is "Decision document
  includes at least two feasible alternatives or explicit blocker." The revised report's own
  `FeasibleCandidateCount` is **0** (the real, verified sherpa-onnx Spleeter candidate is deliberately
  `FeasibleWithDisclosedLicenseRisk`, which by design never counts toward this threshold; Demucs-onnx remains
  `Unproven`; UVR-MDX-NET remains rejected) — so the revised report is still, correctly, `IsBlocked=true`
  with an honest `BlockerReason`. Phase 06's own phase-gate status therefore remains `BLOCKED` under Step
  038's own original, unmodified acceptance rule.
- **The one, explicit, named path to resolving it:** per PHASE-06A.md §8's own "License-risk acceptance" ADR
  question, a human maintainer may independently review the real, disclosed
  `LicenseRiskDisclosure`/`Decision` text this step produced and explicitly grant a named waiver upgrading
  the real candidate's verdict from `FeasibleWithDisclosedLicenseRisk` to `Feasible` — at which point a
  future, later report revision (not this one, and not authored by this step on its own authority) would
  correctly report `FeasibleCandidateCount=1` for this candidate alone, still short of the required `2`
  unless Demucs-onnx or another candidate is also independently proven. No such waiver exists today, and
  none is fabricated here.

**No further numbered step remains in this addendum.** Per PHASE-06A.md §1 ("Unblocks: Phase 06's own
carried-forward `BLOCKED` gate status — not Phase 07, which does not depend on this"), Phase 07's own
already-implemented Steps 045-051 are unaffected and remain exactly as built; no Phase 07 file was read for
editing purposes in this step, and no Phase 07 step number is renumbered or modified.
