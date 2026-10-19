# PHASE 07 — Step 049 Evidence

**Step:** "Implement editable piano roll and low-confidence markers"
**Spec:** `docs/phase-specs/PHASE-07.md` §6, Step 049 (lines 258-272) — the fifth numbered step of Phase 07 (045-051)
**Date:** 2026-10-13

## 1. Pre-implementation baseline inspection

- Read `docs/phase-specs/PHASE-07.md` in full before writing any code: required acceptance ("User
  corrections persist and regenerate only dependent phrases"), implementation guidance ("Render note
  lengths and pitch rows, support drag/resize, and link selection to affected effect proposals; never
  lose original transcription"), failure/edge case ("Out-of-scope, locked or stale revision must be
  rejected without partial mutation" — the same three-category cross-cutting rule PHASE-03's
  `EditRejectionReason` already names for `SequenceDocument`/`EffectInstance` edits), and Section 5's
  SCREEN 10 PIANO ROLL requirement (loading/empty/error states, responsive resizing, keyboard
  navigation, explicit version/status indicator).
- Read `implementation-evidence/PHASE-07/STEP-045.md` through `STEP-048.md` in full per the user's own
  instruction to "treat all preceding completed step contracts as immutable unless the current step has
  an accepted migration plan." Steps 045-048's `NoteEvent`/`MelodyTrack`/`MelodyTrackValidator`/
  `MelodicGesture`/`MelodicContourClassifier`/`GestureClassificationSettings`/`IMidiTranscriptionBackend`
  are all **untouched** by this step — no migration was needed or made. `NoteEvent`'s own doc comment
  ("Always 1.0 for a trusted-MIDI or UserCorrected note... Step 049's own piano-roll correction
  boundary") was read closely — Step 045 already anticipated this step's own `UserCorrected`/confidence
  convention.
- Inspected PHASE-03's `XlightsAI.Studio.Editing` project in full (`EditScope`, `EditCommand`,
  `EditRejectionReason`, `EditCommandRejectedException`, `EditTransactionService`,
  `EffectLockPolicy`) — the one existing, already-accepted precedent in this repository for
  "out-of-scope, locked, stale revision must be rejected without partial mutation," applied here to
  `SequenceDocument`/`EffectInstance`. This step's own `NoteEditScope`/`CorrectNoteCommand`/
  `NoteEditRejectionReason`/`NoteCorrectionRejectedException`/`NoteTrackEditor` mirror that exact
  relationship, applied to `MelodyTrack`/`NoteEvent` instead.
- Inspected `src/XlightsAI.Studio.Shell/` in full (`TimelineViewModel`, `AudioImportViewModel`,
  `ShellViewModel`, `ShellScreen`, `ObservableObject`, `DelegateCommand`, `ScreenLoadState`,
  `EffectBlockItemView`/`TimelineRowItemView`) to confirm the existing, already-accepted "Avalonia-free
  screen view model + UI-only projection record + DelegateCommand-captured-identity" convention this
  step's own `PianoRollViewModel`/`PianoRollNoteRowView` follow.
- Searched the whole repository for any existing revision/lock concept on `MelodyTrack`/`NoteEvent`
  itself (none exists — confirmed by reading every file in `src/XlightsAI.Studio.Melody/` end to end):
  this step is the first to introduce a revision-tracked, lockable editing session for a `MelodyTrack`.
- Baseline regression before any Step 049 code existed, run fresh in this session
  (`dotnet test XlightsAI.Studio.slnx --nologo`): **636/636 passing** across 9 test assemblies, exactly
  Step 048's own final recorded count, confirming the starting state matched the prior step's own
  recorded evidence before any Step 049 line of code existed.

## 2. Scope actually implemented

### New files in `src/XlightsAI.Studio.Melody/`

- **`NoteEditScope.cs`** — `NoteEditScope(TimeWindow Time, IReadOnlySet<Guid>? NoteIds, bool
  RespectLocks = true)`. Melody-local sibling of `XlightsAI.Studio.Editing.EditScope`, omitting
  `EditScope.Models` entirely (a `MelodyTrack` has no "target model" concept).
