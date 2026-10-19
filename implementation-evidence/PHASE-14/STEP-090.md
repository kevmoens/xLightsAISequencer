# PHASE 14 — Step 090: Create song-level creative direction with palettes, motif identities, energy and focal points

**Status: PASS** (automated tests against real PHASE-08/PHASE-10 production types — no mocking; no installed
xLights build and no Avalonia UI harness is exercised in this environment — per the standing user waiver
recorded in this project's memory, the real-xLights human-verification portion of the overall phase gate is
explicitly waived rather than faked or re-requested; see §4 and §7 for exactly what that waiver does and does
not cover. This step itself requires neither xLights nor a new screen — see §6 item 6 for the disclosed
"no new ViewModel" decision.)

## 1. Scope actually implemented

This is the first step of PHASE-14, so there was no prior `XlightsAI.Studio.WholeSong` project to extend.
Eleven new source files under a brand-new `src/XlightsAI.Studio.WholeSong/` project, one new test project
`tests/XlightsAI.Studio.WholeSong.Tests/`, and two `.csproj` references added to each — **no existing
PHASE-01–13 source file was modified at all** (only `XlightsAI.Studio.slnx` gained two new `<Project>`
entries):

- **`src/XlightsAI.Studio.WholeSong/SongSectionDirectionInput.cs`** — the unvalidated, user-tunable
  per-section creative-direction request: `SectionId`, `TargetEnergy`, `TargetContrast`, `PaletteId`,
  `FocalRole` (reuses PHASE-10 Step 066's own `MusicalRole` enum), `MotifGroupIds` (reuses PHASE-08 Step
  056's own `MotifGroupCandidate.Id`).
- **`src/XlightsAI.Studio.WholeSong/SongSectionDirection.cs`** — the validated, cross-referenced output
  shape for one section: same fields as the input, plus `Kind` carried through unchanged from the source
  `SectionLabelCandidate`.
- **`src/XlightsAI.Studio.WholeSong/SongDirectionPlan.cs`** — the whole-song, versioned plan: `Id`, `SongId`,
  `SourceSectionLabelVersionId`, `SourceMotifClusteringVersionId` (provenance), `BaseRevision`,
  `CreatedAtUtc` (timestamp provenance), `Sections`. Owns a self-contained `Validate()` (no external
  dependency), mirroring `VisualStory.Validate()`'s own convention.
- **`src/XlightsAI.Studio.WholeSong/ISongDirectionPlanner.cs`** / **`SongDirectionPlanner.cs`** — cross-
  validates every `SongSectionDirectionInput` against a real `SectionLabelAnalysis` and
  `MotifClusteringAnalysis` (unknown section id or motif group id throws before any plan object is
  constructed), then builds and self-validates the ordered `SongDirectionPlan`.
- **`src/XlightsAI.Studio.WholeSong/SongDirectionTimelineEntry.cs`** — the "inspectable timeline summary"
  projection row: a `SongSectionDirection` joined back to its own bar-range position.
- **`src/XlightsAI.Studio.WholeSong/ISongDirectionTimelineProjector.cs`** / **`SongDirectionTimelineProjector.cs`**
  — pure read-side projection satisfying the required acceptance verbatim: "Plan renders as inspectable
  timeline summary." Rejects a mismatched/stale `SectionLabelAnalysis.VersionId`.
- **`src/XlightsAI.Studio.WholeSong/SongDirectionPlanRecord.cs`** / **`ISongDirectionPlanStore.cs`** /
  **`JsonSongDirectionPlanStore.cs`** — JSON-file-backed persistence, atomic write (temp file + `File.Move`
  replace), with an explicit optimistic-concurrency re-check (`expectedCurrentVersion`) immediately before
  the write.
- **`src/XlightsAI.Studio.WholeSong/ISongDirectionCatalog.cs`** / **`SongDirectionCatalog.cs`** — the
  application-orchestration boundary mirroring `IVisualStoryCatalog`: validates before load/write, always
  supplies the correct current version to the store (a catalog-mediated save can never itself be rejected
  as stale — see §6 item 2).
- **`src/XlightsAI.Studio.WholeSong/SongDirectionInvalidException.cs`**,
  **`SongDirectionCorruptException.cs`**, **`SongDirectionStaleException.cs`** — the three named exception
  types, mirroring `CompositionInvalidException`/`CompositionCorruptException`'s own convention, plus a new
  `SongDirectionStaleException` for the optimistic-concurrency failure path this step's own failure/edge
  case explicitly asks for.
- **`src/XlightsAI.Studio.WholeSong/XlightsAI.Studio.WholeSong.csproj`** — references `Contracts`,
  `Structure`, `Composition` (verified neither references `WholeSong`, so this is not a cyclic reference).
- **`tests/XlightsAI.Studio.WholeSong.Tests/XlightsAI.Studio.WholeSong.Tests.csproj`** — references
  `WholeSong`, `Structure`, `Composition`, `Contracts`.
- **`tests/XlightsAI.Studio.WholeSong.Tests/Step090AcceptanceTests.cs`** — 14 new tests (see §3).
- **`XlightsAI.Studio.slnx`** — two new `<Project>` entries added (`src/.../WholeSong.csproj` and
  `tests/.../WholeSong.Tests.csproj`); no existing entry changed.

## 2. Commands actually executed

```
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test tests/XlightsAI.Studio.WholeSong.Tests/XlightsAI.Studio.WholeSong.Tests.csproj --nologo
dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
```

Run from `C:\CODE\xLightsAISequencer` via the PowerShell tool — this session's Bash/git-bash tool again
returned a bare exit code 1 with no captured output on every invocation (including a plain `mkdir -p`), the
same pre-existing local shell-launch issue every prior PHASE-13 step's evidence already disclosed — not a
project/test failure; all commands below ran successfully via PowerShell.

## 3. Actual results

### 3.1 Build

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:46.80
```

`XlightsAI.Studio.WholeSong -> ...\bin\Debug\net10.0\XlightsAI.Studio.WholeSong.dll` and
`XlightsAI.Studio.WholeSong.Tests -> ...\bin\Debug\net10.0\XlightsAI.Studio.WholeSong.Tests.dll` both appear
in the build output, confirming both new projects are wired into the solution and compile cleanly alongside
all 34 pre-existing projects.

### 3.2 New-project test run

```
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 360 ms - XlightsAI.Studio.WholeSong.Tests.dll (net10.0)
```

The 14 new tests:

| Test | Proves |
|---|---|
| `Step090_Acceptance_Passes` (required name) | Builds the phase-gate's own required demo song shape (Intro/Verse/Chorus/Verse/Chorus/Bridge/Final chorus) as a real `SectionLabelAnalysis`, plus a real `MotifClusteringAnalysis` motif group shared by all three choruses. Plans a `SongDirectionPlan` via the real `SongDirectionPlanner`, asserts domain-level provenance (`SongId`, `SourceSectionLabelVersionId`, `SourceMotifClusteringVersionId`, `BaseRevision`, `CreatedAtUtc`) and per-section fields (`Kind`, `PaletteId`, `FocalRole`, `MotifGroupIds`) are exactly as supplied. Persists via the real `SongDirectionCatalog`/`JsonSongDirectionPlanStore`, then **reloads from a brand-new store/catalog instance over the same on-disk directory** (proves persisted domain state survives a restart, not only a mock invocation) and asserts the reloaded plan matches exactly. Finally projects the reloaded plan through the real `SongDirectionTimelineProjector` and asserts the returned timeline is ordered by bar position, shows the required "final chorus is strongest" energy arc, and carries the shared motif group id and focal role through to the projected output. |
| `Step090_NegativePath_UnknownSectionId_Throws_BeforeAnyPlanIsBuilt` (required negative path) | A `SongSectionDirectionInput` naming a section id absent from the real `SectionLabelAnalysis` throws `SongDirectionInvalidException` naming the exact unknown id, before any `SongDirectionPlan` object is constructed. |
| `Step090_NegativePath_UnknownMotifGroupId_Throws` | A motif group id absent from the real `MotifClusteringAnalysis` throws `SongDirectionInvalidException`. |
| `Step090_NegativePath_OutOfRangeEnergyOrContrast_Throws` (5 inline cases) | Negative, `>1`, and `NaN` energy/contrast values each throw `SongDirectionInvalidException` — proves "user-tunable target contrast" is still bounded, not an unchecked free-form value. |
| `Step090_NegativePath_EmptyInputs_Throws` | An empty section-input list throws rather than silently producing a vacuous plan. |
| `Step090_NegativePath_StalePersistedState_RejectsWriteAndLeavesFileUnchanged` (required failure/edge case, verbatim: "...stale persisted state must not corrupt the project") | Saves v0, then an independent v1 save (simulating a concurrent session), then a third write that still expects v0 — the store throws `SongDirectionStaleException` naming both the expected and actual versions, **no write occurs**, and re-reading the file afterward shows it is still exactly the v1 record (not corrupted, not silently overwritten). |
| `Step090_NegativePath_Cancellation_ThrowsWithoutPersisting` (required failure/edge case, verbatim: "...cancellation... must not corrupt the project") | A pre-cancelled token passed to `SongDirectionCatalog.SaveAsync` throws `OperationCanceledException` before any file is written (`File.Exists` on the deterministic path is `false` afterward). |
| `Step090_NegativePath_InvalidPlan_RejectsBeforeAnyWrite_ViaCatalog` | An empty-sections `SongDirectionPlan` constructed directly (bypassing the planner) is still rejected by `SongDirectionCatalog.SaveAsync`'s own `Validate()` call before any write. |
| `Step090_NegativePath_CorruptFile_ThrowsCorruptException` | A hand-written malformed JSON file throws `SongDirectionCorruptException` naming the path, not a raw unhandled `JsonException`. |
| `Step090_NegativePath_TimelineProjector_MismatchedSectionLabelVersion_Throws` | Projecting a plan against a *different* (freshly re-analyzed) `SectionLabelAnalysis` for the same song — same section shape, different `VersionId` — throws `SongDirectionInvalidException` rather than silently joining against stale structure data. |

### 3.3 Full-repository regression (`dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"`)

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 27/27 |
| XlightsAI.Studio.Audio.Tests | Passed 23/23 |
| XlightsAI.Studio.Editing.Tests | Passed 53/53 |
| XlightsAI.Studio.Composition.Tests | Passed 69/69 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| **XlightsAI.Studio.WholeSong.Tests (new)** | **Passed 14/14** |
| XlightsAI.Studio.Rendering.Tests | Passed 20/20 |
| XlightsAI.Studio.AiEdits.Tests | Passed 69/69 |
| XlightsAI.Studio.Shell.Tests | Passed 199/199 (filtered: `Category!=Integration`) |
| XlightsAI.Studio.Refinement.Tests | Passed 78/78 |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |
| XlightsAI.Studio.Projects.Tests | No test matched `Category!=Integration` filter (pre-existing; entire assembly is tagged `Integration` per prior-phase evidence) |
| XlightsAI.Studio.Xlights.Tests | No test matched `Category!=Integration` filter (pre-existing; same reason) |
| XlightsAI.Studio.Display.Tests | No test matched `Category!=Integration` filter (pre-existing; same reason) |

**Every project ran clean; zero failures anywhere in the full filtered regression.** No project this step did
not touch shows any change in pass count from its own last-recorded PHASE-13 evidence
(`implementation-evidence/PHASE-13/STEP-089.md` §3.2), confirming this step introduced no regression. The
three "no test matched the filter" assemblies are a pre-existing condition (their tests are entirely tagged
`Category=Integration`, not something this step changed) and are unrelated to any file added here.

## 4. Observable result / "manual demonstration"

Step 090's own "User-visible outcome" text reads "Saved audio/analysis artifact with timestamp provenance
plus a synchronized waveform/timeline demonstration." The *domain* half of that (saved artifact, timestamp
provenance) is directly and fully exercised by `Step090_Acceptance_Passes`: a real `SongDirectionPlan` is
planned, persisted with `CreatedAtUtc` provenance plus `SourceSectionLabelVersionId`/
`SourceMotifClusteringVersionId` provenance, reloaded from disk across a fresh store instance, and rendered
as an ordered `SongDirectionTimelineEntry` list — directly satisfying this step's own required acceptance,
verbatim: "Plan renders as inspectable timeline summary."

