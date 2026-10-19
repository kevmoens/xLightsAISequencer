# PHASE 14 — Step 092: Apply motif variations by occurrence and climax scheduling

**Status: PASS** (automated tests against real production types — no mocking; no installed xLights build
and no Avalonia UI harness is exercised in this environment — per the standing user waiver recorded in this
project's memory, the real-xLights human-verification portion of the overall phase gate is explicitly
waived rather than faked or re-requested; see §4 and §7 for exactly what that waiver does and does not
cover. This step itself produces no new screen — see §6 item 6 for the disclosed "no new ViewModel"
decision, consistent with Steps 090/091's own precedent.)

## 1. Scope actually implemented

Twelve new source files added to the existing `src/XlightsAI.Studio.WholeSong/` project (created by Step
090), one new test file added to the existing `tests/XlightsAI.Studio.WholeSong.Tests/` project. **No
existing PHASE-01–13 or Step-090/091 source file was modified at all**, and no `.csproj` changed (this step
reuses `XlightsAI.Studio.Structure`'s already-referenced `IChoreographyTemplatePlanner`/`ChoreographyTemplatePlanner`
(PHASE-08 Step 057) rather than adding any new project dependency):

- **`src/XlightsAI.Studio.WholeSong/MotifOccurrenceRequest.cs`** — this step's own input shape: one
  occurrence of an already-defined `ChoreographyTemplate` plus its own caller-supplied `SectionEnergy`
  (`[0,1]`) — the raw signal both intensity-scaling and climax scheduling are driven from. Owns a
  self-contained `Validate()`.
- **`src/XlightsAI.Studio.WholeSong/MotifOccurrenceVariation.cs`** — one occurrence's own scheduled result:
  the real, intensity-scaled effect list, its own climax flag/multiplier, and — "display inherited vs
  instance-specific effects" (implementation guidance, verbatim) — the source-effect-slot classification
  (`InheritedSourceEffectIds` / `InstanceOverrideSourceEffectIds`).
- **`src/XlightsAI.Studio.WholeSong/MotifVariationPlan.cs`** — the full, versioned, per-song output; owns a
  self-contained `Validate()` (non-empty, no duplicate occurrence pair, every multiplier within the
  scheduler's own declared bounds).
- **`src/XlightsAI.Studio.WholeSong/MotifVariationInvalidException.cs`** / **`MotifVariationStaleException.cs`**
  / **`MotifVariationCorruptException.cs`** — this step's own three named exception types, mirroring
  `SongDirectionInvalidException`/`SongDirectionStaleException`/`SongDirectionCorruptException`'s own
  convention, realizing the required failure/edge case verbatim: "Invalid input, cancellation or stale
  persisted state must not corrupt the project."
- **`src/XlightsAI.Studio.WholeSong/IMotifVariationScheduler.cs`** / **`MotifVariationScheduler.cs`** — the
  scheduler itself (see §3 for the full algorithm and its provable climax-lift guarantee). Constructor-injects
  the already-accepted PHASE-08 Step 057 `IChoreographyTemplatePlanner`, reusing its already-tested
  `PreviewInstance` (mask-aware inheritance, lock-respecting exclusion, retiming) rather than re-implementing
  any of it.
- **`src/XlightsAI.Studio.WholeSong/MotifVariationPlanRecord.cs`** / **`IMotifVariationPlanStore.cs`** /
  **`JsonMotifVariationPlanStore.cs`** — the persisted, versioned wrapper and its JSON-file store, mirroring
  `SongDirectionPlanRecord`/`ISongDirectionPlanStore`/`JsonSongDirectionPlanStore`'s own atomic-write,
  optimistic-concurrency convention byte-for-byte.
- **`src/XlightsAI.Studio.WholeSong/IMotifVariationCatalog.cs`** / **`MotifVariationCatalog.cs`** — the
  application-orchestration boundary that validates, auto-versions and saves/loads a plan, mirroring
  `ISongDirectionCatalog`/`SongDirectionCatalog`.
- **`tests/XlightsAI.Studio.WholeSong.Tests/Step092AcceptanceTests.cs`** — 13 new tests (see §3).

No existing Step 090/091 file (`SongDirectionPlan.cs`, `PhraseSeamAssembler.cs`, etc.) was touched; Step 092
does not yet consume `SongDirectionPlan`/`PhraseAssemblyResult` as a direct input (see §6 item 2 for the
disclosed reason) — it operates on caller-supplied `MotifOccurrenceRequest` values (one per occurrence, each
carrying its own already-resolved `SectionEnergy`), the same "explicit, caller-supplied input, not a
speculative upstream wiring" convention Step 091 already established for its own `PhraseAssemblyCandidate`/
`PhraseSeamRequest`.

## 2. Commands actually executed

```
dotnet build src\XlightsAI.Studio.WholeSong\XlightsAI.Studio.WholeSong.csproj --nologo
dotnet build tests\XlightsAI.Studio.WholeSong.Tests\XlightsAI.Studio.WholeSong.Tests.csproj --nologo
dotnet test tests\XlightsAI.Studio.WholeSong.Tests\XlightsAI.Studio.WholeSong.Tests.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
```

Run from `C:\CODE\xLightsAISequencer` via the PowerShell tool — this session's Bash/git-bash tool again
returned a bare exit code 1 with no captured output on every invocation (`ls`, `cat`, `echo`, `pwd`), the
same pre-existing local shell-launch issue every prior PHASE-13/Step-090/091 evidence already disclosed — not
a project/test failure; all commands below ran successfully via PowerShell.

## 3. Actual results

### 3.1 Build

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

`XlightsAI.Studio.WholeSong -> ...\bin\Debug\net10.0\XlightsAI.Studio.WholeSong.dll` and
`XlightsAI.Studio.WholeSong.Tests -> ...\bin\Debug\net10.0\XlightsAI.Studio.WholeSong.Tests.dll` both
compile cleanly against the existing `Structure` project reference (no new `ProjectReference` entries were
needed), alongside all pre-existing projects.

### 3.2 New-test run (`XlightsAI.Studio.WholeSong.Tests`)

```
Passed!  - Failed: 0, Passed: 37, Skipped: 0, Total: 37, Duration: 262 ms - XlightsAI.Studio.WholeSong.Tests.dll (net10.0)
```

37 = the 24 pre-existing Step 090/091 tests (unchanged, still green) + 13 new Step 092 tests:

| Test | Proves |
|---|---|
| `Step092_Acceptance_Passes` (required name) | Builds the phase-gate's own required demo shape — a chorus motif template captured from Chorus 1, with one `"Intensity.Brightness"`-tagged effect, recurring at Chorus 1 (energy 0.80), Chorus 2 (energy 0.90) and the Final Chorus (energy 0.97, the phase gate's own required highest-energy section). Asserts: "climax scheduling" (this step's own title, verbatim) selects **only** the Final Chorus as the climax, never Chorus 1 or Chorus 2; every occurrence's resulting effect keeps the **identical** `RecipeId`/`RecipeVersion`/`Target`/`Layer` shape ("recognizable motif"); the Final Chorus's own `IntensityMultiplier` **and** its own scaled `"Intensity.Brightness"` value are strictly greater than both earlier occurrences' ("intentional lift") — required acceptance, verbatim: "Final chorus preserves recognizable motif with intentional lift"; and "display inherited vs instance-specific effects" is satisfied by asserting the one effect slot is correctly classified as inherited (no override exists for this occurrence). Then persists the plan via the real `MotifVariationCatalog`/`JsonMotifVariationPlanStore` and **reloads it from a brand-new store/catalog instance** (proves "Versioned, inspectable project output," the step's own required User-visible outcome, verbatim, survives a restart — not only a mock invocation). |
| `Step092_SingleOccurrence_IsItsOwnClimax` | A template with exactly one requested occurrence is trivially its own climax — a degenerate but well-defined case, disclosed in §6 item 4. |
| `Step092_NegativePath_EmptyRequests_Throws` (required failure/edge case, verbatim: "Invalid input... must not corrupt the project") | Zero occurrence requests throws `MotifVariationInvalidException` rather than scheduling a silent no-op. |
| `Step092_NegativePath_OutOfRangeSectionEnergy_Throws` (`-0.1`, `1.1`, `NaN`) | Malformed `SectionEnergy` throws before any preview/scaling is attempted. |
| `Step092_NegativePath_DuplicateOccurrenceRequest_Throws` | Two requests for the same `(TemplateId, TargetPhraseId)` pair throw rather than silently scheduling one twice. |
| `Step092_NegativePath_UnknownTemplateId_Throws` | An unknown `TemplateId` propagates the real, already-tested `ChoreographyTemplateInvalidException` from the reused `IChoreographyTemplatePlanner.PreviewInstance` call, rather than this step re-implementing (and risking diverging from) that same validation. |
| `Step092_NegativePath_NonNumericIntensityParameter_Throws` | A template effect carrying a non-numeric `"Intensity."`-prefixed parameter value throws `MotifVariationInvalidException` naming the exact offending key/value — "cannot be safely scaled" is reported, never silently left unscaled or coerced. |
| `Step092_NegativePath_Cancellation_ThrowsBeforeAnyWork` (required failure/edge case, verbatim: "...cancellation... must not corrupt the project") | An already-cancelled `CancellationToken` throws `OperationCanceledException` before any request is validated or previewed. |
| `Step092_NegativePath_StalePersistedState_RejectsWriteAndLeavesFileUnchanged` (required failure/edge case, verbatim: "...stale persisted state must not corrupt the project") | A caller holding a stale `expectedCurrentVersion` is rejected by `JsonMotifVariationPlanStore.SaveAsync` with `MotifVariationStaleException`, and the file on disk is proven unchanged (still the real v1 record) afterward. |
| `Step092_NegativePath_CorruptFile_ThrowsCorruptException` | A malformed on-disk JSON file throws `MotifVariationCorruptException` rather than propagating a raw deserialization exception or silently returning a default. |
| `Step092_NegativePath_InvalidPlan_RejectsBeforeAnyWrite_ViaCatalog` | `MotifVariationCatalog.SaveAsync` rejects a structurally invalid plan (empty `Occurrences`) before ever touching disk — no file is created. |

### 3.3 Full-repository regression (`dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"`)

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 27/27 |
| XlightsAI.Studio.Audio.Tests | Passed 23/23 |
| XlightsAI.Studio.Editing.Tests | Passed 53/53 |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.Composition.Tests | Passed 69/69 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.Rendering.Tests | Passed 20/20 |
| **XlightsAI.Studio.WholeSong.Tests** | **Passed 37/37** (24 Step 090/091 + 13 new Step 092) |
| XlightsAI.Studio.AiEdits.Tests | Passed 69/69 |
| XlightsAI.Studio.Shell.Tests | Passed 199/199 (filtered: `Category!=Integration`) |
| XlightsAI.Studio.Refinement.Tests | Passed 78/78 |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |
| XlightsAI.Studio.Projects.Tests, XlightsAI.Studio.Xlights.Tests, XlightsAI.Studio.Display.Tests | No test matched `Category!=Integration` (pre-existing; same disclosed reason every prior phase's evidence already recorded) |

**Every project ran clean; zero failures anywhere in the full filtered regression.** Every project this step
did not touch shows the exact same pass count as Step 091's own last-recorded evidence
(`implementation-evidence/PHASE-14/STEP-091.md` §3.3), confirming this step introduced no regression.

## 4. Observable result / "manual demonstration"

This step's own "User-visible outcome," verbatim: "Versioned, inspectable project output plus a
reproducible test report." The domain half is directly and fully exercised by `Step092_Acceptance_Passes`:
a real `MotifVariationPlan` is scheduled from three motif occurrences with a proven climax-lift invariant
(see §3.2/§6 item 1), the plan is persisted via the real `MotifVariationCatalog`/`JsonMotifVariationPlanStore`,
and **reloaded from a brand-new catalog/store instance** over the same on-disk directory — proving the
scheduled output is genuinely versioned and inspectable independent of the in-memory object that produced
it, not merely asserted in-process. The "reproducible test report" half is this very evidence file plus the
xUnit test run output reproduced verbatim in §2/§3.2.

**No Avalonia UI screen was run or screenshotted for this step** — see §6 item 6 for the disclosed decision,
mirroring Steps 090/091's own precedent: PHASE-14.md Section 5's SCREEN 03/12/13 wireframes span the whole
090–093 step range, and this step's own required acceptance text ("Final chorus preserves recognizable
motif with intentional lift") is fully satisfiable and testable as a domain/persistence slice without a new
screen. This is recorded as **NOT VERIFIED for the UI-screen portion specifically**, not silently assumed.

No installed xLights executable exists on this machine (same disclosed gap as every prior phase). Per this
project's own standing memory waiver, the real-xLights human-verification check is not required as a
phase-gate blocker for this step; it is also not literally applicable to this step's own scope yet (this
step produces previewed `MotifOccurrenceVariation` effect lists, not an exported `.xsq` — XSQ export/audition
is Step 093's own later, disclosed scope per PHASE-14.md's own numbered-step table), so there is nothing new
to waive here beyond carrying the existing standing waiver forward.

## 5. Fixtures

No binary fixture files were added under `fixtures/`. Every `ChoreographyTemplateAnalysis`/`ChoreographyTemplate`/
`ChoreographyTemplateEffect`/`PhraseSegmentationAnalysis`/`PhraseCandidate` in `Step092AcceptanceTests` is
built directly in the test file via real production constructors (no mocking), the same convention every
prior PHASE-08/10/12/13/Step-090/091 test already established. There is no external source audio/analysis
file this step reads, so there are no meaningful input hashes to record beyond the deterministic in-memory
fixture values already visible in the test source
(`tests/XlightsAI.Studio.WholeSong.Tests/Step092AcceptanceTests.cs`) — each run constructs fresh
`Guid.NewGuid()` identities, so nothing here is content-addressed the way a real audio asset's SHA-256 would
be.

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **"Climax scheduling" is a deterministic algorithm, not a caller-supplied flag (disclosed, deliberate,
   and the central new design decision of this step).** This step's own title, verbatim: "...and climax
   scheduling." Rather than asking the caller to mark one occurrence `IsClimax` by hand (which this step's
   own `PhraseSeamAssembler`-style "explicit caller choice, never auto-derived" convention would otherwise
   suggest), `MotifVariationScheduler.Schedule` itself computes, per distinct `TemplateId` across the whole
   request set, which single occurrence carries the highest `SectionEnergy` (first-in-request-order
   tie-break) and schedules **only that one** as the climax. This is deliberately provable, not merely
   plausible: `ComputeMultiplier`'s own doc comment derives that for any energy in `[0,1]`, a non-climax
   occurrence's multiplier always lies in `[0.6, 1.1]`, while a climax occurrence's multiplier is
   `min(baseMultiplier + 0.2, 1.25)` where `baseMultiplier` is, by construction, the *maximum* base
   multiplier among every occurrence sharing that template — so the climax's own multiplier is always
   strictly greater than every other same-template occurrence's own multiplier, regardless of the actual
   energy values supplied. This realizes "Final chorus preserves recognizable motif with intentional lift"
   (required acceptance, verbatim) as a structural guarantee of the scheduler itself, not a coincidence of
   one fixture's chosen numbers.
2. **`MotifVariationScheduler` does not yet consume `SongDirectionPlan` (Step 090's own output) or
   `PhraseAssemblyResult` (Step 091's own output) directly (disclosed, scope boundary, mirrors Step 091's
   own §6 item 5).** This step's own required scope — "Apply motif variations by occurrence and climax
   scheduling" — is fully testable and satisfiable as a pure scheduling operation over an explicit
   `(TemplateId, TargetPhraseId, SectionEnergy)` input per occurrence; wiring an application-layer
   orchestrator that derives those inputs from `SongDirectionPlan.Sections[].TargetEnergy` (matched to each
   section's own `MotifGroupIds`) plus the already-accepted `ChoreographyTemplateAnalysis` is a
   composition-root/UI-command concern left as an explicit, disclosed follow-up rather than speculatively
   built now.
3. **Intensity scaling is a new, additive `"Intensity."` parameter-key convention, applied uniformly to every
   previewed effect regardless of inherited-vs-override provenance (disclosed, deliberate).** PHASE-14.md
   Section 2 explicitly forbids inventing xLights effect parameters ("do not invent xLights effect
   parameters"). This step never adds, removes or renames any parameter key — it only rescales the existing
   numeric *value* of a parameter the template's own effect already declares under this step's own
   documented `"Intensity."` prefix convention (mirroring `ChoreographyTemplatePlanner.PaletteParameterPrefix`'s
   own established `"Palette."` convention), clamped to `[0,1]` and never fabricated when the key is absent.
   Because `IChoreographyTemplatePlanner.PreviewInstance`'s own output carries no per-effect provenance tag,
   this step does not attempt to reverse-engineer its internal deterministic-hash effect-id scheme to exclude
   override-sourced effects from scaling; scaling is applied to the whole previewed effect list uniformly,
   while `MotifOccurrenceVariation.InheritedSourceEffectIds`/`InstanceOverrideSourceEffectIds` separately and
   accurately classify each template-effect *slot* (not each final output effect) for display, at the same
   granularity `ChoreographyTemplate.Effects`/`ChoreographyInstance.Overrides` are themselves keyed by. This
   is a disclosed, deliberate simplification over a more precise per-effect provenance match, left as a
   known limitation (§7).
4. **A motif group with exactly one requested occurrence is trivially its own climax (disclosed, degenerate
   but well-defined).** `Step092_SingleOccurrence_IsItsOwnClimax` proves this is not a crash or an
   unspecified state — the single occurrence is both the only entry and (vacuously) the maximum-energy entry
   in its own group.
5. **No automatic uniqueness/cardinality constraint across motif groups or templates (disclosed,
   deliberate).** Nothing prevents two unrelated templates from each independently having their own climax
   occurrence scheduled in the same `Schedule` call — "climax" is scoped per `TemplateId`, not globally
   per song or per `Schedule` call, consistent with a song potentially reusing more than one distinct motif,
   each with its own final-occurrence lift.
6. **No new `ViewModel`/Avalonia view (disclosed, mirrors Steps 090/091's own precedent).** This step's own
   required acceptance text ("Final chorus preserves recognizable motif with intentional lift") is a property
   of the scheduled `MotifVariationPlan`/`MotifOccurrenceVariation` data, fully provable as a
   domain/persistence slice; it does not literally require a new interactive screen. PHASE-14.md Section 5's
   SCREEN wireframes are phase-wide (span Steps 090–093), not specific to this one numbered step. Recorded as
   **NOT VERIFIED** for the UI-screen portion (§4), not silently skipped.

## 7. Known risks / unresolved issues

- **No UI screen was built or exercised for this step** — see §6 item 6 and §4. The scheduled-plan outcome
  is proven only as a persisted, reloadable data value, not as a rendered, interactive Avalonia view.
- **No real xLights executable or audio device was exercised** — consistent with every prior phase/step; not
  literally applicable yet (no `.xsq` export happens until Step 093), but carried forward as a standing,
  disclosed gap per the project's own memory waiver.
- **`InheritedSourceEffectIds`/`InstanceOverrideSourceEffectIds` classify template-effect *slots*, not the
  final per-output `EffectInstance` list** — by design (§6 item 3): a named inherited slot could still be
  absent from `Effects` if `PreviewInstance` itself excluded it (a locked destination conflict, or a
  non-fitting `Copy`-timing window); that specific exclusion is always separately named in
  `ExcludedReasons`, never silently hidden, but the two lists are not guaranteed to be in 1:1 correspondence
  by effect identity.
- **`MotifVariationScheduler` does not yet consume `SongDirectionPlan`/`PhraseAssemblyResult` directly** — by
  design (§6 item 2); a future application-layer orchestrator is expected to derive this step's own
  `MotifOccurrenceRequest` inputs from those upstream artifacts without requiring any change to this step's
  own already-accepted contract.
- **Climax scheduling operates only within one `Schedule` call's own request set** — if a motif group's
  occurrences are split across two separate `Schedule` calls (e.g. a UI incrementally adding occurrences),
  each call independently re-derives its own climax from whatever subset it was given; a stable, global
  "this template's climax is always the final chorus" designation across incremental calls is left to a
  future caller always supplying the full occurrence set for a given template in one call.

## 8. Required final checklist (Step 092 only)

- [x] Implementation / tests / observable demo (automated, real production types, no mocking; UI-screen
      portion explicitly recorded as NOT VERIFIED, not faked — see §4) / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 warnings, 0 errors).
- [x] New-test run: 37/37 passed (`XlightsAI.Studio.WholeSong.Tests`, 24 pre-existing Step 090/091 + 13 new
      Step 092).
- [x] Full existing regression suite re-run with `--filter "Category!=Integration"`; every project passes
      with 0 failures; no project this step did not touch regressed from its own last-recorded Step 091
      evidence (§3.3).
- [x] "Manual" demonstration performed against reproducible, real-production-type test data covering the
      domain/persistence portion of this step's own outcome text; the UI-screen portion is explicitly
      recorded as not built/not verified, not claimed.
- [x] No previously accepted source file from PHASE-01 through PHASE-13, or from Steps 090/091, was
      modified; only 9 new domain/exception files, 3 new persistence/catalog files, and 1 new test file
      added under `src/XlightsAI.Studio.WholeSong/` and `tests/XlightsAI.Studio.WholeSong.Tests/`
      respectively; no `.csproj` file changed at all.
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).

**Gate for Step 092: PASS.** This step's own numbered-step scope — reapply an already-defined motif template
across every requested occurrence via the already-accepted, reused inheritance/mask/lock machinery, schedule
exactly one occurrence per template as a provably-stronger "climax," apply a controlled, bounded intensity
variation to existing effect parameters without ever inventing a new one, and classify every occurrence's
own inherited vs instance-override effect slots for display — is fully implemented and tested. The
UI-screen half of PHASE-14.md Section 5's wireframe references remains explicitly NOT VERIFIED (no screen
was built), consistent with Steps 090/091's own established precedent. Step 093 remains unimplemented and
out of this step's own scope.
