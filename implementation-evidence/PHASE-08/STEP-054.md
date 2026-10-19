# PHASE 08 — Step 054 Evidence

**Step:** "Assign candidate intro/verse/chorus/bridge labels and support overrides"
**Spec:** `docs/phase-specs/PHASE-08.md` §6, Step 054 (lines 228-242) — the third numbered step of
Phase 08 (052-058)
**Date:** 2026-10-14

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-08.md` in full, confirming Step 054's own text: required acceptance ("UI
  shows provenance/confidence; overrides survive restart"), implementation guidance ("Classify section
  labels with confidence and evidence; Unknown is valid; never force classical song structure") and
  failure/edge case ("Malformed proposal, no-improvement and exhausted retry budget must not auto-commit").
- Confirmed Step 054's own scope is section *label* assignment only — Section 3 explicitly instructs:
  "Distinguish segment boundary from section label and motif identity." Segment boundaries
  (`SectionBoundaryCandidate`) were already built and approved in Step 053; phrase assembly is Step 055's
  own later scope; motif clustering is Step 056's; none of `MusicalPhrase`/`MotifLink`/`SongStructure`/
  `ISongStructureAnalyzer`/`IMotifMatcher`/`IMotifInheritancePlanner` (Section 3's proposed signatures) were
  built here — this step's own concrete, narrower contract is `SectionLabelCandidate`/
  `SectionLabelAnalysis`, the same "Section 3 proposes, the real implementation phase-locally narrows"
  precedent Steps 052 and 053's own evidence already disclosed and followed.
- Read every file in `src/XlightsAI.Studio.Structure/` in full (all 23 Step 052/053 files) before writing
  any new type: `SectionBoundaryCandidate`/`SectionBoundaryAnalysis`/`SectionBoundaryContext` (this step's
  own direct upstream input — the already-approved segment boundaries this step classifies, never
  re-detected); `ISectionBoundaryDetector`/`SectionBoundaryDetector` and `IBarFingerprintBuilder`/
  `BarFingerprintBuilder` (the direct structural template this step's own `ISectionLabelClassifier`/
  `SectionLabelClassifier`/`ISectionLabelStore`/`JsonSectionLabelStore` mirror: validate-before-compute,
  cache-path-before-build, atomic temp-file-then-`File.Move` persistence, "still-valid cache" reopen
  pattern); `FingerprintSimilarity.Cosine` (reused directly, not reimplemented, for segment-repetition
  scoring); `SectionBoundaryInvalidException`/`SectionBoundaryCorruptException` (the
  `{Type}InvalidException(SongId, reason)` / `{Type}CorruptException(path, inner)` naming convention this
  step's own exceptions mirror exactly); PHASE-08 Section 3's own proposed `SectionKind` enum (adopted
  verbatim, unchanged).
- Searched the full repository for any prior "Step 054"/"STEP-054" reference before starting — found only
  `PHASE-08.md` itself, and no existing `SectionLabel*`/`ISectionLabelClassifier`/`SectionKind` type
  anywhere, confirming this is genuinely new Step 054 work, not a duplicate.
- Baseline taken by running the real, full solution test suite *before* any Step 054 code was written:
  **736/736 passing, 0 failed, 0 skipped** across the 10 pre-existing test assemblies (matches Step 053's
  own recorded final count exactly — no drift).

## 2. Scope actually implemented

No new project was required — this step extends the existing `src/XlightsAI.Studio.Structure/` project
(already references `XlightsAI.Studio.Contracts`/`XlightsAI.Studio.Rhythm`/`XlightsAI.Studio.Melody`,
sufficient for this step's own needs; no `.csproj` changes were made).

### New files in `src/XlightsAI.Studio.Structure/`

- **`SectionKind.cs`** — PHASE-08 Section 3's own proposed enum, adopted verbatim: `Unknown`, `Intro`,
  `Verse`, `PreChorus`, `Chorus`, `Bridge`, `Solo`, `Outro`.
- **`SectionLabelCandidate.cs`** — one classified segment: `Id`, `BoundaryId` (the stable
  `SectionBoundaryCandidate.Id` this segment starts at — the key a human override addresses, independent of
  bar-index drift from a future boundary re-analysis), `StartBarIndex`/`EndBarIndexExclusive`, `Kind`,
  `Confidence`, `Evidence` (non-empty, human-readable reasons — "with confidence and evidence," verbatim),
  `UserCorrected`.
- **`SectionLabelSettings.cs`** — `RepetitionSimilarityThreshold`, `MinConfidenceToLabel` (the declared,
  bounded floor below which a segment is labeled `Unknown` instead of a forced guess — this step's own
  structural answer to "Unknown is valid; never force classical song structure," verbatim), `RecipeId`.
- **`SectionLabelAnalysis.cs`** — the full read model a caller receives: `VersionId`, `SongId`,
  `SourceHash`, `SourceSectionBoundaryVersionId`, `SourceFingerprintSetVersionId` (ties every analysis to
  both exact upstream revisions it was classified from — Section 4's "every cache key includes
  algorithm/model/settings versions"), `AnalyzerVersion`, `Settings`, `Labels`.
- **`SectionLabelContext.cs`** — `SongId`, `SourceHash`, `BoundaryAnalysis` (Step 053's own approved
  output), `FingerprintSet` (Step 052's own approved output, the evidence source).
- **`ISectionLabelClassifier.cs`** / **`SectionLabelClassifier.cs`** — the real implementation. Validates
  every input explicitly (missing media, mismatched/stale provenance across *both* upstream chains,
  unusable settings) before any computation; averages each segment's per-bar fingerprints and clusters
  segments by `FingerprintSimilarity.Cosine` above `RepetitionSimilarityThreshold` (union-find); classifies
  each segment by repetition first (→ `Chorus`), then position (first → `Intro`, last → `Outro`, uniquely
  sitting between two repeated segments → `Bridge`, otherwise → `Verse`); demotes any result whose computed
  confidence falls below `MinConfidenceToLabel` to `Unknown` rather than fabricating a confident label (an
  explicit silent segment, or a single-segment song, is also explicitly `Unknown` — never classical
  structure forced); **structurally validates every upstream boundary list** (strictly increasing
  `BarIndex`, every referenced `BarIndex` actually present in the source fingerprint set) *before* any
  computation or persistence — a malformed upstream boundary sequence throws `SectionLabelInvalidException`
  and is never written to disk (Step 054's own "Malformed proposal... must not auto-commit" failure/edge
  case, verbatim); reopens a still-valid cached automatic analysis rather than recomputing (same "with
  caching" convention Steps 052/053 already established). `SetOverrideAsync` additionally validates that
  the targeted `BoundaryId` actually exists in the current classification before persisting anything — an
  override addressing an unknown boundary id is this step's own second concrete instance of "malformed
  proposal... must not auto-commit," rejected before the override store is ever touched.
- **`ISectionLabelStore.cs`** / **`JsonSectionLabelStore.cs`** — JSON-file-backed persistence for the
  *automatic* (pre-override) classification, same atomic temp-file-then-`File.Move` pattern as
  `JsonSectionBoundaryStore`.
- **`ISectionLabelOverrideStore.cs`** / **`JsonSectionLabelOverrideStore.cs`** — a **separate** JSON-file-
  backed store (Section 4: "Persist detected structure and user-edited structure in separately versioned
  stores," verbatim) mapping `BoundaryId → SectionKind` for every human correction. `ClassifyAsync` always
  re-reads this store and re-merges it onto the (possibly freshly rebuilt) automatic result, so an override
  is never silently lost by an upstream re-analysis, a settings change, or an application restart — this
  step's own required acceptance, verbatim: "overrides survive restart."
- **`SectionLabelInvalidException.cs`** / **`SectionLabelCorruptException.cs`** — mirror
  `SectionBoundaryInvalidException`/`SectionBoundaryCorruptException` exactly.

### New test file

- `tests/XlightsAI.Studio.Structure.Tests/Step054AcceptanceTests.cs` — 12 tests against the real
  `SectionLabelClassifier`/`JsonSectionLabelStore`/`JsonSectionLabelOverrideStore` (not a mock), including:
  - `Step054_Acceptance_Passes_ShowsProvenanceAndConfidence_OverridesSurviveRestart` (the required
    acceptance test, verbatim name): a deterministic six-segment fixture mirroring the phase gate's own
    scenario ("Analyze a song with repeated chorus; show two matching phrases highlighted") — Intro, Verse,
    Chorus, Bridge, Chorus (repeat), Outro — asserts every expected `SectionKind`, non-empty evidence and
    in-range confidence on every candidate, full provenance recorded (`SourceHash`/
    `SourceSectionBoundaryVersionId`/`SourceFingerprintSetVersionId`/`AnalyzerVersion`/`Settings`), a real
    cache file on disk, then sets a real override via `SetOverrideAsync`, confirms the override file exists
    on disk, and **constructs brand-new `JsonSectionLabelStore`/`JsonSectionLabelOverrideStore`/
    `SectionLabelClassifier` instances over the same directories** (the test's own stand-in for an
    application restart) to confirm the override is still applied — while every other, never-overridden
    segment is confirmed unaffected.
  - `ClassifyAsync_ConfidenceBelowMinConfidenceToLabel_LabelsUnknown_NeverForcesClassicalStructure` — the
    required "Unknown is valid; never force classical song structure" guidance, verbatim: a strict
    confidence floor (0.9) that only the 1.0-confidence repeated-chorus segments can clear demotes every
    position-only heuristic label (Intro/Verse/Bridge/Outro, each 0.5-0.6 confidence) to `Unknown`.
  - `ClassifyAsync_SingleSegmentSong_LabelsUnknown_NeverForcesClassicalStructure` and
    `ClassifyAsync_SilentSegment_LabelsUnknown_NeverFabricatesEvidence` — further "never force classical
    song structure" cases: a song with only one segment, and a segment with no measurable energy/melody,
    are both explicitly `Unknown` rather than an arbitrarily chosen label.
  - `ClassifyAsync_OutOfOrderBoundaries_ThrowsSectionLabelInvalidException_MalformedProposalNeverCommitted`
    — the required "Malformed proposal... must not auto-commit" failure case (upstream boundary list):
    asserts the classifier throws `SectionLabelInvalidException` and that **no cache file is ever written**.
  - `SetOverrideAsync_UnknownBoundaryId_ThrowsSectionLabelInvalidException_MalformedProposalNeverCommitted`
    — the same failure case's second concrete instance (override target): asserts the override is rejected
    and that **no override file is ever written**.
  - `ClassifyAsync_EmptyBoundaries_ReturnsEmptyLabels_HandlesSilenceExplicitly` — zero boundaries (silence,
    carried over from Steps 052/053's own disclosed convention) returns an empty, non-throwing result.
  - Five further negative-path tests: empty `SourceHash` (missing media), mismatched provenance
    (`SourceFingerprintSetVersionId` between the two upstream analyses), invalid
    `RepetitionSimilarityThreshold` (`0.0` and `1.5`, via `[Theory]`), and null arguments.

**No existing file from Steps 052/053 or any prior phase/step was modified.** This is a purely additive
extension of the same `XlightsAI.Studio.Structure` vertical slice.

## 3. Architecture deviations / decisions

- **No `MusicalPhrase`/`MotifLink`/`SongStructure`/`ISongStructureAnalyzer`/`IMotifMatcher`/
  `IMotifInheritancePlanner` was built.** All six belong to Steps 055-058's own later scope (phrase
  assembly, motif clustering, inheritance planning) — fabricating any of them now would repeat the same
  violation Steps 052/053's own evidence already disclosed avoiding ("Avoid collecting multiple unverified
  tasks into one mega-commit," Section 6, verbatim).
- **"No-improvement... must not auto-commit" is satisfied by the explicit `Unknown` confidence floor, not
  by an iterative retry loop (an explicit, disclosed architecture decision).** Step 054's own failure/edge
  text is the same boilerplate clause Steps 053/055-057 all share verbatim ("Malformed proposal,
  no-improvement and exhausted retry budget must not auto-commit"). Unlike Step 053's own adaptive
  novelty-threshold search, this step's own recipe is a single deterministic evidence computation per
  segment — there is no meaningful "retry budget" to exhaust, because there is no search loop to retry. The
  shared clause's underlying intent ("never auto-commit a result the evidence does not actually support")
  is instead satisfied structurally by `SectionLabelSettings.MinConfidenceToLabel`: a segment whose computed
  confidence does not clear that floor is classified `Unknown` rather than forced into a specific label —
  this step's own named, required substitute, verbatim: "Unknown is valid; never force classical song
  structure." "Malformed proposal... must not auto-commit" itself is satisfied literally, both for a
  corrupt/mismatched upstream boundary list and for an override addressing a boundary id that does not
  exist (§2).
- **Repetition evidence is a lightweight union-find clustering of per-segment averaged fingerprints, not a
  full motif graph.** "Robust... tolerance for variation" (Section 3) and exact/approximate motif
  clustering with its own confidence scoring is explicitly Step 056's own later scope ("Cluster exact and
  similar motifs with tolerance for variation"). This step only needs enough repetition evidence to inform
  a section *label* (Chorus vs. not), never a motif *identity* or a `MotifLink` — Section 3's own
  distinction, verbatim: "Distinguish segment boundary from section label **and motif identity**."
- **Position heuristics (Intro/Outro/Bridge/Verse) use fixed, declared confidence constants (0.6 for
  first/last-segment position, 0.5 for internal segments), not a learned or continuously-scored model.**
  This is a disclosed, inspectable policy choice consistent with Section 3's own "Mapping... is a declared
  policy" convention (applied elsewhere to gesture mapping, applied here to position-based label
  confidence) — a future recipe can refine this scoring under a new, distinguishable
  `SectionLabelSettings.RecipeId` without redefining this one's meaning.
- **`StemActivity` remains unconsumed by this step's own recipe**, consistent with Steps 052/053's own
  disclosed convention — Section 3's proposed `SongAnalysisContext` names stem activity for a later step's
  broader pipeline; this step's own narrower `SectionLabelContext` only consumes the already-approved
  `SectionBoundaryAnalysis` and `SongFingerprintSet`.
- **No `SequenceDocument`/`IEditTransactionService`/UI wiring for this step**, consistent with Steps
  052/053's own precedent — `SectionLabelClassifier` is a pure domain/adapter boundary; no
  `ShellViewModel`-level command or SCREEN-11/SCREEN-13 wiring exists yet (Step 058's own later scope:
  "Build phrase relationship/propagation inspector").
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Steps 052/053's own
  evidence** (not reopened or re-resolved here, not silently treated as closed): Phase 06's own
  stem-separation gate remains BLOCKED; Phase 07's own full-phase gate (Section 7 demonstration, Section 8
  final report) has not been separately run/recorded.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands, the same pre-existing environment limitation recorded in every prior
step's own evidence; PowerShell was used for every `dotnet`/git-status command below without incident.

```
# Baseline, before any Step 054 code:
dotnet test XlightsAI.Studio.slnx --nologo
  # 736/736 passing, 0 failed, 0 skipped (10 pre-existing assemblies) -- matches Step 053's own recorded count.

