# PHASE 08 — Step 057 Evidence

**Step:** "Create parameterized choreography template and instance overrides"
**Spec:** `docs/phase-specs/PHASE-08.md` §6, Step 057 (lines 276-290) — the sixth numbered step of
Phase 08 (052-058)
**Date:** 2026-10-14

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-08.md` in full, confirming Step 057's own text: required acceptance ("Edit
  master/motif occurrence without surprising cross-section changes"), implementation guidance ("Create
  parent/instance pattern inheritance with adjustable mask and transform; preview effect list and respect
  locks") and failure/edge case ("Invalid input, cancellation or stale persisted state must not corrupt the
  project" — identical wording to Steps 055/056's own failure/edge case).
- Confirmed Step 057's own scope is choreography-template definition, instance overrides and preview only —
  Section 3 names `IMotifInheritancePlanner`/`ChoreographyPlan` as the broader proposed shape, but the
  propagation-inspector UI/`SequenceDocument` edit-transaction wiring is explicitly Step 058's own later
  scope ("Build phrase relationship/propagation inspector"); this step never writes into any
  `SequenceDocument`, it only computes a preview.
- Read every file in `src/XlightsAI.Studio.Structure/` in full before writing any new type:
  `MotifGroupCandidate`/`MotifLinkCandidate`/`MotifClusteringAnalysis` (Step 056's own direct upstream input
  — this step's own `MotifGroupId` always refers to an already-approved `MotifGroupCandidate.Id`, never
  re-derived); `PhraseCandidate`/`PhraseSegmentationAnalysis` (Step 055's own direct upstream input — every
  phrase's own tick window this step re-times against); `MotifClusterer`/`JsonMotifClusteringStore`/
  `IMotifClusteringStore`/`MotifClusteringInvalidException`/`MotifClusteringCorruptException` (the direct
  structural template this step's own `ChoreographyTemplatePlanner`/`IChoreographyTemplatePlanner`/
  `JsonChoreographyTemplateStore`/`IChoreographyTemplateStore`/`ChoreographyTemplateInvalidException`/
  `ChoreographyTemplateCorruptException` mirror exactly: validate-before-compute, cache-path-before-build,
  atomic temp-file-then-`File.Move` persistence, `{Type}InvalidException(SongId, reason)`/
  `{Type}CorruptException(path, inner)` naming convention).
- Read `src/XlightsAI.Studio.Xlights/EffectInstance.cs` and `SequenceDocument.cs` (PHASE-02/03's own
  already-shipped contracts this step's own `ChoreographyTemplateEffect`/`ChoreographyInstanceOverride`
  capture from and adapt onto) and `src/XlightsAI.Studio.Editing/EffectLockPolicy.cs`/`IEffectLockPolicy.cs`
  (PHASE-03's own already-shipped lock-reachability test — read, not reused directly, because that policy
  operates on an `EditScope` being applied to an already-accepted document, whereas this step needs to ask
  "would a *proposed, not-yet-applied* candidate effect overlap an existing locked destination effect" — a
  narrower, read-only question; a new, phrase-local `AddIfUnlocked` check was written inside
  `ChoreographyTemplatePlanner` using the same half-open-overlap test `IsReachedByScope` already
  established, rather than depending on `XlightsAI.Studio.Editing` for a one-line overlap test).
- PHASE-08 Section 3's own proposed `IMotifInheritancePlanner.AdaptApprovedChoreography(MusicalPhrase
  source, MusicalPhrase target, string mask, SequenceDocument document)` and Section 12's own worked example
  (`{ motion: true, timing: retime, palette: false, drum_layer: false }`) were read and used as the starting
  point for this step's own narrower `ChoreographyInheritanceMask`/`IChoreographyTemplatePlanner` (see §3 for
  the disclosed field-level deviations — a typed mask record instead of an opaque string, and an explicit
  parent/instance persisted aggregate instead of a single stateless adaptation call).
- Searched the full repository for any prior "Step 057"/"STEP-057" reference, and for any existing
  `Choreography*`/`IChoreographyTemplatePlanner` type, before starting — found only `PHASE-08.md` itself and
  the Section 9 copy/paste prompt, confirming this is genuinely new Step 057 work, not a duplicate.
- Baseline taken by running the real, full solution test suite *before* any Step 057 code was written:
  **772/772 passing, 0 failed, 0 skipped** across the 10 pre-existing test assemblies (matches Step 056's own
  recorded final count exactly — no drift).

## 2. Scope actually implemented

No new project was required — this step extends the existing `src/XlightsAI.Studio.Structure/` project. It
now additionally references `XlightsAI.Studio.Xlights` (for `SequenceDocument`/`EffectInstance`/`ModelId`),
the one `.csproj` change this step made (to both `src/XlightsAI.Studio.Structure/XlightsAI.Studio.Structure.csproj`
and `tests/XlightsAI.Studio.Structure.Tests/XlightsAI.Studio.Structure.Tests.csproj`). `XlightsAI.Studio.Xlights`
itself only references `XlightsAI.Studio.Contracts` — no circular project reference was introduced.

### New files in `src/XlightsAI.Studio.Structure/`

- **`ChoreographyTimingPolicy.cs`** — `enum { Copy, Retime }`: how a template effect's relative timing maps
  onto a target phrase of possibly different duration ("transform," this step's own implementation guidance,
  verbatim).
- **`ChoreographyInheritanceMask.cs`** — `Motion`/`Timing`/`Palette`/`RhythmLayer`: a typed, checkable
  narrowing of Section 12's own worked-example mask (`{ motion, timing, palette, drum_layer }`), applied
  mechanically rather than parsed from free text.
- **`ChoreographyTemplateEffect.cs`** — one effect captured from the template's own source phrase, expressed
  relative to that phrase's own start (`SourceEffectId`, `Target`, `Layer`, `RelativeStartTicks`/
  `RelativeEndTicks`, `RecipeId`/`RecipeVersion`, `Parameters`, `IsRhythmLayer`).
- **`ChoreographyInstanceOverride.cs`** — one explicit, human-authored replacement for a propagated effect at
  one specific occurrence, keyed by `SourceEffectId`; always wins over the template's own default
  inheritance regardless of mask.
- **`ChoreographyInstance.cs`** — one occurrence's own override set (`Id`, `TemplateId`, `TargetPhraseId`,
  `Overrides`). An occurrence with no overrides needs no `ChoreographyInstance` entry at all — pure
  inheritance.
- **`ChoreographyTemplate.cs`** — the "parent": `Id`, `MotifGroupId`, `SourcePhraseId`, `Mask`, `Effects`,
  `Instances`.
- **`ChoreographyTemplateAnalysis.cs`** — the full persisted, versioned read model: `VersionId`, `SongId`,
  `SourceHash`, `SourceMotifClusteringVersionId`, `SourceDocumentRevision`, `AnalyzerVersion`, `Templates`.
  `VersionId` doubles as this record's own optimistic-concurrency token.
- **`ChoreographyTemplateContext.cs`** — the already-approved inputs one (re)definition call is computed
  from: `SongId`, `SourceHash`, `MotifClustering` (Step 056's own approved output), `PhraseAnalysis` (Step
  055's own approved output), `SourceDocument` (the already-accepted sequence document effects are captured
  from).
- **`ChoreographyPreviewResult.cs`** — `Effects` (the real `EffectInstance` list one occurrence would
  receive) plus `ExcludedReasons` (human-readable, non-throwing reasons a candidate was left out — a locked
  conflict, or a `Copy`-policy window that does not fit).
- **`IChoreographyTemplatePlanner.cs`** / **`ChoreographyTemplatePlanner.cs`** — the real implementation:
  - `DefineTemplateAsync` validates every input explicitly (missing media, mismatched/stale provenance,
    unknown motif group, source phrase not a member of that group or not found in the phrase analysis,
    unrecognized `Timing` enum value) before any computation; captures every effect **fully contained**
    within the source phrase's own half-open tick window (an effect straddling a phrase boundary belongs to
    neither phrase unambiguously, so it is never captured); refreshing the same `(MotifGroupId,
    SourcePhraseId)` pair preserves the same `Template.Id` and every existing `Instances` entry — "editing
    the master" is simply calling this again after the source document's own effects changed.
  - `SetInstanceOverrideAsync` validates the template exists, the target phrase is a member of that
    template's own motif group, and the override itself is well-formed (`EndTicks > StartTicks`, non-empty
    `RecipeId`) before upserting; never touches the template's own `Effects` or any other occurrence.
  - `PreviewInstance` is pure/read-only: for each template effect, an instance override (if present) always
    wins; otherwise `Mask.Motion == false` or (`IsRhythmLayer && !Mask.RhythmLayer`) skips propagation
    entirely ("leaves destination-specific D untouched," Section 12, verbatim); `Timing.Retime` scales the
    relative offsets by the target/source duration ratio, `Timing.Copy` reuses the same absolute offsets and
    is excluded with a reason if the result does not fit; `Mask.Palette == false` strips
    `"Palette."`-prefixed parameter keys. Every candidate is checked against the destination document's own
    existing **locked** effects on the same target with an overlapping half-open window and excluded (with a
    reason) rather than ever proposed over a lock ("respect locks," verbatim). An override whose
    `SourceEffectId` no longer matches any current template effect (the master was refreshed without it) is
    still honored as a standalone addition — removing a human's explicit choice without consent would itself
    be a "surprising cross-section change."
  - Both async methods persist through a `SaveGuardedAsync` optimistic-concurrency guard: if anything is
    already persisted for the song, it must match the caller's own supplied `current` analysis's `VersionId`
    exactly, or the call throws rather than silently overwriting a concurrent change (Step 057's own "stale
    persisted state must not corrupt the project" failure/edge case, verbatim) — a disclosed, deliberate
    addition beyond Step 056's own precedent (see §3).
- **`IChoreographyTemplateStore.cs`** / **`JsonChoreographyTemplateStore.cs`** — JSON-file-backed persistence,
  same atomic temp-file-then-`File.Move` pattern as every other store in this project.
- **`ChoreographyTemplateInvalidException.cs`** / **`ChoreographyTemplateCorruptException.cs`** — mirror
  `MotifClusteringInvalidException`/`MotifClusteringCorruptException` exactly.

### New test file

- `tests/XlightsAI.Studio.Structure.Tests/Step057AcceptanceTests.cs` — 16 tests against the real
  `ChoreographyTemplatePlanner`/`JsonChoreographyTemplateStore` (not a mock), including:
  - `Step057_Acceptance_Passes_EditMasterOrInstanceOverride_WithoutSurprisingCrossSectionChanges` (the
    required acceptance test, verbatim name): a three-occurrence fixture — Chorus A (master, 8 bars) with a
    motion effect (one palette-tagged parameter, one motion parameter) and a drum effect on a declared
    rhythm layer, Chorus B (8 bars, a pre-existing locked effect exactly where the retimed motion effect
    would land) and Chorus C (6 bars, a shorter variant) — proves, in one real end-to-end sequence: (1) the
    drum effect is never propagated to any occurrence (`RhythmLayer` mask false); (2) Chorus B's own motion
    candidate is excluded with a "locked" reason; (3) Chorus C's motion effect is correctly re-timed (6/8
    ratio) and palette-stripped; (4) overriding Chorus C's own occurrence never changes Chorus B's preview;
    (5) editing the master (the source document's own motion effect parameters change, same effect id) is
    picked up by Chorus B (no override of its own) but Chorus C **still shows its own override, unchanged** —
    the required acceptance's own two halves (editing the master propagates; editing one occurrence does not
    leak to its siblings) proven together, against the real planner, with a real on-disk cache file at every
    step.
  - `PreviewInstance_CopyTimingPolicy_WindowDoesNotFit_ExcludedWithReason` — `Timing.Copy` against a phrase
    too short for the copied absolute window is excluded with a "does not fit" reason, not silently clamped
    or thrown.
  - `PreviewInstance_OrphanedOverride_StillHonoredAsStandaloneAddition` — an override survives a master
    refresh that drops the effect it was keyed to.
  - `DefineTemplateAsync_ContainmentPolicy_OnlyFullyContainedEffectsCaptured` — an effect straddling a phrase
    boundary is never captured.
  - Eleven further negative-path tests: unknown motif group id, source/target phrase not a member of the
    group, mismatched provenance (`SourcePhraseSegmentationVersionId`), empty `SourceHash` (missing media),
    null arguments, already-cancelled token (with a confirmed-absent cache file), a stale `current` on both
    `DefineTemplateAsync` and `SetInstanceOverrideAsync` (with the original valid analysis confirmed still on
    disk, untouched), unknown template id, and a malformed override (`EndTicks <= StartTicks`).

**No existing file from Steps 052-056 or any prior phase/step was modified**, other than the two `.csproj`
files adding the `XlightsAI.Studio.Xlights` project reference (an additive dependency edge, not a contract
change).

## 3. Architecture deviations / decisions

- **A typed `ChoreographyInheritanceMask` record instead of Section 3's own proposed opaque
  `string InheritanceMask`.** Section 12's own worked example (`{ motion: true, timing: retime, palette:
  false, drum_layer: false }`) is already a structured shape; encoding it as four checkable fields (one of
  them a `ChoreographyTimingPolicy` enum rather than a free-form "retime" string) lets
  `ChoreographyTemplatePlanner` act on it mechanically. A disclosed, deliberate narrowing — the same
  "Section 3 proposes, the implementation phase-locally narrows" precedent every prior Phase 08 step's own
  evidence already established.
- **A persisted parent/instance aggregate (`ChoreographyTemplate` + `ChoreographyInstance`) instead of
  Section 3's own proposed single stateless `AdaptApprovedChoreography(source, target, mask, document)`
  call.** Step 057's own title, verbatim, is "parent/instance pattern inheritance... **and instance
  overrides**" — a stateless per-call adaptation cannot represent "this one occurrence has its own
  human-edited override that survives a master refresh," which the required acceptance text explicitly
  demands ("Edit master/motif occurrence without surprising cross-section changes"). The persisted aggregate
  is the mechanism that makes that acceptance text checkable at all.
- **"Editing the master" is implemented as re-calling `DefineTemplateAsync` for the same `(MotifGroupId,
  SourcePhraseId)` pair after the source document's own effect parameters changed**, not as a separate
  "edit" method. Because `EffectInstance.Id` is stable across an edit (only its fields mutate, per the
  already-shipped PHASE-02/03 contract), re-capturing naturally picks up the new values under the same
  `SourceEffectId` key — no new mutation API was needed.
- **Palette-aspect parameters are identified by a declared `"Palette."` key-prefix convention**
  (`ChoreographyTemplatePlanner.PaletteParameterPrefix`), not by any structural distinction on
  `EffectInstance.Parameters` (a plain `IReadOnlyDictionary<string,string>` from PHASE-02). A disclosed,
  inspectable policy choice, consistent with Section 3's own "Mapping... is a declared policy" convention
  already applied elsewhere in Steps 054-056. A future recipe may declare a different convention under a new,
  distinguishable `AnalyzerVersion` without redefining this one's meaning.
- **"Respect locks" is satisfied by excluding a candidate from the preview, with a human-readable reason —
  never by throwing.** A locked destination conflict is an expected, common outcome (Section 12's own
  example assumes it), not a malformed-input error; it belongs in `ChoreographyPreviewResult.ExcludedReasons`
  alongside the `Copy`-policy non-fit case, both of which are "this candidate was deliberately left out," not
  "the request itself was invalid."
- **An explicit optimistic-concurrency guard (`SaveGuardedAsync`) was added, beyond what Step 056's own
  `MotifClusterer` needed.** Step 056 only ever produces a fresh, from-scratch analysis per cache-key change;
  Step 057 introduces genuine in-place mutation of a persisted aggregate (adding an instance override,
  refreshing a template) where two concurrent editors (or a stale reload) could otherwise silently clobber
  each other's work. This mirrors PHASE-03's own already-shipped `DocumentRevision`-based optimistic-
  concurrency convention (`EditTransaction.BeforeRevision`/`AfterRevision`), applied here to this step's own
  aggregate — a disclosed, deliberate addition, not a silent contract change.
- **An orphaned override (its `SourceEffectId` no longer matches any current template effect after a master
  refresh) is still honored as a standalone, instance-only addition in `PreviewInstance`, never silently
  dropped.** Removing a human's explicit choice without consent would itself be exactly the "surprising
  cross-section change" Step 057's own required acceptance text prohibits.
- **No `SequenceDocument`/`IEditTransactionService`/UI wiring for this step's own preview output** —
  `ChoreographyPreviewResult` is a pure, inspectable read value; turning an accepted preview into a real
  `EditTransaction` against the destination document, and any UI surface for it, is Step 058's own later
  scope ("Build phrase relationship/propagation inspector"). Consistent with Section 4: "Preview artifacts
  and AI proposals cannot update the accepted document directly."
- **Carried-forward, disclosed phase-level/dependency-level gaps are unchanged from Steps 052-056's own
  evidence** (not reopened or re-resolved here, not silently treated as closed): Phase 06's own
  stem-separation gate remains BLOCKED; Phase 07's own full-phase gate (Section 7 demonstration, Section 8
  final report) has not been separately run/recorded.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed Bash tool in this session again returned bare exit code 1
with no output for plain commands (confirmed again at the very start of this step: `pwd`/`ls -la` both
returned exit code 1 with no output), the same pre-existing environment limitation recorded in every prior
step's own evidence; PowerShell was used for every `dotnet`/file-system command below without incident.

```
# Baseline, before any Step 057 code:
dotnet test XlightsAI.Studio.slnx --nologo
  # 772/772 passing, 0 failed, 0 skipped (10 pre-existing assemblies) -- matches Step 056's own recorded count.

