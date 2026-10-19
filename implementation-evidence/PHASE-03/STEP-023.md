# PHASE 03 — Step 023 Evidence

**Step:** "Scope-aware export and regeneration contract"
**Spec:** `docs/phase-specs/PHASE-03.md` §6, Step 023 (lines 307–321)
**Date:** 2026-10-11

## 1. Pre-implementation baseline inspection

- Re-read `docs/phase-specs/PHASE-03.md` §6 Step 023 in full: required acceptance ("Hash unaffected
  effects before/after scoped edit; no changes outside scope"), implementation guidance ("Export scoped
  edits without regenerating other effect recipes; hash unaffected effects and inspect XSQ after
  round-trip"), and the step's own failure/edge case ("Invalid model/format/version must fail safely
  without rewriting source files" — the PHASE-02 Step 013/015 wording, reused verbatim for this step).
- Re-read every existing type this step builds directly on: `IXsqExporter`/`XsqExporter` (the real,
  already-verified full-document XSQ writer — `ExportAsync`/`ExportNewAsync`, `XsqExportPolicy`,
  `ExportResult`, the public `FrameIntervalMs` frame-rounding constant), `IXsqStructuralReader`/
  `XsqStructuralReader`/`XsqStructuralDocument`/`XsqStructuralElement`/`XsqStructuralEffectPlacement`
  (the existing "reference reader" from PHASE-02 Step 013, built for exactly this kind of independent
  round-trip inspection), `EditScope` (model/effect-id scope membership semantics, PHASE-03 Step 019),
  and `EditTransactionService.ResolveEditableEffect`'s own existing scope-membership check (`scope.Models
  .Contains(effect.Target)` plus optional `scope.EffectIds`) — reused as the exact, already-established
  definition of "in scope" rather than inventing a second one for this step.
- Confirmed `XlightsAI.Studio.Xlights` has **no** project reference to `XlightsAI.Studio.Editing`
  (`Editing` → `Xlights` → `Contracts`, one direction only, per both `.csproj` files) — so a type that
  needs both `EditScope` (Editing-owned) and the XSQ exporter/reader (Xlights-owned) must live in
  `XlightsAI.Studio.Editing`, not `Xlights`. This is the one real architectural decision this step
  required; recorded in §3.
- Baseline regression before any Step 023 code change, re-run fresh in this session: **269/269 passing**
  (`dotnet test XlightsAI.Studio.slnx --nologo`), matching Step 022's own recorded final count exactly —
  confirmed independently, not assumed from the prior evidence file.

## 2. Scope actually implemented

**New types in `XlightsAI.Studio.Editing`** (pure domain + a thin wrapper around the existing
`Xlights` exporter/reader; no Avalonia types):

- `IScopedXsqExporter` / `ScopedXsqExporter` — wraps a real `IXsqExporter` + `IXsqStructuralReader`
  (constructor-injected, no new singletons). `ExportScopedAsync(layout, before, after, scope,
  outputPath, targetXlightsVersion, policy, ct)`:
  1. Partitions both `before.Effects` and `after.Effects` into "out-of-scope" sets using the exact
     membership rule `EditTransactionService.ResolveEditableEffect` already uses:
     `scope.Models.Contains(effect.Target) && (scope.EffectIds is null ||
     scope.EffectIds.Contains(effect.Id))` defines "in scope"; everything else is "out of scope".
  2. Rejects (no I/O yet) if the *set* of out-of-scope effect ids differs between `before`/`after` (an
     out-of-scope effect was added, removed, or moved into/out of scope).
  3. For every out-of-scope effect id present in both, computes a canonical SHA-256 content hash
     (target, layer, start/end ticks, recipe id/version, locked bit, and every parameter key=value
     sorted by key) and rejects — no I/O yet — the instant any pair differs.
  4. Only once every out-of-scope effect is proven byte-for-semantic-byte unchanged does it call the
     real, already-verified `IXsqExporter.ExportAsync(layout, after, outputPath, targetXlightsVersion,
     policy, ct)` — reusing that exporter's own "Invalid model/format/version must fail safely without
     rewriting source files" validation and atomic-write guarantee verbatim, not duplicating it.
  5. Reads the just-written file back with the real `IXsqStructuralReader` and confirms every
     out-of-scope effect's frame-rounded `[start, end)` millisecond pair actually appears in the file's
     own `<ElementEffects>` section for its target — "inspect XSQ after round-trip" (Section 6)
     satisfied against the real written bytes, not just the in-memory document.
  6. Returns `ScopedExportResult(ExportResult Export, IReadOnlyDictionary<Guid,string>
     OutOfScopeEffectHashes)` — the verified-identical hash per out-of-scope effect id is the literal,
     inspectable evidence for "Hash unaffected effects before/after scoped edit."
- `ScopedExportResult` — the result record described above.
- `ScopedExportContractViolatedException` — thrown by every rejection path in step 2/3/5 above; always
  before any file is written (step 2/3) or in addition to a file that was already written by the
  trusted, independently-validated `IXsqExporter` call and is now proven not to match its own promise
  (step 5 — see §6 for the one disclosed gap this creates).

**No changes to any existing `Xlights`, `Editing`, `Shell`, or `App` type.** `XsqExporter`,
`XsqStructuralReader`, `EditTransactionService`, `EditScope` and every other Step 013–022 contract are
completely unmodified — Step 023 is purely additive, composing existing, already-verified pieces.

## 3. Architecture deviations / decisions

- **`IScopedXsqExporter`/`ScopedXsqExporter` live in `XlightsAI.Studio.Editing`, not
  `XlightsAI.Studio.Xlights`.** The spec's own cross-cutting table (Section 2) names `EditScope` as a
  PHASE-03/`Editing`-owned contract, while `IXsqExporter`/`IXsqStructuralReader` are PHASE-02/`Xlights`-
  owned. Since `Xlights` has no reference to `Editing` (confirmed in §1, one-directional by design —
  `Editing`'s own existing `.csproj` comment from Step 017 already documents this project boundary
  choice), and inventing a second, `Xlights`-local copy of scope-membership semantics would risk
  silently diverging from `EditTransactionService`'s own authoritative definition, the only option that
  does not invert or duplicate an existing boundary is to place the new scope-aware wrapper in
  `Editing`, which already depends on both `Xlights` (for the exporter/reader) and its own `EditScope`.
  No existing project reference direction changed.
- **Scope-contract violation and "invalid model/format/version" are deliberately two distinct
  exception types.** `ScopedExportContractViolatedException` (new, Step 023's own) for "an out-of-scope
  effect changed", vs. the existing `XsqExportInvalidException` (PHASE-02 Step 013, unmodified) for
  "the request itself is malformed" (bad path/version/unresolvable target/unknown recipe) — reusing
  `XsqExportInvalidException` for the latter rather than inventing a parallel copy, since
  `ScopedXsqExporter` delegates directly to the real `IXsqExporter.ExportAsync` for that validation and
  lets its exception propagate unchanged. A caller can distinguish "my scoped-export contract was
  violated" from "my export request was itself invalid" without inspecting message text.
- **Step 5's post-export structural check can observe a file that was already written.** Steps 2–4 all
  guarantee no file exists before a scope violation is reported. Step 5 (reading the just-written file
  back) runs *after* the real `IXsqExporter.ExportAsync` has already completed its own atomic write —
  by that point a file genuinely exists at `outputPath`. Given Steps 2–4 already proved every
  out-of-scope effect's *domain* state is unchanged, and `XsqExporter`'s own serialization is a pure,
  deterministic function of the `SequenceDocument` it is given (traced through its source in §1), this
  step 5 check is expected to never actually fire in practice; it exists as defense-in-depth against a
  mismatch between `XsqExporter`'s real behavior and the frame-rounding formula this file independently
  mirrors (see next bullet), not as the primary gate. This is disclosed, not left implicit.
- **Frame-rounding is intentionally duplicated, not extracted into a shared helper.** `XsqExporter`'s
  own `RoundToFrame`/`TicksToMilliseconds` are `private`; per the task's own instruction to treat
  completed step contracts as immutable without an accepted migration, this step does not change
  `XsqExporter`'s existing member visibility. `XsqExporter.FrameIntervalMs` (already `public`) is reused
  directly; the two-line rounding formula itself is duplicated locally in `ScopedXsqExporter`, with an
  explicit doc comment stating it mirrors `XsqExporter`'s own convention. Flagged as the one piece of
  logic that must be kept in sync by hand if `XsqExporter`'s own rounding ever changes.

## 4. Commands actually executed

```
dotnet build src\XlightsAI.Studio.Editing\XlightsAI.Studio.Editing.csproj --nologo
dotnet test XlightsAI.Studio.slnx --nologo
dotnet test tests\XlightsAI.Studio.Editing.Tests\XlightsAI.Studio.Editing.Tests.csproj --nologo --filter "FullyQualifiedName~Step023" --logger "console;verbosity=normal"
dotnet test XlightsAI.Studio.slnx --nologo
dotnet build src\XlightsAI.Studio.Shell\XlightsAI.Studio.Shell.csproj --nologo
dotnet build src\XlightsAI.Studio.App\XlightsAI.Studio.App.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
```

All run via the PowerShell tool — the sandboxed POSIX Bash tool in this session is non-functional
(every invocation, including a plain `sleep`, returns exit code 1 with no output), the same
pre-existing environment limitation recorded in every prior PHASE-03 step's own evidence.

### Baseline (before any Step 023 code existed)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71 - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 70, Skipped: 0, Total: 70 - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25 - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 72, Skipped: 0, Total: 72 - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31 - XlightsAI.Studio.Reference.Tests.dll
```

269 total, 0 failed — matches Step 022's own recorded final count exactly, confirmed independently in
this session before a single Step 023 line of code existed.

### Build results (after implementation)

`Editing`, `Shell`, `App`, and the full solution all: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Targeted test results (new tests only)

```
tests\XlightsAI.Studio.Editing.Tests — filter "~Step023": 5/5 passed
  Step023_Acceptance_Passes_HashesUnaffectedEffects_NoChangesOutsideScope
  ScopedExport_EffectOutsideScopeChanged_IsRejected_NoFileWritten_NegativePath
  ScopedExport_OutOfScopeEffectRemoved_IsRejected_NoFileWritten_NegativePath
  ScopedExport_InvalidModelTarget_FailsSafely_NoFileWritten_NegativePath
  ScopedExport_BlankTargetVersion_FailsSafely_NoFileWritten_NegativePath
```

### Full regression (after implementation)

```
Passed!  - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 300 ms - XlightsAI.Studio.Xlights.Tests.dll
Passed!  - Failed: 0, Passed: 75, Skipped: 0, Total: 75, Duration: 315 ms - XlightsAI.Studio.Editing.Tests.dll
Passed!  - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 2 s   - XlightsAI.Studio.Projects.Tests.dll
Passed!  - Failed: 0, Passed: 72, Skipped: 0, Total: 72, Duration: 1 s   - XlightsAI.Studio.Shell.Tests.dll
Passed!  - Failed: 0, Passed: 31, Skipped: 0, Total: 31, Duration: 31 s  - XlightsAI.Studio.Reference.Tests.dll
```

**274 total, 0 failed, 0 skipped** — up from the Step 022 baseline of 269 (70→75 in `Editing.Tests`; the
`+5` reconciles exactly against the 5 new tests above). `Xlights.Tests`, `Shell.Tests`,
`Projects.Tests`, `Reference.Tests` are all unchanged (no source in those projects was modified this
step). All 269 pre-existing tests still pass unmodified.

### Manual/desktop demonstration

```
$p = Start-Process -FilePath "src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe" -PassThru
Start-Sleep -Seconds 5
# Still running after 5s: True
Stop-Process -Id $p.Id -Force
```

The real compiled Avalonia desktop app started and ran for 5+ seconds with no exception and no early
exit. Since this step made no Shell/App/XAML changes, this smoke test confirms only that the unrelated
`Editing`/`Xlights` changes did not regress the app's own startup — it is **not** a UI demonstration of
scoped export, because no such UI exists yet (disclosed, not silently implied — see §6, the same
standing gap every PHASE-03 step since 019 has disclosed).

## 5. Fixtures / input hashes

No new fixture files were added. All tests construct the gate demo's own exact layout/plan shape
(`DisplayGraph` with `Roofline`/`Arch 1`/`Arch 2`, `ThreeEffectPlanFactory.CreatePlan(...)`) in-memory,
identical in shape to every other PHASE-03 step's own fixtures. `Step023AcceptanceTests` writes real
`.xsq` files under `%TEMP%\xlai-step023-<guid>\`, deleted by the test class's `IDisposable.Dispose()`
(identical cleanup convention to `Step020AcceptanceTests`/`Step022AcceptanceTests`), and reads them back
with the real `XsqStructuralReader` — no XML assertions are made against mocked content.

## 6. Unresolved issues / known gaps

- **No Shell/App UI wiring for scoped export** — consistent with the pre-existing, already-disclosed
  Step 019/020/022 gap (the apply pipeline itself, and now this export pipeline, are not wired into
  `TimelineViewModel`/`ShellViewModel`). This step's own required acceptance ("Hash unaffected effects
  before/after scoped edit; no changes outside scope") is fully testable, and tested, at the
  command/service layer, exactly like every prior Step 019–022 acceptance.
- **No real xLights installation available in this environment** (continuing the gap disclosed in every
  prior PHASE-02/PHASE-03 step) — this step's own required acceptance does not depend on a real xLights
  render; it is proven by domain-level hashing plus independent structural XML inspection of the real
  exported bytes, not by xLights itself loading the file. Recorded per the user's standing waiver on
  requiring a real-xLights render check as a phase-gate blocker.
- **The step 5 post-export structural check is believed unreachable in practice** (§3) — it exists as
  defense-in-depth, not as the primary gate; the primary, always-exercised gate is the pre-export
  domain-level hash comparison in steps 2–3.
- **No retention/versioning of exported `.xsq` history** — this step's own scope is a single export
  call's own before/after contract; it does not add any export-history/undo concept beyond what Step
  022's `IEditTransactionHistoryStore` already covers for the `SequenceDocument` itself.

## 7. PASS / BLOCKED

**Status: PASS** for Step 023's own stated required acceptance and failure/edge case:

- "Hash unaffected effects before/after scoped edit; no changes outside scope" — proven by
  `Step023_Acceptance_Passes_HashesUnaffectedEffects_NoChangesOutsideScope` (drags Arch 1 scoped to
  Arch 1 only, exports, then resizes Roofline scoped to Roofline only, exports again — the phase gate's
  own "drag Arch 1... resize Roofline" sequence restricted to Step 023's own per-operation scope —
  asserting the untouched effects' reported hashes are stable across both exports and the real exported
  XSQ, read back independently via `XsqStructuralReader`, still carries their exact original
  frame-rounded timing) and the two addition/removal negative paths
  (`ScopedExport_EffectOutsideScopeChanged_IsRejected_NoFileWritten_NegativePath`,
  `ScopedExport_OutOfScopeEffectRemoved_IsRejected_NoFileWritten_NegativePath`), which prove the
  contract is an enforced rejection — with **no file written** — not merely an assertion made after the
  fact.
- "Export scoped edits without regenerating other effect recipes" — proven by the same acceptance test:
  the second export (Roofline resize) reports an identical hash for Arch 2 as the first export (Arch 1
  move) computed, across two independent scoped operations that never touched it.
- "Inspect XSQ after round-trip" — proven by the same acceptance test reading the real exported file
  back with `XsqStructuralReader` and asserting the out-of-scope effects' placements are present with
  their exact expected frame-rounded timing.
- "Invalid model/format/version must fail safely without rewriting source files" — proven by
  `ScopedExport_InvalidModelTarget_FailsSafely_NoFileWritten_NegativePath` (an effect target that no
  longer resolves against the supplied layout) and
  `ScopedExport_BlankTargetVersion_FailsSafely_NoFileWritten_NegativePath` (blank target xLights
  version) — both propagate the real, unmodified `XsqExportInvalidException` from the underlying
  `XsqExporter`, with no file ever written.

No previously accepted step's contract was modified — `XsqExporter`, `IXsqExporter`,
`XsqStructuralReader`, `EditTransactionService`, `EditScope`, and every other Step 013–022 public member
are completely unchanged, confirmed by the full pre-existing 269-test suite passing unmodified before a
single Step 023 line of code existed (§4), and again at 274/274 (269 old + 5 new) after implementation.

## 8. Phase-wide status after this session

Steps 016–023 — every numbered step in PHASE 03 — now have recorded PASS evidence. Per §6 of the phase
spec and the required final gate report (Section 8), the phase-wide gate demonstration ("Open a
three-effect sample, drag Arch 1 effect, resize Roofline, undo/redo both operations, lock Arch 2,
attempt forbidden modification, export and verify other effects stay bit-for-bit semantically
identical") has every one of its named sub-behaviors proven by an automated test at the domain/service
layer across Steps 016–023, but the full sequence has still **not** been run as one single, continuous,
UI-driven desktop demonstration — the Move/Resize/Lock/Undo/Redo/Export apply pipeline remains unwired
to any Avalonia View/ViewModel end to end (the standing, repeatedly disclosed Step 019/020/022/023 gap).
A future phase-closing task should either (a) wire the full pipeline into
`TimelineViewModel`/`ShellViewModel` and re-run the gate as a literal desktop interaction, or (b) accept
this step-by-step domain-layer proof as satisfying the gate per an explicit, scoped architecture waiver,
consistent with the user's own standing waiver on requiring a real-xLights render check specifically
(which is a narrower, already-granted waiver and does not by itself cover the UI-wiring gap). This
decision is for the user/project owner to make explicitly — it is not assumed here.