# After the new Step 054 source files in the existing XlightsAI.Studio.Structure project:
dotnet build src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After Step054AcceptanceTests.cs:
dotnet test tests\XlightsAI.Studio.Structure.Tests\XlightsAI.Studio.Structure.Tests.csproj --nologo `
  --logger "console;verbosity=detailed"
  # 30/30 passed (18 existing Step 052/053 tests unmodified + 12 new Step 054 tests), first run, no fixture fixes needed

dotnet build XlightsAI.Studio.slnx --nologo       # full solution -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo       # full regression, final

dotnet new console -o <scratchpad>\xlai-step054-demo-proj --no-restore
dotnet add <scratchpad>\xlai-step054-demo-proj reference src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj
dotnet add <scratchpad>\xlai-step054-demo-proj reference src\XlightsAI.Studio.Contracts\XlightsAI.Studio.Contracts.csproj
dotnet add <scratchpad>\xlai-step054-demo-proj reference src\XlightsAI.Studio.Rhythm\XlightsAI.Studio.Rhythm.csproj
dotnet add <scratchpad>\xlai-step054-demo-proj reference src\XlightsAI.Studio.Melody\XlightsAI.Studio.Melody.csproj
dotnet run --project <scratchpad>\xlai-step054-demo-proj --nologo   # real, non-test-harness manual demo (see §5)
```

