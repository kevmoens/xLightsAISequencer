# PHASE 08 — Step 055 Evidence

**Step:** "Segment sections into phrase units with manual corrections"
**Spec:** `docs/phase-specs/PHASE-08.md` §6, Step 055 (lines 244-258) — the fourth numbered step of
Phase 08 (052-058)
**Date:** 2026-10-14

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-08.md` in full, confirming Step 055's own text: required acceptance
  ("Phrase boundaries align to bars or user-chosen events"), implementation guidance ("Segment into
  phrases with bar-based hypothesis and melodic cadences; permit irregular length and phrase overlaps
  only by deliberate policy") and failure/edge case ("Invalid input, cancellation or stale persisted
  state must not corrupt the project").
- Confirmed Step 055's own scope is phrase *segmentation* only — Section 3 explicitly instructs:
  "Distinguish segment boundary from section label and motif identity." Section labels
  (`SectionLabelCandidate`) were already built and approved in Step 054 (which itself consumed Step
  053's segment boundaries and Step 052's bar fingerprints); motif clustering is Step 056's own later
  scope; choreography template inheritance is Step 057's; the propagation inspector UI is Step 058's.
  None of `MotifLink`/`SongStructure`/`ISongStructureAnalyzer`/`IMotifMatcher`/
  `IMotifInheritancePlanner` (Section 3's proposed signatures) were built here — this step's own
  concrete, narrower contract is `PhraseCandidate`/`PhraseSegmentationAnalysis`, the same "Section 3
  proposes, the real implementation phase-locally narrows" precedent Steps 052-054's own evidence
  already disclosed and followed.
- Read every file in `src/XlightsAI.Studio.Structure/` in full (all 35 Step 052/053/054 files) before
  writing any new type: `SectionLabelCandidate`/`SectionLabelAnalysis`/`SectionLabelContext` (this
  step's own direct upstream input — the already-approved section labels this step segments into
  phrases, never re-classified); `SongFingerprintSet`/`BarFingerprint` (the per-bar evidence this
  step's own `PhraseCandidate.SignatureHash` is computed from); `ISectionLabelClassifier`/
  `SectionLabelClassifier` and `ISectionBoundaryDetector`/`SectionBoundaryDetector` (the direct
  structural template this step's own `IPhraseSegmenter`/`PhraseSegmenter`/
  `IPhraseSegmentationStore`/`JsonPhraseSegmentationStore` mirror: validate-before-compute,
  cache-path-before-build, atomic temp-file-then-`File.Move` persistence, "still-valid cache" reopen
  pattern, overrides-applied-at-read-time-from-a-separate-store); `SectionLabelInvalidException`/
  `SectionLabelCorruptException` (the `{Type}InvalidException(SongId, reason)` /
  `{Type}CorruptException(path, inner)` naming convention this step's own exceptions mirror exactly).
  Also read `src/XlightsAI.Studio.Melody/NoteEvent.cs` and `MelodyTrack.cs` in full (PHASE-07's own
  proposed signatures, already implemented) — the melody evidence source this step's own melodic-cadence
  refinement consumes. PHASE-08 Section 3's own proposed `MusicalPhrase(Guid Id, Guid SectionId, long
  StartTicks, long EndTicks, int? Bars, string SignatureHash)` signature was read and used as the
  starting point for this step's own narrower `PhraseCandidate` (see §3 for the disclosed field-level
  deviations).
- Searched the full repository for any prior "Step 055"/"STEP-055" reference before starting — found
  only `PHASE-08.md` itself, and no existing `Phrase*`/`IPhraseSegmenter`/`MusicalPhrase` type anywhere,
  confirming this is genuinely new Step 055 work, not a duplicate.
- Baseline taken by running the real, full solution test suite *before* any Step 055 code was written:
  **748/748 passing, 0 failed, 0 skipped** across the 10 pre-existing test assemblies (matches Step
  054's own recorded final count exactly — no drift).

## 2. Scope actually implemented

No new project was required — this step extends the existing `src/XlightsAI.Studio.Structure/` project
(already references `XlightsAI.Studio.Contracts`/`XlightsAI.Studio.Rhythm`/`XlightsAI.Studio.Melody`,
sufficient for this step's own needs; no `.csproj` changes were made).

### New files in `src/XlightsAI.Studio.Structure/`

- **`PhraseBoundarySource.cs`** — `BarHypothesis`, `MelodicCadence`, `UserChosen`: Step 055's own
  declared evidence source for one phrase's own start boundary, directly naming the two automatic
  sources the implementation guidance names verbatim plus this step's own third, manual-correction
  source ("...with manual corrections," the step's own title, verbatim).
- **`PhraseCandidate.cs`** — one segmented phrase: `Id`, `SectionLabelId` (the owning
  `SectionLabelCandidate.Id` — Section 3's own `SectionId`, narrowed to this step's own concrete
  upstream type), `StartBarIndex`/`EndBarIndexExclusive`/`StartTicks`/`EndTicks`, `Bars` (always
  present — a declared, disclosed narrowing of Section 3's own proposed `int? Bars`, since every
  phrase this recipe produces is bar-aligned by construction), `Source`, `Confidence`, `Evidence`
  (non-empty, human-readable reasons), `SignatureHash` (a stable SHA-256 digest of the phrase's own
  averaged `BarFingerprint.Features`, populated for Step 056's own later motif-clustering work without
  this step itself comparing any two hashes), `UserCorrected`.
- **`PhraseSegmentationSettings.cs`** — `TargetPhraseLengthBars` (the bar-hypothesis grid spacing;
  must be positive), `CadenceSearchRadiusBars` (how far a melodic-cadence snap may move a hypothesis
  boundary), `MinCadenceGapTicks` (the minimum melody rest length counted as a cadence), `RecipeId`.
- **`PhraseSegmentationAnalysis.cs`** — the full read model a caller receives: `VersionId`, `SongId`,
  `SourceHash`, `SourceSectionLabelVersionId`, `SourceFingerprintSetVersionId`, `SourceMelodyTrackId`
  (nullable — melody is an optional evidence source), `AnalyzerVersion`, `Settings`, `Phrases`.
- **`PhraseSegmentationContext.cs`** — `SongId`, `SourceHash`, `LabelAnalysis` (Step 054's own approved
  output), `FingerprintSet` (Step 052's own approved output), `MelodyTrack` (nullable — PHASE-07's own
  approved output when present).
- **`IPhraseSegmenter.cs`** / **`PhraseSegmenter.cs`** — the real implementation. Validates every
  input explicitly (missing media, mismatched/stale provenance across all three upstream chains,
  unusable settings) before any computation; for each already-approved section, proposes a plain
  bar-count hypothesis split every `TargetPhraseLengthBars` bars (a section shorter than or equal to
  that target becomes a single, explicitly irregular-length phrase — "permit irregular length... only
  by deliberate policy," verbatim, this recipe's own declared instance of that policy); when a
  `MelodyTrack` is supplied, each internal hypothesis boundary is snapped to the nearest melodic
  cadence (a melody rest of at least `MinCadenceGapTicks`) found within `CadenceSearchRadiusBars`,
  provided the snap would not collide with or cross an already-accepted neighboring split (this
  recipe's own declared "never overlap, ever" policy — Step 055's own "permit... phrase overlaps only
  by deliberate policy," verbatim, satisfied by deliberately disallowing overlap entirely); **validates
  every upstream section label list** (`EndBarIndexExclusive > StartBarIndex`, every referenced bar
  index actually present in the source fingerprint set) *before* any computation, and
  **structurally validates its own computed phrase list** (no overlaps) *before* any persistence — a
  malformed upstream section or a self-contradictory computed phrase list throws
  `PhraseSegmentationInvalidException` and is never written to disk (Step 055's own "invalid input...
  must not corrupt the project" failure/edge case, verbatim); propagates `CancellationToken` at both
  validation entry and before any file write, and reopens a still-valid cached automatic analysis
  rather than recomputing (same "with caching" convention Steps 052-054 already established, including
  treating a corrupt cache file as a cache miss rather than a hard failure — "stale persisted state
  must not corrupt the project," verbatim, satisfied by never trusting an invalidated or corrupt cache).
  `SetManualBoundariesAsync` additionally validates that the targeted `SectionLabelId` actually exists
  in the current label analysis, and that every supplied boundary bar index exists in the source
  fingerprint set and falls strictly inside that section's own bar range, before persisting anything —
  this step's own second and third concrete instances of "invalid input... must not corrupt the
  project," rejected before the override store is ever touched.
- **`IPhraseSegmentationStore.cs`** / **`JsonPhraseSegmentationStore.cs`** — JSON-file-backed
  persistence for the *automatic* (pre-override) segmentation, same atomic
  temp-file-then-`File.Move` pattern as `JsonSectionLabelStore`.
- **`IPhraseBoundaryOverrideStore.cs`** / **`JsonPhraseBoundaryOverrideStore.cs`** — a **separate**
  JSON-file-backed store (Section 4: "Persist detected structure and user-edited structure in
  separately versioned stores," verbatim) mapping `SectionLabelId → sorted internal split bar indices`
  for every human correction (an empty list is a valid, explicit "no manual split points for this
  section" correction, distinct from no entry at all). `SegmentAsync` always re-reads this store and
  re-merges it onto the (possibly freshly rebuilt) automatic result, so a manual correction is never
  silently lost by an upstream re-analysis, a settings change, or an application restart.
- **`PhraseSegmentationInvalidException.cs`** / **`PhraseSegmentationCorruptException.cs`** — mirror
  `SectionLabelInvalidException`/`SectionLabelCorruptException` exactly.

### New test file

- `tests/XlightsAI.Studio.Structure.Tests/Step055AcceptanceTests.cs` — 12 tests against the real
  `PhraseSegmenter`/`JsonPhraseSegmentationStore`/`JsonPhraseBoundaryOverrideStore` (not a mock),
  including:
  - `Step055_Acceptance_Passes_PhraseBoundariesAlignToBarsOrUserChosenEvents` (the required acceptance
    test, verbatim name): a two-section (Verse, Chorus) fixture where the Verse (4 bars, at the
    `TargetPhraseLengthBars` floor) becomes a single declared irregular-length phrase and the Chorus (8
    bars) produces a bar-hypothesis split at bar 8 that a real melodic rest at bar 7 snaps to — asserts
    every phrase's bar range, `Source`, non-empty evidence, in-range confidence, non-empty
    `SignatureHash`, full provenance (`SourceHash`/`SourceSectionLabelVersionId`/
    `SourceFingerprintSetVersionId`/`SourceMelodyTrackId`/`AnalyzerVersion`/`Settings`), a real cache
    file on disk, then sets a real manual boundary correction via `SetManualBoundariesAsync` (moving the
    Chorus's own internal split from bar 7 to a user-chosen bar 9), confirms the override file exists on
    disk, and **constructs a brand-new `PhraseSegmenter`/store pair over the same on-disk directories**
    (the test's own stand-in for an application restart) to confirm the correction is still applied
    while the never-overridden Verse section is unaffected.
  - `SegmentAsync_NoMelodyTrack_FallsBackToBarHypothesis` — "bar-based hypothesis and melodic cadences":
    with no `MelodyTrack` supplied, every internal boundary falls back to the unsnapped bar-hypothesis
    grid at the recipe's own declared lower confidence, never throwing for the missing optional input.
  - `SegmentAsync_EmptyLabels_ReturnsEmptyPhrases_HandlesSilenceExplicitly` — zero section labels
    (silence, carried over from Steps 052-054's own disclosed convention) returns an empty,
    non-throwing result.
  - `SegmentAsync_MalformedSectionRange_ThrowsPhraseSegmentationInvalidException_NeverCommitted` — the
    required "invalid input... must not corrupt the project" failure case (upstream section range):
    asserts the segmenter throws and that **no cache file is ever written**.
  - `SetManualBoundariesAsync_UnknownSectionLabelId_ThrowsPhraseSegmentationInvalidException_NeverCommitted`
    and
    `SetManualBoundariesAsync_BoundaryOutsideSectionRange_ThrowsPhraseSegmentationInvalidException_NeverCommitted`
    — the same failure case's second and third concrete instances (correction target, correction
    range): both assert the correction is rejected and that **no override file is ever written**.
  - `SegmentAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverCommitted` — the
    required "cancellation... must not corrupt the project" failure case: an already-cancelled token
    throws before any computation or persistence, with **no cache file ever written**.
  - Five further negative-path tests: empty `SourceHash` (missing media), mismatched provenance
    (`SourceFingerprintSetVersionId` between the label analysis and fingerprint set), invalid
    `TargetPhraseLengthBars` (`0` and `-1`, via `[Theory]`), and null arguments (`context`, `settings`,
    `boundaryBarIndices`).

**No existing file from Steps 052-054 or any prior phase/step was modified.** This is a purely additive
extension of the same `XlightsAI.Studio.Structure` vertical slice.

## 3. Architecture deviations / decisions

- **`PhraseCandidate.Bars` is a non-nullable `int`, narrower than Section 3's own proposed `int? Bars`
  on `MusicalPhrase`.** Every phrase this step's own single recipe produces is bar-aligned by
  construction — there is no code path that produces a phrase whose bar count is unknown — so the
  nullability in the proposed signature is simply unneeded for this step's own narrower, concrete
  recipe. A disclosed, deliberate narrowing, not a silent contract change (the same "Section 3 proposes,
  the implementation phase-locally narrows" precedent Steps 052-054 already established, e.g. Step
  054's own `SectionKind` adoption verbatim alongside its own narrower supporting types).
- **No `MotifLink`/`SongStructure`/`ISongStructureAnalyzer`/`IMotifMatcher`/`IMotifInheritancePlanner`
  was built.** All five belong to Steps 056-058's own later scope (motif clustering, inheritance
  planning, propagation UI) — fabricating any of them now would repeat the same violation Steps
  052-054's own evidence already disclosed avoiding ("Avoid collecting multiple unverified tasks into
  one mega-commit," Section 6, verbatim). `PhraseCandidate.SignatureHash` is populated (it is cheap and
  directly useful to Step 056) but this step itself never compares two hashes against each other —
  that comparison is explicitly Step 056's own later scope.
- **"Permit... phrase overlaps only by deliberate policy" is satisfied by declaring that policy as
  "never overlap, ever" (an explicit, disclosed architecture decision), not by implementing a
  configurable overlap mode.** A melodic-cadence snap that would collide with or cross an
  already-accepted neighboring split is rejected for that one boundary (falls back to the unsnapped
  bar-hypothesis bar) rather than ever producing an overlapping or zero-length phrase. This is this
  step's own single, disclosed policy choice; a future recipe could declare a different policy under a
  new, distinguishable `PhraseSegmentationSettings.RecipeId` without redefining this one's meaning.
- **Melodic-cadence detection is a single declared heuristic — a rest of at least `MinCadenceGapTicks`
  between two consecutive melody notes (or after the final note), snapped to the nearest in-range
  hypothesis boundary — not a claim that it matches any specific published cadence-detection
  algorithm.** This is a disclosed, inspectable policy choice consistent with Section 3's own
  "Mapping... is a declared policy" convention (applied elsewhere in Steps 054/057 to label/gesture
  confidence, applied here to boundary snapping).
- **Manual corrections are scoped per-section, not per-individual-boundary.** `SetManualBoundariesAsync`
  replaces a whole section's own set of internal split points in one call (an empty list clears all
  manual splits for that section, reverting to automatic), rather than offering a move/add/remove API
  for one boundary at a time. This mirrors the step's own required acceptance text literally ("Phrase
  boundaries align to bars **or user-chosen events**" — a user-chosen event is itself just a bar index)
  while keeping the correction's own blast radius explicit and bounded to the one section a human is
  actually looking at; a future step (058's own propagation inspector UI) can build a finer-grained
  single-boundary affordance on top of this same store without changing its own on-disk contract.
- **Every phrase belonging to a section with at least one manual correction is marked
  `UserCorrected = true`, including the phrase that still starts at the section's own already-approved
  boundary.** The correction target is the whole section's own internal layout, not one isolated split
  point, so every resulting phrase in that section is disclosed as human-influenced rather than only the
  phrases whose own start bar actually changed.
- **No `MusicalPhrase`-to-`SequenceDocument`/`IEditTransactionService`/UI wiring for this step**,
  consistent with Steps 052-054's own precedent — `PhraseSegmenter` is a pure domain/adapter boundary;
  no `ShellViewModel`-level command or SCREEN-11/SCREEN-13 wiring exists yet (Step 058's own later
  scope: "Build phrase relationship/propagation inspector").
- **`StemActivity` remains unconsumed by this step's own recipe**, consistent with Steps 052-054's own
  disclosed convention — Section 3's proposed `SongAnalysisContext` names stem activity for a later
  step's broader pipeline; this step's own narrower `PhraseSegmentationContext` only consumes the
  already-approved `SectionLabelAnalysis`, `SongFingerprintSet` and optional `MelodyTrack`.
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Steps 052-054's own
  evidence** (not reopened or re-resolved here, not silently treated as closed): Phase 06's own
  stem-separation gate remains BLOCKED; Phase 07's own full-phase gate (Section 7 demonstration,
  Section 8 final report) has not been separately run/recorded.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code
1 with no output for plain commands, the same pre-existing environment limitation recorded in every
prior step's own evidence; PowerShell was used for every `dotnet`/file-system command below without
incident.

```
# Baseline, before any Step 055 code:
dotnet test XlightsAI.Studio.slnx --nologo
  # 748/748 passing, 0 failed, 0 skipped (10 pre-existing assemblies) -- matches Step 054's own recorded count.

