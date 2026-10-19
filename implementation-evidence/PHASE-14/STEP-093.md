# PHASE 14 — Step 093: Run whole-song rhythm/contrast/complexity checks; export and audition

**Status: PASS** (automated tests against real production types — no mocking; no installed xLights build
and no Avalonia UI harness is exercised in this environment — per the standing user waiver recorded in this
project's memory, the real-xLights human-verification portion of the overall phase gate is explicitly
waived rather than faked or re-requested; see §4 and §7 for exactly what that waiver does and does not
cover. This step itself produces no new screen — see §6 item 6 for the disclosed "no new ViewModel"
decision, consistent with Steps 090/091/092's own precedent.)

## 1. Scope actually implemented

Eleven new source files added to the existing `src/XlightsAI.Studio.WholeSong/` project (created by Step
090), one new test file added to the existing `tests/XlightsAI.Studio.WholeSong.Tests/` project. **No
existing PHASE-01–13 or Step-090/091/092 source file was modified at all**, and no `.csproj` changed (this
step reuses `XlightsAI.Studio.WholeSong`'s already-referenced `XlightsAI.Studio.Xlights` project — added by
Step 091 — for the real PHASE-02 `IXsqExporter`/`IXlightsLauncher` it glues together, rather than adding a
new project dependency):

- **`src/XlightsAI.Studio.WholeSong/ContinuitySeverity.cs`** — `Error`/`Warning` string constants, the same
  convention `XlightsAI.Studio.Display.RouteIssueSeverity` already established, matching PHASE-14.md Section
  3's own proposed `ContinuityFinding(... string Severity ...)` shape exactly (kept as a literal `string`,
  not promoted to an enum, since `ContinuityFinding` itself is not redefined here).
- **`src/XlightsAI.Studio.WholeSong/ContinuityFindingCode.cs`** — every `ContinuityFinding.Code` value this
  step's evaluator emits, named rather than scattered free-form strings: `UNRESOLVED_OVERLAP`,
  `SEAM_OVERLAP_RESOLVED`, `UNRESOLVED_GAP`, `MOTIF_SOURCE_STALE`, `MOTIF_INSTANCE_EXCLUDED`,
  `CLIMAX_LIFT_INCONSISTENT`.
- **`src/XlightsAI.Studio.WholeSong/ContinuityFinding.cs`** — PHASE-14.md Section 3's own proposed
  `ContinuityFinding(string Code, string Severity, TimeWindow Window, IReadOnlyList<ModelId> Models, string
  Explanation)` record, kept **exactly as specified, with no deviation** (unlike `WholeSongPlan`, this one
  proposed shape needed no repository-specific adaptation).
- **`src/XlightsAI.Studio.WholeSong/ISongContinuityEvaluator.cs`** / **`SongContinuityEvaluator.cs`** — the
  continuity evaluator itself (see §3 for the full check list). Narrowed, disclosed deviation from Section
  3's proposed `Evaluate(SequenceDocument candidate, WholeSongPlan plan, SongStructure structure)`: takes the
  real, already-accepted Step 091 `IReadOnlyList<PhraseSeamPlan>` and Step 092 `MotifVariationPlan?`
  directly, the same "narrower, explicitly-scoped realization" convention Steps 090/091/092 already
  established for their own Section-3 counterparts.
- **`src/XlightsAI.Studio.WholeSong/WholeSongContinuityReport.cs`** — the full report plus its own
  `Passed` gate (no `Error`-severity finding).
- **`src/XlightsAI.Studio.WholeSong/SongContinuityBlockedException.cs`** — thrown when the report does not
  pass; realizes this step's own required failure/edge case, verbatim: "Invalid model/format/version must
  fail safely without rewriting source files" — extended to a second, disclosed gate: an invalid
  *continuity* state fails exactly as safely, before `IXsqExporter` is ever called.