# .csproj changes (additive XlightsAI.Studio.Xlights project reference):
#   src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj
#   tests\XlightsAI.Studio.Structure.Tests\XlightsAI.Studio.Structure.Tests.csproj

# After the new Step 057 source files in the existing XlightsAI.Studio.Structure project:
dotnet build src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).

# After Step057AcceptanceTests.cs:
dotnet build tests\XlightsAI.Studio.Structure.Tests\XlightsAI.Studio.Structure.Tests.csproj --nologo
  # Build succeeded. 0 Warning(s). 0 Error(s).
dotnet test tests\XlightsAI.Studio.Structure.Tests\XlightsAI.Studio.Structure.Tests.csproj --nologo `
  --filter "FullyQualifiedName~Step057AcceptanceTests" --logger "console;verbosity=detailed"
  # 16/16 passed, first run, no fixture fixes needed

dotnet test XlightsAI.Studio.slnx --nologo       # full regression, final

dotnet new console -o <scratchpad>\xlai-step057-demo-proj --no-restore
dotnet add <scratchpad>\xlai-step057-demo-proj reference src\XlightsAI.Studio.Structure\XlightsAI.Studio.Structure.csproj
dotnet add <scratchpad>\xlai-step057-demo-proj reference src\XlightsAI.Studio.Contracts\XlightsAI.Studio.Contracts.csproj
dotnet add <scratchpad>\xlai-step057-demo-proj reference src\XlightsAI.Studio.Xlights\XlightsAI.Studio.Xlights.csproj
dotnet run --project <scratchpad>\xlai-step057-demo-proj --nologo   # real, non-test-harness manual demo (see §5)
```