# After the new Step 055 source files in the existing XlightsAI.Studio.Structure project:
dotnet build src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After Step055AcceptanceTests.cs:
dotnet build tests\XlightsAI.Studio.Structure.Tests\XlightsAI.Studio.Structure.Tests.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet test tests\XlightsAI.Studio.Structure.Tests\XlightsAI.Studio.Structure.Tests.csproj --nologo `
  --logger "console;verbosity=detailed"
  # 42/42 passed (30 existing Step 052/053/054 tests unmodified + 12 new Step 055 tests), first run, no fixture fixes needed

dotnet build XlightsAI.Studio.slnx --nologo       # full solution -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo       # full regression, final

dotnet new console -o <scratchpad>\xlai-step055-demo-proj --no-restore
dotnet add <scratchpad>\xlai-step055-demo-proj reference src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj
dotnet add <scratchpad>\xlai-step055-demo-proj reference src\XlightsAI.Studio.Contracts\XlightsAI.Studio.Contracts.csproj
dotnet add <scratchpad>\xlai-step055-demo-proj reference src\XlightsAI.Studio.Rhythm\XlightsAI.Studio.Rhythm.csproj
dotnet add <scratchpad>\xlai-step055-demo-proj reference src\XlightsAI.Studio.Melody\XlightsAI.Studio.Melody.csproj
dotnet run --project <scratchpad>\xlai-step055-demo-proj --nologo   # real, non-test-harness manual demo (see §5)
```