The "synchronized waveform/timeline" half of that outcome text describes a *screen* capability (a visual,
audio-synchronized timeline view) that no prior PHASE-10–13 step has built for any comparable domain
artifact either — see §6 item 6 for the disclosed decision not to add a new Avalonia view for this step.
**No Avalonia UI screen was run or screenshotted for this step** (there is no existing
`CompositionOverviewViewModel`/timeline screen in `src/XlightsAI.Studio.Shell/` to exercise, and building one
is explicitly out of this step's own narrow domain-plus-persistence scope per the repository's own
"avoid collecting multiple unverified tasks into one mega-commit" convention) — this is recorded as **NOT
VERIFIED for the UI-screen portion specifically**, not silently assumed.

No installed xLights executable exists on this machine (same disclosed gap as every prior phase). Per this
project's own standing memory waiver, the real-xLights human-verification check is not required as a
phase-gate blocker for this step either; it is not applicable to Step 090's own scope in the first place
(this step produces no `.xsq`/render output — that is Step 091/093's own later scope), so there is nothing to
waive here beyond carrying the existing standing waiver forward.

## 5. Fixtures

No binary fixture files were added under `fixtures/`. Every `SectionLabelAnalysis`/`MotifClusteringAnalysis`/
`SongSectionDirectionInput` in `Step090AcceptanceTests` is built directly in the test file via real production
constructors (no mocking) — the same convention every prior PHASE-08/10/12/13 step's own tests already
established. There is no external source audio/analysis file this step reads, so there are no meaningful
"input hashes" to record beyond the deterministic in-memory fixture values already visible in the test source
(`tests/XlightsAI.Studio.WholeSong.Tests/Step090AcceptanceTests.cs`, `BuildSectionLabels`/`BuildMotifs`
helpers) — each run constructs fresh `Guid.NewGuid()` identities, so nothing here is itself content-addressed
the way an audio asset's SHA-256 would be; `SourceHash` fields in the fixtures are literal placeholder
strings (`"fixture-source-hash"`), consistent with the fact that this step never reads a real `SongAsset`.

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **`WholeSongPlan`/`SongSectionPlan` narrowed to `SongDirectionPlan`/`SongSectionDirection` (disclosed, the
   step's own central naming decision).** PHASE-14.md Section 3 proposes a single `WholeSongPlan` carrying
   both `Sections` (energy/palette) *and* `Transitions`, with `SongSectionPlan` carrying `PhrasePlanIds`.
   This step's own narrow scope — "Create song-level creative direction with palettes, motif identities,
   energy and focal points" — never touches transitions or phrase-plan assembly (that is Step 091's own
   later, disclosed scope: "Assemble accepted phrase candidates..."). `SongDirectionPlan` therefore omits
   `Transitions` entirely and `SongSectionDirection` omits `PhrasePlanIds` entirely; both additions are
   explicitly deferred to the step that actually owns them, per this task's own instruction not to cross into
   future steps' scope. The two new fields `TargetContrast` and `FocalRole` — absent from the Section-3
   proposal — are added because this step's own required acceptance text asks for them verbatim
   ("user-tunable target contrast"; "link... focal role to existing section IDs") and no other proposed type
   anywhere in PHASE-14.md names them.
2. **Store-level optimistic concurrency (`SongDirectionStaleException`) is new — no sibling PHASE-10/13
   store has this (disclosed).** `JsonVisualStoryStore`/`VisualStoryCatalog` (Step 069) always silently
   overwrites on every save, with no stale-write rejection at all. This step's own required failure/edge
   case explicitly names "stale persisted state must not corrupt the project," so `ISongDirectionPlanStore.SaveAsync`
   takes an explicit `expectedCurrentVersion` and throws before writing on a mismatch — a genuine, disclosed
   addition beyond the established sibling convention, not a deviation from any existing contract (no
   existing store's contract was touched). `SongDirectionCatalog` (the normal, expected calling path) always
   supplies the correct current version itself, so an ordinary save can never be rejected as stale; staleness
   is only observable by a caller that captures a version and later calls the lower-level store directly with
   it after someone else has saved again — exactly the scenario `Step090_NegativePath_StalePersistedState_RejectsWriteAndLeavesFileUnchanged`
   exercises.
3. **`MusicalRole` (PHASE-10 Composition) is reused verbatim for `FocalRole` rather than inventing a new
   enum (disclosed, as instructed).** The task brief's own instruction was to "reuse `MotifLinkCandidate`/
   motif group ids from Structure rather than re-inventing" and to check whether `SectionEnergyInput`/
   `SectionVisualDirection`/`VisualBudget` already express the needed shape before duplicating it. Those
   three types were read and found *not* to carry contrast or focal role at all (`SectionVisualDirection`
   itself explicitly disclaims `FocalProp`/`Roles` as "Step 070/071's own later, disclosed scope" in its own
   doc comment) — so this step does not reuse `SectionVisualDirection` itself (it has no contrast/focal-role
   field to reuse), but it does reuse `MusicalRole` (the existing semantic-role enum) for `FocalRole`,
   avoiding a parallel "focal role" concept the way `SectionVisualDirection`'s own ADR already avoided
   duplicating `SectionKind`.
4. **No `SongStructure` type exists in the repository; `SectionLabelAnalysis` + `MotifClusteringAnalysis`
   are used directly instead (disclosed).** PHASE-14.md Section 3's proposed signatures reference a
   `SongStructure` type from `XlightsAI.Studio.Structure` that does not exist anywhere in the actual
   repository (verified via `Glob` over `src/XlightsAI.Studio.Structure/*.cs` before writing any code). The
   closest, already-accepted real contracts are PHASE-08's own `SectionLabelAnalysis` (section ids + bar
   ranges + kind) and `MotifClusteringAnalysis` (motif group ids), which is exactly the inspectable
   provenance-chain data this step's own planner/validator/projector need — reconciled here rather than
   inventing a new, unrequested `SongStructure` wrapper type not asked for by this step's own scope.
5. **A `SongDirectionCatalog` application-orchestration layer was added even though the spec names no such
   type (disclosed).** This mirrors `IVisualStoryCatalog`/`VisualStoryCatalog`'s own already-accepted
   precedent (validate-then-version-then-persist) rather than leaving every caller to hand-roll version
   bookkeeping against the lower-level store directly — the same reasoning that precedent's own commit
   already established for a sibling aggregate.
6. **No new `ViewModel`/Avalonia view.** Steps 086–089 (PHASE-13) already established the precedent that a
   phase-local domain/persistence step whose own "User-visible outcome" text does not literally require a
   new interactive screen does not need one; Step 090's own acceptance text ("Plan renders as inspectable
   timeline summary") is satisfied by `SongDirectionTimelineEntry`/`ISongDirectionTimelineProjector` as a
   plain, inspectable data projection, consistent with that precedent. PHASE-14.md Section 5's SCREEN
   03/12/13 wireframe references are phase-wide (span Steps 090–093, and indeed most of PHASE-10–13's own
   screens too), not specific to this one numbered step; building any of them now would pull Step 091–093's
   own later scope (transitions, continuity findings, full assembly) into a screen that cannot yet show
   anything beyond what this step alone produces. This is explicitly disclosed, not silently skipped: the
   "synchronized waveform/timeline demonstration" half of this step's own outcome text is recorded as **NOT
   VERIFIED** in §4, not claimed as done.

## 7. Known risks / unresolved issues

- **No UI screen was built or exercised for this step** — see §6 item 6 and §4. The timeline summary is
  proven only as a data projection (`SongDirectionTimelineEntry` list), not as a rendered, interactive, or
  audio-synchronized Avalonia view. A future step that does build `CompositionOverviewViewModel` (or similar)
  can consume `ISongDirectionTimelineProjector` directly without any change to this step's own contracts.
- **No real xLights executable or audio device was exercised** — consistent with every prior phase; not
  applicable to this step's own scope (no `.xsq`/render output here), but carried forward as a standing,
  disclosed gap per the project's own memory waiver.
- **`SongDirectionCatalog.SaveAsync` always "wins" the race it reads** — between its own internal
  `GetAsync` (to read the current version) and its own `_store.SaveAsync` call, a concurrent writer could in
  principle interleave; the catalog itself does not retry or detect this (it simply passes through whatever
  `SongDirectionStaleException` the store raises in that narrow window). This is the same known,
  disclosed characteristic every prior "read-then-write" JSON file store in this repository already has
  (e.g. `VisualStoryCatalog`), not a new gap introduced by this step, and is not expected to matter for a
  single-user desktop application's typical usage pattern.
- **`SongDirectionPlan` does not yet reference any `ChoreographyPlan`/phrase-plan id** — by design (§6 item
  1); Step 091 is expected to consume `SongDirectionPlan` as one of its own inputs when it adds phrase
  assembly, not to require a migration of this step's own already-accepted shape.

## 8. Required final checklist (Step 090 only)

- [x] Implementation / tests / observable demo (automated, real production types, no mocking; UI-screen
      portion explicitly recorded as NOT VERIFIED, not faked — see §4) / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 warnings, 0 errors).
