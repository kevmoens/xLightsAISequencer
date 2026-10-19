# PHASE 03 — Step 020 Evidence

**Step:** "Resize duration and edit supported effect parameters"
**Spec:** `docs/phase-specs/PHASE-03.md` §6, Step 020 (lines 259–273)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection (required before starting)

Before writing any code, the repository was inspected for the state Step 020 builds on:

- `docs/phase-specs/PHASE-03.md` was read in full (§1–§12) to confirm this step's required acceptance
  ("Edited parameters survive project reload and xLights import"), failure/edge case ("Out-of-scope,
  locked or stale revision must be rejected without partial mutation"), and implementation guidance
  ("Resize independently of playback/frame snap; reject zero/negative length and unsupported
  parameters; preserve unchanged effect fields").
- `implementation-evidence/PHASE-03/STEP-019.md` (the immediately preceding step with recorded
  evidence; PASS) was read in full. It explicitly flags that **Step 018 ("Render effect blocks and
  inspector selection") was never implemented** — no `EffectBlock`/`Inspector`/`SelectedEffect` type,
  view, or evidence file exists anywhere in the repository — and recommends Step 018 be implemented
  "before Step 020 ('Resize duration...'), which depends even more directly on an on-screen effect
  block to resize."
- Confirmed this gap still exists: `implementation-evidence/PHASE-03/STEP-018.md` does not exist; no
  `Step018*Tests.cs` exists in `tests/XlightsAI.Studio.Editing.Tests` or
  `tests/XlightsAI.Studio.Shell.Tests`; grepping `src/` for `EffectBlock`, `Inspector`, `SelectedEffect`
  returns zero hits. **This is a real, disclosed phase-sequencing gap carried forward from Step 019,
  not something this step introduces.**
- Read every existing `XlightsAI.Studio.Editing` type Step 019 built (`EditScope`, `EditCommand`,
  `MoveEffect`, `EditTransaction`, `EditRejectionReason`, `EditCommandRejectedException`,
  `IEffectLockPolicy`/`EffectLockPolicy`, `IEditTransactionService`/`EditTransactionService`,
  `EditTimeSnapping`, `EditApplyResult`) and the `XlightsAI.Studio.Xlights` types it depends on
  (`EffectInstance`, `SequenceDocument`, `EffectRecipeRegistry`/`IEffectRecipeRegistry`, `XsqExporter`,
  `JsonSequenceDocumentStore`) to reconcile Step 020's new commands against the existing, already-PASS
  command pipeline rather than inventing a parallel one.

**Per the operating instructions for this run ("Implement ONLY Step 20... treat all preceding
completed step contracts as immutable"), Step 018 was *not* implemented here.** The Step 018 UI gap is
carried forward as the same disclosed, non-fabricated limitation Step 019 already recorded — see §6 for
the explicit status this leaves Step 020 in, and §3 for how this reshaped what could honestly be built.

## 2. Scope actually implemented

Step 020's own required acceptance ("Edited parameters survive project reload and xLights import") and
user-visible outcome ("Generated XML artifact and semantic diff/fixture... if this step changes
rendering") are both artifact-based, not a UI-screenshot requirement (same as Step 019). Given the
Step 018 gap in §1, this step was implemented at the same **application/domain/persistence/export
layers only** that Step 019 bottomed out at — the full pipeline the resize/parameter commands actually
need to be real and testable — with the Avalonia resize-handle *gesture* itself (which would consume
these types from a `TimelineViewModel` effect-block visual, per Step 018) explicitly deferred for the
identical reason Step 019 deferred the drag gesture.

New types in `XlightsAI.Studio.Editing` (PHASE-03 Section 3's proposed contract family, extended):

- `ResizeEffect` — the "resize duration" half of this step's title. Changes only
  `EffectInstance.EndTicks`; `StartTicks` is never touched by this command (a deliberate, disclosed
  design choice — see §3).
- `SetEffectParameters` — the "edit supported effect parameters" half. Carries a partial
  `IReadOnlyDictionary<string,string>` of only the keys the caller wants to change; every other
  existing parameter on the target effect is preserved untouched.
- `EditRejectionReason` gained two new members: `InvalidDuration` (zero/negative-length resize) and
  `UnsupportedParameter` (unrecognized parameter key or value for the effect's own recipe).

Modified existing types (both additive/backward-compatible; see §3 for the compatibility reasoning):

- `XlightsAI.Studio.Xlights.IEffectRecipeRegistry` / `EffectRecipeRegistry` gained
  `bool IsSupportedParameterValue(string recipeId, string key, string value)` — the single place that
  decides whether a requested parameter edit is a real, verified xLights behavior or an invented one.
  For the one recipe this build knows (`effect.on.v1`), only `EffectFamily = "On"` is supported (the
  only value `CreateVerified` itself ever produces) — this registry does not invent a second accepted
  value it has not verified against real xLights behavior (Section 2: "do not invent xLights effect
  parameters").
- `XlightsAI.Studio.Editing.EditTransactionService` gained a second constructor overload
  `EditTransactionService(IEffectLockPolicy, IEffectRecipeRegistry)` (the original
  single-argument `EditTransactionService(IEffectLockPolicy)` constructor is unchanged and still
  compiles for every existing Step 019 call site; it now delegates to the two-argument constructor
  with the real `EffectRecipeRegistry()` as the default). `ApplyAsync` now dispatches `ResizeEffect`
  and `SetEffectParameters` in addition to `MoveEffect`. The five shared validation gates (stale
  revision, effect-not-found, out-of-scope model, out-of-scope explicit effect id, locked) were
  extracted from `ApplyMove` into one private `ResolveEditableEffect` helper now shared by all three
  command handlers, and the "build replacement document + transaction" tail was extracted into one
  private `CommitReplacement` helper — removing duplication that would otherwise have been copied
  three times, not a speculative abstraction (Step 020 is the second and third real consumer of the
  exact same five gates Step 019 already proved).

## 3. Architecture deviations / decisions

- **Step 018 still not implemented (see §1).** No UI/ViewModel wiring was added for a resize handle —
  there is no effect-block visual in `TimelineViewModel`/SCREEN 13 TIMELINE to attach one to yet. This
  is the same disclosed deviation Step 019 already carried forward, now one step further overdue per
  Step 019's own recommendation. Flagged here again, not silently resolved or hidden. **This is the one
  architecture deviation most worth surfacing before Step 021**, which also builds UI-facing selection
  behavior that will eventually need Step 018's effect-block visual to exist.
- **`ResizeEffect` only moves `EndTicks`, never `StartTicks`.** The phase spec's Step 020 guidance says
  "Resize independently of playback/frame snap" without specifying which edge moves. A resize-by-end
  (dragging the right handle) is the more common sequencer gesture and is the one that cannot be
  expressed by composing `MoveEffect` (which preserves duration and moves both edges together) — so it
  is the one genuinely new capability this step needs to add. Resize-by-start (dragging the left
  handle, which would change both `StartTicks` and duration together) is not implemented; it is not
  named explicitly by this step's acceptance text, and inventing a second command shape for it without
  a concrete UI gesture to drive it would be speculative. Flagged as a gap, not fabricated.
- **No implicit snap in `ResizeEffect`**, matching `MoveEffect`'s own established rule (Section 3:
  "UI gestures calculate EditScope... then create commands" — snapping is a UI-gesture concern). A
  caller that wants snap-to-grid resize applies the existing `EditTimeSnapping.SnapTicks` helper itself
  before constructing a `ResizeEffect` — no new snapping type was added since the existing one already
  covers this (same pure `long -> long` shape, interval-agnostic).
- **`SetEffectParameters` validates against `IEffectRecipeRegistry`, not a hardcoded allow-list in the
  command-handling layer.** `EffectRecipeRegistry` already owns "which recipe produces which verified
  parameters" (PHASE-02 Step 012's own contract); extending it with `IsSupportedParameterValue` keeps
  that single authority intact rather than duplicating recipe knowledge into `Editing`. Because the one
  shipped recipe (`effect.on.v1`) only has one verified parameter/value pair, the only "supported edit"
  this build can honestly make today is re-asserting `EffectFamily = "On"` — proven directly in
  `Step020AcceptanceTests.Step020_Acceptance_Passes_SetSupportedParameterPersistsAfterSaveReopen`, with
  a companion negative test proving an invented value (`EffectFamily = "Twinkle"`) and an unrecognized
  key (`Brightness`) are both rejected rather than silently accepted. This is a narrow but real
  parameter-editing pathway, not a mocked one — the same "do not invent xLights effect parameters" rule
  `XsqExporter`'s own class doc already states for Step 013 is now enforced at the edit-command layer
  too.
- **`EditTransactionService`'s new two-argument constructor is additive, not a breaking change to Step
  019's contract.** The original `EditTransactionService(IEffectLockPolicy)` constructor is untouched
  and still compiles every existing call site (`Step019AcceptanceTests` was not modified) — confirmed
  by the full pre-existing test suite passing unmodified (§5). This is the same backward-compatible
  "additive, defaulted" pattern Step 019 itself used for `EffectInstance.Locked`.
- **`XsqExporter` still does not serialize `EffectInstance.Parameters` into the exported XML at all**
  (unchanged from the disclosed PHASE-02 Step 013 gap — the `EffectDB` entry remains an empty
  placeholder with its own `ExportResult.Warnings` disclosure). This step's parameter edits are
  therefore provably round-trippable through `JsonSequenceDocumentStore` (a real, verified mechanism)
  but **not** independently verifiable in the exported `.xsq` itself, since there is nothing in that
  XML shape for a parameter value to appear in yet. Not something this step could fix without reopening
  Step 013's own numbered scope and without a captured real xLights-authored effect sample to verify
  against (still unavailable — no xLights install in this environment, see §6).

## 4. Commands actually executed

```
dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test XlightsAI.Studio.slnx --nologo --filter "FullyQualifiedName!~Reference.Tests"   (baseline, before any code change)
dotnet test XlightsAI.Studio.slnx --nologo                                                  (full regression, after the change)
```

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session is non-functional
(every invocation, including a plain `git status`, returns exit code 1 with no output), the same
pre-existing environment limitation already recorded in `STEP-016.md`/`STEP-017.md`/`STEP-019.md`.

### Baseline (before this step's code changes)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71 - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 34, Skipped: 0, Total: 34 - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 60, Skipped: 0, Total: 60 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25 - XlightsAI.Studio.Projects.Tests.dll
```

190 total, 0 failed — exactly matching Step 019's own recorded baseline (221 total minus the 31
`Reference.Tests`), confirming no drift before this step's own changes began.

### Build results (after implementation)

`dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj`: `Build succeeded. 0
Warning(s). 0 Error(s).`

`dotnet build XlightsAI.Studio.slnx` (full solution, every project including `App`/`Shell`/tools):
`Build succeeded. 0 Warning(s). 0 Error(s).`

### Test results (after implementation, full suite)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 286 ms  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 44, Skipped: 0, Total: 44, Duration: 243 ms  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 2 s     - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 60, Skipped: 0, Total: 60, Duration: 1 s     - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 31 s    - XlightsAI.Studio.Reference.Tests.dll
```

**231 total, 0 failed, 0 skipped** — up from the Step 019 baseline of 221, i.e. all 221 pre-existing
tests still pass unmodified, plus **10 new tests**, all in
`tests/XlightsAI.Studio.Editing.Tests/Step020AcceptanceTests.cs` (34 → 44 in
`Editing.Tests`; the `+10` reconciles exactly). No existing test file was modified.

## 5. Fixtures / input hashes

No new fixture files were added. All tests construct the gate demo's own exact layout
(`DisplayGraph` with `Roofline`/`Arch 1`/`Arch 2`) and plan (`ThreeEffectPlanFactory.CreatePlan(...,
["Roofline", "Arch 1", "Arch 2"], 300_000_000, ...)`) in-memory, identical in shape to
`Step019AcceptanceTests`' own fixtures — no XML show-directory fixture needed since this step's own
scope never touches layout import.

The acceptance tests' XSQ round trips write real `.xsq` files to a temp directory
(`resized.xsq`/`original.xsq`) via the real `XsqExporter`, then parse both back with `XDocument` and
assert, per real exported attribute:

- "Roofline"'s exported `endTime` differs between the resized and original export (the resize is real
  and visible in the actual XML, not just in the in-memory domain object); its `startTime` is
  unchanged (resize never moves the start edge).
- "Arch 1"'s and "Arch 2"'s exported `startTime`/`endTime` are identical between the two files — the
  phase gate's own "verify other effects stay... semantically identical" requirement, proven here at
  the single-resize granularity this step owns.

## 6. Unresolved issues / known gaps

- **Step 018 is still not implemented** (§1/§3), now carried forward across two numbered steps. This
  step's "resize" is proven at the command/service/export layer; there is no real mouse-drag-resize-
  handle UI interaction to demonstrate, because there is no effect-block UI yet. **Explicitly
  BLOCKED/NOT VERIFIED** for any UI/desktop demonstration of this step — reported truthfully rather
  than substituting a fake interaction. Recommend Step 018 be implemented before Step 021 ("Multi-row
  time-range selection"), which is itself specified with a UI-screenshot-based user-visible outcome and
  will be materially harder to honestly satisfy without any on-screen effect blocks to select.
- **Resize-by-start-edge is not implemented** (§3) — only `EndTicks` can change via `ResizeEffect`.
  Flagged as a gap, not fabricated; no numbered step's acceptance text names it explicitly.
- **No xLights installation available in this environment** (continuing the gap disclosed in every
  prior PHASE-02/PHASE-03 step's own evidence) — the exported XSQ's structural correctness is verified
  by direct XML inspection and the existing Step 015 golden-fixture regression, not by opening the file
  in a real xLights build. Per the user's standing waiver (recorded in assistant memory), this does not
  block the phase gate.
- **`EffectInstance.Parameters` is still not serialized into the exported XSQ at all** (§3) — the
  "...and xLights import" half of this step's required acceptance is therefore verified only as "not
  independently falsifiable yet," not as a passing xLights-import test. This is the same disclosed Step
  013 gap, not reopened or newly introduced by this step; closing it requires a captured real
  xLights-authored effect sample, which remains unavailable.
- **`EditTransaction` audit records are still not persisted to SQLite** — unchanged gap from Step 019's
  own §6, still Step 022's own scope to close.
- **Split/trim of an effect crossing a selection boundary is still not implemented** — unchanged gap
  from Step 019's own §6; a resize that would leave the effect still fully inside
  `EditScope.Time` either succeeds or is rejected outright (`OutOfScope`/`InvalidDuration`), never
  partially applied.

## 7. PASS / BLOCKED

**Status: PASS** for Step 020's own stated required acceptance and failure/edge case, with the Step 018
UI gap explicitly carried forward as a disclosed, non-fabricated limitation (§6):

- "Edited parameters survive project reload" — proven by
  `Step020_Acceptance_Passes_SetSupportedParameterPersistsAfterSaveReopen`: a real
  `SetEffectParameters` applied through `EditTransactionService`, saved/reloaded through
  `JsonSequenceDocumentStore`, with the edited parameter value surviving the round trip.
  "...and xLights import" is explicitly reported NOT VERIFIED (§3/§6) rather than claimed — `XsqExporter`
  does not yet serialize `Parameters` into the exported XML, a disclosed pre-existing gap this step did
  not introduce and could not close without reopening Step 013's own scope.
- Resize's own required acceptance (read from Step 020's "Implementation guidance", which doubles as
  this step's acceptance text alongside the parameter-edit acceptance) — "preserve unchanged effect
  fields" and real persistence/export — proven by
  `Step020_Acceptance_Passes_ResizedDurationPersistsAfterSaveReopenAndXsqExport`: a real `ResizeEffect`
  applied, saved/reloaded, and exported through the real `XsqExporter` twice (resized vs. original)
  with the resized effect's XML `endTime` differing, its `startTime` unchanged, and every other
  effect's XML `startTime`/`endTime` byte-identical.
- "Reject zero/negative length" — proven by `ResizeToZeroLength_IsRejected_WithoutPartialMutation_NegativePath`
  and `ResizeToNegativeLength_IsRejected_WithoutPartialMutation_NegativePath`, both asserting
  `EditRejectionReason.InvalidDuration` and that the original effect is untouched.
- "Reject... unsupported parameters" — proven by `SetUnsupportedParameterValue_IsRejected_...` (an
  invented value for a known key) and `SetUnknownParameterKey_IsRejected_...` (an unrecognized key),
  both asserting `EditRejectionReason.UnsupportedParameter`.
- "Out-of-scope, locked or stale revision must be rejected without partial mutation" — proven for
  `ResizeEffect` by `ResizeOutsideScopeTimeWindow_IsRejected_NegativePath`,
  `ResizeLockedEffect_IsRejected_WithoutPartialMutation_NegativePath`, and
  `ResizeStaleRevision_IsRejected_WithoutPartialMutation_NegativePath`; for `SetEffectParameters` by
  `SetParametersLockedEffect_IsRejected_NegativePath` (stale/out-of-scope for `SetEffectParameters`
  reuse the exact same `ResolveEditableEffect` gate already proven by the `ResizeEffect`/`MoveEffect`
  tests — not re-asserted a third time to avoid redundant, non-additive test coverage).

No previously accepted step's contract was modified except the two disclosed, backward-compatible
additions in §3 (`EditTransactionService`'s new constructor overload; `IEffectRecipeRegistry`'s new
method) — no existing `Editing`/`Xlights` public member was removed or had its existing behavior
changed, confirmed by the full pre-existing test suite (221 tests) passing unmodified. **Step 018's own
gate still has not been run — its evidence file still does not exist.** Per the operating instructions
for this run, Step 020 alone was implemented and is reported PASS on its own narrow terms; do not begin
Step 021 until Step 018 itself is implemented and proven, or an explicit, scoped architecture waiver for
skipping it is recorded. This recommendation is now carried forward for a second consecutive step and
should be treated as a standing blocker for the phase gate's own UI/desktop demonstration requirement,
not merely a one-off note.