### Targeted test results (Step 052 + Step 053 + Step 054 + Step 055 tests, real run, first attempt, no fixture fixes needed)

```
Test Run Successful.
Total tests: 42
     Passed: 42
 Total time: 1.5336 Seconds
```

Includes all 12 new tests:
`Step055_Acceptance_Passes_PhraseBoundariesAlignToBarsOrUserChosenEvents`,
`SegmentAsync_NoMelodyTrack_FallsBackToBarHypothesis`,
`SegmentAsync_EmptyLabels_ReturnsEmptyPhrases_HandlesSilenceExplicitly`,
`SegmentAsync_MalformedSectionRange_ThrowsPhraseSegmentationInvalidException_NeverCommitted`,
`SetManualBoundariesAsync_UnknownSectionLabelId_ThrowsPhraseSegmentationInvalidException_NeverCommitted`,
`SetManualBoundariesAsync_BoundaryOutsideSectionRange_ThrowsPhraseSegmentationInvalidException_NeverCommitted`,
`SegmentAsync_EmptySourceHash_ThrowsPhraseSegmentationInvalidException_MissingMediaNegativePath`,
`SegmentAsync_MismatchedProvenanceVersionIds_ThrowsPhraseSegmentationInvalidException`,
`SegmentAsync_InvalidTargetPhraseLengthBars_ThrowsPhraseSegmentationInvalidException` (×2, `[Theory]`),
`SegmentAsync_NullArguments_ThrowArgumentNullException`,
`SegmentAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverCommitted`.

