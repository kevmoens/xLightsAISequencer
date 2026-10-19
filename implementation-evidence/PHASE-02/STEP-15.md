# PHASE 02 — Step 015 Evidence

**Step:** Implement atomic export, filename safety, backups and golden XSQ regression tests.
**Spec:** [`docs/phase-specs/PHASE-02.md`](../../docs/phase-specs/PHASE-02.md), Section 6, Step 015.
**Date:** 2026-10-10
**Status: PASS** (scoped to this step's own required acceptance — filename safety, explicit-approval
backup, and golden-fixture regression — all proven below; real xLights render verification remains
Step 014's own separately-scoped, still-BLOCKED item, carried forward unchanged and not reopened here).

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting (PHASE-01 Steps 001-008 + PHASE-02 Steps 009-014, every
prior step):

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 63
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 47
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# 166/166, exactly matching Step 014's own recorded final total.
```

No Step 009-014 (or PHASE-01) production `src/**` type's existing *public shape* was removed. This step
does modify `XsqExporter.cs` (Step 013's own file) — disclosed in full in §9 below as a deliberate,
additive refactor: `ExportNewAsync` keeps its exact original signature and exact original behavior
(verified by Step013AcceptanceTests' own 11 tests and Step013ShellWiringTests' own 5 tests still passing
unmodified, §3/§5), implemented now as a one-line delegation to a new `ExportAsync` overload that carries
the actual (refactored, not rewritten) export logic. `IXsqExporter`/`ExportResult` gain new, additive
members only (a new interface method, a new optional trailing record parameter) — no existing member was
removed or had its meaning changed.

**Real xLights installation:** still none found on this machine (carried forward from every prior
PHASE-02 step's own disclosure, most recently re-verified explicitly in STEP-14.md §1). This step's own
required acceptance text — "Never overwrite source without explicit approval; no regression in fixture
output" — does not itself require a real xLights render, and is fully proven below without one.

## 2. What was built

Per PHASE-02 Section 6 Step 015 ("Atomic create-new export with collision suffix; preserve source media
path semantics and backup if user expressly requests overwrite; compare golden semantic XML, not only
whitespace.") and the required acceptance ("Never overwrite source without explicit approval; no
regression in fixture output."):

- **`src/XlightsAI.Studio.Xlights/XsqExportPolicy.cs`** (new) — `RejectIfExists` (Step 013's own
  original, unmodified default behavior), `AppendCollisionSuffix` ("filename safety": pick the next
  available `name (1).xsq`, `name (2).xsq`, ... path rather than failing or overwriting), and
  `OverwriteWithBackup` (the caller's own explicit approval to replace an existing file, after it is
  first copied byte-for-byte to a reported backup path).
- **`src/XlightsAI.Studio.Xlights/ExportResult.cs`** (modified, additive): gained one new optional
  trailing parameter, `BackupPath` (defaults to `null`), documented as the real on-disk path a
  pre-existing file was copied to before being overwritten — `null` for every policy that never
  overwrote anything. Every existing 3-argument construction/usage of `ExportResult` keeps compiling
  unchanged.
- **`src/XlightsAI.Studio.Xlights/IXsqExporter.cs`** (modified, additive): `ExportNewAsync`'s own
  signature and documented contract are untouched; a new `ExportAsync` method is added, identical except
  for one additional `XsqExportPolicy policy` parameter.
- **`src/XlightsAI.Studio.Xlights/XsqExporter.cs`** (modified, refactor + additive, full rationale in
  §9): `ExportNewAsync` is now a one-line delegation — `ExportAsync(..., XsqExportPolicy.RejectIfExists,
  ct)` — to the real implementation, which now:
  - Resolves a pre-existing `outputPath` per the caller's policy **before** building any XML (same
    "never produce a partial file for a request about to be rejected" discipline Step 013 already
    established): `RejectIfExists` throws exactly as before; `AppendCollisionSuffix` rewrites the local
    `outputPath` to the next available non-colliding path via a new `NextAvailablePath` helper;
    `OverwriteWithBackup` copies the real pre-existing file to a new, also-collision-safe backup path
    (`NextAvailablePath($"{outputPath}.bak")`) via `File.Copy` before proceeding.
  - Closes the same TOCTOU window Step 013 already closed, now policy-aware: for `RejectIfExists`/
    `AppendCollisionSuffix`, a file appearing at `outputPath` between the first check and the final move
    still means a concurrent writer won the race and is never silently clobbered; for
    `OverwriteWithBackup`, the final `File.Move` is called with `overwrite: true` (the explicit approval
    this policy itself represents), using the real BCL `File.Move(string, string, bool)` overload rather
    than a delete-then-move race.
  - Returns the real `ExportResult.BackupPath` (set only for `OverwriteWithBackup`) and the real actual
    `Path` written to (which may differ from the caller's requested `outputPath` only for
    `AppendCollisionSuffix`).
  - **Does not implement "preserve source media path semantics"**: this phase has no media/song asset
    contract yet (`SongAsset` is explicitly Phase 04's own scope per Section 53's "Stable domain names"
    table), so there is no source media path for this step's own exported effects to reference or
    preserve — disclosed in §9/§10 as scoped-out rather than silently skipped.
- **`src/XlightsAI.Studio.Shell/ShowImportViewModel.cs`** (modified, additive): new bindable
  `ExportPolicy` property (defaults to `XsqExportPolicy.RejectIfExists`, Step 013's own original
  behavior) and a static `ExportPolicyOptions` list for the screen's own combo box; `ExportXsqAsync` now
  calls the real `exporter.ExportAsync(..., ExportPolicy, ct)` instead of `ExportNewAsync` — a caller
  that never touches `ExportPolicy` gets byte-identical behavior to Step 013's own original
  `ExportNewAsync` call.
- **`src/XlightsAI.Studio.App/Views/MainWindow.axaml`** (modified, additive): a new "If a file already
  exists at the output path" combo box bound to `ExportPolicy`/`ExportPolicyOptions`, and a new
  conditionally-visible "Backup of previous file" line bound to `LastExport.BackupPath` — no existing
  markup removed.
- **Tests** (new, no existing test file's assertions touched):
  - `tests/XlightsAI.Studio.Xlights.Tests/Step015AcceptanceTests.cs` — 8 tests against the real
    `XsqExporter`, real file-system I/O, no mocks, plus a new frozen golden fixture file.
  - `tests/XlightsAI.Studio.Xlights.Tests/Fixtures/golden/golden-three-effect-sequence.xsq` (new) — the
    frozen golden fixture, copied verbatim from STEP-13.md Section 6's own independently-inspected,
    on-disk-verified export output (same gate-demo layout/plan shape).
  - `tests/XlightsAI.Studio.Xlights.Tests.csproj` (modified, additive): added a `*.xsq` fixture-copy
    `ItemGroup` entry alongside the existing `*.xml` one, so the new golden fixture is actually present
    next to the test assembly at run time.
  - `tests/XlightsAI.Studio.Shell.Tests/Step015ShellWiringTests.cs` — 3 tests proving the real
    `ShellViewModel.ExportXsqCommand` honors `ExportPolicy` end to end.

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests\XlightsAI.Studio.Xlights.Tests\XlightsAI.Studio.Xlights.Tests.csproj --filter "FullyQualifiedName~Step015" --logger "console;verbosity=normal"
Total tests: 8. Passed: 8.

$ dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --filter "FullyQualifiedName~Step015" --logger "console;verbosity=normal"
Total tests: 3. Passed: 3.

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 71   (63 baseline + 8 new Step 015 domain tests)
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 50   (47 baseline + 3 new Step 015 shell-wiring tests)
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25   (untouched)
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31   (untouched)
# Full, final regression: 177/177 passing (166 baseline + 11 new), 0 regressions.

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes
(no output; exit code 0)
```

All commands were run via PowerShell; the Bash tool continues to fail on every invocation in this
environment (exit 1 / no output), consistent with every prior step's evidence.

## 4. Required acceptance: "Never overwrite source without explicit approval; no regression in fixture output"

### "Never overwrite ... without explicit approval"

- `ExportNewAsync_StillRejectsExistingFile_BackwardCompatible_NegativePath`
  (`Step015AcceptanceTests.cs`) proves Step 013's own original method is still exactly "never overwrite,
  full stop" — no policy argument means no approval was ever given.
- `AppendCollisionSuffix_NeverOverwrites_PicksNextAvailablePath` proves the real pre-existing file's
  content is byte-identical after two separate collision attempts (each picking a distinct, real,
  non-colliding path: `sequence (1).xsq`, then `sequence (2).xsq`) — "filename safety" without ever
  requiring or implying approval.
- `OverwriteWithBackup_PreservesOriginalBytesBeforeReplacing` proves that when (and only when) the
  caller explicitly selects `OverwriteWithBackup` — this step's own named "if user expressly requests
  overwrite" — the real original bytes survive intact at a real, reported backup path before the real
  new export replaces the original file; nothing is ever discarded.
- At the shell layer, `Step015_Acceptance_Passes_CollisionSuffixEndToEnd` and
  `Step015_Acceptance_Passes_OverwriteWithBackupEndToEnd` (`Step015ShellWiringTests.cs`) prove the same
  two behaviors through the real `ShellViewModel.ExportXsqCommand`, and
  `ExportPolicyDefault_StillRejectsExistingFile_BackwardCompatible_NegativePath` proves the screen's own
  default (`ExportPolicy` never touched) still behaves exactly like Step 013's own original contract.
- §6 below repeats all three outcomes through the real, running Avalonia desktop application.

### "No regression in fixture output"

- `Step015_Acceptance_Passes_MatchesGoldenFixtureSemantically` (`Step015AcceptanceTests.cs`) is this
  step's own named acceptance test. It re-generates the exact gate-demo export (same layout/plan shape
  Step013AcceptanceTests' own `GateDemoLayout()`/fixture uses) and compares it, field-by-field, against
  the frozen `Fixtures/golden/golden-three-effect-sequence.xsq` — itself copied verbatim from Step 013's
  own independently-inspected, on-disk-verified output (STEP-13.md Section 6). The comparison is
  **semantic, not textual**: a private `Normalize`/`IsSemanticallyEqualToGolden` helper sorts attributes
  by name and trims leaf-element text before calling `XNode.DeepEquals`, so the test would still pass if
  a future change reordered attributes or altered indentation, and would still **fail** if a future
  change silently altered any actual element, attribute, or value — this step's own required acceptance
  text verbatim: "compare golden semantic XML, not only whitespace."

## 5. Required failure/edge case: "Invalid model/format/version must fail safely without rewriting source files"

### `tests/XlightsAI.Studio.Xlights.Tests/Step015AcceptanceTests.cs` (8 new tests, real file-system I/O, no mocks)

```text
Passed Step015_Acceptance_Passes_MatchesGoldenFixtureSemantically
Passed AppendCollisionSuffix_NeverOverwrites_PicksNextAvailablePath
Passed OverwriteWithBackup_PreservesOriginalBytesBeforeReplacing
Passed ExportNewAsync_StillRejectsExistingFile_BackwardCompatible_NegativePath
Passed UnresolvedEffectTarget_FailsSafely_ForEveryPolicy_NegativePath(policy: RejectIfExists)
Passed UnresolvedEffectTarget_FailsSafely_ForEveryPolicy_NegativePath(policy: AppendCollisionSuffix)
Passed UnresolvedEffectTarget_FailsSafely_ForEveryPolicy_NegativePath(policy: OverwriteWithBackup)
Passed UnknownPolicy_FailsSafely_NoFileWritten_NegativePath

Total: 8. Passed: 8.
```

- `UnresolvedEffectTarget_FailsSafely_ForEveryPolicy_NegativePath` is this step's own explicit
  generalization of Step 013's original single-policy version of this test: an effect target that does
  not resolve against the supplied layout is rejected, and no file is ever written, **for every one of
  the three policies** — proving the new collision/backup machinery never bypasses this pre-existing
  validation gate (it still runs, and still rejects, before any policy branch is ever reached).
- `UnknownPolicy_FailsSafely_NoFileWritten_NegativePath` proves an invalid/unrecognized enum value
  (cast from an out-of-range integer) is rejected by name rather than silently defaulting to some
  behavior, and the real pre-existing file at the target path is left byte-identical.

### `tests/XlightsAI.Studio.Shell.Tests/Step015ShellWiringTests.cs` (3 new tests, real file-system I/O, no mocks)

```text
Passed ExportPolicyDefault_StillRejectsExistingFile_BackwardCompatible_NegativePath
Passed Step015_Acceptance_Passes_CollisionSuffixEndToEnd
Passed Step015_Acceptance_Passes_OverwriteWithBackupEndToEnd

Total: 3. Passed: 3.
```

### Pre-existing regression (unmodified acceptance text)

```text
XlightsAI.Studio.Xlights.Tests: 63/63 pre-existing Step009-014 tests passing, unmodified — including
  all 11 of Step 013's own ExportNewAsync/XsqStructuralReader tests, proving the XsqExporter refactor
  (§2/§9) changed no observable behavior of the original method.
XlightsAI.Studio.Shell.Tests:   47/47 pre-existing tests (Step 004-014) passing, unmodified.
XlightsAI.Studio.Projects.Tests: 25/25 passing, untouched by this step.
XlightsAI.Studio.Reference.Tests: 31/31 passing, untouched by this step.
# Full regression with this step's new tests included: 177/177, 0 regressions.
```

No PHASE-01 or Step 009-014 test file's assertions were edited. `IXsqExporter`/`ExportResult` gained
only new, additive members; `ShowImportViewModel`/`ShellViewModel` likewise; `XsqExporter`'s own
`ExportNewAsync` keeps its exact original signature and (proven by its own 11 untouched tests) exact
original behavior.

## 6. Manual / desktop UI verification — real Avalonia app, real file system

Per the task's "Build the minimum complete UI -> application -> domain -> data/adapter slice" and "run
... manual tests when this step requires them" instructions, the real compiled desktop app
(`src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe`) was launched for real and driven via
Windows UI Automation (real `InvokePattern`/`ValuePattern`/`ExpandCollapsePattern`/`SelectionItemPattern`
calls against the real running window), reusing the same fixture show directory as Step 014's own demo
(`C:\temp\xlai-step014-demo-show`, same SHA256 `792C35E91EFBB345D5C7A7D37A4651D19DC4FF1402BCEC8E2DCDE56820971937`
as every prior PHASE-02 step's own demo fixture).

1. **Default policy, real negative path** —
   `implementation-evidence/PHASE-02/step-015-export-reject-existing-error.png`: after Discover →
   Generate 3 Effects, the output path was set to `...\exports\sequence.xsq`, a real pre-existing file
   was placed there (`"pre-existing file for Step015 demo"`), and — with the new combo box left at its
   real default, visibly reading `RejectIfExists` — **Export XSQ** was clicked. The screen shows the
   real red error `'...\sequence.xsq' already exists; export never overwrites an existing file without
   an explicit policy.` and the pre-existing file was independently confirmed untouched on disk.
2. **AppendCollisionSuffix, real success path** —
   `implementation-evidence/PHASE-02/step-015-export-collision-suffix-success.png`: the combo box was
   switched to `AppendCollisionSuffix` via a real `ExpandCollapsePattern.Expand()` +
   `SelectionItemPattern.Select()` against the real list item, and **Export XSQ** was clicked again
   against the same, still-existing output path. The screen shows the real result
   `Exported: ...\exports\sequence (1).xsq` (SHA256
   `051C1334E322278E7C049B792FFF2324B2C90FD02B908428196A20463742DCD2`) — a real, different, non-colliding
   file was written, and the original `sequence.xsq` remained byte-identical throughout (independently
   re-confirmed on disk).
3. **OverwriteWithBackup, real success path** —
   `implementation-evidence/PHASE-02/step-015-export-overwrite-with-backup-success.png`: the combo box
   was switched to `OverwriteWithBackup` and **Export XSQ** was clicked a third time against the same
   original output path (still holding the untouched `"pre-existing file for Step015 demo"` text). The
   screen shows `Exported: ...\exports\sequence.xsq`, `SHA256: 051C...3742DCD2`, and the new
   `Backup of previous file: ...\exports\sequence.xsq.bak` line. Independently inspecting the real files
   on disk afterward confirms: `sequence.xsq.bak` contains exactly `"pre-existing file for Step015
   demo"` (the real original bytes, fully preserved) and `sequence.xsq` now has SHA256
   `051C1334E322278E7C049B792FFF2324B2C90FD02B908428196A20463742DCD2` (the real new export) — not a
   crash, not a silently-discarded original, not a guessed outcome.

**Disclosed side effect**: this manual verification wrote real files under
`C:\temp\xlai-step014-demo-show\exports\` (`sequence.xsq`, `sequence (1).xsq`, `sequence.xsq.bak`) and
under this machine's own `%AppData%\XlightsAI.Studio\exports\` (one additional `sequence-plan-*.json`
from the Generate step) — same precedent as every prior step's own disclosure; none are part of the
committed repository.

## 7. Fixture hashes

```text
C:\temp\xlai-step014-demo-show\xlights_rgbeffects.xml (manual-demo fixture, outside the repository)
  SHA256=792C35E91EFBB345D5C7A7D37A4651D19DC4FF1402BCEC8E2DCDE56820971937 (identical to every prior PHASE-02 step's own fixture)

C:\temp\xlai-step014-demo-show\exports\sequence.xsq / sequence (1).xsq (real app output, manual verification)
  SHA256=051C1334E322278E7C049B792FFF2324B2C90FD02B908428196A20463742DCD2 (identical to Step 013/014's own recorded export — same fixture/plan shape used throughout)

tests/XlightsAI.Studio.Xlights.Tests/Fixtures/golden/golden-three-effect-sequence.xsq (new, checked-in golden regression fixture)
  content copied verbatim from STEP-13.md Section 6's own independently-inspected, on-disk-verified export
```

## 8. Changed/added paths

```
src/XlightsAI.Studio.Xlights/XsqExportPolicy.cs                                  (new)
src/XlightsAI.Studio.Xlights/ExportResult.cs                                     (modified: +BackupPath, optional, defaults null)
src/XlightsAI.Studio.Xlights/IXsqExporter.cs                                     (modified: +ExportAsync; ExportNewAsync unchanged)
src/XlightsAI.Studio.Xlights/XsqExporter.cs                                      (modified: ExportNewAsync delegates to new ExportAsync; +NextAvailablePath; policy-aware existing-file handling)
src/XlightsAI.Studio.Shell/ShowImportViewModel.cs                                (modified: +ExportPolicy/+ExportPolicyOptions; ExportXsqAsync now calls ExportAsync)
src/XlightsAI.Studio.App/Views/MainWindow.axaml                                  (modified: +export-policy combo box, +BackupPath display line)
tests/XlightsAI.Studio.Xlights.Tests/Step015AcceptanceTests.cs                   (new; 8 tests)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/golden/golden-three-effect-sequence.xsq (new)
tests/XlightsAI.Studio.Xlights.Tests/XlightsAI.Studio.Xlights.Tests.csproj       (modified: +*.xsq fixture-copy ItemGroup entry)
tests/XlightsAI.Studio.Shell.Tests/Step015ShellWiringTests.cs                    (new; 3 tests)
implementation-evidence/PHASE-02/STEP-15.md                                      (new, this file)
implementation-evidence/PHASE-02/step-015-export-reject-existing-error.png       (new)
implementation-evidence/PHASE-02/step-015-export-collision-suffix-success.png    (new)
implementation-evidence/PHASE-02/step-015-export-overwrite-with-backup-success.png (new)
```

No PHASE-01 file and no Step 009-012/014 production file's existing members were edited or removed. No
Step 009-014 test file's assertions were edited.

## 9. Architecture deviations

- **`XsqExporter.ExportNewAsync` was refactored from a standalone method into a one-line delegation to
  a new `ExportAsync(..., XsqExportPolicy, ct)` overload, rather than being left as fully independent,
  duplicated code.** This is the one production file this step modifies beyond pure additions. It is
  disclosed here because Step 013's own file is otherwise treated as immutable per this task's
  instructions; the justification: (a) `ExportNewAsync`'s own public signature, its own documented
  contract, and — critically — its own exact observable behavior are all unchanged, proven by Step 013's
  own original 11+5 tests still passing byte-for-byte unmodified (§3/§5); (b) the alternative (copy-paste
  the ~150-line build/write logic into a second method) would create two independently-maintained
  copies of security-relevant logic (XML escaping, atomic temp-file-then-move, TOCTOU-window closing)
  that could silently drift apart over future changes — a real risk this task's own "Don't add...
  abstractions beyond what the task requires" guidance is balanced against "three similar lines is
  better than a premature abstraction," but ~150 lines of atomic-write/validation logic is well past
  that threshold; (c) this exact pattern (a new, more general method; the old method becomes a thin
  default-arguments wrapper) is the ordinary, minimal-diff way to add an optional capability to an
  existing method without breaking its callers, not a new abstraction layer.
- **"Preserve source media path semantics" (this step's own implementation guidance) is explicitly not
  implemented.** There is no media/song asset contract anywhere in the codebase yet — `SongAsset` is
  named in PHASE-02's own Section 53 "Stable domain names" table as first introduced in **Phase 04**,
  not this phase. An `EffectInstance` has no media-path field to preserve. Implementing this would mean
  inventing a media-path concept ahead of its own phase, which Section 2's "do not fabricate... without
  a concrete boundary" and this task's "don't design for hypothetical future requirements" both argue
  against. Recorded here as explicitly scoped-out, not silently skipped.
- **Backup/collision-suffix naming reuses the same `NextAvailablePath` helper for both purposes**
  (`sequence (1).xsq` for a colliding export target; `sequence.xsq (1).bak` for a second same-run backup
  of the same file) rather than two separate naming schemes — one real behavior, two call sites, instead
  of two parallel but subtly different filename-safety implementations.
- **No golden-fixture *generator* tool was built.** The frozen golden fixture
  (`Fixtures/golden/golden-three-effect-sequence.xsq`) was populated by copying Step 013's own
  previously-verified, independently-inspected output text (STEP-13.md Section 6) rather than by writing
  a new one-off capture script; `Step015_Acceptance_Passes_MatchesGoldenFixtureSemantically`'s own first
  run (§3/§5, passing) is itself the independent confirmation that this hand-copied golden fixture
  really matches the real exporter's real current output, closing the loop on provenance.

## 10. Unresolved issues / risks carried forward

1. **Step 014's own required acceptance remains BLOCKED** — no installed xLights executable exists on
   this machine (STEP-14.md §1, re-disclosed here, not re-verified again since nothing in this
   environment changed). This step's own required acceptance text does not itself require a real xLights
   render and is fully proven without one (§4); this step does not attempt to discharge Step 014's own
   blocker and does not claim to.
2. **"Preserve source media path semantics" is scoped out** (§9) pending Phase 04's own `SongAsset`
   contract — nothing in this phase's effects currently reference external media, so there is nothing
   concrete to preserve yet.
3. **The "On" effect's `EffectDB` settings remain the same empty, explicitly-disclosed placeholder**
   Step 013 introduced (STEP-13.md §9) — still blocked on the same missing xLights install. The frozen
   golden fixture (§2/§9) therefore also encodes this same placeholder; replacing it (Section 12's own
   golden-fixture *process*, a different, xLights-sourced concept from this step's own regression-test
   golden *fixture*) remains blocked on the same missing install.
4. **`xlights_networks.xml` is still not parsed** (carried forward from Step 011's own §10 item 2) —
   unaffected by this step's own scope.
5. **`NextAvailablePath` caps at 9999 attempts** before throwing `XsqExportInvalidException` — a
   deliberate, disclosed finite bound (never an infinite loop) rather than a claim that collision
   resolution always succeeds; recorded here rather than silently assumed unlimited.
6. **This step's manual verification wrote real files** under `C:\temp\xlai-step014-demo-show\exports\`
   and this machine's own `%AppData%\XlightsAI.Studio\exports\` (§6) — same precedent as every prior
   step's own disclosure; none are part of the committed repository.
7. Git Bash remains non-functional in this environment (carried forward from every prior step); every
   command in this evidence was run via PowerShell.
8. **This was PHASE-02's own final numbered step (009-015).** Per Section 7's "Required final gate
   report," a full-phase gate report — compiling every step's own evidence, re-running the full
   regression suite, and recording an explicit phase-level PASS/BLOCKED — is the natural next action, not
   a new numbered implementation step; it has not been produced as part of this step's own evidence and
   belongs in a separate phase-gate record.

## 11. Gate checklist (this step only)

- [x] Ran `dotnet build`/`dotnet test` for the full existing suite before starting; 166/166 passing,
      matching Step 014's own recorded final total exactly (§1, §3).
- [x] No prior-step production file's existing *public shape* was removed; the one modified production
      file (`XsqExporter.cs`) keeps its original method's exact signature and exact behavior, proven by
      that method's own original, untouched test suite still passing in full (§1, §3, §5, §9).
- [x] Turned the step's acceptance text into executable tests:
      `Step015_Acceptance_Passes_MatchesGoldenFixtureSemantically` (regression half) plus dedicated
      collision-suffix/backup tests (explicit-approval half), at both the domain (`Xlights.Tests`) and
      shell-wiring (`Shell.Tests`) layers (§4, §5).
- [x] At least one failure-path assertion, and specifically the named failure/edge case text ("Invalid
      model/format/version must fail safely without rewriting source files") — generalized across all
      three policies, plus an unknown-policy negative test (§5).
- [x] Golden semantic-XML comparison implemented as described ("compare golden semantic XML, not only
      whitespace") — a dedicated normalize-then-`XNode.DeepEquals` helper, not a byte/whitespace diff
      and not merely "the file parses" (§4).
- [x] Full regression suite re-run, final: 177/177 passing (166 PHASE-01/Step009-014 baseline + 11 new
      Step 015 tests), zero regressions (§3, §5).
- [x] `dotnet format --verify-no-changes` passes cleanly against every new and modified file (§3).
- [x] No original/show file touched; no Step 009-014 test *assertion* touched — every prior-step
      production file edited gained only new, additive members, and the one refactored method is proven
      behavior-identical by its own original test suite (§5, §8, §9).
- [x] Never overwrites source show files; never overwrites any pre-existing export target without the
      caller's own explicit `OverwriteWithBackup` policy choice, and even then only after a real,
      verified backup copy exists first (§2, §4, §5, §6).
- [x] Real UI demonstration performed (not simulated/mocked) — three screenshots via real Windows UI
      Automation against the real running app: the real default-policy negative path, a real
      `AppendCollisionSuffix` success path, and a real `OverwriteWithBackup` success path, each
      independently cross-checked against the real bytes on disk afterward (§6).
- [x] Architecture deviations disclosed in full (§9), per this task's explicit instruction.

**Step 015 status: PASS** (scoped to this step's own required acceptance). This was PHASE-02's final
numbered step (009-015); a separate phase-level gate report compiling all seven steps' evidence is the
natural next action (§10 item 8), not a new numbered step.