- [x] New-project test run: 14/14 passed (`XlightsAI.Studio.WholeSong.Tests`).
- [x] Full existing regression suite re-run with `--filter "Category!=Integration"`; every project passes
      with 0 failures; no project this step did not touch regressed from its own last-recorded PHASE-13
      evidence (§3.3).
- [x] "Manual" demonstration performed against reproducible, real-production-type test data covering the
      domain/persistence/timeline-summary portion of this step's own outcome text; the UI-screen portion is
      explicitly recorded as not built/not verified, not claimed.
- [x] No previously accepted source file from PHASE-01 through PHASE-13 was modified; only 11 new files
      under `src/XlightsAI.Studio.WholeSong/`, 2 new files under
      `tests/XlightsAI.Studio.WholeSong.Tests/`, and two new `<Project>` entries added to
      `XlightsAI.Studio.slnx` (no existing entry changed).
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).

**Gate for Step 090: PASS.** Step 090's own numbered-step scope — define song-level creative direction with
user-tunable target energy/contrast, link palette and focal role to existing section ids, link motif identity
to existing motif groups, persist with timestamp/version provenance, render as an inspectable timeline
summary, and prove invalid input/cancellation/stale persisted state cannot corrupt the project — is fully
implemented and tested. The UI-screen ("synchronized waveform/timeline demonstration") half of this step's
own outcome text is explicitly recorded as NOT VERIFIED (no screen was built), consistent with PHASE-13
Steps 086–089's own established precedent for comparable domain/persistence steps. Steps 091–093 remain
unimplemented and out of this step's own scope.