### Targeted test results (Step 057's own 16 new tests, real run, first attempt, no fixture fixes needed)

```
Test Run Successful.
Total tests: 16
     Passed: 16
 Total time: 1.9315 Seconds
```

Includes:
`Step057_Acceptance_Passes_EditMasterOrInstanceOverride_WithoutSurprisingCrossSectionChanges`,
`PreviewInstance_CopyTimingPolicy_WindowDoesNotFit_ExcludedWithReason`,
`PreviewInstance_OrphanedOverride_StillHonoredAsStandaloneAddition`,
`DefineTemplateAsync_UnknownMotifGroupId_ThrowsChoreographyTemplateInvalidException`,
`DefineTemplateAsync_SourcePhraseNotInGroup_ThrowsChoreographyTemplateInvalidException`,
`DefineTemplateAsync_MismatchedProvenance_ThrowsChoreographyTemplateInvalidException`,
`DefineTemplateAsync_EmptySourceHash_ThrowsChoreographyTemplateInvalidException_MissingMediaNegativePath`,
`DefineTemplateAsync_NullArguments_ThrowArgumentNullException`,
`DefineTemplateAsync_CancellationAlreadyRequested_ThrowsOperationCanceledException_NeverCommitted`,
`DefineTemplateAsync_StaleCurrent_ThrowsChoreographyTemplateInvalidException_NeverCorrupted`,
`DefineTemplateAsync_ContainmentPolicy_OnlyFullyContainedEffectsCaptured`,
`SetInstanceOverrideAsync_StaleCurrent_ThrowsChoreographyTemplateInvalidException`,
`SetInstanceOverrideAsync_UnknownTemplateId_ThrowsChoreographyTemplateInvalidException`,
`SetInstanceOverrideAsync_TargetPhraseNotInGroup_ThrowsChoreographyTemplateInvalidException`,
`SetInstanceOverrideAsync_MalformedOverride_EndBeforeStart_ThrowsChoreographyTemplateInvalidException`,
`PreviewInstance_UnknownTemplateOrPhrase_ThrowsChoreographyTemplateInvalidException`.