- **`NoteEditRejectionReason.cs`** — `{ StaleRevision, NoteNotFound, OutOfScope, Locked }`. This step's
  own required failure/edge case, verbatim, turned into a stable, non-string enum (mirrors
  `EditRejectionReason`'s own role for PHASE-03).
- **`NoteCorrectionRejectedException.cs`** — carries a `NoteEditRejectionReason Reason`, mirrors
  `EditCommandRejectedException`.
- **`CorrectNoteCommand.cs`** — `(DocumentRevision ExpectedRevision, NoteEditScope Scope, NoteEvent
  Corrected)`. **Disclosed architecture deviation** (see §3) from Section 3's proposed bare
  `INoteTrackEditor.CorrectAsync(Guid trackId, NoteEvent corrected, CancellationToken ct)`.
- **`SetNoteLockedCommand.cs`** — `(DocumentRevision ExpectedRevision, Guid NoteId, bool Locked)`,
  mirroring `XlightsAI.Studio.Editing.SetEffectLocked`'s own relationship to `EditCommand`.
- **`MelodyTrackRevisionState.cs`** — `(Guid TrackId, MelodyTrack Original, MelodyTrack Current,
  DocumentRevision Revision, IReadOnlyList<Guid> LockedNoteIds, GestureClassificationSettings
  ClassificationSettings)`. This step's own "Versioned, inspectable project output" (user-visible
  outcome, verbatim) and Section 4's "Store original transcription version and corrected MelodyTrack as
  separate revisions."
- **`IMelodyTrackRevisionStore.cs`** / **`JsonMelodyTrackRevisionStore.cs`** — atomic-write JSON
  persistence for `MelodyTrackRevisionState`, mirroring `JsonMelodyTrackStore`'s own same-directory
  temp-file-then-`File.Move` pattern.
- **`MelodyTrackRevisionStateCorruptException.cs`** — mirrors `MelodyTrackCorruptException`.
- **`INoteTrackEditor.cs`** — `BeginEditingAsync` / `GetStateAsync` / `CorrectAsync` / `SetNoteLockedAsync`.
  See §3 for the disclosed deviation from Section 3's proposed signature.
- **`NoteTrackEditor.cs`** — the real, single implementation. `CorrectAsync` gates, in order (every gate
  runs to completion before any new state object is constructed, let alone persisted — "no partial
  mutation"): stale revision → unknown note id → out-of-scope (by explicit `NoteIds`, or by time-window
  overlap) → locked → corrected boundaries must themselves stay inside the scope's own time window →
  only then replaces the note, marks it `UserCorrected = true` and `Confidence = 1.0`, and reclassifies
  gestures via the already-committed, unmodified `MelodicContourClassifier.Classify`.

### New files in `src/XlightsAI.Studio.Shell/`

- **`PianoRollNoteRowView.cs`** — UI-only projection of one `NoteEvent`: `X`/`Width` render its time
  span ("Render note lengths," implementation guidance, verbatim), `Y` renders its pitch row ("...and
  pitch rows"), `IsLowConfidence` is this step's own named "low-confidence markers," `SelectCommand`
  captures `NoteId` the same way `EffectBlockItemView.SelectCommand` already captures `EffectId`.
- **`PianoRollViewModel.cs`** — SCREEN 10 PIANO ROLL's own Avalonia-free screen view model (no Avalonia
  types, same convention as `TimelineViewModel`/`AudioImportViewModel`). Explicit
  `ScreenLoadState.Empty`/`Error`/`Loaded` state machine. `BeginEditingAsync`/`OpenTrackAsync` load a
  `MelodyTrackRevisionState` through a real `INoteTrackEditor`; `CorrectSelectedNoteAsync` is the real
  "drag/resize" entry point (implementation guidance, verbatim), building a whole-track-bounds
  `NoteEditScope` and routing through `INoteTrackEditor.CorrectAsync`; `ToggleLockSelectedNoteAsync`
  routes through `SetNoteLockedAsync`. Every mutating method catches `NoteCorrectionRejectedException`/
  `MelodyTrackInvalidException`, sets `ActionErrorMessage`, and leaves `Rows` completely unchanged on
  failure — the same "catch, set an error property, never let an exception reach the UI, never a
  partial mutation" convention every other screen action in this project already follows.

### New test files

- `tests/XlightsAI.Studio.Melody.Tests/Step049AcceptanceTests.cs` — 12 tests against the real
  `NoteTrackEditor`/`JsonMelodyTrackRevisionStore` (not a mock), including the required
  `Step049_Acceptance_Passes_UserCorrectionsPersistAndRegenerateOnlyDependentPhrases` test.
- `tests/XlightsAI.Studio.Shell.Tests/Step049PianoRollViewModelTests.cs` — 6 tests against the real
  `PianoRollViewModel` + real editor/store, plus one `ShellViewModel` navigation-wiring test.