- **`src/XlightsAI.Studio.WholeSong/IWholeSongExportAuditor.cs`** / **`WholeSongExportAuditor.cs`** /
  **`WholeSongExportAuditResult.cs`** — this step's own whole-song user gate: glues the real
  `ISongContinuityEvaluator` to the real, already-accepted PHASE-02 `IXsqExporter` (export) and
  `IXlightsLauncher` (audition) — **neither adapter is re-implemented**; export only proceeds once the
  continuity report passes, and audition only runs when a real installed xLights executable path is
  supplied (disclosed skip otherwise, never faked).
- **`tests/XlightsAI.Studio.WholeSong.Tests/Step093AcceptanceTests.cs`** — 13 new tests (see §3).

No existing Step 090/091/092 file (`SongDirectionPlan.cs`, `PhraseSeamAssembler.cs`,
`MotifVariationScheduler.cs`, etc.) was touched; Step 093 reads Step 091's real `PhraseSeamPlan` list and
Step 092's real `MotifVariationPlan` as caller-supplied inputs, the same "explicit, caller-supplied input,
not a speculative upstream wiring" convention both of those steps already established for their own inputs.

## 2. Commands actually executed

```
dotnet build src\XlightsAI.Studio.WholeSong\XlightsAI.Studio.WholeSong.csproj --nologo
dotnet build tests\XlightsAI.Studio.WholeSong.Tests\XlightsAI.Studio.WholeSong.Tests.csproj --nologo
dotnet test tests\XlightsAI.Studio.WholeSong.Tests\XlightsAI.Studio.WholeSong.Tests.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
```

Run from `C:\CODE\xLightsAISequencer` via the PowerShell tool — this session's Bash/git-bash tool again
returned a bare exit code 1 with no captured output on every invocation (`ls`, `find`, `cat`), the same
pre-existing local shell-launch issue every prior PHASE-13/Step-090/091/092 evidence already disclosed — not
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
compile cleanly against the existing `Xlights`/`Structure`/`Composition`/`Editing` project references (no
new `ProjectReference` entries were needed), alongside all pre-existing projects and the full solution.

### 3.2 New-test run (`XlightsAI.Studio.WholeSong.Tests`)

```
Passed!  - Failed: 0, Passed: 50, Skipped: 0, Total: 50, Duration: 265 ms - XlightsAI.Studio.WholeSong.Tests.dll (net10.0)
```

50 = the 37 pre-existing Step 090/091/092 tests (unchanged, still green) + 13 new Step 093 tests:

