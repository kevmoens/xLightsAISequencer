# UI cleanup plan — current project state + orphaned screens

**Context:** follow-up to the shell navigation/styling pass (see git history on
`src/XlightsAI.Studio.App` and `src/XlightsAI.Studio.Shell/ShellScreen.cs`,
`ShellViewModel.cs` for that work). That pass fixed navigation order, path entry,
theming and the Show Import/Generate-Export split. It deliberately did not touch
the two gaps below, because both are bigger than a view restyle: one is a missing
piece of application state, the other is ~4,000 lines of tested, working logic
with no way for a user to reach it.

This document is the plan for those two, in the order to do them: **current
project first**, because item 2's screens are more useful once they actually
operate on something instead of global ambient paths.

---

## 1. Give the app a concept of "the current project"

### The problem

`ProjectsViewModel.CreateProjectAsync` / `OpenProjectAsync` call
`IProjectService.CreateAsync` / `OpenAsync`, both of which already return a full
`ProjectManifest` (`Id`, `Name`, `RootDirectory`, `SchemaVersion`, `CreatedUtc`) —
and then **discard it**:

```csharp
// src/XlightsAI.Studio.Shell/ProjectsViewModel.cs
await projectService.CreateAsync(WorkspaceDirectory, NewProjectName, ct).ConfigureAwait(true);
return true;
```

So clicking "Create project" or "Open existing" does something real on disk, but
nothing in the app's own state reflects it afterward. Every other screen —
Show Import, Audio, Generate & Export — reads and writes its own independent
global path (`ShowImport.ShowDirectory`, `Audio.AudioFilePath`,
`ShowImport.XsqOutputPath`, …) with no relationship to a project at all. A user
can have "no project open" and still generate and export a sequence; conversely
opening a second project changes nothing about what's currently loaded. The
project screen is a dead end that happens to run file I/O.

This is also why, in the nav rail shipped in the previous pass, step 1's
"done" checkmark had to be approximated as "the workspace directory has any
projects in it" — `Projects.IsLoaded` — rather than "a project is open," because
the latter doesn't exist as state yet.

### The fix

**Add `ShellViewModel.CurrentProject` (type: `ProjectManifest?`).**

1. `ProjectsViewModel.CreateProjectAsync` / `OpenProjectAsync` return the
   `ProjectManifest` (or `null` on failure) instead of `bool`, or add an
   `out`/settable result the caller can read after `await` — match the existing
   "never throw out of a UI command" contract, just stop throwing the manifest
   away. Check `tests/XlightsAI.Studio.Shell.Tests/Step006ShellWiringTests.cs`
   for the existing contract shape before changing the return type signature.
2. `ShellViewModel` stores the result as `CurrentProject`, raises
   `OnPropertyChanged`, and exposes `HasCurrentProject` the same way every other
   `IsShowing…`/`Is…` flag in that file is exposed.
3. Persist `CurrentProject.RootDirectory` into `AppSettings` (it already has a
   `with`-based save path for additive fields — see the `OnNavigatorChanged`
   settings-save block in `ShellViewModel.cs`) so the app reopens the same
   project on restart, same convention as `LastShowDirectory` /
   `WorkspaceDirectory` today.
4. Add a persisted switch: `ShellViewModel.CloseProjectCommand` (or similar) that
   clears `CurrentProject` without touching disk — the explicit "I'm done with
   this project" action a desktop app needs.

**Scope the per-project paths to it.** Once `CurrentProject` exists, decide
(this needs a real decision, not an assumption — see Open Question below) how
far to go:

- *Minimal*: keep `ShowImport.ShowDirectory` / `Audio.AudioFilePath` /
  `ShowImport.XsqOutputPath` as free-standing fields, but default
  `XsqOutputPath` to `{CurrentProject.RootDirectory}/exports/…` instead of an
  empty text box, and gate the Generate & Export screen's prerequisite list
  (already built in the nav pass) on `HasCurrentProject` too, alongside its
  existing `ShowImport.IsLoaded` / `Audio.HasProbedAsset` checks.
- *Full*: persist the show directory / audio path / export path **into the
  project manifest itself** (an additive field, following the existing
  `with`-pattern precedent in `AppSettings`), so re-opening a project restores
  them automatically instead of restoring only the last-used globals. This is
  the materially better outcome (a project becomes a real saved unit of work)
  but touches `ProjectManifest`'s on-disk schema and `IProjectService`, which
  are shared, tested contracts — bigger surface, do only with explicit sign-off.

**Recommendation:** do the Minimal version first, ship it, then decide about
Full once "which screen does X belong to" has been lived with for a bit.