### Full regression (final, after all Step 055 implementation and tests)

```
Passed!  - Failed: 0, Passed:  74, Skipped: 0, Total:  74  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  42, Skipped: 0, Total:  42  - XlightsAI.Studio.Structure.Tests.dll  (30 existing + 12 new)
Passed!  - Failed: 0, Passed: 117, Skipped: 0, Total: 117  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  38, Skipped: 0, Total:  38  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 159, Skipped: 0, Total: 159  - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
Passed!  - Failed: 0, Passed: 101, Skipped: 0, Total: 101  - XlightsAI.Studio.Stems.Tests.dll
```

**760 total (across all 10 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from
Steps 001-054 still passes unmodified (748 baseline, unchanged); the `XlightsAI.Studio.Structure.Tests`
assembly adds exactly this step's own 12 new tests (30 existing Step 052/053/054 tests + 12 new Step 055
tests = 42).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session
scratchpad, deleted after this run) exercises the real `PhraseSegmenter`/`JsonPhraseSegmentationStore`/
`JsonPhraseBoundaryOverrideStore` directly against an in-memory `SectionLabelAnalysis`/
`SongFingerprintSet`/`MelodyTrack` fixture -- no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step055-demo-proj --nologo
```

Observed real output:

```
=== Step 055 manual demo: phrase segmentation, provenance, melodic-cadence snapping, manual corrections survive restart ===