### Project/solution wiring (modified files)

- **`src/XlightsAI.Studio.Shell/XlightsAI.Studio.Shell.csproj`** — added a `ProjectReference` to
  `XlightsAI.Studio.Melody` (the Shell project had no prior dependency on the Melody domain project).
- **`src/XlightsAI.Studio.Shell/ShellScreen.cs`** — added `ShellScreen.PianoRoll`.
- **`src/XlightsAI.Studio.Shell/ShellViewModel.cs`** — added the `PianoRoll` screen property,
  `NavigateToPianoRollCommand`, `IsShowingPianoRoll`, and the corresponding `OnNavigatorChanged`/
  `OnPropertyChangedAll`/`CanExecuteChanged` wiring, following the exact same minimal pattern Steps
  016/024 already established for Timeline/Audio. **Disclosed, deliberate scope limit** (see §3):
  only navigation is wired into `ShellViewModel` — the correction/lock *action* commands
  (`CorrectSelectedNoteAsync`/`ToggleLockSelectedNoteAsync`) are exercised directly against
  `PianoRollViewModel` (by tests and by the real demo in §5), not re-wrapped as additional
  `ShellViewModel` `DelegateCommand`s in this step.

**No changes to any Step 045/046/047/048 file.**

## 3. Architecture deviations / decisions

- **`INoteTrackEditor.CorrectAsync` takes a `CorrectNoteCommand`, not Section 3's proposed bare
  `CorrectAsync(Guid trackId, NoteEvent corrected, CancellationToken ct)`.** Disclosed, deliberate, same
  category as Step 048's `IMidiTranscriptionBackend` vs. the proposed `IMelodyAnalyzer`: this step's own
  required failure/edge case, verbatim, is "Out-of-scope, locked or stale revision must be rejected
  without partial mutation" — none of those three can be expressed by a bare
  `(Guid, NoteEvent, CancellationToken)` tuple with no revision token and no scope. The real command
  shape instead mirrors `XlightsAI.Studio.Editing.IEditTransactionService.ApplyAsync(SequenceDocument,
  EditCommand, ...)`'s own identical relationship to PHASE-03's identically-worded rule.
