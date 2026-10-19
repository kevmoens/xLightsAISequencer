# PHASE 14 — Step 091: Assemble accepted phrase candidates with transition-aware overlap/gap handling

**Status: PASS** (automated tests against real production types — no mocking; no installed xLights build
and no Avalonia UI harness is exercised in this environment — per the standing user waiver recorded in this
project's memory, the real-xLights human-verification portion of the overall phase gate is explicitly
waived rather than faked or re-requested; see §4 and §7 for exactly what that waiver does and does not
cover. This step itself produces no new screen — see §6 item 6 for the disclosed "no new ViewModel"
decision, consistent with Step 090's own precedent.)

## 1. Scope actually implemented

Nine new source files added to the existing `src/XlightsAI.Studio.WholeSong/` project (created by Step 090),
one new test file added to the existing `tests/XlightsAI.Studio.WholeSong.Tests/` project, and two
`ProjectReference` entries added to each project's own `.csproj` — **no existing PHASE-01–13 or Step-090
source file was modified at all**:

- **`src/XlightsAI.Studio.WholeSong/SeamStrategy.cs`** — the four explicit seam policies this step's own
  implementation guidance names verbatim: `Trim`, `Hold`, `Release`, `Bridge`.
- **`src/XlightsAI.Studio.WholeSong/PhraseAssemblyCandidate.cs`** — one already-accepted phrase's own
  effects scoped to its own canonical `TimeWindow`; effects are deliberately *not* required to lie fully
  inside the window (an effect bleeding past its own phrase boundary is exactly the "cross-phrase
  overlapping effect" scenario this step must detect and resolve). Owns a self-contained `Validate()`.
- **`src/XlightsAI.Studio.WholeSong/PhraseSeamRequest.cs`** — the caller's own explicit, unvalidated
  per-seam strategy choice (`FromPhraseId`, `ToPhraseId`, `Strategy`).
- **`src/XlightsAI.Studio.WholeSong/PhraseSeamPlan.cs`** — the validated, resolved outcome of one seam:
  `FromPhraseId`, `ToPhraseId`, `Window`, `Strategy`, `Warnings`. Deliberately *not* named `TransitionPlan`
  (PHASE-14.md Section 3's own proposed name) — see §6 item 1 for the disclosed name collision this avoids.
- **`src/XlightsAI.Studio.WholeSong/PhraseAssemblyResult.cs`** — the full assembly outcome: the new
  `SequenceDocument`, every resolved `PhraseSeamPlan` (in phrase order), and the flattened union of every
  seam's own warnings.
- **`src/XlightsAI.Studio.WholeSong/PhraseAssemblyInvalidException.cs`** / **`PhraseAssemblyExhaustedException.cs`**
  — the two named exception types realizing this step's own required failure/edge case, verbatim:
  "Malformed proposal, no-improvement and exhausted retry budget must not auto-commit."
- **`src/XlightsAI.Studio.WholeSong/IPhraseSeamAssembler.cs`** / **`PhraseSeamAssembler.cs`** — the
  assembler itself (see §3 for the full algorithm). Constructor-injects the already-accepted
  `XlightsAI.Studio.Editing.IEffectLockPolicy`, the same dependency `ChoreographyCompiler` (PHASE-10 Step
  071) already takes.
- **`src/XlightsAI.Studio.WholeSong/XlightsAI.Studio.WholeSong.csproj`** — two new `ProjectReference`
  entries added (`XlightsAI.Studio.Xlights`, `XlightsAI.Studio.Editing`); no existing entry changed.
  Verified neither `Xlights` nor `Editing` references `WholeSong`, so this is not a cyclic reference.
- **`tests/XlightsAI.Studio.WholeSong.Tests/XlightsAI.Studio.WholeSong.Tests.csproj`** — the same two new
  `ProjectReference` entries added.
- **`tests/XlightsAI.Studio.WholeSong.Tests/Step091AcceptanceTests.cs`** — 10 new tests (see §3).

No existing Step 090 file (`SongDirectionPlan.cs`, `SongDirectionPlanner.cs`, etc.) was touched; Step 091
does not yet consume `SongDirectionPlan` as an input (see §6 item 5 for the disclosed reason) — it operates
directly on caller-supplied `PhraseAssemblyCandidate`/`PhraseSeamRequest` values, consistent with the
narrow, numbered-step-at-a-time scope this task requires.

## 2. Commands actually executed

```
dotnet build src\XlightsAI.Studio.WholeSong\XlightsAI.Studio.WholeSong.csproj --nologo
dotnet build tests\XlightsAI.Studio.WholeSong.Tests\XlightsAI.Studio.WholeSong.Tests.csproj --nologo
dotnet test tests\XlightsAI.Studio.WholeSong.Tests\XlightsAI.Studio.WholeSong.Tests.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
```

Run from `C:\CODE\xLightsAISequencer` via the PowerShell tool — this session's Bash/git-bash tool again
returned a bare exit code 1 with no captured output on every invocation (`ls`, `cat`, `echo`), the same
pre-existing local shell-launch issue every prior PHASE-13/Step-090 evidence already disclosed — not a
project/test failure; all commands below ran successfully via PowerShell.

## 3. Actual results

### 3.1 Build

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

`XlightsAI.Studio.WholeSong -> ...\bin\Debug\net10.0\XlightsAI.Studio.WholeSong.dll` and
`XlightsAI.Studio.WholeSong.Tests -> ...\bin\Debug\net10.0\XlightsAI.Studio.WholeSong.Tests.dll` both
compile cleanly against the two newly added `Xlights`/`Editing` project references, alongside all
pre-existing projects.

### 3.2 New-test run (`XlightsAI.Studio.WholeSong.Tests`)

```
Passed!  - Failed: 0, Passed: 24, Skipped: 0, Total: 24, Duration: 329 ms - XlightsAI.Studio.WholeSong.Tests.dll (net10.0)
```

24 = the 14 pre-existing Step 090 tests (unchanged, still green) + 10 new Step 091 tests:

| Test | Proves |
|---|---|
| `Step091_Acceptance_Passes` (required name) | Assembles three consecutive phrases (A, B, C) with one `Hold` seam (closing a genuine 50-tick silent gap by extending phrase A's own boundary-touching effect forward) and one `Trim` seam (clipping a trailing Arch effect that bleeds 30 ticks past phrase B's own boundary into phrase C's territory — the required "cross-phrase overlapping effect" scenario, Section 7 item 4). Asserts: new document revision is bumped by exactly 1; the Hold-closed gap leaves **no unintended timing hole** (phrase A's Tree effect now ends exactly at 1050, not 1000); the Trim seam leaves **no truncated-by-accident effect** (the bleeding effect is clipped exactly at the boundary, 2030→2000, and this is disclosed via a warning, never silent); every effect on a given model+layer across the *whole* assembled document is non-overlapping (no unintended conflicts); seams are reported in phrase order with the exact strategy applied. Then persists the assembled document via the real `JsonSequenceDocumentStore` and **reloads it from a brand-new store instance** (proves "Versioned, inspectable project output," the step's own required User-visible outcome, verbatim, survives a restart — not only a mock invocation). |
| `Step091_BridgeSeam_LeavesGapDisclosedRatherThanSilent` | A `Bridge`-requested seam over a real 200-tick gap leaves the gap genuinely unfilled (no effect is invented to fill it — "do not invent xLights effect parameters," PHASE-14.md Section 2, verbatim) but discloses it via a warning naming the exact gap size, rather than silently reporting success. |
| `Step091_LockedBaseDocumentEffect_RejectsEntireAssembly_BeforeAnyMutation` | A locked effect in `baseDocument` reached by the permitted scope rejects the *entire* assembly via the real `EffectLockPolicy`/`EditCommandRejectedException`, before any phrase is resolved — the same "no partial mutation" rule `ChoreographyCompiler` already enforces. |
| `Step091_NegativePath_MalformedProposal_OverlappingPhraseWindows_Throws_BeforeAnyMutation` (required failure/edge case, verbatim: "Malformed proposal... must not auto-commit") | Two phrase candidates whose own declared windows overlap each other throw `PhraseAssemblyInvalidException` naming the exact violation, and the caller's own `baseDocument` instance is provably untouched (same `Revision` field re-asserted after the throw) — not merely "an exception was thrown," but no partial commit occurred. |
| `Step091_NegativePath_MissingSeamRequest_Throws` | Two phrases supplied with zero seam requests (one is required) throws rather than silently assuming a default policy. |
| `Step091_NegativePath_HoldRequestedAgainstOverlappingSeam_Throws` | `Hold` requested at a seam where an effect actually bleeds across the boundary throws — Hold cannot "hold through" a real overlap; this is a policy/data mismatch, a malformed proposal, not something to silently paper over. |
| `Step091_NegativePath_EmptyPhrases_Throws` | Zero phrase candidates throws rather than assembling a silent no-op. |
| `Step091_NegativePath_EffectOutsideAllowedScope_Throws` | An effect whose target model is outside the permitted `EditScope.Models` set throws before any mutation. |
| `Step091_NegativePath_ExhaustedRetryBudget_ThrowsWithoutCommitting` (required failure/edge case, verbatim: "...exhausted retry budget must not auto-commit") | `baseDocument` is seeded with all 8 (`PhraseSeamAssembler.MaxLayers`) layers already fully occupied and overlapping on the same model/window; a new phrase effect targeting that same model/window cannot find any free layer after trying all 8, so `PhraseAssemblyExhaustedException` is thrown naming the exact target/window/layer-count — and the caller's own `baseDocument` is proven unchanged afterward (same `Revision`, same `Effects.Count`): **no partial document was ever constructed**, exactly the literal retry-budget-exhaustion failure path this step's own text requires. |
| `Step091_NegativePath_NoOpTrimRequest_IsDisclosedNotSilent` | `Trim` requested at a seam with no actual overlap is applied as a disclosed no-op (a warning naming it as such), and the assertion directly confirms neither effect was truncated — proving a no-op request can never be silently mistaken for "something was fixed." |

### 3.3 Full-repository regression (`dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"`)

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 27/27 |
| XlightsAI.Studio.Audio.Tests | Passed 23/23 |
| XlightsAI.Studio.Editing.Tests | Passed 53/53 |
| XlightsAI.Studio.Rendering.Tests | Passed 20/20 |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.Composition.Tests | Passed 69/69 |
| **XlightsAI.Studio.WholeSong.Tests** | **Passed 24/24** (14 Step 090 + 10 new Step 091) |
| XlightsAI.Studio.AiEdits.Tests | Passed 69/69 |
| XlightsAI.Studio.Shell.Tests | Passed 199/199 (filtered: `Category!=Integration`) |
| XlightsAI.Studio.Refinement.Tests | Passed 78/78 |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |
| XlightsAI.Studio.Projects.Tests, XlightsAI.Studio.Xlights.Tests, XlightsAI.Studio.Display.Tests | No test matched `Category!=Integration` (pre-existing; same disclosed reason every prior phase's evidence already recorded) |

**Every project ran clean; zero failures anywhere in the full filtered regression.** Every project this step
did not touch shows the exact same pass count as Step 090's own last-recorded evidence
(`implementation-evidence/PHASE-14/STEP-090.md` §3.3), confirming this step introduced no regression.

## 4. Observable result / "manual demonstration"

This step's own "User-visible outcome," verbatim: "Versioned, inspectable project output plus a
reproducible test report." The domain half is directly and fully exercised by `Step091_Acceptance_Passes`:
a real `SequenceDocument` is assembled from three phrase candidates with a real `Hold` and a real `Trim`
seam, the resulting document's `Revision` is bumped exactly once, the document is persisted via the real
`JsonSequenceDocumentStore.SaveAsync`, and **reloaded from a brand-new store instance** over the same
on-disk directory — proving the assembled output is genuinely versioned and inspectable independent of the
in-memory object that produced it, not merely asserted in-process. The "reproducible test report" half is
this very evidence file plus the xUnit test run output reproduced verbatim in §2/§3.2.

**No Avalonia UI screen was run or screenshotted for this step** — see §6 item 6 for the disclosed decision,
mirroring Step 090's own precedent: PHASE-14.md Section 5's SCREEN 03/12/13 wireframes span the whole
090–093 step range, and this step's own required acceptance text ("No unintended timing holes, conflicts or
truncated effects") is fully satisfiable and testable as a domain/persistence slice without a new screen.
This is recorded as **NOT VERIFIED for the UI-screen portion specifically**, not silently assumed.

No installed xLights executable exists on this machine (same disclosed gap as every prior phase). Per this
project's own standing memory waiver, the real-xLights human-verification check is not required as a
phase-gate blocker for this step; it is also not literally applicable to this step's own scope yet (this
step produces an app-owned `SequenceDocument`, not an exported `.xsq` — XSQ export/audition is Step 093's
own later, disclosed scope per PHASE-14.md's own numbered-step table), so there is nothing new to waive here
beyond carrying the existing standing waiver forward.

## 5. Fixtures

No binary fixture files were added under `fixtures/`. Every `PhraseAssemblyCandidate`/`EffectInstance`/
`SequenceDocument` in `Step091AcceptanceTests` is built directly in the test file via real production
constructors (no mocking), the same convention every prior PHASE-08/10/12/13/Step-090 test already
established. There is no external source audio/analysis file this step reads, so there are no meaningful
input hashes to record beyond the deterministic in-memory fixture values already visible in the test source
(`tests/XlightsAI.Studio.WholeSong.Tests/Step091AcceptanceTests.cs`) — each run constructs fresh
`Guid.NewGuid()` identities for phrase/effect ids, so nothing here is content-addressed the way a real audio
asset's SHA-256 would be.

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **`TransitionPlan` (Section 3's proposed per-seam record) renamed to `PhraseSeamPlan` — a genuine, disclosed
   name collision, not a stylistic choice.** PHASE-14.md Section 3 proposes `TransitionPlan(Guid FromPhraseId,
   Guid ToPhraseId, TimeWindow Window, string Strategy, IReadOnlyList<string> Warnings)` inside
   `XlightsAI.Studio.WholeSong`. But `XlightsAI.Studio.Composition.TransitionPlan` (PHASE-10 Step 070) already
   exists with a completely different shape (`Guid VersionId, IReadOnlyList<TransitionGesture> Gestures`), and
   `WholeSong` already has `using XlightsAI.Studio.Composition;` from Step 090. Reusing the same name would
   either collide outright or silently shadow the wrong type depending on file-level usings. `PhraseSeamPlan`
   is used instead, and `Strategy` is the strongly-typed `SeamStrategy` enum rather than the spec's proposed
   free-form `string`, consistent with the repository's own "never an invented free-form value when a closed
   set exists" convention (verified via `Glob`/`Grep` over `src/XlightsAI.Studio.Composition/*.cs` before
   writing any new code — see the research summary this step's own planning phase recorded).
2. **`IWholeSongAssembler.Assemble(WholeSongPlan, SequenceDocument)` narrowed to
   `IPhraseSeamAssembler.Assemble(SequenceDocument, IReadOnlyList<PhraseAssemblyCandidate>, IReadOnlyList<PhraseSeamRequest>, EditScope)`
   (disclosed).** No `WholeSongPlan`/`ChoreographyPlan`/`SongStructure` type exists anywhere in the actual
   repository (verified via `Glob` before writing any code; Step 090's own evidence §6 items 1/4 already
   disclosed the same gap for its own scope). `SongDirectionPlan` (Step 090's own narrowed realization)
   deliberately omits `Transitions`/`PhrasePlanIds` — there is no existing aggregate yet that names, in
   order, the concrete phrase ids and accepted effects to assemble. This step therefore takes that
   information as an explicit, caller-supplied, already-ordered `IReadOnlyList<PhraseAssemblyCandidate>` plus
   one explicit `PhraseSeamRequest` per consecutive pair — a human/caller-directed input, consistent with
   PHASE-14.md Section 2's own "AI/system proposes, user approves" convention — rather than inventing an
   unrequested `WholeSongPlan`/`ChoreographyPlan` wrapper type this step was not asked to build. A future step
   that does define such a wrapper can project it down to this exact same `(candidates, seamRequests)` shape
   without any change to `PhraseSeamAssembler`'s own contract.
3. **Seam strategy is an explicit, caller-chosen policy per seam, never auto-derived from gap/overlap shape
   (disclosed, deliberate).** Implementation guidance, verbatim: "explicit seam policies (trim, hold,
   release, bridge)." `PhraseSeamAssembler` therefore validates and applies whichever strategy the caller
   names for each seam; it never guesses "this looks like a gap, so Hold" on its own. A caller requesting a
   strategy incompatible with the actual seam shape (e.g. `Hold`/`Bridge` against a seam with a real
   effect-level overlap) is rejected as a malformed proposal (`PhraseAssemblyInvalidException`) rather than
   silently reinterpreted as a different strategy.
4. **`Release` applies the identical clipping mechanics as `Trim`, differing only in its own disclosed
   provenance label (disclosed, deliberate).** PHASE-14.md Section 2 explicitly forbids inventing xLights
   effect parameters ("do not invent xLights effect parameters"). A genuine artistic fade-out curve is an
   actual xLights effect-recipe concern this step has no verified recipe for: `IEffectRecipeRegistry` (PHASE-
   02) only compiles already-known, already-verified recipes, and no "fade" recipe is named anywhere in this
   step's own required scope. `Release` is kept as a distinct, named strategy (for audit/provenance -- "this
   seam was an intentional artistic release, not a mechanical trim") without fabricating a new effect
   parameter to realize a difference this step has no verified means to express.
5. **`PhraseSeamAssembler` does not yet consume `SongDirectionPlan` (Step 090's own output) as an input
   (disclosed, scope boundary).** Step 090's own evidence (§7) already flagged this forward: "`SongDirectionPlan`
   does not yet reference any `ChoreographyPlan`/phrase-plan id... Step 091 is expected to consume
   `SongDirectionPlan` as one of its own inputs when it adds phrase assembly, not to require a migration of
   this step's own already-accepted shape." This step's own required scope — "Assemble accepted phrase
   candidates with transition-aware overlap/gap handling" — is fully testable and satisfiable as a pure
   assembly operation over explicit phrase/seam inputs; wiring an application-layer orchestrator that derives
   those inputs from a `SongDirectionPlan` plus upstream `PhraseSegmentationAnalysis`/accepted
   `ChoreographyCompilationResult`s is a composition-root/UI-command concern this step's own narrow,
   numbered-step scope does not require, and is left as an explicit, disclosed follow-up rather than
   speculatively built now ("avoid collecting multiple unverified tasks into one mega-commit," this
   repository's own standing convention).
6. **No new `ViewModel`/Avalonia view (disclosed, mirrors Step 090's own precedent).** This step's own
   required acceptance text ("No unintended timing holes, conflicts or truncated effects") is a property of
   the assembled `SequenceDocument`/`PhraseSeamPlan` data, fully provable as a domain/persistence slice; it
   does not literally require a new interactive screen. PHASE-14.md Section 5's SCREEN wireframes are
   phase-wide (span Steps 090–093), not specific to this one numbered step. Recorded as **NOT VERIFIED** for
   the UI-screen portion (§4), not silently skipped.
7. **Cross-phrase layer-collision resolution reuses `ChoreographCompiler`'s own bounded-retry layer-search
   pattern (`MaxLayers = 8`), rather than inventing a new strategy (disclosed, deliberate reuse).** This
   realizes "respect... layer boundaries" (implementation guidance, verbatim) and gives this step's own
   required "exhausted retry budget must not auto-commit" failure case a concrete, literal, testable meaning:
   an effect that cannot find a free layer among the same bounded set `ChoreographyCompiler` already uses
   throws `PhraseAssemblyExhaustedException` before any document is constructed, rather than looping
   indefinitely or silently stacking onto an unbounded number of layers.

## 7. Known risks / unresolved issues

- **No UI screen was built or exercised for this step** — see §6 item 6 and §4. The assembled-document
  outcome is proven only as a persisted, reloadable data value, not as a rendered, interactive Avalonia view.
- **No real xLights executable or audio device was exercised** — consistent with every prior phase/step; not
  literally applicable yet (no `.xsq` export happens until Step 093), but carried forward as a standing,
  disclosed gap per the project's own memory waiver.
- **`PhraseSeamAssembler` does not yet consume `SongDirectionPlan`/`PhraseSegmentationAnalysis` directly** —
  by design (§6 item 5); a future application-layer orchestrator is expected to derive this step's own
  `PhraseAssemblyCandidate`/`PhraseSeamRequest` inputs from those upstream artifacts without requiring any
  change to this step's own already-accepted contract.
- **`Bridge` never materializes an actual transition-gesture effect** — by design (§6 item 4): inventing one
  without a verified xLights recipe would violate PHASE-14.md Section 2's own "do not invent xLights effect
  parameters" rule. The gap is always disclosed via a warning naming its exact size, never silently hidden;
  a future step wiring PHASE-10's own already-accepted `ITransitionRecipePlanner`/`TransitionGesture` into an
  actual bridge effect can consume `PhraseSeamPlan.Window` directly for any `Bridge`-strategy seam with a
  nonzero gap.
- **Layer-collision retry resolves by reassignment, not by re-ordering/re-planning seam strategies** — an
  effect that cannot find a free layer always throws (`PhraseAssemblyExhaustedException`) rather than
  attempting an alternate seam strategy automatically; resolving such a conflict is left to the human/caller
  re-submitting a different `PhraseSeamRequest`, consistent with this step's own "explicit seam policies,
  never auto-derived" ADR (§6 item 3).

## 8. Required final checklist (Step 091 only)

- [x] Implementation / tests / observable demo (automated, real production types, no mocking; UI-screen
      portion explicitly recorded as NOT VERIFIED, not faked — see §4) / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 warnings, 0 errors).
- [x] New-test run: 24/24 passed (`XlightsAI.Studio.WholeSong.Tests`, 14 pre-existing Step 090 + 10 new Step 091).
- [x] Full existing regression suite re-run with `--filter "Category!=Integration"`; every project passes
      with 0 failures; no project this step did not touch regressed from its own last-recorded Step 090
      evidence (§3.3).
- [x] "Manual" demonstration performed against reproducible, real-production-type test data covering the
      domain/persistence portion of this step's own outcome text; the UI-screen portion is explicitly
      recorded as not built/not verified, not claimed.
- [x] No previously accepted source file from PHASE-01 through PHASE-13, or from Step 090, was modified;
      only 9 new files under `src/XlightsAI.Studio.WholeSong/`, 1 new file under
      `tests/XlightsAI.Studio.WholeSong.Tests/`, and two new `ProjectReference` entries added to each of the
      two already-existing `.csproj` files (no existing `ProjectReference` entry changed).
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).

**Gate for Step 091: PASS.** This step's own numbered-step scope — assemble explicitly-ordered, already-
accepted phrase candidates into a single `SequenceDocument` via one of four explicit seam policies (trim,
hold, release, bridge), close genuine gaps and resolve genuine cross-phrase overlaps without ever silently
truncating or conflicting an effect, enforce effect locks and layer boundaries, and prove that a malformed
proposal or an exhausted layer-retry budget can never auto-commit a partial document — is fully implemented
and tested. The UI-screen half of PHASE-14.md Section 5's wireframe references remains explicitly NOT
VERIFIED (no screen was built), consistent with Step 090's own established precedent. Steps 092–093 remain
unimplemented and out of this step's own scope.