--- Scenario 1: real segmentation (bar hypothesis + melodic cadence snap), persisted to disk ---
  Cache file: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step055-demo-cache-.../phrase-segmentation\phrase-segmentation-....json
  Exists on disk: True
  Bars [0,4): BarHypothesis confidence=1.00 userCorrected=False evidence="Section is shorter than or equal to TargetPhraseLengthBars; entire section is a single irregular-length phrase (declared policy)."
  Bars [4,7): BarHypothesis confidence=1.00 userCorrected=False evidence="Phrase starts at the already-approved section boundary."
  Bars [7,12): MelodicCadence confidence=0.85 userCorrected=False evidence="Phrase boundary snapped to a melodic cadence at bar 7, 1 bar(s) from the bar-based hypothesis (bar 8)."

--- Scenario 2: set a manual phrase boundary correction, then reopen with brand-new store instances (simulated restart) ---
  Override file: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step055-demo-cache-.../phrase-boundary-overrides\phrase-boundary-overrides-....json
  Exists on disk: True
  Chorus phrase [4,9): BarHypothesis userCorrected=True
  Chorus phrase [9,12): UserChosen userCorrected=True
  After simulated restart: chorus phrase count=2, second phrase=[9,12) userCorrected=True
  Verse section unaffected: [0,4) userCorrected=False