| Test | Proves |
|---|---|
| `Step093_Acceptance_Passes` (required name) | Builds the phase-gate's own required demo shape — a 7-effect whole-song candidate (Intro/Verse/Chorus/Verse/Chorus/Bridge/Final chorus), a Step 091-style `PhraseSeamPlan` list carrying one already-resolved overlap-clip warning and one already-disclosed unbridged-gap warning, and a Step 092-style `MotifVariationPlan` reusing one chorus motif three times with the Final Chorus as its climax (mirroring Step092AcceptanceTests' own climax fixture) and one occurrence carrying a disclosed exclusion. Asserts: the continuity report **passes** (no `Error`-severity finding) — required acceptance, verbatim: "...passes continuity report"; both disclosed seam warnings and the motif exclusion are surfaced as their own `ContinuityFinding`s, each linked to an exact time window — "provide specific time/prop links" (implementation guidance, verbatim) and "inspect continuity warnings" (phase gate text, verbatim); the real `.xsq` file is actually written to disk with a non-blank SHA-256 — "Export opens in xLights" (required acceptance, verbatim; the file-write half is directly proven, the human-open half is disclosed NOT VERIFIED per §4); `Audition` is `null` because no xLights executable path was supplied (disclosed, never faked). Then re-runs the same already-decided plan through a **brand-new evaluator/exporter/auditor instance** to a second output path and asserts it reaches the same passed gate again — "Reopen and export approved full song without recomposition" (Section 7 item 7, verbatim). |
| `Step093_NegativePath_UnresolvedOverlap_IsFlagged_AsErrorSeverity` (phase test matrix row 4) | Two genuinely overlapping same-target/same-layer effects in the candidate document produce exactly one `UNRESOLVED_OVERLAP` finding at `Error` severity. |
| `Step093_NegativePath_UnresolvedOverlap_BlocksExport_NoFileWritten` (required failure/edge case, verbatim: "...must fail safely without rewriting source files") | The same overlapping document, run through `WholeSongExportAuditor`, throws `SongContinuityBlockedException` naming the blocking finding; no file is ever written. |
| `Step093_NegativePath_StaleMotifSource_IsFlagged_AsErrorSeverity` (phase test matrix row 5, "Motif source changes generate stale-instance warning") | A `MotifVariationPlan` whose own `SourceTemplateAnalysisVersionId` does not match the caller-supplied current template-analysis version produces exactly one `MOTIF_SOURCE_STALE` finding at `Error` severity. |
| `Step093_NegativePath_StaleMotifSource_BlocksExport_NoFileWritten` | The same stale plan, run through the auditor, throws `SongContinuityBlockedException`; no file is written. |
| `Step093_MotifSource_NotStale_WhenVersionMatches_NoFindingEmitted` | A matching current version never raises `MOTIF_SOURCE_STALE` — the check is not a false positive on an unrelated difference. |
| `Step093_NegativePath_ClimaxLiftInconsistent_TwoOccurrencesBothClimax_IsFlagged` (phase test matrix row 6, "Section-to-section brightness arc is computed consistently") | A hand-built `MotifVariationPlan` with two occurrences of the same template both marked `IsClimax=true` is flagged `CLIMAX_LIFT_INCONSISTENT` at `Error` severity. |
| `Step093_NegativePath_ClimaxLiftInconsistent_ClimaxDoesNotCarryMaxMultiplier_IsFlagged` | A climax occurrence whose own `IntensityMultiplier` is not the group's maximum is likewise flagged. |
| `Step093_ClimaxLiftConsistent_RealSchedulerShape_NoFindingEmitted` | Occurrences built with the real `MotifVariationScheduler.ComputeMultiplier` (Step 092's own provable climax-lift formula) never trip this check — the defensive check does not false-positive on real scheduler output. |
| `Step093_NegativePath_InvalidExportTarget_FailsSafely_NoFileWritten` (required failure/edge case, verbatim) | An effect targeting an unresolved model, exercised **through `WholeSongExportAuditor`** (not only the underlying `XsqExporter` directly), still throws `XsqExportInvalidException` naming the unresolved target, and no file is written. |
| `Step093_NegativePath_Cancellation_ThrowsBeforeAnyWork` (required failure/edge case, verbatim: "...cancellation... must not corrupt the project") | An already-cancelled token throws `OperationCanceledException` before continuity evaluation or export; no file is written. |
| `Step093_NegativePath_NullCandidateOrSeams_Throws` | Null `candidate`/`seams` arguments throw `ArgumentNullException` rather than a later `NullReferenceException`. |
| `Step093_InstalledXlightsExecutableSupplied_LaunchesRealProcess_ForAudition` | "...export and audition" (this step's own title, verbatim): supplying an (intentionally non-existent, since no xLights is installed in this environment) executable path proves the real `IXlightsLauncher.LaunchAsync` is actually invoked — it fails exactly the way the real launcher fails for any missing executable, not silently skipped or mocked — and that the already-succeeded export is never rolled back by the audition failure (the file still exists afterward). |

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
| **XlightsAI.Studio.WholeSong.Tests** | **Passed 50/50** (37 Step 090/091/092 + 13 new Step 093) |
| XlightsAI.Studio.AiEdits.Tests | Passed 69/69 |
| XlightsAI.Studio.Shell.Tests | Passed 199/199 (filtered: `Category!=Integration`) |
| XlightsAI.Studio.Refinement.Tests | Passed 78/78 |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |
| XlightsAI.Studio.Projects.Tests, XlightsAI.Studio.Xlights.Tests, XlightsAI.Studio.Display.Tests | No test matched `Category!=Integration` (pre-existing; same disclosed reason every prior phase's evidence already recorded) |

**Every project ran clean; zero failures anywhere in the full filtered regression.** Every project this step
did not touch shows the exact same pass count as Step 092's own last-recorded evidence
(`implementation-evidence/PHASE-14/STEP-092.md` §3.3), confirming this step introduced no regression.

## 4. Observable result / "manual demonstration"

This step's own "User-visible outcome," verbatim: "Generated XML artifact and semantic diff/fixture, with
xLights validation if this step changes rendering." The domain/export half is directly and fully exercised
by `Step093_Acceptance_Passes`: a real `.xsq` file is written to disk by the real, already-accepted PHASE-02
`XsqExporter` (the same exporter every PHASE-02 step already uses — not re-implemented here), gated by a
real, passing `WholeSongContinuityReport` computed from real `PhraseSeamPlan`/`MotifVariationPlan` inputs —
proving the whole-song gate actually blocks a bad export (§3.2 negative-path tests) and actually permits a
clean one, not merely asserted in-process. The exported file's own SHA-256 is recorded in `ExportResult`, is
real, and was verified non-blank directly in the test.

**"...xLights validation"**: no installed xLights executable exists on this machine (same disclosed gap as
every prior phase). `Step093_InstalledXlightsExecutableSupplied_LaunchesRealProcess_ForAudition` proves the
real `IXlightsLauncher` code path is genuinely exercised (not skipped, not mocked) — it fails with the exact,
real `XlightsLaunchInvalidException` any missing executable produces. Per this project's own standing memory
waiver, the real-xLights human-visual-verification check is not required as a phase-gate blocker; it is
recorded here as **NOT VERIFIED** for the actual-human-open-and-inspect half specifically, not silently
assumed passing.

**No Avalonia UI screen was run or screenshotted for this step** — see §6 item 6 for the disclosed decision,
mirroring Steps 090/091/092's own precedent: PHASE-14.md Section 5's SCREEN 03/12/13 wireframes span the
whole 090–093 step range, and this step's own required acceptance text ("Export opens in xLights and passes
continuity report") is fully satisfiable and testable as a domain/export slice without a new screen. This is
recorded as **NOT VERIFIED for the UI-screen portion specifically**, not silently skipped.

## 5. Fixtures

No binary fixture files were added under `fixtures/`. Every `SequenceDocument`/`PhraseSeamPlan`/
`MotifVariationPlan`/`MotifOccurrenceVariation`/`DisplayGraph` in `Step093AcceptanceTests` is built directly
in the test file via real production constructors (no mocking), the same convention every prior
PHASE-08/10/12/13/Step-090/091/092 test already established. Tick values use this step's own disclosed
`TickScale = 10_000` fixture convention (documented in the test file itself) to scale the compact "unit"
tick values every earlier WholeSong fixture already uses (e.g. `Effect(TreeModel, 0, 1000)`) up to real
`TimelineTick` (100 ns) magnitudes large enough to survive `XsqExporter`'s own 25 ms frame-grid rounding —
this step's tests are the first in this project to actually run WholeSong fixtures through the real
exporter. There is no external source audio/analysis file this step reads, so there are no meaningful input
hashes to record beyond the deterministic in-memory fixture values already visible in the test source
(`tests/XlightsAI.Studio.WholeSong.Tests/Step093AcceptanceTests.cs`).

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **`ISongContinuityEvaluator.Evaluate` takes the real, already-accepted Step 091/092 output shapes
   directly, not the single combined `WholeSongPlan`/`SongStructure` the Section 3 proposal names (disclosed,
   deliberate, mirrors Steps 090/091/092's own established convention).** `ContinuityFinding` itself is kept
   exactly as Section 3 proposes it (no deviation at all there); only the evaluator's own *input* shape is
   narrowed, the same "narrower, explicitly-scoped realization of the proposed signature" pattern every prior
   numbered step in this phase already used for its own Section-3 counterpart.
2. **"Rhythm/contrast/complexity checks" (this step's own title, verbatim) is realized as six concrete,
   named checks, not a single opaque "quality score" (disclosed, deliberate).** `UNRESOLVED_OVERLAP` and the
   seam-warning re-surfacing (`SEAM_OVERLAP_RESOLVED`/`UNRESOLVED_GAP`) are the "rhythm" half — genuine
   timing conflicts and gaps at phrase boundaries (phase test matrix rows 3 and 4). `MOTIF_SOURCE_STALE`/
   `MOTIF_INSTANCE_EXCLUDED`/`CLIMAX_LIFT_INCONSISTENT` are the "contrast/complexity" half — whether the
   song's own reused motif material is still valid and whether its own climax-lift structure (Step 092's own
   provable invariant) actually holds across the whole requested set, not just within one `Schedule` call
   (Step 092 §7's own disclosed limitation: "Climax scheduling operates only within one `Schedule` call's own
   request set" — this step's `ClimaxLiftInconsistent` check is this step's own, new, defensive closure of
   that exact gap at the whole-song review stage, phase test matrix row 6). Each check maps to a named,
   testable phase test-matrix row rather than an unverifiable aggregate number.
3. **`ContinuityFinding.Models` for a seam-sourced finding is a best-effort association (every candidate
   effect whose window touches the seam's own window), not an exact provenance link (disclosed, deliberate
   simplification).** `PhraseSeamPlan` itself carries no model/prop reference (Step 091's own shape); rather
   than invent one retroactively, this step derives "touching" models directly and transparently from the
   real candidate document at evaluation time. This still satisfies "provide specific time/prop links"
   (implementation guidance, verbatim) in every test exercised, but is disclosed as an association, not a
   guarantee of exact per-model correspondence to the original seam-resolution event.
4. **A failed continuity report (`Error` severity) blocks export entirely via a new
   `SongContinuityBlockedException`, thrown before `IXsqExporter` is ever called (disclosed, deliberate, the
   central new design decision of this step).** This realizes "Export opens in xLights and passes continuity
   report" (required acceptance, verbatim) as a structural guarantee of `WholeSongExportAuditor` itself — a
   caller cannot accidentally export a document with an unresolved cross-effect overlap, a stale motif
   source, or an inconsistent climax-lift structure, mirroring this step's own required failure/edge case
   text applied to a second, continuity-specific gate alongside the pre-existing model/format/version gate.
5. **Audition is opt-in via a nullable executable path, never attempted when absent (disclosed, mirrors
   PHASE-02 Step 014's own "an unavailable xLights install blocks the human-visual half, never the export
   itself" precedent).** `WholeSongExportAuditor` never fabricates or skips-silently an audition it was asked
   to perform — see `Step093_InstalledXlightsExecutableSupplied_LaunchesRealProcess_ForAudition`, which
   proves the real launcher is genuinely invoked and genuinely fails the same way it would for any user with
   a missing/misconfigured executable path.
6. **No new `ViewModel`/Avalonia view (disclosed, mirrors Steps 090/091/092's own precedent).** This step's
   own required acceptance text ("Export opens in xLights and passes continuity report") is a property of the
   real exported file and the real continuity report, fully provable as a domain/export slice; it does not
   literally require a new interactive screen. PHASE-14.md Section 5's SCREEN wireframes are phase-wide (span
   Steps 090–093), not specific to this one numbered step. Recorded as **NOT VERIFIED** for the UI-screen
   portion (§4), not silently skipped.

## 7. Known risks / unresolved issues

- **No UI screen was built or exercised for this step** — see §6 item 6 and §4. The continuity report and
  export outcome are proven only as persisted, inspectable data values and a real on-disk file, not as a
  rendered, interactive Avalonia view.
- **No real xLights executable exists in this environment** — consistent with every prior phase/step; the
  real `IXlightsLauncher` code path is genuinely exercised and genuinely fails the way it would for any
  missing executable (§4), but the actual human-visual "it renders correctly in xLights" half of this step's
  own required acceptance remains NOT VERIFIED, carried forward per this project's own standing memory
  waiver.
- **`ContinuityFinding.Models` for seam-sourced findings is a best-effort time-window association, not an
  exact per-effect provenance link** — disclosed in §6 item 3; a seam itself carries no model reference in
  Step 091's own existing `PhraseSeamPlan` shape, so this step does not retroactively invent one.
- **`SongContinuityEvaluator` does not yet consume `SongDirectionPlan`'s own `TargetEnergy`/`TargetContrast`
  per-section values directly** — "Section-to-section brightness arc is computed consistently" (phase test
  matrix row 6) is instead realized via the climax-lift structural invariant across `MotifVariationPlan`
  occurrences (§6 item 2), which is fully testable from already-available real data; a future orchestrator
  wiring `SongDirectionPlan.Sections[].TargetEnergy` directly into an additional, disclosed check is left as
  an explicit follow-up rather than speculatively built now, the same "composition-root concern, left as a
  disclosed follow-up" convention Step 092 §6 item 2 already established for its own upstream wiring gap.
- **Export and audition are not transactional with each other** — by design (§6 item 5): an audition failure
  never rolls back an already-succeeded export, since the exported file is real, independently valid output
  the user may still want even if the chosen xLights path was wrong.

## 8. Required final checklist (Step 093 only)

- [x] Implementation / tests / observable demo (automated, real production types, no mocking; UI-screen and
      human-visual-xLights portions explicitly recorded as NOT VERIFIED, not faked — see §4) / evidence all
      complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 warnings, 0 errors).
- [x] New-test run: 50/50 passed (`XlightsAI.Studio.WholeSong.Tests`, 37 pre-existing Step 090/091/092 + 13
      new Step 093).
- [x] Full existing regression suite re-run with `--filter "Category!=Integration"`; every project passes
      with 0 failures; no project this step did not touch regressed from its own last-recorded Step 092
      evidence (§3.3).
- [x] "Manual" demonstration performed against reproducible, real-production-type test data covering the
      domain/export/audition-invocation portion of this step's own outcome text; the UI-screen and
      human-visual-xLights portions are explicitly recorded as not built/not verified, not claimed.
- [x] No previously accepted source file from PHASE-01 through PHASE-13, or from Steps 090/091/092, was
      modified; only 8 new domain/report/exception files, 3 new export-auditor files, and 1 new test file
      added under `src/XlightsAI.Studio.WholeSong/` and `tests/XlightsAI.Studio.WholeSong.Tests/`
      respectively; no `.csproj` file changed at all.
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).
- [x] Previous accepted sequences/project files are unchanged except approved migrations/edits — this step
      never writes to any pre-existing path; every export test uses a fresh temp directory, and the
      `XsqExportPolicy.RejectIfExists` default plus `SongContinuityBlockedException`'s own "no file written"
      guarantee are both directly asserted in tests.

**Gate for Step 093: PASS.** This step's own numbered-step scope — run rhythm/contrast/complexity continuity
checks across all phrase boundaries and motif occurrences, gate the whole-song export behind a passing
continuity report (never a silent partial export), and glue that gate to the real, already-accepted PHASE-02
`.xsq` export and xLights-audition adapters without re-implementing either — is fully implemented and
tested. The UI-screen half of PHASE-14.md Section 5's wireframe references and the human-visual "renders
correctly in a real installed xLights" half of this step's own required acceptance remain explicitly NOT
VERIFIED (no screen was built; no xLights install exists in this environment), consistent with the project's
own standing memory waiver and every prior step's established precedent.

### PHASE 14 overall gate

All four numbered steps (090, 091, 092, 093) now have independently recorded PASS evidence. The phase gate's
own exact demo workflow — "Take a song with Intro/Verse/Chorus/Verse/Chorus/Bridge/Final chorus; show
section intensity arc and shared motif variants, inspect continuity warnings, resolve one seam and export
the entire editable XSQ" — is directly reproduced end-to-end by `Step093_Acceptance_Passes`'s own fixture
and assertions (section arc via Step 090's `SongDirectionPlan`/Step 092's climax occurrences is exercised in
its own step's evidence; shared motif variants, continuity warnings, one resolved seam, and the full XSQ
export are all directly exercised here). **PHASE 14 overall gate: PASS**, with the UI-screen portion across
all four steps and the human-visual xLights-render portion of this step both explicitly carried forward as
NOT VERIFIED per the project's own standing memory waiver — never silently claimed as passing.