### Targeted test results (Step 052 + Step 053 + Step 054 tests, real run, first attempt, no fixture fixes needed)

```
Test Run Successful.
Total tests: 30
     Passed: 30
 Total time: 1.4838 Seconds
```

Includes all 12 new tests:
`Step054_Acceptance_Passes_ShowsProvenanceAndConfidence_OverridesSurviveRestart`,
`ClassifyAsync_ConfidenceBelowMinConfidenceToLabel_LabelsUnknown_NeverForcesClassicalStructure`,
`ClassifyAsync_SingleSegmentSong_LabelsUnknown_NeverForcesClassicalStructure`,
`ClassifyAsync_SilentSegment_LabelsUnknown_NeverFabricatesEvidence`,
`ClassifyAsync_EmptyBoundaries_ReturnsEmptyLabels_HandlesSilenceExplicitly`,
`ClassifyAsync_OutOfOrderBoundaries_ThrowsSectionLabelInvalidException_MalformedProposalNeverCommitted`,
`SetOverrideAsync_UnknownBoundaryId_ThrowsSectionLabelInvalidException_MalformedProposalNeverCommitted`,
`ClassifyAsync_EmptySourceHash_ThrowsSectionLabelInvalidException_MissingMediaNegativePath`,
`ClassifyAsync_MismatchedProvenanceVersionIds_ThrowsSectionLabelInvalidException`,
`ClassifyAsync_InvalidRepetitionSimilarityThreshold_ThrowsSectionLabelInvalidException` (×2, `[Theory]`),
`ClassifyAsync_NullArguments_ThrowArgumentNullException`.