### UI consequence

- `MainWindow`'s nav rail and the Welcome screen gain a real "Project: *Demo
  Song*" indicator (title bar or a line under the nav rail header), replacing
  the silence that follows clicking Create/Open today.
- Step 1's nav checkmark changes from `Projects.IsLoaded` to
  `shell.HasCurrentProject` — the thing it was always supposed to mean.
- `ProjectsViewModel`'s screen gets a visible "current project" card instead of
  (or above) the recent-projects list, so closing the loop is visible, not just
  internally correct.

### Work estimate

Small-to-medium. The hard part is the return-type/contract change on
`CreateProjectAsync`/`OpenProjectAsync` plus its existing tests
(`Step006ShellWiringTests.cs` and whatever `ProjectsViewModelTests` exist) —
expect to touch 3-4 test files. The view changes are incremental on top of the
nav-rail pattern already established.

### Open question (ask before starting)

Minimal vs. Full scoping above. Minimal is strictly additive and safe to just
do; Full changes a persisted file format other code depends on and should be a
deliberate choice, not a default.

---

## 2. Surface the 15 orphaned screens/view-models

### The problem

`ShellViewModel` wires 11 of the 26 screen-shaped view-models under
`src/XlightsAI.Studio.Shell/`. The other 15 exist, have constructors, have
passing unit tests, and are invisible to a user:

**Not referenced by `ShellViewModel` at all** (not instantiated, no `ShellScreen`
entry, no nav command) — 12 view-models, ~3,100 lines:

| View-model | What it does (per its own code) | Entry point |
|---|---|---|
| `StemMixerViewModel` | Six-track instrument stem mixer: solo/mute, per-stem confidence/activity, selection → choreography handoff | `LoadManifest(StemManifest)` |
| `RoleRoutingEditorViewModel` | Assigns musical roles (lead/rhythm/bass/atmosphere) to props | — |
| `VisualBudgetEditorViewModel` | Tunes visual attention budget / density per section | — |
| `VisualStoryEditorViewModel` | Section energy arc / story shape editor | — |
| `TransitionRecipeEditorViewModel` | Effect transition recipe editor | — |
| `SelectionContextEditorViewModel` | Edits the captured selection context (scope chips: time range, props, locks) that AI edits key off of | — |
| `StructuredCommandQuickActionsViewModel` | One-click structured edit commands (deterministic, no free-text) | — |
| `AiEditRequestPanelViewModel` | Natural-language scoped AI edit request drawer | — |
| `AiEditProposalReviewViewModel` | Review/accept/reject an AI edit proposal | — |
| `MotifPropagationPanelViewModel` | Propagate an edit across all linked motif occurrences | — |
| `CandidateCompareViewModel` | Side-by-side original/candidate comparison, score breakdown, accept/reject | `LoadAsync(...)` |
| `RenderPreviewViewModel` | Render preview + export readiness | `LoadAsync(...)` |