--- Scenario 3: no melody track supplied -- falls back to plain bar-based hypothesis, never throws ---
  Chorus phrase [4,8): BarHypothesis confidence=1.00
  Chorus phrase [8,12): BarHypothesis confidence=0.50

--- Scenario 4: a malformed (end <= start) section range must be rejected, never committed to disk ---
  Correctly threw PhraseSegmentationInvalidException: Section at position 0 has EndBarIndexExclusive (2) <= StartBarIndex (2) -- malformed proposal.
  Cache file written despite rejection: False (expected False)

--- Scenario 5: a manual boundary outside the targeted section's own range must be rejected, never persisted ---
  Correctly threw PhraseSegmentationInvalidException: Manual phrase boundary bar 2 falls outside section [4, 12) -- malformed correction, not persisted.
  Override file written despite rejection: False (expected False)

Step 055 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation: (1) a real `.json` phrase-segmentation cache file is
written to disk and its existence verified, with every phrase carrying an inspectable `Source`,
confidence and evidence string, including the declared irregular-length-phrase policy for a
short Verse section and a real melodic-cadence snap (bar 8 hypothesis → bar 7 cadence) for the Chorus
section ("Phrase boundaries align to bars," required user-visible outcome, verbatim); (2) a real manual
boundary correction is persisted to a **separate** override file, and — critically — a **brand-new
segmenter and store pair constructed over the same on-disk directories** (the real stand-in for an
application restart, since no in-process state is reused) still surfaces the correction correctly while
leaving the other, never-corrected section unaffected ("...or user-chosen events," verbatim); (3) with
no melody track supplied, segmentation falls back cleanly to the unsnapped bar hypothesis rather than
throwing for a genuinely optional input; (4) a malformed (`EndBarIndexExclusive <= StartBarIndex`)
upstream section is rejected with a real, descriptive exception and **no cache file is ever written**
("invalid input... must not corrupt the project," verbatim); (5) a manual boundary correction targeting
a bar outside its own section's range is rejected and **the override file is confirmed never written**
(the same failure/edge case's second concrete instance).

## 6. Fixtures / input hashes

No binary fixture *file* is committed to the repository for this step — every `SectionLabelAnalysis`/
`SongFingerprintSet`/`MelodyTrack` fixture is constructed in-memory at test/demo run time (the same
approach Steps 052-054's own evidence already used and disclosed). `SourceHash` is the literal
placeholder string `"deadbeef"` in the unit tests (matching Steps 052-054's own already-committed
convention) and `"deadbeef-step055-demo-hash"` / `"deadbeef-corrupt"` in the demo. The throwaway demo
*project* itself (outside the repository, under the session scratchpad) and its own real exported
cache/override files (under the OS temp directory) were deleted after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **Phase 06 (stem-separation model) remains BLOCKED** at the phase-gate level, carried forward
  unresolved from Steps 052-054's own evidence — unchanged by this step, which has no stem/audio input
  of its own either.
- **Phase 07's own full-phase gate (Section 7 demonstration, Section 8 final gate report) has not been
  separately run/recorded** — unchanged, carried forward from Steps 052-054's own evidence.
- **No `MotifLink`/motif-clustering, choreography-template or propagation-inspector pipeline exists
  yet** — Steps 056-058's own later scope, not fabricated here (§3).
- **No real `SequenceDocument`/`IEditTransactionService`/UI integration for this step** — phrase
  review/correction UI is Step 058's own later scope (§3).
- **The bar-hypothesis/melodic-cadence segmentation recipe is this step's own declared, disclosed
  policy, not a claim that it matches any specific published phrase-segmentation algorithm.** A future
  step may refine the recipe under a new, distinguishable `PhraseSegmentationSettings.RecipeId` without
  redefining this one's meaning (§3).
- Per the user's own standing waiver (`memory: feedback_skip_xlights_human_verification`), the
  real-xLights render/desktop-interaction check is not treated as a blocker for this step's own scope —
  this step's own required acceptance text does not itself name an xLights-specific manual action (it is
  a pure domain/adapter claim, proven directly against the real `PhraseSegmenter`/
  `JsonPhraseSegmentationStore`/`JsonPhraseBoundaryOverrideStore`, with the persistence and
  corrections-survive-restart claims additionally round-tripped through real cache/override files on
  disk via brand-new store instances, and the invalid-input-rejection claims additionally round-tripped
  through real, confirmed-absent files).

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — segmenting already-approved section
labels into bar-aligned phrase units via a bar-count hypothesis refined by melodic cadences, supporting
human boundary corrections that survive both an upstream re-analysis and an application restart, and
never producing an overlapping or malformed phrase — not a claim that Phase 06's own separately-tracked
BLOCKED verdict is resolved, nor that Phase 07's own full-phase gate has been separately closed out; see
§3/§7).

