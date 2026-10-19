# PHASE 08 — Step 056 Evidence

**Step:** "Cluster exact and similar motifs with tolerance for variation"
**Spec:** `docs/phase-specs/PHASE-08.md` §6, Step 056 (lines 260-274) — the fifth numbered step of
Phase 08 (052-058)
**Date:** 2026-10-14

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-08.md` in full, confirming Step 056's own text: required acceptance
  ("Repeated eight-bar fixture shares motif ID; distinct passage does not"), implementation guidance
  ("Use self-similarity and robust time-scaling to propose repeated motifs; score separately from section
  label") and failure/edge case ("Invalid input, cancellation or stale persisted state must not corrupt the
  project" — identical wording to Step 055's own failure/edge case).
- Confirmed Step 056's own scope is motif *clustering* only — Section 3 explicitly instructs: "Distinguish
  segment boundary from section label and motif identity." Section labels (Step 054) and phrase
  segmentation (Step 055) are already-approved upstream inputs this step consumes without re-deriving;
  choreography template inheritance is Step 057's own later scope; the propagation inspector UI is Step
  058's. None of `SongStructure`/`ISongStructureAnalyzer`/`IMotifInheritancePlanner` (Section 3's proposed
  signatures) were built here — only a phrase-local, concrete narrowing of `MotifLink`/`IMotifMatcher`
  (see §3 for the disclosed field-level deviations), the same "Section 3 proposes, the real implementation
  phase-locally narrows" precedent Steps 052-055's own evidence already disclosed and followed.
- Read every file in `src/XlightsAI.Studio.Structure/` in full (all 47 Step 052/053/054/055 files) before
  writing any new type: `PhraseCandidate`/`PhraseSegmentationAnalysis` (this step's own direct upstream
  input — the already-approved phrases this step clusters, never re-segmented); `BarFingerprint`/
  `SongFingerprintSet` (the per-bar evidence this step's own phrase embeddings are resampled from);
  `FingerprintSimilarity.Cosine` (the existing per-bar cosine-similarity definition — read, not modified,
  and not reused directly because it compares two single `BarFingerprint`s, whereas this step compares two
  multi-bar *resampled embeddings*; a new, phrase-local `Cosine(double[], double[])` was written inside
  `MotifClusterer` rather than overloading or editing the existing type); `PhraseSegmenter`/
  `IPhraseSegmenter` and `SectionLabelClassifier`/`ISectionLabelClassifier` (the direct structural template
  this step's own `IMotifClusterer`/`MotifClusterer`/`IMotifClusteringStore`/`JsonMotifClusteringStore`
  mirror: validate-before-compute, cache-path-before-build, atomic temp-file-then-`File.Move` persistence,
  "still-valid cache" reopen pattern); `PhraseSegmentationInvalidException`/`PhraseSegmentationCorruptException`
  (the `{Type}InvalidException(SongId, reason)` / `{Type}CorruptException(path, inner)` naming convention
  this step's own exceptions mirror exactly). PHASE-08 Section 3's own proposed `MotifLink(Guid
  SourcePhraseId, Guid TargetPhraseId, double Similarity, bool UserApproved, string InheritanceMask)` was
  read and used as the starting point for this step's own narrower `MotifLinkCandidate` (see §3 for the
  disclosed field-level deviations).
- Searched the full repository for any prior "Step 056"/"STEP-056" reference before starting — found only
  `PHASE-08.md` itself, and no existing `Motif*`/`IMotifClusterer`/`MotifLink` type anywhere, confirming
  this is genuinely new Step 056 work, not a duplicate.
- Baseline taken by running the real, full solution test suite *before* any Step 056 code was written:
  **760/760 passing, 0 failed, 0 skipped** across the 10 pre-existing test assemblies (matches Step 055's
  own recorded final count exactly — no drift).

## 2. Scope actually implemented

No new project was required — this step extends the existing `src/XlightsAI.Studio.Structure/` project
(already references `XlightsAI.Studio.Contracts`/`XlightsAI.Studio.Rhythm`/`XlightsAI.Studio.Melody`,
sufficient for this step's own needs — this step itself only consumes `Contracts` types plus the
project's own existing `PhraseCandidate`/`PhraseSegmentationAnalysis`/`SongFingerprintSet`/`BarFingerprint`;
no `.csproj` changes were made).

### New files in `src/XlightsAI.Studio.Structure/`

- **`MotifLinkCandidate.cs`** — one pairwise proposal: `SourcePhraseId`/`TargetPhraseId` (the earlier/later
  of the two phrases by `StartBarIndex` — an arbitrary-but-deterministic orientation, not a canonical-source
  claim), `Similarity` (cosine similarity in `[-1, 1]` between the two phrases' own time-scale-normalized
  embeddings — "score separately from section label," this step's own implementation guidance, verbatim:
  never reads `SectionLabelCandidate.Kind`), `Evidence` (non-empty, human-readable). A disclosed, narrower
  precursor to Section 3's own proposed `MotifLink` — `UserApproved`/`InheritanceMask` are Step 057/058's
  own later scope (see §3).
- **`MotifGroupCandidate.cs`** — one connected-component cluster: `Id` (the "motif ID" every member
  shares), `PhraseIds` (ordered by `StartBarIndex`, always two or more entries — a singleton is never
  emitted), `RepresentativePhraseId` (the earliest member — a deterministic default "first approved
  performance," Section 12, not a final canonical-source claim), `Confidence` (the minimum pairwise
  similarity among every pair of members — a conservative, never-overstated group score), `Evidence`.
- **`MotifClusteringSettings.cs`** — `CanonicalSampleCount` (how many evenly-spaced bars each phrase's
  per-bar feature sequence is resampled to before comparison — "robust time-scaling," this step's own
  implementation guidance, verbatim; must be positive), `SimilarityThreshold` (the minimum cosine
  similarity in `[0, 1]` for a link to be proposed — "tolerance for variation," this step's own title,
  verbatim: less than `1.0` so a close-but-not-identical repeat still links), `RecipeId`.
- **`MotifClusteringContext.cs`** — `SongId`, `SourceHash`, `PhraseAnalysis` (Step 055's own approved
  output), `FingerprintSet` (Step 052's own approved output).
- **`MotifClusteringAnalysis.cs`** — the full read model a caller receives: `VersionId`, `SongId`,
  `SourceHash`, `SourcePhraseSegmentationVersionId`, `SourceFingerprintSetVersionId`, `AnalyzerVersion`,
  `Settings`, `Groups`, `Links`.
- **`IMotifClusterer.cs`** / **`MotifClusterer.cs`** — the real implementation. Validates every input
  explicitly (missing media, mismatched/stale provenance across both upstream chains, unusable settings,
  a phrase whose bar range references a bar absent from the fingerprint set) before any computation; for
  each already-approved phrase, resamples its own per-bar `BarFingerprint.Features` sequence to exactly
  `CanonicalSampleCount` samples via nearest-neighbor index mapping (so an 8-bar phrase and a 9-bar
  variation of the same idea are each mapped to the same fixed-length, directly comparable embedding —
  "robust time-scaling," verbatim), flattens each embedding into one vector, computes pairwise cosine
  similarity between every phrase pair, proposes a `MotifLinkCandidate` for every pair at or above
  `SimilarityThreshold` ("self-similarity... to propose repeated motifs," verbatim), and builds
  connected-component `MotifGroupCandidate`s over the resulting link graph via union-find, **discarding any
  component with fewer than two members** (a phrase with no sufficiently similar partner is simply absent
  from every group — "distinct passage does not [share a motif ID]," this step's own required acceptance,
  verbatim, satisfied structurally rather than by a special-cased threshold). Fewer than two total phrases
  is an explicit, non-throwing empty result (no pair exists to compare — the same "silence... must be
  handled explicitly" convention Steps 052-055 already established). Validates every phrase's own bar range
  against the supplied fingerprint set **before** any embedding is computed, and never persists when
  validation fails (Step 056's own "invalid input... must not corrupt the project" failure/edge case,
  verbatim); propagates `CancellationToken` at both validation entry and before any file write, and reopens
  a still-valid cached automatic analysis rather than recomputing (same "with caching" convention every
  prior step already established, including treating a corrupt cache file as a cache miss rather than a
  hard failure — "stale persisted state must not corrupt the project," verbatim).
- **`IMotifClusteringStore.cs`** / **`JsonMotifClusteringStore.cs`** — JSON-file-backed persistence for the
  computed `MotifClusteringAnalysis`, same atomic temp-file-then-`File.Move` pattern as every other store in
  this project.
- **`MotifClusteringInvalidException.cs`** / **`MotifClusteringCorruptException.cs`** — mirror
  `PhraseSegmentationInvalidException`/`PhraseSegmentationCorruptException` exactly.

### New test file

- `tests/XlightsAI.Studio.Structure.Tests/Step056AcceptanceTests.cs` — 12 tests against the real
  `MotifClusterer`/`JsonMotifClusteringStore` (not a mock), including:
  - `Step056_Acceptance_Passes_RepeatedEightBarFixtureSharesMotifId_DistinctPassageDoesNot` (the required
    acceptance test, verbatim name): a three-phrase fixture — Chorus A `[0,8)` and Chorus B `[16,24)`
    sharing a bit-identical deterministic 4-dimensional per-bar pattern, with a genuinely distinct Verse
    `[8,16)` in between using a different pattern — asserts exactly one `MotifGroupCandidate` containing
    both choruses (and never the Verse), confidence in range, non-empty evidence, the underlying pairwise
    `MotifLinkCandidate` present for the two choruses and absent for any Verse pairing, full provenance
    (`SourceHash`/`SourcePhraseSegmentationVersionId`/`SourceFingerprintSetVersionId`/`AnalyzerVersion`), a
    real cache file on disk, and a **brand-new `MotifClusterer`/store pair constructed over the same
    on-disk directory** (the test's own stand-in for an application restart / re-analysis) returning the
    identical `VersionId` (cache reused, not silently recomputed).
  - `ClusterAsync_RobustTimeScaling_DifferentBarCountsStillLink` — an 8-bar Chorus A and a 9-bar Chorus B
    using the same repeating pattern still land in one group ("robust time-scaling," verbatim).
  - `ClusterAsync_FewerThanTwoPhrases_ReturnsEmptyResult_HandlesExplicitly` — a single phrase (no pair to
    compare) returns an empty, non-throwing result.
  - `ClusterAsync_PhraseReferencesMissingBar_ThrowsMotifClusteringInvalidException_NeverCommitted` — the
    required "invalid input... must not corrupt the project" failure case (malformed/mismatched phrase
    range): asserts the clusterer throws and that **no cache file is ever written**.
  - `ClusterAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverCommitted` — the
    required "cancellation... must not corrupt the project" failure case: an already-cancelled token throws
    before any computation or persistence, with **no cache file ever written**.
  - Six further negative-path tests: empty `SourceHash` (missing media), mismatched provenance
    (`SourceFingerprintSetVersionId` between the phrase analysis and fingerprint set), invalid
    `CanonicalSampleCount` (`0` and `-1`, via `[Theory]`), invalid `SimilarityThreshold` (`-0.1` and `1.1`,
    via `[Theory]`), and null arguments (`context`, `settings`).

**No existing file from Steps 052-055 or any prior phase/step was modified.** This is a purely additive
extension of the same `XlightsAI.Studio.Structure` vertical slice.

## 3. Architecture deviations / decisions

- **No `UserApproved`/`InheritanceMask` on `MotifLinkCandidate`**, narrower than Section 3's own proposed
  `MotifLink(Guid SourcePhraseId, Guid TargetPhraseId, double Similarity, bool UserApproved, string
  InheritanceMask)`. This step's own scope is pure acoustic-similarity proposal; human approval and
  choreography-inheritance masking belong to Steps 057/058's own later scope ("Create parameterized
  choreography template and instance overrides" / "Build phrase relationship/propagation inspector"). A
  disclosed, deliberate narrowing, not a silent contract change — the same "Section 3 proposes, the
  implementation phase-locally narrows" precedent every prior Phase 08 step's own evidence already
  established (e.g. Step 055's own non-nullable `Bars` narrowing of `MusicalPhrase.Bars`).
- **No `SongStructure`/`ISongStructureAnalyzer`/`IMotifInheritancePlanner` was built.** All three belong to
  Steps 057-058's own later scope (choreography template inheritance, propagation UI) — fabricating any of
  them now would repeat the same violation Steps 052-055's own evidence already disclosed avoiding ("Avoid
  collecting multiple unverified tasks into one mega-commit," Section 6, verbatim).
- **A new, phrase-local `Cosine(double[], double[])` helper was written inside `MotifClusterer` rather than
  reusing or extending the existing `FingerprintSimilarity.Cosine(BarFingerprint, BarFingerprint)`.** The
  existing method compares two single bars; this step compares two multi-bar *resampled embeddings*
  (`CanonicalSampleCount * featureDims` long), a structurally different input shape. Writing a new, private
  helper avoids widening an already-shipped Step 052 public contract for a Step 056-only need, consistent
  with the phase's own "do not fabricate generic interfaces without a concrete boundary" architectural
  assumption (Section 2).
- **"Robust time-scaling" is satisfied by nearest-neighbor resampling to a fixed
  `CanonicalSampleCount`, not by dynamic time warping (DTW) or any other alignment algorithm.** This is a
  disclosed, inspectable, declared policy choice (consistent with Section 3's own "Mapping... is a declared
  policy" convention, applied elsewhere in Steps 054/055 to label/boundary confidence, applied here to
  time-normalization) — not a claim that it matches any specific published motif-matching algorithm. A
  future recipe could implement DTW or another alignment strategy under a new, distinguishable
  `MotifClusteringSettings.RecipeId` without redefining this one's meaning.
- **A motif group requires two or more members; a single unrepeated phrase is never reported as its own
  group.** This is the single declared mechanism satisfying "distinct passage does not [share a motif ID]"
  — a phrase with no sufficiently similar partner is structurally absent from `Groups`, not merely assigned
  a low-confidence or empty group ID.
- **Group `Confidence` is the minimum pairwise similarity among all members, not the mean.** A disclosed,
  conservative choice: a motif group is only as strong as its weakest internal link, avoiding a scenario
  where one highly-similar pair inflates the reported confidence of a looser three-or-more-member cluster.
- **`SimilarityThreshold` is constrained to `[0, 1]`, narrower than the full `[-1, 1]` cosine-similarity
  range.** A disclosed, deliberate choice: a negative-similarity "motif link" (two phrases whose embeddings
  point in opposite directions) is never a musically meaningful repetition threshold for this recipe, so
  the settings' own valid domain is restricted to the range that can plausibly represent "similar enough to
  repeat."
- **No `MotifGroupCandidate`-to-`SequenceDocument`/`IEditTransactionService`/UI wiring for this step**,
  consistent with Steps 052-055's own precedent — `MotifClusterer` is a pure domain/adapter boundary; no
  `ShellViewModel`-level command or SCREEN-11/SCREEN-13 wiring exists yet (Step 058's own later scope:
  "Build phrase relationship/propagation inspector").
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Steps 052-055's own
  evidence** (not reopened or re-resolved here, not silently treated as closed): Phase 06's own
  stem-separation gate remains BLOCKED; Phase 07's own full-phase gate (Section 7 demonstration, Section 8
  final report) has not been separately run/recorded.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands, the same pre-existing environment limitation recorded in every prior
step's own evidence; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 056 code:
dotnet test XlightsAI.Studio.slnx --nologo
  # 760/760 passing, 0 failed, 0 skipped (10 pre-existing assemblies) -- matches Step 055's own recorded count.

# After the new Step 056 source files in the existing XlightsAI.Studio.Structure project:
dotnet build src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After Step056AcceptanceTests.cs:
dotnet build tests\XlightsAI.Studio.Structure.Tests\XlightsAI.Studio.Structure.Tests.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet test tests\XlightsAI.Studio.Structure.Tests\XlightsAI.Studio.Structure.Tests.csproj --nologo `
  --logger "console;verbosity=detailed"
  # 54/54 passed (42 existing Step 052/053/054/055 tests unmodified + 12 new Step 056 tests), first run, no fixture fixes needed

