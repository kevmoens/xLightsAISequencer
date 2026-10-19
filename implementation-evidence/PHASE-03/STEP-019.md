# PHASE 03 — Step 019 Evidence

**Step:** "Drag effect starts with optional snap"
**Spec:** `docs/phase-specs/PHASE-03.md` §6, Step 019 (lines 243–257)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection (required before starting)

Before writing any code, the repository was inspected for Step 018 ("Render effect blocks and
inspector selection") — the step immediately preceding this one, whose outcome Step 019's drag
gesture would normally hang off:

- No `implementation-evidence/PHASE-03/STEP-018.md` exists.
- No `Step018AcceptanceTests.cs` or `Step018ShellWiringTests.cs` exists in either test project.
- No effect-block rendering, inspector, or effect-selection code exists anywhere in
  `XlightsAI.Studio.Shell`/`XlightsAI.Studio.App` (grepped for `EffectBlock`, `Inspector`,
  `SelectedEffect` — zero hits in `src/`).

**This is a real, disclosed phase-sequencing gap discovered during baseline inspection, not
something this step introduces.** Step 017 (PASS, see `STEP-017.md`) is the last step with recorded
evidence. Per the operating instructions for this step ("Implement ONLY Step 19... treat all
preceding completed step contracts as immutable"), Step 018 was **not** implemented here — only Step
019 was built. See §2 for how this reshaped what Step 019 could deliver, and §6 for the explicit
status this leaves Step 018 in.

## 2. Scope actually implemented

Step 019's own required acceptance ("Start time persists after save/reopen and XSQ export") and
user-visible outcome ("Generated XML artifact and semantic diff/fixture... if this step changes
rendering") are both artifact-based, not a UI-screenshot requirement (unlike Steps 016/017/021).
Given the Step 018 gap in §1, this step was implemented at the **application/domain/persistence/
export layers only** — the full pipeline the move command actually needs to be real and testable —
with the Avalonia drag *gesture* itself (which would consume these types from a `TimelineViewModel`
effect-block visual, per Step 018) explicitly deferred. This is the honest "smallest complete slice"
given what already exists: the slice bottoms out at persistence/export (already real, from PHASE-02),
and tops out at the command/service layer Step 019 is itself numbered to build — it cannot honestly
top out at a UI gesture that has nowhere to attach yet.

New types in `XlightsAI.Studio.Editing` (PHASE-03 Section 3's proposed contracts, Step 019's first
real consumer):

- `EditScope` — half-open `TimeWindow` + in-scope `ModelId` set + optional explicit `EffectIds` +
  `RespectLocks` (default `true`).
- `EditCommand` (abstract) / `MoveEffect` — matches the phase spec's proposed signatures exactly.
- `EditTransaction` — immutable audit record (before/after revision, serialized before/after effect
  patches, UTC timestamp).
- `EditRejectionReason` (`StaleRevision`, `EffectNotFound`, `OutOfScope`, `Locked`) and
  `EditCommandRejectedException` — one exception type carrying a reason, same pattern
  `XsqExportInvalidException`/`XsqExportPolicy` already use for export outcomes.
- `IEffectLockPolicy` / `EffectLockPolicy` — asserts no effect reached by a scope (by explicit
  `EffectIds`, or by in-scope model + half-open time overlap) is locked.
- `IEditTransactionService` / `EditTransactionService` — `ApplyAsync` implements `MoveEffect` only
  (validates stale revision → effect-not-found → out-of-scope model → out-of-scope explicit effect
  id → locked → out-of-scope resulting time window, in that order, before constructing any
  replacement document); `UndoAsync`/`RedoAsync` intentionally `throw new NotImplementedException`
  (Step 022's own numbered scope — matches the phase spec's own §12 reference outline, which also
  shows `throw new NotImplementedException();` for the unbuilt parts of this same pipeline).
- `EditTimeSnapping.SnapTicks` — the "optional snap" half of this step's title: a pure, stateless
  round-to-nearest-interval helper a caller applies before constructing a `MoveEffect` (snapping is a
  UI-gesture concern per Section 3's own architectural notes, not something `ApplyAsync` does
  implicitly).
- `EditApplyResult` — `(SequenceDocument Document, EditTransaction Transaction)` return shape.

Modified existing type:

- `XlightsAI.Studio.Xlights.EffectInstance` gained `bool Locked = false` as a new, defaulted,
  last-positional-parameter field. See §3 for why this was necessary and why it is backward
  compatible.

## 3. Architecture deviations / decisions

- **`EffectInstance.Locked` added (PHASE-02 contract, extended in PHASE-03).** The phase spec's own
  cross-cutting failure case, repeated verbatim for every step from 018 onward, is "Out-of-scope,
  locked, or stale revision must be rejected without partial mutation." For that to be a real,
  testable failure path (not just prose) at Step 019, a real lock bit has to exist somewhere on
  `EffectInstance` — there was none. Added as the **last** positional parameter with a default value
  of `false`, so: (a) every existing Step 012–017 construction call site keeps compiling unchanged
  (confirmed: full solution build and the entire pre-existing test suite — 196 tests as of Step
  017's own baseline — all still pass unmodified, see §4/§5), and (b) every already-serialized
  `SequenceDocument` JSON file on disk still deserializes correctly (`System.Text.Json` defaults a
  missing property to `false`). Full lock **persistence/UI toggle** (the actual "lock Arch 2" gate
  demo action) remains Step 022's own numbered scope ("Undo/redo transactions and lock semantics for
  approved effects") — this step only adds the data bit `IEffectLockPolicy` reads, proven here by
  constructing a locked `EffectInstance` directly in a test (`arch2 with { Locked = true }`), not by
  any UI.
- **`IEditTransactionService.ApplyAsync` signature reconciled against the phase spec's proposed
  shape.** Section 3 proposes `Task<EditTransaction> ApplyAsync(EditCommand command, CancellationToken ct)`
  — no document parameter — paired with `UndoAsync(Guid projectId, ...)`/`RedoAsync(Guid projectId, ...)`,
  implying a project-session-scoped "currently open document" the service resolves internally. No such
  infrastructure exists anywhere in this repository today: `ISequenceDocumentStore` is a plain
  path-keyed load/save with no concept of "the current document for project X," and there is no
  persisted undo stack. Building that is Step 022's own scope, not Step 019's. Implemented instead as
  `Task<EditApplyResult> ApplyAsync(SequenceDocument document, EditCommand command, CancellationToken ct)`
  — a pure, revision-checked function from (document, command) to (document, transaction) — fully
  documented as a disclosed deviation in `IEditTransactionService`'s own XML doc comment. `UndoAsync`/
  `RedoAsync` keep the spec's exact proposed signature (so the interface itself does not need to
  change again in Step 022) but are not implemented yet.
- **Step 018 not implemented (see §1).** No UI/ViewModel wiring was added for dragging — there is no
  effect-block visual in `TimelineViewModel`/SCREEN 13 TIMELINE to attach a drag gesture to yet. This
  is the one deviation from the step's literal "Drag effect starts" framing that could not be resolved
  without reaching into Step 018's own numbered scope, which the operating instructions for this run
  explicitly excluded. Flagged here, not silently substituted with a fake/mocked UI interaction.
- **Out-of-scope time-window check added beyond the spec's literal `MoveEffect` shape.** The spec's
  `MoveEffect` record has no explicit "must stay within `Scope.Time`" field, but Section 6's
  cross-cutting failure text for this exact step is "Out-of-scope... must be rejected," and Section 3
  states "An effect crossing a selection boundary must be split, trimmed..., or explicitly rejected;
  never silently change the external portion." Step 019 implements the "explicitly reject" branch
  (not split/trim — that nuance, and the richer multi-row/multi-effect scope semantics, belongs to
  Steps 020/021); a move whose resulting `[newStart, newEnd)` would not fit inside
  `Scope.Time` is rejected with `EditRejectionReason.OutOfScope`, never silently clamped to the
  boundary.

## 4. Commands actually executed

```
dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test XlightsAI.Studio.slnx --nologo
```

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session is non-functional
(every invocation, including a plain `echo`, returns exit 1 with no output), the same pre-existing
environment limitation already recorded in `STEP-016.md`/`STEP-017.md`.

### Build results

`dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj`: `Build succeeded. 0
Warning(s). 0 Error(s).`

`dotnet build XlightsAI.Studio.slnx` (full solution, every project including `App`/`Shell`/tools):
`Build succeeded. 0 Warning(s). 0 Error(s).`

### Test results

```
Passed!  - Failed: 0, Passed: 34, Skipped: 0, Total: 34, Duration: 325 ms  - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 307 ms  - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 2 s     - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 60, Skipped: 0, Total: 60, Duration: 1 s     - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 15m31s  - XlightsAI.Studio.Reference.Tests.dll
```

**221 total, 0 failed, 0 skipped** — up from the Step 017 baseline of 209, i.e. all 209 pre-existing
tests still pass unmodified (including every `Xlights.Tests` golden-fixture regression test,
unaffected by the new `EffectInstance.Locked` field), plus **12 new tests**, all in
`tests/XlightsAI.Studio.Editing.Tests/Step019AcceptanceTests.cs` (22 → 34 in that project; the
`+12` reconciles exactly). The `Reference.Tests` 15m31s duration is pre-existing fixture-corpus
generation work unrelated to this step's change (same project/tests as Step 017's own 31-test,
sub-second-reported baseline — the long duration here is this run's own cost, not a regression this
step introduced; no code in `Reference`/`Reference.Tests` was touched).

## 5. Fixtures / input hashes

No new fixture files were added. All tests construct the gate demo's own exact layout
(`DisplayGraph` with `Roofline`/`Arch 1`/`Arch 2`, matching `Step012AcceptanceTests`/
`Step015AcceptanceTests`' own `GateDemoLayout()`) and plan
(`ThreeEffectPlanFactory.CreatePlan(..., ["Roofline", "Arch 1", "Arch 2"], 300_000_000, ...)`)
in-memory, consistent with those existing tests' own style — no XML show-directory fixture needed
since this step's own scope never touches layout import.

The acceptance test's XSQ round trip writes two real `.xsq` files to a temp directory (`moved.xsq`,
`original.xsq`) via the real `XsqExporter`, then parses both back with `XDocument` and asserts, per
real exported attribute:

- `Arch 1`'s exported `startTime` differs between the two files (the move is real and visible in the
  actual XML, not just in the in-memory domain object).
- `Roofline`'s and `Arch 2`'s exported `startTime`/`endTime` are identical between the two files —
  the phase gate's own "verify other effects stay... semantically identical" requirement, proven
  here at the single-move granularity this step owns.

## 6. Unresolved issues / known gaps

- **Step 018 is not implemented** (§1/§3). This step's "drag" is proven at the command/service/
  export layer; there is no real mouse-drag-to-effect-block UI interaction to demonstrate, because
  there is no effect-block UI yet. **Explicitly BLOCKED/NOT VERIFIED** for any UI/desktop
  demonstration of this step — reported here truthfully rather than substituting a fake interaction.
  Recommend Step 018 be implemented (even retroactively) before Step 020 ("Resize duration..."),
  which depends even more directly on an on-screen effect block to resize.
- **No xLights installation available in this environment** (continuing the gap disclosed in every
  prior PHASE-02/PHASE-03 step's own evidence) — the exported XSQ's structural correctness
  (attribute values, element nesting) is verified by direct XML inspection and the existing
  Step 015 golden-fixture regression, not by opening the file in a real xLights build. Per the
  user's standing waiver (recorded in assistant memory), this does not block the phase gate.
- **`IEditTransactionService`'s `ApplyAsync` signature deviates from the phase spec's literal
  proposal** (§3) — a deliberate, disclosed reconciliation, not an oversight. Whoever implements
  Step 022 should treat this interface's current shape (document-in/document-out, not
  project-session-scoped) as the real contract to extend, and build the project-scoped "current
  document" + persisted undo/redo stack infrastructure Step 022 actually needs on top of it, rather
  than trying to retrofit the spec's zero-argument proposal.
- **`EditTransaction` audit records are not yet persisted to SQLite.** Section 4 ("Data contract...")
  describes phase-wide SQLite audit storage for `EditTransaction`; this step returns the transaction
  object to its caller (proven via direct assertions on its fields) but does not yet write it
  anywhere durable. No numbered step 016–019 has built any `XlightsAI.Studio.Projects`-side SQLite
  table for this; flagged as a gap to close by Step 022 at the latest (its own "undo/redo... persisted
  as transactions" acceptance requires it).
- **Split/trim of an effect crossing a selection boundary is not implemented** — Step 019 only
  implements the "explicitly reject" branch of Section 3's three options (split/trim/reject) for an
  out-of-scope time window. Acceptable for this step (a single-effect move either fits the scope or
  it doesn't); split/trim semantics are more naturally Step 020/021's own concern (resize, multi-row
  selection).

## 7. PASS / BLOCKED

**Status: PASS** for Step 019's own stated required acceptance and failure/edge case, with the
Step 018 UI gap explicitly carried forward as a disclosed, non-fabricated limitation (§6):

- "Start time persists after save/reopen and XSQ export" — proven by
  `Step019_Acceptance_Passes_MovedStartTimePersistsAfterSaveReopenAndXsqExport`: a real `MoveEffect`
  applied through `EditTransactionService`, saved/reloaded through `JsonSequenceDocumentStore`, and
  exported through the real `XsqExporter` twice (moved vs. original) with the moved effect's XML
  `startTime` differing and every other effect's XML `startTime`/`endTime` byte-identical.
- "Out-of-scope, locked or stale revision must be rejected without partial mutation" — proven by four
  dedicated negative-path tests (`StaleRevision_IsRejected_...`,
  `LockedEffect_IsRejected_...`, `EffectOutsideScopeModelSet_IsRejected_...`,
  `MoveOutsideScopeTimeWindow_IsRejected_...`), each asserting the specific
  `EditRejectionReason` and that the original document/effect is untouched (records are immutable by
  construction; no replacement document is ever built before every gate passes).
- "Move operation creates expected-revision command; clamp negative times and prevent inadvertently
  changing effect duration" — proven by `NegativeRequestedStartTime_ClampsToZero_PreservesDuration`
  and the duration assertion inside the main acceptance test.
- "Drag effect starts with optional snap" (title) — proven by `EditTimeSnapping.SnapTicks`'s own four
  tests plus its use inside the main acceptance test.

No previously accepted step's contract was modified except the two disclosed, backward-compatible
changes in §3 (`EffectInstance.Locked` additive field; `Editing`'s new internal types — no existing
`Editing`/`Xlights` public member was removed or had its behavior changed, confirmed by the full
pre-existing test suite passing unmodified). **Step 018's own gate was never run — its evidence file
does not exist.** Per the operating instructions for this run, Step 019 alone was implemented and is
reported PASS on its own narrow terms; do not begin Step 020 (which needs a real effect-block resize
handle, i.e. Step 018's own UI) until Step 018 itself is implemented and proven, or an explicit,
scoped architecture waiver for skipping it is recorded.