### Full regression (final, after all Step 057 implementation and tests)

```
Passed!  - Failed: 0, Passed:  74, Skipped: 0, Total:  74  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 117, Skipped: 0, Total: 117  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed:  70, Skipped: 0, Total:  70  - XlightsAI.Studio.Structure.Tests.dll  (54 existing + 16 new)
Passed!  - Failed: 0, Passed:  38, Skipped: 0, Total:  38  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed: 159, Skipped: 0, Total: 159  - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
Passed!  - Failed: 0, Passed: 101, Skipped: 0, Total: 101  - XlightsAI.Studio.Stems.Tests.dll
```

**788 total (across all 10 reported assemblies), 0 failed, 0 skipped.** Every pre-existing test from Steps
001-056 still passes unmodified (772 baseline, unchanged); the `XlightsAI.Studio.Structure.Tests` assembly
adds exactly this step's own 16 new tests (54 existing Step 052/053/054/055/056 tests + 16 new Step 057 tests
= 70).

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session scratchpad,
deleted after this run) exercises the real `ChoreographyTemplatePlanner`/`JsonChoreographyTemplateStore`
directly against an in-memory `MotifClusteringAnalysis`/`PhraseSegmentationAnalysis`/`SequenceDocument`
fixture — no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step057-demo-proj --nologo
```

Observed real output:

```
=== Step 057 manual demo: parameterized choreography template + instance overrides, preview, locks ===