- **"Locked" is tracked via `MelodyTrackRevisionState.LockedNoteIds`, not a new field on the
  already-committed, immutable Step 045 `NoteEvent` record.** Disclosed, deliberate: adding a field to
  `NoteEvent` would be a migration against an already-accepted contract (Section 2: "Keep IDs stable...
  Schema changes require versioned migrations"); the lock bit instead lives alongside the revision state
  that this step itself introduces, the same "don't touch an already-accepted contract, add a
  phase-local sibling instead" precedent Step 048 already established for MIDI import settings.
- **`LockedNoteIds` is typed `IReadOnlyList<Guid>`, not `IReadOnlySet<Guid>`.** Disclosed, deliberate,
  found by a real test failure during this step's own implementation: `System.Text.Json`'s built-in
  collection converters throw `NotSupportedException` attempting to deserialize into
  `IReadOnlySet<T>` ("The collection type 'System.Collections.Generic.IReadOnlySet\`1[System.Guid]' is
  abstract, an interface, or is read only, and could not be instantiated and populated."). Switched to
  `IReadOnlyList<Guid>` (the same collection-interface shape `MelodyTrack.Notes` already uses and that
  already round-trips correctly); uniqueness is still enforced at every write site inside
  `NoteTrackEditor` via an internal `HashSet<Guid>` before the list is ever persisted.
- **Equality assertions in `Step049AcceptanceTests` compare specific fields/lists, not whole
  `MelodyTrack`/`MelodyTrackRevisionState` records via a single `Assert.Equal`.** Disclosed, found by a
  second real test failure during this step's own implementation: a C# record's compiler-generated
  `Equals` compares a `List<T>`/array-typed property using `EqualityComparer<T>.Default`, which — for a
  property declared as an interface type like `IReadOnlyList<NoteEvent>` with no `IEquatable<T>`
  implementation — falls back to reference equality, not content equality. xunit's `Assert.Equal<T>`
  only gets "deep," element-wise collection comparison when `T` *itself* is the collection type being
  compared (e.g. `Assert.Equal(expected.Notes, actual.Notes)`), not when the collection is a property
  several levels inside a larger record being compared as a whole. Every assertion that needs to compare
  a reloaded/reclassified track's contents now asserts the specific `Notes`/`Gestures`/`LockedNoteIds`
  lists (and scalar fields) directly, never the containing record as a single opaque unit. No prior
  step's tests in this repository ever attempted a whole-record `Assert.Equal` across a list property
  for this same reason (confirmed by inspection: Step 045/048's own round-trip tests compare counts or
  invoke `store.LoadAsync` and assert individual fields, never a bare `Assert.Equal(trackA, trackB)`).
- **Only `ShellScreen`/navigation is wired into `ShellViewModel`; the correction/lock action commands are
  not re-wrapped as additional `ShellViewModel` `DelegateCommand`s in this step.** Disclosed, deliberate,
  given this step's own generic, phase-wide "Versioned, inspectable project output plus a reproducible
  test report" user-visible-outcome text (not "a full SCREEN 10 AXAML view," which this repository has
  no AXAML convention for yet — every existing screen view model, including this one, is exercised
  directly by tests/a real console demo, never through an actual rendered Avalonia window, exactly as
  Steps 045-048 already did for their own analysis pipelines). A future step that adds the real SCREEN
  10 AXAML view can wire `PianoRollViewModel.CorrectSelectedNoteAsync`/`ToggleLockSelectedNoteAsync`
  directly from its own code-behind/bindings without needing a `ShellViewModel`-level command wrapper
  first.
- **No real `IMelodicGestureMapper`/effect-proposal linkage.** Disclosed, consistent with Section 3's own
  scope boundary (Step 050 owns `IMelodicGestureMapper`/`EffectInstance` mapping): "link selection to
  affected effect proposals" (implementation guidance) is satisfied at this step's own scope by
  `PianoRollNoteRowView`/`PianoRollViewModel.SelectedNoteId` linking a selected *note* to its own
  `MelodicGesture` (already present on `MelodyTrackRevisionState.Current.Gestures`, keyed by
  `MelodicGesture.NoteId`) — there are no real `EffectInstance` proposals to link to yet in this phase
  (Step 050's own later scope), so nothing is fabricated here.
- **`CorrectSelectedNoteAsync`'s own default `NoteEditScope` covers the whole loaded track's own content
  bounds.** Disclosed, deliberate: `PianoRollViewModel` is a convenience wrapper for the common case (no
  narrower selection concept like PHASE-03's own drag-rectangle `TimelineViewModel.ApplyRangeSelection`
  exists for a piano roll yet); a caller that needs a narrower scope (e.g. "only this phrase," to
  deliberately trigger the out-of-scope rejection path) calls `INoteTrackEditor.CorrectAsync` directly
  with its own `NoteEditScope` — proven directly by `Step049AcceptanceTests`'s own out-of-scope tests,
  which bypass the view model for exactly this reason.
- **Phase 06 (this phase's own declared dependency) remains BLOCKED at the phase-gate level**, carried
  forward unresolved from Steps 045-048 (unchanged by this step). This step's own editor has no
  stem/audio input of its own (it corrects an already-produced `MelodyTrack`, regardless of which
  fallback-ladder rung produced it), so it does not depend on, or attempt to resolve, Phase 06's own
  separately-tracked production stem-separation gap.

## 4. Commands actually executed

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session again returned a bare
exit code 1 with no output for plain commands (confirmed again this session with a literal `echo test`),
the same pre-existing environment limitation recorded in every prior step's own evidence; PowerShell was
used for every command below without incident.

```
dotnet test XlightsAI.Studio.slnx --nologo                                                   # baseline, before any Step 049 code existed -> 636/636 passing (run in background; output captured and inspected after the fact)

# New: src/XlightsAI.Studio.Melody/{NoteEditScope.cs, NoteEditRejectionReason.cs,
#      NoteCorrectionRejectedException.cs, CorrectNoteCommand.cs, SetNoteLockedCommand.cs,
#      MelodyTrackRevisionState.cs, IMelodyTrackRevisionStore.cs,
#      MelodyTrackRevisionStateCorruptException.cs, JsonMelodyTrackRevisionStore.cs,
#      INoteTrackEditor.cs, NoteTrackEditor.cs}
# New: src/XlightsAI.Studio.Shell/{PianoRollNoteRowView.cs, PianoRollViewModel.cs}
# New: tests/XlightsAI.Studio.Melody.Tests/Step049AcceptanceTests.cs
# New: tests/XlightsAI.Studio.Shell.Tests/Step049PianoRollViewModelTests.cs
# Modified: src/XlightsAI.Studio.Shell/{XlightsAI.Studio.Shell.csproj, ShellScreen.cs, ShellViewModel.cs}

dotnet build src\XlightsAI.Studio.Melody\XlightsAI.Studio.Melody.csproj --nologo              # 0 Warnings, 0 Errors

dotnet test tests\XlightsAI.Studio.Melody.Tests\XlightsAI.Studio.Melody.Tests.csproj --nologo --logger "console;verbosity=normal"
  # first run: 11 failures -- IReadOnlySet<Guid> deserialization NotSupportedException (2 call sites)
  #            plus whole-record Assert.Equal list-reference-equality mismatches (several tests)
  # Fixed: LockedNoteIds retyped IReadOnlyList<Guid>; BeginEditingAsync seeds `new List<Guid>()` (not
  #        Array.Empty<Guid>()) so a fresh in-memory state's concrete collection type matches what
  #        System.Text.Json always deserializes an IReadOnlyList<T> JSON array back into; rewrote the
  #        affected test assertions to compare specific fields/lists instead of whole records (see §3).
  # second run: 86/86 passed (74 pre-existing Step 045/046/047/048 tests + 12 new Step 049 tests)

dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo                 # 0 Warnings, 0 Errors
dotnet test  tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --nologo --logger "console;verbosity=normal"
  # 159/159 passed (153 pre-existing + 6 new Step 049 tests) -- no fixture fixes needed here

dotnet build XlightsAI.Studio.slnx --nologo                                                   # full solution, after Step 049 implementation -> 0 Warnings, 0 Errors
dotnet test  XlightsAI.Studio.slnx --nologo                                                    # full regression, final

dotnet new console -o <scratchpad>\xlai-step049-demo-proj --no-restore
dotnet add <scratchpad>\xlai-step049-demo-proj reference src\XlightsAI.Studio.Melody\XlightsAI.Studio.Melody.csproj
dotnet add <scratchpad>\xlai-step049-demo-proj reference src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj
dotnet add <scratchpad>\xlai-step049-demo-proj reference src\XlightsAI.Studio.Contracts\XlightsAI.Studio.Contracts.csproj
dotnet run --project <scratchpad>\xlai-step049-demo-proj --nologo                              # real, non-test-harness manual demo (see §5)
```

### Build results

Both targeted projects and the full solution: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (Step 049 tests only, real run, after the two fixture/assertion fixes)

```
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.Step049_Acceptance_Passes_UserCorrectionsPersistAndRegenerateOnlyDependentPhrases
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.CorrectAsync_StaleRevision_RejectsWithoutPartialMutation_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.CorrectAsync_OutOfScope_RejectsWithoutPartialMutation_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.CorrectAsync_CorrectionMovesOutsideScopeWindow_RejectsWithoutPartialMutation_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.CorrectAsync_LockedNote_RejectsWithoutPartialMutation_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.CorrectAsync_LockedNote_WithRespectLocksFalse_IsPermitted
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.CorrectAsync_UnknownNoteId_ThrowsNoteNotFound_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.CorrectAsync_InvalidCorrectedNote_ThrowsMelodyTrackInvalidException_WithoutPartialMutation_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.SetNoteLockedAsync_StaleRevision_RejectsWithoutPartialMutation_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.BeginEditingAsync_CalledTwiceForSameTrack_Throws_NegativePath
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.CorrectAsync_CancelledBeforeApplying_ThrowsWithoutProducingAChange_CancellationNegativePath
Passed XlightsAI.Studio.Melody.Tests.Step049AcceptanceTests.CorrectAsync_NoRevisionStateExists_ThrowsNoteNotFound_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step049PianoRollViewModelTests.BeginEditingAsync_RendersRowsWithLowConfidenceMarkers
Passed XlightsAI.Studio.Shell.Tests.Step049PianoRollViewModelTests.CorrectSelectedNoteAsync_PersistsCorrectionAndNeverLosesOriginalTranscription
Passed XlightsAI.Studio.Shell.Tests.Step049PianoRollViewModelTests.CorrectSelectedNoteAsync_NothingSelected_SetsActionErrorAndLeavesRowsUnchanged_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step049PianoRollViewModelTests.ToggleLockSelectedNoteAsync_ThenCorrect_RejectsWithoutPartialMutation_NegativePath
Passed XlightsAI.Studio.Shell.Tests.Step049PianoRollViewModelTests.OpenTrackAsync_NoSessionEverStarted_IsEmptyNotError
Passed XlightsAI.Studio.Shell.Tests.Step049PianoRollViewModelTests.ShellViewModel_PianoRollScreen_NavigatesAndStartsEmpty

Melody.Tests total: 86. Passed: 86, Failed: 0. (74 pre-existing Step 045/046/047/048 + 12 new Step 049)
Shell.Tests total: 159. Passed: 159, Failed: 0. (153 pre-existing + 6 new Step 049)
```

### Full regression (after implementation, final run)

```
Passed!  - Failed: 0, Passed:  71, Skipped: 0, Total:  71  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed:  91, Skipped: 0, Total:  91  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed:  86, Skipped: 0, Total:  86  - XlightsAI.Studio.Melody.Tests.dll
Passed!  - Failed: 0, Passed:  75, Skipped: 0, Total:  75  - XlightsAI.Studio.Stems.Tests.dll
Passed!  - Failed: 0, Passed:  34, Skipped: 0, Total:  34  - XlightsAI.Studio.Audio.Tests.dll
Passed!  - Failed: 0, Passed:  25, Skipped: 0, Total:  25  - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 159, Skipped: 0, Total: 159 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed:  82, Skipped: 0, Total:  82  - XlightsAI.Studio.Rhythm.Tests.dll
Passed!  - Failed: 0, Passed:  31, Skipped: 0, Total:  31  - XlightsAI.Studio.Reference.Tests.dll
```

**654 total, 0 failed, 0 skipped** — up from the Step 048 baseline of 636 (+12 new
`Step049AcceptanceTests` in `Melody.Tests`, +6 new `Step049PianoRollViewModelTests` in `Shell.Tests`).
Every one of the 636 pre-existing tests still passes unmodified, confirming no prior step's contract was
broken.

## 5. Persisted-output inspection / observable demonstration for this step

A real, non-test-harness console program (not part of the repository, built under the session
scratchpad, deleted after this run) exercises the real `PianoRollViewModel`/`NoteTrackEditor`/
`JsonMelodyTrackRevisionStore`/`ShellViewModel` directly — no test harness, no mocks:

```
dotnet run --project <scratchpad>\xlai-step049-demo-proj --nologo
```

Observed real output:

```
Demo root directory: C:\Users\kmoens.LAKECO\AppData\Local\Temp\xlai-step049-demo-fbef3aa43d5c40d7b595115feb46cba7

=== Scenario 1: begin editing a real analyzed MelodyTrack through the real PianoRollViewModel ===
BeginEditingAsync succeeded: True; Revision=0; Rows=7; LowConfidenceRows=1
  MidiNote= 60  X=       0  Width=     8  LowConfidence=False  Locked=False  UserCorrected=False
  MidiNote= 62  X=       8  Width=     7  LowConfidence=False  Locked=False  UserCorrected=False
  MidiNote= 64  X=      16  Width=     7  LowConfidence=False  Locked=False  UserCorrected=False
  MidiNote= 67  X=      23  Width=     9  LowConfidence=True   Locked=False  UserCorrected=False
  MidiNote= 64  X=      32  Width=     6  LowConfidence=False  Locked=False  UserCorrected=False
  MidiNote= 62  X=      38  Width=     6  LowConfidence=False  Locked=False  UserCorrected=False
  MidiNote= 60  X=      44  Width=     8  LowConfidence=False  Locked=False  UserCorrected=False

=== Scenario 2: select the E4 (index 2) and drag/correct it to F4 (65) -- 'drag/resize' ===
CorrectSelectedNoteAsync succeeded: True; new Revision=1
  Corrected note now: MidiNote=65  UserCorrected=True
  'User corrections persist': reloaded from disk independently -- Current MidiNote=65, Confidence=1
  'never lose original transcription': Original MidiNote is still the analyzer's own E4=64

=== Scenario 3: 'regenerate only dependent phrases' -- gestures before the corrected note are untouched ===
  Note 0: before=Rest/0  after=Rest/0  unchanged=True
  Note 1: before=StepUp/2  after=StepUp/2  unchanged=True
  Note 3 (G4, downstream of the correction): before=LeapUp/3  after=StepUp/2  regenerated=True

=== Scenario 4: lock the first note (C4), then attempt to correct it -- rejected, no partial mutation ===
ToggleLockSelectedNoteAsync succeeded: True; row.IsLocked=True
Correcting the locked note succeeded: False (expected false); ActionErrorMessage: Note 'dbb9d6d9-1ffa-4174-9c9e-4c83e973796d' is locked and cannot be corrected.
Persisted file byte-identical after the rejected attempt (no partial mutation): True

=== Scenario 5: stale revision rejected directly against INoteTrackEditor ===
  Correctly rejected: StaleRevision -- Expected revision 100 does not match track 'fa1521e7-37ea-47c2-b5e7-fa3f726a1404''s current revision 2; no edit was applied.

=== Scenario 6: out-of-scope correction rejected directly against INoteTrackEditor ===
  Correctly rejected: OutOfScope -- Note '07a512cc-164d-4da0-85ae-5515a1cde815' is outside the permitted edit scope; no edit was applied.

=== Scenario 7: ShellViewModel navigation to SCREEN 10 PIANO ROLL ===
  IsShowingPianoRoll: True; PianoRoll.IsEmpty (fresh screen): True

Step 049 manual demo completed successfully.

Cleaning up demo directory.
```

Direct, real, non-test-harness confirmation: a real 7-note analyzed `MelodyTrack` (C4-D4-E4-G4-E4-D4-C4,
Section 12's own worked example, with the G4 given a deliberately low 0.3 confidence) renders 7 real
piano-roll rows with the low-confidence marker correctly set on exactly the one note below the default
0.5 threshold ("low-confidence markers," this step's own named required scope); dragging/correcting the
E4 to F4 persists to a real on-disk JSON file and is independently re-read back (required acceptance,
verbatim: "User corrections persist"), while the previously-saved `Original` revision keeps the
analyzer's own untouched E4 ("never lose original transcription," implementation guidance, verbatim);
every gesture for a note before the corrected one is provably byte-for-byte unchanged, while the gesture
immediately after it is provably regenerated against the new pitch ("regenerate only dependent phrases,"
required acceptance, verbatim); locking a note and then attempting to correct it is rejected with the
previously-persisted file proven byte-identical afterward, a stale-revision command is rejected, and an
out-of-scope command is rejected — all three of this step's own named failure/edge-case categories,
verbatim: "Out-of-scope, locked or stale revision must be rejected without partial mutation"; and the
real `ShellViewModel` navigates to the real SCREEN 10 PIANO ROLL screen, starting in the explicit
`ScreenLoadState.Empty` state ("missing media must be handled explicitly," the same convention every
other screen in this project already follows).

## 6. Fixtures / input hashes

No binary fixture *file* is committed to the repository for this step — every `NoteEvent`/`MelodyTrack`
fixture is constructed in-memory at test/demo run time (the same approach Steps 045/047/048 already use
for their own in-line fixtures). `SourceAssetHash` is the literal placeholder string `"deadbeef"` in the
unit tests (matching every prior Melody step's own already-committed test convention) and
`"deadbeef-demo-hash"` in the demo. Every persisted `MelodyTrackRevisionState` JSON file in this step's
own demo is written to its own temporary directory for inspection
(`%TEMP%\xlai-step049-demo-*\track-revisions\melody-track-revision-*.json`) and deleted, along with the
rest of that directory, by the demo's own `finally` block (confirmed by "Cleaning up demo directory." in
the real output above); the throwaway demo *project* itself (outside the repository, under the session
scratchpad) was deleted after this evidence was recorded.

## 7. Unresolved issues / known gaps

- **Phase 06 (this phase's declared dependency) remains BLOCKED** at the phase-gate level, carried
  forward unresolved from Steps 045/046/047/048 (§3) — no production stem-separation model exists
  anywhere in this repository. This step's editor has no stem/audio input of its own and does not touch
  this gap either way.
- **No real SCREEN 10 Avalonia/AXAML view.** `PianoRollViewModel` is real, fully exercised by tests and
  the real demo above, but — consistent with every prior screen view model added in this phase and in
  PHASE-03/04/06 at the time their own step first introduced them — no `.axaml` file renders it inside
  `XlightsAI.Studio.App` yet. `ShellViewModel.PianoRoll`/`NavigateToPianoRollCommand`/
  `IsShowingPianoRoll` are wired so a future AXAML view has a real, tested binding target to attach to
  without needing further `ShellViewModel`-level plumbing.
- **The correction/lock action commands are not wrapped as `ShellViewModel`-level `DelegateCommand`s.**
  Disclosed in §3 — only navigation is wired at the `ShellViewModel` level in this step; a future step
  adding the real AXAML view (or a dedicated follow-up) can add
  `CorrectSelectedNoteAsync`/`ToggleLockSelectedNoteAsync` wrapper commands the same way
  `ProbeAudioFileCommand`/`ScrubTimelinePlayheadCommand` already wrap their own view models' async
  methods.
- **No real `IMelodicGestureMapper`/`EffectInstance` link.** Disclosed in §3 — Step 050 owns compiling
  gestures to concrete effect proposals across an ordered prop path; this step links a selected note only
  to its own already-existing `MelodicGesture`, not to any (not-yet-existing) effect proposal.
- **No MIDI-sourced-track-specific UI treatment.** A track imported via Step 048's
  `StandardMidiFileTranscriptionBackend` (`Confidence = 1.0` for every note) and a track produced by a
  real future `IMelodyAnalyzer` are both handled identically by `PianoRollViewModel`/`NoteTrackEditor` —
  this is intentional (`INoteTrackEditor` operates only on the already-canonical `MelodyTrack` shape,
  regardless of which fallback-ladder rung produced it), not an oversight.

## 8. PASS / BLOCKED

**Status: PASS** (for this step's own narrow, disclosed scope — a revision-aware, lockable note
correction boundary over an already-analyzed `MelodyTrack`, plus the real SCREEN 10 PIANO ROLL view model
that renders and drives it — not a claim that Phase 06's own separately-tracked, carried-forward BLOCKED
verdict is resolved).

- "User corrections persist and regenerate only dependent phrases" (required acceptance, verbatim) —
  proven by `Step049_Acceptance_Passes_UserCorrectionsPersistAndRegenerateOnlyDependentPhrases` (a real
  correction is reloaded from disk independently, and every gesture strictly before the corrected note is
  asserted byte-for-byte unchanged while the gesture immediately after it is asserted to have changed) —
  independently confirmed by the real demo's Scenarios 2 and 3.
- "Render note lengths and pitch rows, support drag/resize, and link selection to affected effect
  proposals; never lose original transcription" (implementation guidance, verbatim) — proven by
  `Step049PianoRollViewModelTests.BeginEditingAsync_RendersRowsWithLowConfidenceMarkers` (real `X`/
  `Width`/`Y` row geometry) and `CorrectSelectedNoteAsync_PersistsCorrectionAndNeverLosesOriginalTranscription`
  (the real "drag/resize" entry point; `Original` proven untouched after a correction) — independently
  confirmed by the real demo's Scenarios 1 and 2.
- "Out-of-scope, locked or stale revision must be rejected without partial mutation" (failure/edge case,
  verbatim) — proven by 9 distinct rejection-path tests covering all three categories
  (`CorrectAsync_StaleRevision_...`, `CorrectAsync_OutOfScope_...`,
  `CorrectAsync_CorrectionMovesOutsideScopeWindow_...`, `CorrectAsync_LockedNote_...`,
  `SetNoteLockedAsync_StaleRevision_...`, plus
  `ToggleLockSelectedNoteAsync_ThenCorrect_RejectsWithoutPartialMutation_NegativePath` at the view-model
  layer), each asserting the previously-persisted file is byte-for-byte unchanged (or the in-memory state
  is unchanged) after the rejection — independently confirmed by the real demo's Scenarios 4, 5 and 6,
  which print the actual `NoteEditRejectionReason` and prove byte-identical persisted bytes after the
  locked-note rejection.
- Full regression: **654/654 passing**, 0 failed, 0 skipped; every pre-existing test from Steps 001-048
  still passes unmodified.
- **Carried-forward, disclosed phase-level/dependency-level gap (not this step's own blocker):** Phase
  06's own gate remains BLOCKED (§3/§7, carried forward from Steps 045/046/047/048) — no production
  stem-separation model exists in this repository. This does not block Step 049's own required acceptance
  text, which this step satisfies in full. Per the user's own standing waiver (`memory:
  feedback_skip_xlights_human_verification`), the xLights human-render check is not treated as a blocker
  for this or any other step, and is not fabricated here; no xLights-specific manual verification applies
  to this step in any case (it does not touch the `.xsq`/`xlights_rgbeffects.xml` pathway). Per Section 8's
  own instruction ("Do not start the next phase when blocked"), **Step 050 should not begin against a
  silent assumption that this evidence constitutes a Phase 07 gate PASS** — only Step 049's own narrow
  scope is proven here; the phase-level gate (Section 7's full-phase test matrix, the phase gate
  demonstration, and Section 8's required final gate report) remains open until Steps 050-051 are each
  independently implemented and evidenced in turn.