**Wired into the shell, with tests, with zero markup** (temporarily patched over
in the nav pass with a shared `UnbuiltScreenView` placeholder so navigating to
them doesn't paint a blank window) — 3 view-models, ~940 lines:

| View-model | `ShellScreen` | Placeholder text currently shown |
|---|---|---|
| `PianoRollViewModel` | `PianoRoll` | "An editable note view of the detected melody…" |
| `PhraseRelationshipInspectorViewModel` | `StructureInspector` | "How the song's phrases relate…" |
| `DisplayPathEditorViewModel` | `DisplayPathEditor` | "A graphical editor for where your props physically sit…" |

These three map directly to **S10 Piano Roll**, **S11 Song Structure and
Motifs**, and **S05 Display Path Editor** in
`docs/UI-UX-SCREEN-BLUEPRINTS.md`, which already has a layout/interactions/
states/acceptance spec for each. Building their views is "turn the existing
blueprint into Avalonia XAML against an already-implemented, already-tested
view-model" — no new design work needed, just execution.

The 12 fully-orphaned ones correspond to blueprint screens too (S08 Stem Mixer,
S12 Choreography Designer, S14 Scoped AI Edit Drawer, S15 Candidate Comparison,
S16 Render Preview, plus the structured-command/selection-context/motif-
propagation pieces that sit inside S13/S14 per the blueprint's "dockable
surfaces" section) — but have no `ShellScreen` entry or nav command yet, so
there's an extra step before a view: deciding where each one lives.

### Why this matters more than any individual screen

This is ~40% of the application's engineering, fully built and tested, that a
user cannot reach. The three placeholder screens prove the pattern already
works end-to-end (nav entry → `IsShowing…` flag → activation-switch case →
view model with tests); the 12 fully-orphaned ones need that same plumbing
added before a view is even relevant.

### Plan of attack

Do **not** attempt all 15 at once. Sequence by payoff and dependency:

**Phase 2a — finish what the nav pass already wired (3 screens).**
Replace each `UnbuiltScreenView` instance in `MainWindow.axaml` with a real
view, one at a time, against `docs/UI-UX-SCREEN-BLUEPRINTS.md`'s S05/S10/S11
sections:
1. `PianoRollViewModel` → Piano Roll view (S10). Needs a pitched-keyboard +
   note-block layout; check `PianoRollViewModel`'s public surface for what's
   already modeled (note list, confidence flags, selection) before designing
   the control — don't re-derive from the blueprint alone.
2. `PhraseRelationshipInspectorViewModel` → Song Structure view (S11).
3. `DisplayPathEditorViewModel` → Display Path Editor (S05) — this one has a
   canvas/drag-ordering interaction per the blueprint; likely the most work of
   the three.

Each is independently shippable and has zero risk to the rest of the shell —
delete one `UnbuiltScreenView` line, add one real view, done.

**Phase 2b — decide where the 12 fully-orphaned view-models attach.**
Before writing any XAML: for each one, answer (a) is it a new top-level
`ShellScreen` (own nav entry), or (b) is it a drawer/dialog hosted from an
existing screen (the blueprint explicitly calls out S14 Scoped AI Edit Drawer
as "a drawer on S13, not a second canonical editor" — `AiEditRequestPanelViewModel`
and `AiEditProposalReviewViewModel` likely follow that same pattern, not a nav
entry). Group by the blueprint's own screen catalog:

- **Likely new top-level screens:** `StemMixerViewModel` (S08), a Choreography
  Designer screen composing `RoleRoutingEditorViewModel` +
  `VisualBudgetEditorViewModel` + `VisualStoryEditorViewModel` +
  `TransitionRecipeEditorViewModel` (S12 — the blueprint shows these as one
  screen's panels, not four), `CandidateCompareViewModel` (S15),
  `RenderPreviewViewModel` (S16, and likely folds into/extends the existing
  Generate & Export screen rather than becoming a fully separate one — check
  for overlap with `ShowImportViewModel`'s export section before building a
  parallel path).
- **Likely drawers/panels on the Timeline screen, not new nav entries:**
  `AiEditRequestPanelViewModel`, `AiEditProposalReviewViewModel`,
  `SelectionContextEditorViewModel`, `StructuredCommandQuickActionsViewModel`,
  `MotifPropagationPanelViewModel` — all S13/S14-adjacent per the blueprint's
  "Dockable surfaces / modal dialogs (not separate primary screens)" table.

This grouping is a judgment call to confirm, not execute blind — flag it before
committing to nav structure, since adding a `ShellScreen` enum value is a
wider-blast-radius change (touches the activation switch, the
`OnPropertyChangedAll`/`RaiseCanExecuteChanged` boilerplate lists, and
`ShellNavigationView`) than adding a drawer to an existing screen.

**Phase 2c — build.** Once 2b's grouping is confirmed, each screen/drawer is
independent work, same shape as Phase 2a: nav plumbing (if new) → view against
the matching blueprint section → wire to the existing, already-tested
view-model.

### Work estimate

- Phase 2a: small, 3 independent slices, no architectural decisions needed.
- Phase 2b: a short working session, no code — just confirm the
  screen-vs-drawer grouping above (or correct it) before 2c starts.
- Phase 2c: large — effectively 7-9 more screen/drawer builds (Choreography
  Designer collapses 4 view-models into 1 screen; the 5 drawer candidates are
  smaller than full screens). Expect this to be the bulk of remaining shell
  work on the app.

### Open question (ask before starting 2b)

Confirm the screen-vs-drawer split above against the blueprint before any nav
enum changes — it's cheap to get this right up front and expensive to re-plumb
a `ShellScreen` after a drawer turns out to need a dialog host instead (or vice
versa).

---

## Suggested order

1. **Item 1 (current project)**, Minimal scoping. Small, self-contained,
   unblocks a real piece of missing state other work will otherwise route
   around.
2. **Item 2, Phase 2a** (the 3 already-wired placeholder screens). Also small,
   also self-contained, immediately replaces a known stop-gap
   (`UnbuiltScreenView`) with real product.
3. **Item 2, Phase 2b** — a short planning conversation, not a coding task.
4. **Item 2, Phase 2c** — the large remaining build, sequenced screen by
   screen once 2b is settled.