dotnet build XlightsAI.Studio.slnx --nologo       # full solution -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo       # full regression, final

dotnet new console -o <scratchpad>\xlai-step056-demo-proj --no-restore
dotnet add <scratchpad>\xlai-step056-demo-proj reference src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj
dotnet add <scratchpad>\xlai-step056-demo-proj reference src\XlightsAI.Studio.Contracts\XlightsAI.Studio.Contracts.csproj
dotnet run --project <scratchpad>\xlai-step056-demo-proj --nologo   # real, non-test-harness manual demo (see §5)
```

### Targeted test results (Step 052 + 053 + 054 + 055 + 056 tests, real run, first attempt, no fixture fixes needed)

```
Test Run Successful.
Total tests: 54
     Passed: 54
 Total time: 1.5633 Seconds
```

Includes all 12 new tests:
`Step056_Acceptance_Passes_RepeatedEightBarFixtureSharesMotifId_DistinctPassageDoesNot`,
`ClusterAsync_RobustTimeScaling_DifferentBarCountsStillLink`,
`ClusterAsync_FewerThanTwoPhrases_ReturnsEmptyResult_HandlesExplicitly`,
`ClusterAsync_PhraseReferencesMissingBar_ThrowsMotifClusteringInvalidException_NeverCommitted`,
`ClusterAsync_EmptySourceHash_ThrowsMotifClusteringInvalidException_MissingMediaNegativePath`,
`ClusterAsync_MismatchedProvenanceVersionIds_ThrowsMotifClusteringInvalidException`,
`ClusterAsync_InvalidCanonicalSampleCount_ThrowsMotifClusteringInvalidException` (×2, `[Theory]`),
`ClusterAsync_InvalidSimilarityThreshold_ThrowsMotifClusteringInvalidException` (×2, `[Theory]`),
`ClusterAsync_NullArguments_ThrowArgumentNullException`,
`ClusterAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverCommitted`.

### Full regression (final, after all Step 056 implementation and tests)

```
Passed!  - Failed: 0, Passed:  74, Skipped: 0, Total:  74  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  54, Skipped: 0, Total:  54  - XlightsAI.Studio.Structure.Tests.dll  (42 existing + 12 new)
Passed!  - Failed: 0, Passed: 117, Skipped: 0, Total: 117  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  38, Skipped: 0, Total:  38  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 159, Skipped: 0, Total: 159  - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
Passed!  - Failed: 0, Passed: 101, Skipped: 0, Total: 101  - XlightsAI.Studio.Stems.Tests.dll
```

**772 total (across all 10 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from Steps
001-055 still passes unmodified (760 baseline, unchanged); the `XlightsAI.Studio.Structure.Tests` assembly
adds exactly this step's own 12 new tests (42 existing Step 052/053/054/055 tests + 12 new Step 056 tests =
54).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `MotifClusterer`/`JsonMotifClusteringStore` directly against an
in-memory `PhraseSegmentationAnalysis`/`SongFingerprintSet` fixture — no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step056-demo-proj --nologo
```