- "Phrase boundaries align to bars or user-chosen events" (required acceptance, verbatim) — proven by
  `Step055_Acceptance_Passes_PhraseBoundariesAlignToBarsOrUserChosenEvents` (every phrase is an exact
  bar range; a bar-hypothesis boundary is demonstrably realigned to a real melodic cadence; a human
  manual correction replaces an automatic split with a user-chosen bar and survives a simulated restart)
  — independently confirmed by the real demo's Scenarios 1 and 2.
- "Segment into phrases with bar-based hypothesis and melodic cadences; permit irregular length and
  phrase overlaps only by deliberate policy" (implementation guidance, verbatim) — proven by the Verse
  section's own declared single irregular-length phrase, the Chorus section's own bar-hypothesis-then-
  cadence-snap split, `SegmentAsync_NoMelodyTrack_FallsBackToBarHypothesis` (the "melodic cadences" input
  is genuinely optional), and the recipe's own declared, disclosed "never overlap" policy (§3) enforced
  by `ValidatePhrases` — independently confirmed by the real demo's Scenarios 1 and 3.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge
  case, verbatim) — proven by
  `SegmentAsync_MalformedSectionRange_ThrowsPhraseSegmentationInvalidException_NeverCommitted`,
  `SetManualBoundariesAsync_UnknownSectionLabelId_ThrowsPhraseSegmentationInvalidException_NeverCommitted`,
  `SetManualBoundariesAsync_BoundaryOutsideSectionRange_ThrowsPhraseSegmentationInvalidException_NeverCommitted`
  and `SegmentAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverCommitted` (invalid
  input and cancellation); stale persisted state is handled structurally by the same
  version/settings-keyed cache-invalidation pattern Steps 052-054 already established (a mismatched or
  corrupt cache is never trusted, see `TryLoadStillValidCacheAsync`) — independently confirmed by the
  real demo's Scenarios 4 and 5 (real, confirmed-absent cache and override files for both rejected
  proposals).
- Full regression: **760/760 passing** (across the 10 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-054 still passes unmodified (748 baseline, unchanged), plus only this
  step's own 12 new tests.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase
  06's own gate remains BLOCKED; Phase 07's own full-phase gate has not been separately run/recorded
  (§3/§7).