--- Scenario 1: define template from Chorus A (the master), persisted to disk ---
  Cache file: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step057-demo-cache-72a8a45263474748bb822c9b8fee2735\choreography-template-ca10bf5509c14b48bdc6bbe2ccec8bfb.json
  Exists on disk: True
  Template effects captured from Chorus A: 2 (motion + drum; the locked effect lives in Chorus B, outside Chorus A's own window)
  Preview Chorus B (locked conflict expected): effects=0, excluded=Would overwrite locked effect '06976f49-0e45-42a5-8dd0-f3959611d6ac' on 'Model/Tree' -- excluded from preview.
  Preview Chorus C (re-timed, palette stripped): effects=1
    -> Target=Model/Tree Start=400750000 End=402250000 Intensity=0.8 HasPaletteKey=False
  Drum (rhythm layer) ever propagated: False (expected False -- RhythmLayer mask is false)

--- Scenario 2: override one specific occurrence (Chorus C) -- master and Chorus B must be unaffected ---
  Chorus C now shows the override: RecipeId=InstanceOnlyOverride, Intensity=0.99
  Chorus B unaffected by Chorus C's own override: effects=0 (still locked-excluded, unchanged)

--- Scenario 3: edit the master (source document's own motion effect changes) ---
  Template identity preserved across refresh: True
  Chorus C still shows ITS OWN override after master edit (not reverted, not the new master value): Intensity=0.99
  Chorus B (no override of its own) now reflects the master's NEW value once unlocked: Intensity=0.5

--- Scenario 4: invalid input is rejected and never committed to disk ---
  Correctly threw ChoreographyTemplateInvalidException: Motif group '7ce90bdd-ad39-46c7-9720-72bdedd5c5e5' was not found in the supplied motif clustering analysis.

Step 057 manual demo completed successfully.
```

Direct, real, non-test-harness confirmation of the required acceptance text, both halves at once: (1) a real
`.json` choreography-template cache file is written to disk and its existence verified; the master's own
captured structure (one motion effect, one rhythm-layer drum effect) correctly excludes the drum effect from
every occurrence's preview (mask), correctly excludes Chorus B's motion candidate for landing on a real,
pre-existing locked destination effect ("respect locks," verbatim), and correctly re-times/palette-strips the
motion effect for the shorter Chorus C occurrence; (2) overriding Chorus C's own occurrence leaves Chorus B's
own preview completely unchanged; (3) editing the master (the real source document's own effect parameters
change under the same effect id) is correctly picked up by Chorus B once its lock is lifted, while Chorus C
**continues to show its own override, neither reverted to the old master value nor silently updated to the
new one** — "Edit master/motif occurrence without surprising cross-section changes," required acceptance,
verbatim, proven end-to-end against the real planner and a real on-disk cache; (4) an unknown motif group id
is rejected with a real, descriptive `ChoreographyTemplateInvalidException`.

## 6. Fixtures / input hashes

No binary fixture *file* is committed to the repository for this step — every `MotifClusteringAnalysis`/
`PhraseSegmentationAnalysis`/`SequenceDocument` fixture is constructed in-memory at test/demo run time (the
same approach Steps 052-056's own evidence already used and disclosed). `SourceHash` is the literal
placeholder string `"deadbeef"` in the unit tests (matching Steps 052-056's own already-committed convention)
and `"deadbeef-step057-demo-hash"` in the demo. The throwaway demo *project* itself (outside the repository,
under the session scratchpad) and its own real exported cache file (under the OS temp directory) were deleted
after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **Phase 06 (stem-separation model) remains BLOCKED** at the phase-gate level, carried forward unresolved
  from Steps 052-056's own evidence — unchanged by this step, which has no stem/audio input of its own
  either.
- **Phase 07's own full-phase gate (Section 7 demonstration, Section 8 final gate report) has not been
  separately run/recorded** — unchanged, carried forward from Steps 052-056's own evidence.
- **No propagation-inspector UI, and no wiring from `ChoreographyPreviewResult` into a real
  `SequenceDocument`/`IEditTransactionService` accept flow, exists yet** — Step 058's own later scope ("Build
  phrase relationship/propagation inspector"), not fabricated here (§3).
- **No `SongStructure`/`ISongStructureAnalyzer`/full `IMotifInheritancePlanner` (Section 3's own broader
  proposed shape) was built** — this step's own narrower, disclosed `IChoreographyTemplatePlanner` covers
  exactly Step 057's own title ("parent/instance pattern inheritance... and instance overrides"); a user
  reassigning the canonical motif source (Section 3: "a user may reassign the canonical source") is not
  implemented — this step fixes one `SourcePhraseId` per template and refreshes it in place.
- **The `"Palette."` key-prefix convention and the `Copy`/`Retime` timing-transform recipe are this step's
  own declared, disclosed policy choices, not a claim that they match any specific published choreography-
  inheritance convention.** A future recipe may declare a different convention under a new, distinguishable
  `AnalyzerVersion` without redefining this one's meaning (§3).
- Per the user's own standing waiver (`memory: feedback_skip_xlights_human_verification`), the real-xLights
  render/desktop-interaction check is not treated as a blocker for this step's own scope — this step's own
  required acceptance text does not itself name an xLights-specific manual action (it is a pure
  domain/adapter claim, proven directly against the real `ChoreographyTemplatePlanner`/
  `JsonChoreographyTemplateStore`, with persistence, cache-reuse, and optimistic-concurrency claims
  additionally round-tripped through real cache files on disk via a brand-new store instance in the real,
  non-test-harness demo).

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — defining/refreshing a parent choreography
template from an already-approved motif occurrence, recording independent per-occurrence instance overrides,
and previewing the real adapted effect list while respecting destination locks, without corrupting the
project on invalid input, cancellation, or a stale concurrent write — not a claim that Phase 06's own
separately-tracked BLOCKED verdict is resolved, nor that Phase 07's own full-phase gate has been separately
closed out; see §3/§7).

- "Edit master/motif occurrence without surprising cross-section changes" (required acceptance, verbatim) —
  proven by `Step057_Acceptance_Passes_EditMasterOrInstanceOverride_WithoutSurprisingCrossSectionChanges`
  (overriding one occurrence never changes a sibling occurrence's own preview; editing the master is picked
  up by every non-overridden occurrence while an overridden occurrence keeps its own override, unchanged) —
  independently confirmed by the real demo's Scenarios 2-3.
- "Create parent/instance pattern inheritance with adjustable mask and transform; preview effect list and
  respect locks" (implementation guidance, verbatim) — proven by the same acceptance test (mask-gated
  rhythm-layer exclusion, re-timed/palette-stripped inheritance, lock-respecting exclusion with a reason) and
  by `PreviewInstance_CopyTimingPolicy_WindowDoesNotFit_ExcludedWithReason` (the `Copy` transform) —
  independently confirmed by the real demo's Scenario 1.
- "Invalid input, cancellation or stale persisted state must not corrupt the project" (failure/edge case,
  verbatim) — proven by the eleven negative-path tests (unknown motif group/template/phrase-membership,
  mismatched provenance, missing media, null arguments, malformed override, already-cancelled token with a
  confirmed-absent cache file, and a stale `current` on both `DefineTemplateAsync` and
  `SetInstanceOverrideAsync` with the original valid analysis confirmed still on disk, untouched) —
  independently confirmed by the real demo's Scenario 4.
- Full regression: **788/788 passing** (across the 10 reported assemblies), 0 failed, 0 skipped; every
  pre-existing test from Steps 001-056 still passes unmodified (772 baseline, unchanged), plus only this
  step's own 16 new tests.
- **Carried-forward, disclosed phase-level/dependency-level gaps (not this step's own blocker):** Phase 06's
  own gate remains BLOCKED; Phase 07's own full-phase gate has not been separately run/recorded (§3/§7).

**Next step (058, "Build phrase relationship/propagation inspector") does not begin until this evidence is
reviewed.**