Observed real output:

```
=== Step 056 manual demo: motif clustering, self-similarity, robust time-scaling, provenance ===

--- Scenario 1: real clustering (self-similarity + time-scale-robust resampling), persisted to disk ---
  Cache file: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step056-demo-cache-.../motif-clustering-....json
  Exists on disk: True
  Groups found: 1
    MotifGroup 44899165-4eef-4333-aebb-52588c93e3ff: members=[ChorusA[0,8), ChorusB[16,25)(9 bars)] confidence=1.000
      Evidence: 2 phrases connected via pairwise similarity (minimum pairwise similarity in this group: 1.000).
  Verse appears in any group: False (expected False -- distinct passage)
  ChorusA/ChorusB linked despite different bar counts (8 vs 9, robust time-scaling): True

--- Scenario 2: reopen with a brand-new clusterer/store pair (simulated restart) -- cache is reused, not recomputed ---
  Same VersionId after restart: True

--- Scenario 3: a phrase referencing a bar outside the fingerprint set must be rejected, never committed to disk ---
  Correctly threw MotifClusteringInvalidException: Phrase at position 1 references bar 4, which does not exist in the source fingerprint set -- malformed/mismatched proposal.
  Cache file written despite rejection: False (expected False)

Step 056 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation: (1) a real `.json` motif-clustering cache file is written to
disk and its existence verified; a repeated Chorus pattern (8-bar Chorus A and a 9-bar variant Chorus B,
proving "robust time-scaling" concretely rather than only via a same-length fixture) is correctly clustered
into a single motif group, while a genuinely distinct Verse passage in between is correctly excluded from
every group ("Repeated eight-bar fixture shares motif ID; distinct passage does not," required acceptance,
verbatim); (2) a brand-new `MotifClusterer`/`JsonMotifClusteringStore` pair constructed over the same
on-disk directory (the real stand-in for an application restart, since no in-process state is reused)
returns the identical cached `VersionId` rather than silently recomputing ("stale persisted state must not
corrupt the project," satisfied by correctly *not* treating a still-valid cache as stale); (3) a phrase
referencing a bar outside the supplied fingerprint set is rejected with a real, descriptive exception and
**no cache file is ever written** ("invalid input... must not corrupt the project," verbatim).

## 6. Fixtures / input hashes

No binary fixture *file* is committed to the repository for this step — every `PhraseSegmentationAnalysis`/
`SongFingerprintSet` fixture is constructed in-memory at test/demo run time (the same approach Steps
052-055's own evidence already used and disclosed). `SourceHash` is the literal placeholder string
`"deadbeef"` in the unit tests (matching Steps 052-055's own already-committed convention) and
`"deadbeef-step056-demo-hash"` / `"deadbeef-corrupt"` in the demo. The throwaway demo *project* itself
(outside the repository, under the session scratchpad) and its own real exported cache file (under the OS
temp directory) were deleted after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **Phase 06 (stem-separation model) remains BLOCKED** at the phase-gate level, carried forward unresolved
  from Steps 052-055's own evidence — unchanged by this step, which has no stem/audio input of its own
  either.
- **Phase 07's own full-phase gate (Section 7 demonstration, Section 8 final gate report) has not been
  separately run/recorded** — unchanged, carried forward from Steps 052-055's own evidence.
- **No choreography-template or propagation-inspector pipeline exists yet** — Steps 057-058's own later
  scope, not fabricated here (§3). `MotifLinkCandidate`/`MotifGroupCandidate` carry no `UserApproved`/
  `InheritanceMask`/human-acceptance state.
- **No real `SequenceDocument`/`IEditTransactionService`/UI integration for this step** — motif review UI is
  Step 058's own later scope (§3).
- **The time-scaled-cosine, connected-component clustering recipe is this step's own declared, disclosed
  policy, not a claim that it matches any specific published motif-detection/self-similarity-matrix
  algorithm.** A future step may refine the recipe (e.g. dynamic time warping) under a new, distinguishable
  `MotifClusteringSettings.RecipeId` without redefining this one's meaning (§3).
- Per the user's own standing waiver (`memory: feedback_skip_xlights_human_verification`), the real-xLights
  render/desktop-interaction check is not treated as a blocker for this step's own scope — this step's own
  required acceptance text does not itself name an xLights-specific manual action (it is a pure
  domain/adapter claim, proven directly against the real `MotifClusterer`/`JsonMotifClusteringStore`, with
  the persistence and cache-reuse-across-restart claims additionally round-tripped through a real cache
  file on disk via a brand-new store instance, and the invalid-input-rejection claim additionally
  round-tripped through a real, confirmed-absent cache file).

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — clustering already-approved phrases into
motif groups via time-scale-normalized self-similarity, tolerating bar-count variation, scoring purely from
acoustic features independent of section label, and never corrupting the project on invalid input or
cancellation — not a claim that Phase 06's own separately-tracked BLOCKED verdict is resolved, nor that
Phase 07's own full-phase gate has been separately closed out; see §3/§7).

- "Repeated eight-bar fixture shares motif ID; distinct passage does not" (required acceptance, verbatim)
  — proven by
  `Step056_Acceptance_Passes_RepeatedEightBarFixtureSharesMotifId_DistinctPassageDoesNot` (two 8-bar
  choruses with an identical pattern share exactly one `MotifGroupCandidate`; the distinct Verse in between
  appears in zero groups) — independently confirmed by the real demo's Scenario 1 (using an even stronger
  8-vs-9-bar variant to also directly exercise time-scaling).
- "Use self-similarity and robust time-scaling to propose repeated motifs; score separately from section
  label" (implementation guidance, verbatim) — proven by `ClusterAsync_RobustTimeScaling_DifferentBarCountsStillLink`
  (an 8-bar and a 9-bar phrase of the same idea are still linked via nearest-neighbor resampling to a common
  canonical length) and by `MotifClusterer`'s own recipe never reading `SectionLabelCandidate.Kind` or any
  section-level data at all — independently confirmed by the real demo's Scenario 1.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by `ClusterAsync_PhraseReferencesMissingBar_ThrowsMotifClusteringInvalidException_NeverCommitted`
  and `ClusterAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverCommitted` (invalid
  input and cancellation, both with a confirmed-absent cache file); stale persisted state is handled
  structurally by the same version/settings-keyed cache-invalidation pattern every prior step already
  established (a mismatched or corrupt cache is never trusted, see `TryLoadStillValidCacheAsync`) —
  independently confirmed by the real demo's Scenarios 2 (a still-valid cache is correctly reused, not
  discarded) and 3 (a real, confirmed-absent cache file for the rejected proposal).
- Full regression: **772/772 passing** (across the 10 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-055 still passes unmodified (760 baseline, unchanged), plus only this
  step's own 12 new tests.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase 06's
  own gate remains BLOCKED; Phase 07's own full-phase gate has not been separately run/recorded (§3/§7).