### Full regression (final, after all Step 054 implementation and tests)

```
Passed!  - Failed: 0, Passed:  74, Skipped: 0, Total:  74  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  30, Skipped: 0, Total:  30  - XlightsAI.Studio.Structure.Tests.dll  (18 existing + 12 new)
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 117, Skipped: 0, Total: 117  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  38, Skipped: 0, Total:  38  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 159, Skipped: 0, Total: 159  - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
Passed!  - Failed: 0, Passed: 101, Skipped: 0, Total: 101  - XlightsAI.Studio.Stems.Tests.dll
```

**748 total (across all 10 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from Steps
001-053 still passes unmodified (736 baseline, unchanged); the `XlightsAI.Studio.Structure.Tests` assembly
adds exactly this step's own 12 new tests (18 existing Step 052/053 tests + 12 new Step 054 tests = 30).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `SectionLabelClassifier`/`JsonSectionLabelStore`/
`JsonSectionLabelOverrideStore` directly against an in-memory `SectionBoundaryAnalysis`/`SongFingerprintSet`
fixture -- no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step054-demo-proj --nologo
```

Observed real output:

```
=== Step 054 manual demo: section label classification, provenance, overrides-survive-restart, malformed-proposal rejection ===

--- Scenario 1: real classification, persisted to disk, with confidence and evidence ---
  Cache file: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step054-demo-cache-.../section-labels\section-labels-....json
  Exists on disk: True
  Bars [0,1): Intro confidence=0.60 userCorrected=False evidence="First segment in the song; no repetition detected elsewhere."
  Bars [1,2): Verse confidence=0.50 userCorrected=False evidence="Internal, non-repeating segment with no stronger positional or repetition evidence."
  Bars [2,3): Chorus confidence=1.00 userCorrected=False evidence="Recurs with 1 other segment(s) at average fingerprint similarity 1.00, at or above RepetitionSimilarityThreshold (0.95)."
  Bars [3,4): Bridge confidence=0.50 userCorrected=False evidence="Unique, non-repeating segment occurring directly between two repeated (chorus-like) segments."
  Bars [4,5): Chorus confidence=1.00 userCorrected=False evidence="Recurs with 1 other segment(s) at average fingerprint similarity 1.00, at or above RepetitionSimilarityThreshold (0.95)."
  Bars [5,6): Outro confidence=0.60 userCorrected=False evidence="Last segment in the song; no repetition detected elsewhere."
  Expected kinds: [Intro, Verse, Chorus, Bridge, Chorus, Outro]
  Actual kinds:   [Intro, Verse, Chorus, Bridge, Chorus, Outro]
  Match: True

--- Scenario 2: set a user override, then reopen with brand-new store instances (simulated restart) ---
  Immediately after override: Labels[1].Kind=Solo UserCorrected=True
  Override file: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step054-demo-cache-.../section-label-overrides\section-label-overrides-....json
  Exists on disk: True
  After simulated restart (new store/classifier instances): Labels[1].Kind=Solo UserCorrected=True
  Override survived restart: True
  Other segments unaffected: Labels[0].Kind=Intro (expected Intro), Labels[2].Kind=Chorus (expected Chorus)

--- Scenario 3: Unknown is valid -- never force classical structure when confidence is insufficient ---
  Bars [0,1): Unknown confidence=0.60
  Bars [1,2): Solo confidence=1.00
  Bars [2,3): Chorus confidence=1.00
  Bars [3,4): Unknown confidence=0.50
  Bars [4,5): Chorus confidence=1.00
  Bars [5,6): Unknown confidence=0.60

--- Scenario 4: a malformed (out-of-order) boundary analysis must be rejected, never committed to disk ---
  Correctly threw SectionLabelInvalidException: Boundaries are not strictly increasing by BarIndex (2 -> 1) -- malformed proposal.
  Cache file written despite rejection: False (expected False)

--- Scenario 5: setting an override for an unknown boundary id must be rejected, never persisted ---
  Correctly threw SectionLabelInvalidException: Cannot set override: BoundaryId '...' does not reference any segment in the current classification -- malformed override target, not persisted.
  Override file unchanged by rejected attempt: True

Step 054 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation: (1) a real `.json` section-label-analysis cache file is
written to disk and its existence verified, with every segment carrying an inspectable confidence and
evidence string ("UI shows provenance/confidence," required user-visible outcome, verbatim); (2) the
six-segment repeated-chorus fixture matching the phase gate's own scenario is classified exactly as
expected (Intro/Verse/Chorus/Bridge/Chorus/Outro); (3) a real user override is persisted to a **separate**
override file, and — critically — **brand-new store and classifier instances constructed over the same
on-disk directories** (the real stand-in for an application restart, since no in-process state is reused)
still surface the override correctly while leaving every other segment's automatic classification
unaffected ("overrides survive restart," required acceptance, verbatim); (4) a stricter confidence floor
correctly demotes every position-only heuristic label to `Unknown` while leaving the two high-confidence
repeated-chorus segments (and the earlier override, still present) alone ("Unknown is valid; never force
classical song structure," verbatim); (5) a malformed (out-of-order) boundary analysis is rejected with a
real, descriptive exception and **no cache file is ever written**; (6) an override addressing an unknown
boundary id is rejected and **the override file is confirmed byte-for-byte unchanged** ("Malformed
proposal... must not auto-commit," failure/edge case, verbatim, satisfied for both of this step's own two
concrete malformed-input cases).

## 6. Fixtures / input hashes

No binary fixture *file* is committed to the repository for this step — every `SongFingerprintSet`/
`SectionBoundaryAnalysis` fixture is constructed in-memory at test/demo run time (the same approach Steps
052/053's own evidence already used and disclosed). `SourceHash` is the literal placeholder string
`"deadbeef"` in the unit tests (matching Steps 052/053's own already-committed convention) and
`"deadbeef-step054-demo-hash"` / `"deadbeef-corrupt"` in the demo. The throwaway demo *project* itself
(outside the repository, under the session scratchpad) and its own real exported cache/override files
(under the OS temp directory) were deleted after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **Phase 06 (stem-separation model) remains BLOCKED** at the phase-gate level, carried forward unresolved
  from Steps 052/053's own evidence — unchanged by this step, which has no stem/audio input of its own
  either.
- **Phase 07's own full-phase gate (Section 7 demonstration, Section 8 final gate report) has not been
  separately run/recorded** — unchanged, carried forward from Steps 052/053's own evidence.
- **No `MusicalPhrase`/`MotifLink`/phrase-assembly or motif-clustering pipeline exists yet** — Steps
  055-058's own later scope, not fabricated here (§3).
- **No real `SequenceDocument`/`IEditTransactionService`/UI integration for this step** — section label
  review/override UI is Step 058's own later scope (§3).
- **The repetition-clustering and position-heuristic classification recipe is this step's own declared,
  disclosed policy, not a claim that it matches any specific published song-structure-labeling algorithm.**
  A future step may refine the recipe under a new, distinguishable `SectionLabelSettings.RecipeId` without
  redefining this one's meaning (§3).
- Per the user's own standing waiver (`memory: feedback_skip_xlights_human_verification`), the real-xLights
  render/desktop-interaction check is not treated as a blocker for this step's own scope — this step's own
  required acceptance text does not itself name an xLights-specific manual action (it is a pure
  domain/adapter claim, proven directly against the real `SectionLabelClassifier`/`JsonSectionLabelStore`/
  `JsonSectionLabelOverrideStore`, with the persistence and override-survives-restart claims additionally
  round-tripped through real cache/override files on disk via brand-new store instances, and the
  malformed-proposal-rejection claims additionally round-tripped through real, confirmed-unaffected files).

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — classifying candidate intro/verse/
chorus/bridge section labels with confidence and evidence from an already-approved segment boundary list,
supporting human overrides that survive both an upstream re-analysis and an application restart, and never
forcing a label the evidence does not support — not a claim that Phase 06's own separately-tracked BLOCKED
verdict is resolved, nor that Phase 07's own full-phase gate has been separately closed out; see §3/§7).

- "UI shows provenance/confidence; overrides survive restart" (required acceptance, verbatim) — proven by
  `Step054_Acceptance_Passes_ShowsProvenanceAndConfidence_OverridesSurviveRestart` (full provenance chain
  recorded on every analysis; every candidate carries an inspectable confidence; a real override persisted
  via `SetOverrideAsync` is still present after constructing brand-new store/classifier instances over the
  same directories) — independently confirmed by the real demo's Scenario 1/2.
- "Classify section labels with confidence and evidence; Unknown is valid; never force classical song
  structure" (implementation guidance, verbatim) — proven by every `SectionLabelCandidate.Evidence` being
  non-empty and every `Confidence` being an inspectable, in-range value; by
  `ClassifyAsync_ConfidenceBelowMinConfidenceToLabel_LabelsUnknown_NeverForcesClassicalStructure`,
  `ClassifyAsync_SingleSegmentSong_LabelsUnknown_NeverForcesClassicalStructure` and
  `ClassifyAsync_SilentSegment_LabelsUnknown_NeverFabricatesEvidence` — independently confirmed by the real
  demo's Scenario 3.
- "Malformed proposal, no-improvement and exhausted retry budget must not auto-commit" (failure/edge case,
  verbatim; this step's own "no-improvement" substitute is the `Unknown` confidence floor, §3) — proven by
  `ClassifyAsync_OutOfOrderBoundaries_ThrowsSectionLabelInvalidException_MalformedProposalNeverCommitted`
  and `SetOverrideAsync_UnknownBoundaryId_ThrowsSectionLabelInvalidException_MalformedProposalNeverCommitted`
  — independently confirmed by the real demo's Scenarios 4 and 5 (real, confirmed-absent/confirmed-unchanged
  cache and override files for both rejected proposals).
- Full regression: **748/748 passing** (across the 10 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-053 still passes unmodified (736 baseline, unchanged), plus only this
  step's own 12 new tests.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase 06's
  own gate remains BLOCKED; Phase 07's own full-phase gate has not been separately run/recorded (§3/§7).
