# PHASE 01 — Step 008 Evidence

**Step:** Add CI, unit/integration/golden test projects, formatting and artifact publishing.
**Spec:** [`docs/phase-specs/PHASE-01.md`](../../docs/phase-specs/PHASE-01.md), Section 6, Step 008.
**Date:** 2026-10-10
**Status: PASS** (with one explicitly scoped BLOCKED sub-item — real GitHub Actions cloud execution; see §6 and §10)

> Filename note: continuing the Step 002–007 precedent (`STEP-02.md`...`STEP-07.md`), this file uses
> `STEP-08.md` rather than the phase spec's own `STEP-008.md` form — the same open, unresolved naming
> inconsistency carried forward from every prior step's evidence file.

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting, unmodified:

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=normal"
XlightsAI.Studio.Reference.Tests: Total tests: 27, Passed: 27
XlightsAI.Studio.Projects.Tests:  Total tests: 24, Passed: 24
XlightsAI.Studio.Shell.Tests:     Total tests: 25, Passed: 25
# 76/76, matching Step 007's own recorded baseline exactly.
```

No pre-existing file was modified except by strictly additive change (one `[Trait(...)]` attribute line
added above each of eight pre-existing test classes — see §2 and §9). No pre-existing test assertion,
fixture, production source file, or `.csproj` was altered.

## 2. What was built

Per PHASE-01 Section 6's Step 008 guidance ("CI builds Windows at minimum and performs tests without
developer Python; store fixture results and log manifest in build artifacts; require formatting/static
checks to pass"):

- **`.editorconfig`** (repo root, new) — `root = true`; UTF-8, LF line endings, trailing-whitespace
  trim and final-newline rules for all files; a `[*.cs]` section (4-space indent) and a
  `[*.{csproj,slnx,json,yml,yaml}]` section (2-space indent). Deliberately conservative: verified against
  all 76 pre-existing source files with `dotnet format --verify-no-changes` (§3) before committing to it,
  specifically so this step's formatting gate does not force a reformat of any prior step's "immutable"
  file. No style/analyzer rule was added beyond whitespace/charset — adding `EnforceCodeStyleInBuild` or
  analyzer-severity rules was considered and deliberately deferred (see §9).
- **`.github/workflows/ci.yml`** (new) — the real CI workflow:
  - Triggers on `push`/`pull_request` to `main`/`master`.
  - `runs-on: windows-latest` — the "Windows at minimum" requirement, satisfied literally (no Linux/macOS
    matrix was added; see §9).
  - Steps, in order: checkout, `actions/setup-dotnet@v4` pinned to `10.0.x`, `dotnet restore`,
    `dotnet format XlightsAI.Studio.slnx --verify-no-changes` (the "formatting... checks to pass" gate,
    placed *before* build so a formatting failure fails fast without wasting a full build), `dotnet build
    -c Release`, three separate `dotnet test -c Release --no-build --filter "Category=X"` steps (Golden,
    Unit, Integration — see below), each with `if: always()` so one category's failure does not skip the
    others, each writing its own named `.trx` file into `TestResults/`, and a final
    `actions/upload-artifact@v4` step (`if: always()`, `if-no-files-found: error`) publishing every
    `.trx` under `TestResults/**/*.trx` as the "test report" artifact PHASE-01 Section 6 and the required
    acceptance text ("...and stores test reports") call for — it runs even if an earlier test step failed,
    so a failing run still produces an inspectable report instead of silently losing it (the "stale
    persisted state must not corrupt the project" / "do not silently lose data" pattern every prior step
    in this phase has followed).
- **Test categorization ("unit/integration/golden test projects")** — see §9 for the explicit
  architecture decision: rather than fragmenting the three existing, already-immutable, feature-sliced
  `.csproj` test projects (`Reference.Tests`, `Projects.Tests`, `Shell.Tests`) into a parallel set of
  `Unit.Tests`/`Integration.Tests`/`Golden.Tests` projects (which would have meant physically relocating
  every prior step's test file — a large, high-risk, purely-mechanical change this step's own
  "Avoid collecting multiple unverified tasks into one mega-commit" and "do not fabricate generic
  interfaces" guidance argues against), each pre-existing acceptance-test *class* was given one additive
  `[Trait("Category", "Unit"|"Integration"|"Golden")]` attribute reflecting what it actually already does:
  - **Golden** (validates output against a pinned, hash-verified reference/fixture corpus):
    `Step001AcceptanceTests`, `Step002AcceptanceTests`, `Step003AcceptanceTests` (all three in
    `Reference.Tests` — they are literally PHASE-01's "baseline fixture corpus" and upstream-pin audit).
  - **Unit** (pure in-memory logic, no filesystem/process/db I/O): `Step004AcceptanceTests`
    (`Shell.Tests` — a framework-agnostic navigation/state-machine test with no I/O).
  - **Integration** (real file system, real SQLite, or real cross-component wiring):
    `Step005AcceptanceTests`, `Step006ShellWiringTests` (`Shell.Tests` — real temp-directory
    settings/diagnostics persistence and real `ProjectService` wiring), `Step006AcceptanceTests`,
    `Step007AcceptanceTests` (`Projects.Tests` — real atomic manifest writes and real SQLite migration
    files).
  - The new `Step008AcceptanceTests` class itself is tagged `Integration` (it shells out to a real
    `dotnet` process and touches the real filesystem).
  - This is a **complete partition**: every one of the 80 tests now in the full suite carries exactly one
    of the three categories (proven in §5 — the three category filters' totals sum to exactly 80, the
    same number the unfiltered full run reports), so the CI workflow's three filtered steps run the
    entire suite with no test silently excluded from all three.
- **`tests/XlightsAI.Studio.Reference.Tests/CiWorkflowShapeCheck.cs`** (new, test-only) — a small,
  deliberately non-generic plain-substring validator (`Validate(string?) -> IReadOnlyList<string>`
  errors) asserting the committed workflow text contains the specific required markers (Windows runner,
  restore/format/build/test commands, all three category filters, the artifact-upload step, and
  `if: always()`). Not a general YAML-schema engine — PHASE-01's own guidance against fabricating generic
  interfaces this phase does not need applies here too.
- **`tests/XlightsAI.Studio.Reference.Tests/Step008AcceptanceTests.cs`** (new) — see §5.

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes
(no output; exit code 0)

$ dotnet build XlightsAI.Studio.slnx -c Release
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests/XlightsAI.Studio.Reference.Tests/XlightsAI.Studio.Reference.Tests.csproj --logger "console;verbosity=normal"
Total tests: 31. Passed: 31.   # 27 Step001-003 (unmodified) + 4 new Step008 (one real ~25s subprocess test, three fast negative-path tests)

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=normal"
XlightsAI.Studio.Reference.Tests: Total tests: 31, Passed: 31
XlightsAI.Studio.Projects.Tests:  Total tests: 24, Passed: 24
XlightsAI.Studio.Shell.Tests:     Total tests: 25, Passed: 25
# Full regression: 80/80 passing.

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes   # re-run after all changes
(no output; exit code 0)

$ dotnet build XlightsAI.Studio.slnx -c Release --no-restore   # (equivalent to CI's build step)
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx -c Release --no-build --filter "Category=Unit" --logger "console;verbosity=minimal"
XlightsAI.Studio.Shell.Tests.dll: Passed! Total: 9

$ dotnet test XlightsAI.Studio.slnx -c Release --no-build --filter "Category=Integration" --logger "console;verbosity=minimal"
XlightsAI.Studio.Shell.Tests.dll:    Passed! Total: 16
XlightsAI.Studio.Projects.Tests.dll: Passed! Total: 24
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 4

$ dotnet test XlightsAI.Studio.slnx -c Release --no-build --filter "Category=Golden" --logger "console;verbosity=minimal"
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 27
```

Category totals: Unit 9 + Integration (16+24+4=44) + Golden 27 = **80**, exactly matching the
unfiltered full-suite total (80) — confirms the three CI category filters jointly exercise the entire
suite with no test left uncovered by all three (§2, §5).

All commands were run via PowerShell; the Bash tool again failed on every invocation in this environment
(exit 1 / no output), consistent with Steps 001–007.

## 4. Required acceptance: "Clean checkout builds, runs fixtures, and stores test reports"

- **Builds** — `dotnet build XlightsAI.Studio.slnx` and the Release configuration both succeed with
  0 Warning(s)/0 Error(s) (§3), exactly as CI's `dotnet restore` + `dotnet format --verify-no-changes` +
  `dotnet build -c Release` steps do.
- **Runs fixtures** — the full 80-test suite (including all of Steps 001, 002, 003's fixture-corpus/
  pinned-manifest audits) passes under every invocation style CI uses: unfiltered, Release/`--no-build`,
  and each of the three category filters (§3).
- **Stores test reports** — proven two ways:
  1. `Step008_Acceptance_Passes` (§5) really shells out to `dotnet test ... --filter "Category=Golden"
     --logger "trx;LogFileName=golden.trx" --results-directory <temp dir>` — the exact pattern
     `.github/workflows/ci.yml` uses — and asserts a real, non-empty `golden.trx` file lands on disk
     afterward. This is not a mocked invocation; it is a real child `dotnet` process writing a real file,
     inspected by the test.
  2. The workflow's final step (`actions/upload-artifact@v4`, `if: always()`, `path:
     TestResults/**/*.trx`) is asserted present by `CiWorkflowShapeCheck` against the actual committed
     `ci.yml` text (§5), so the report-publishing step itself is part of the committed, test-checked
     artifact, not just described in this evidence file.

## 5. Automated test results — detail

### `tests/XlightsAI.Studio.Reference.Tests/Step008AcceptanceTests.cs` (4 tests)

```text
Passed Step008_Acceptance_Passes                                                     [~25 s]
Passed CiWorkflow_WhenRequiredStepsAreMissing_FailsValidation_NegativePath            [< 1 ms]
Passed CiWorkflow_WithEmptyText_FailsSafelyWithoutThrowing_NegativePath               [< 1 ms]
Passed ReadCiWorkflow_WithAlreadyCancelledToken_ThrowsWithoutReadingAnything_NegativePath [3 ms]

Total: 4. Passed: 4.
```

`Step008_Acceptance_Passes` is one deterministic fixture asserting **persisted domain state plus public
output, not only a mock invocation**, exactly as the spec's suggested-test guidance requires, in five
parts:

1. `.github/workflows/ci.yml` exists and `CiWorkflowShapeCheck.Validate(...)` (a real text scan, not a
   mock) returns zero errors against it.
2. `.editorconfig` exists and contains the `root = true` / `[*.cs]` markers the formatting gate depends
   on.
3. A **real** child `dotnet format XlightsAI.Studio.slnx --verify-no-changes` process (via
   `System.Diagnostics.Process`, not a mocked shell) exits 0 against the actual repository on disk right
   now.
4. Every one of the eight pre-existing Step 001–007 test-class files on disk actually contains the exact
   `[Trait("Category", "...")]` line the CI workflow's filters depend on — read from the real file text,
   not asserted against an in-memory copy.
5. A **real** child `dotnet test ... --filter "Category=Golden" --logger "trx;LogFileName=golden.trx"`
   process is run against a real temporary results directory; the test then asserts a real, non-empty
   `golden.trx` file exists on disk afterward, and deletes the temp directory in a `finally` block.

**Failure/edge case required** — "Invalid input, cancellation or stale persisted state must not corrupt
the project" — covered by the three negative-path tests:

- **Invalid input** — `CiWorkflow_WhenRequiredStepsAreMissing_FailsValidation_NegativePath`: a
  deliberately tampered, minimal workflow string (missing the format/upload-artifact/category-filter
  steps) is validated and asserted to produce specific, named errors — the validator fails safely and
  reports *what* is missing rather than silently accepting a broken workflow or throwing.
- **Invalid input (empty)** — `CiWorkflow_WithEmptyText_FailsSafelyWithoutThrowing_NegativePath`: an
  empty workflow string produces exactly one explicit "workflow file is empty" error, not an exception or
  a false-positive pass.
- **Cancellation** — `ReadCiWorkflow_WithAlreadyCancelledToken_ThrowsWithoutReadingAnything_NegativePath`:
  an already-cancelled `CancellationToken` passed to the real `File.ReadAllTextAsync` call on the real
  `ci.yml` path throws `OperationCanceledException` rather than silently returning partial/empty content.
- **Stale/partial state** — the real `Step008_Acceptance_Passes` test's own `finally` block deletes its
  temporary results directory even if an assertion above it fails, and it never writes into or mutates
  any path inside the repository itself (only a `%TEMP%\xlai-step008-{guid}` scratch directory) — no
  `.trx`, backup, or scratch file from this step's tests is left behind in the repository.

### Pre-existing regression (unmodified behavior, additive trait attribute only)

```text
Step001AcceptanceTests: 9/9 passing   (unmodified assertions; + [Trait("Category","Golden")])
Step002AcceptanceTests: 9/9 passing   (unmodified assertions; + [Trait("Category","Golden")])
Step003AcceptanceTests: 9/9 passing   (unmodified assertions; + [Trait("Category","Golden")])
Step004AcceptanceTests: 9/9 passing   (unmodified assertions; + [Trait("Category","Unit")])
Step005AcceptanceTests: 12/12 passing (unmodified assertions; + [Trait("Category","Integration")])
Step006ShellWiringTests: 4/4 passing  (unmodified assertions; + [Trait("Category","Integration")])
Step006AcceptanceTests: 14/14 passing (unmodified assertions; + [Trait("Category","Integration")])
Step007AcceptanceTests: 10/10 passing (unmodified assertions; + [Trait("Category","Integration")])
# Full regression: 80/80 (76 pre-existing + 4 new Step008), 0 regressions.
```

## 6. Manual / environment verification — CI cloud execution: BLOCKED (disclosed, not substituted)

This repository has **no git remote and no commits yet** (`git log` reports "your current branch
'master' does not have any commits yet"; `git remote -v` returns nothing) — confirmed directly before
writing this evidence. A GitHub Actions workflow cannot actually execute in GitHub's cloud runners
without a GitHub-hosted repository to push to, and this task's own constraints do not authorize creating
or pushing to a new external repository on the user's behalf.

Per this phase's own instruction ("If an external environment is unavailable, label the real integration
test BLOCKED instead of pretending a mock is equivalent"), **real cloud execution of
`.github/workflows/ci.yml` on GitHub's `windows-latest` runners is explicitly BLOCKED**, not claimed.

What *was* verified for real, locally, as the next-best honest substitute (not a mock of the workflow —
the literal same commands the workflow file runs, executed directly against this real repository):

- Every command the workflow's steps run (`dotnet restore`, `dotnet format --verify-no-changes`,
  `dotnet build -c Release`, all three `dotnet test -c Release --no-build --filter "Category=X"`
  invocations with `trx` loggers and `--results-directory`) was run directly, for real, with the exact
  same arguments, and all succeeded (§3).
- `actions/setup-dotnet@v4` requesting `10.0.x` was not exercised (no GitHub runner available), but the
  locally installed SDK is `10.0.302`, which satisfies that version constraint; this is a disclosed
  assumption, not a verified cloud fact.
- `actions/checkout@v4` and `actions/upload-artifact@v4` are both widely-used, unmodified upstream
  GitHub Actions; their own correctness is out of this step's scope to re-verify, consistent with
  PHASE-01 Section 2's general stance that infrastructure outside this repository's direct control is not
  re-tested here.
- No xLights/UI manual demo applies to this step — it has no `user-visible outcome` screen (same as
  Step 007's "not applicable" xLights-validation note); the "reproducible test report" output is the
  deliverable, and it was reproduced for real (§4, §5).

## 7. Changed/added paths

```
.editorconfig                                                              (new)
.github/workflows/ci.yml                                                   (new)
tests/XlightsAI.Studio.Reference.Tests/CiWorkflowShapeCheck.cs              (new)
tests/XlightsAI.Studio.Reference.Tests/Step008AcceptanceTests.cs           (new)
tests/XlightsAI.Studio.Reference.Tests/Step001AcceptanceTests.cs           (modified: + 1 Trait attribute line only)
tests/XlightsAI.Studio.Reference.Tests/Step002AcceptanceTests.cs           (modified: + 1 Trait attribute line only)
tests/XlightsAI.Studio.Reference.Tests/Step003AcceptanceTests.cs           (modified: + 1 Trait attribute line only)
tests/XlightsAI.Studio.Shell.Tests/Step004AcceptanceTests.cs               (modified: + 1 Trait attribute line only)
tests/XlightsAI.Studio.Shell.Tests/Step005AcceptanceTests.cs               (modified: + 1 Trait attribute line only)
tests/XlightsAI.Studio.Shell.Tests/Step006ShellWiringTests.cs              (modified: + 1 Trait attribute line only)
tests/XlightsAI.Studio.Projects.Tests/Step006AcceptanceTests.cs            (modified: + 1 Trait attribute line only)
tests/XlightsAI.Studio.Projects.Tests/Step007AcceptanceTests.cs            (modified: + 1 Trait attribute line only)
implementation-evidence/PHASE-01/STEP-08.md
```

No production `src/**` file, `.csproj`, fixture file, `tools/**` file, or `reference/**` file was
touched. No xLights show files exist in or near this repository; this step produces no XSQ/show-folder
writes of any kind. No existing test assertion, fixture hash, or domain behavior changed — every
modification to a pre-existing file is exactly one additive attribute line.

## 8. Package/dependency inventory (new, this step only)

None. No new NuGet package was added to any `.csproj`. The new `Step008AcceptanceTests` uses only
`System.Diagnostics.Process` (BCL) to shell out to the already-installed `dotnet` CLI. The CI workflow
uses only `actions/checkout@v4`, `actions/setup-dotnet@v4`, and `actions/upload-artifact@v4` — all
standard, widely-used GitHub-maintained or GitHub-published Actions, no third-party marketplace action.
No Python, Docker, or Git-at-runtime dependency was added, satisfying Section 6's "performs tests without
developer Python."

## 9. Architecture deviations

- **"unit/integration/golden test projects" implemented as trait-categorized classes inside the three
  existing, immutable, feature-sliced test projects — not as three new physical `.csproj` files.** The
  phase spec's own architecture notes ("Prefer...do not fabricate generic interfaces without a concrete
  boundary", "Avoid collecting multiple unverified tasks into one mega-commit") and the agent instructions
  for this task ("Treat all preceding completed step contracts as immutable unless the current step has
  an accepted migration plan") together argue against physically relocating every Step 001–007 test file
  into a new `Unit.Tests`/`Integration.Tests`/`Golden.Tests` project layout — that would touch every prior
  step's test project reference, `.gitignore`-tracked path, and (for Steps 002/003) fixture-copy wiring,
  for a purely organizational reason with no functional benefit over trait filtering. `dotnet test
  --filter "Category=X"` achieves the identical CI-observable outcome (three independently runnable,
  independently reportable "test projects" in the CI sense of distinct, separately-named `.trx` outputs)
  without the migration risk. This is disclosed here as the explicit interpretation taken, per the task's
  own instruction to "Show me any architecture deviations."
- **No `Directory.Build.props` was added**; formatting is enforced purely via `.editorconfig` +
  `dotnet format --verify-no-changes`, not via `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, or
  added Roslyn analyzers. Considered and deferred: any of those would risk surfacing new warnings in
  Steps 001–007's already-"0 Warning(s)" immutable code, which this step's own instructions say to treat
  as immutable absent an accepted migration plan — introducing new warnings on old code is exactly such a
  migration, and was judged out of this step's narrow scope ("the minimum complete...slice"). Flagged as
  a carried-forward opportunity in §10, not silently skipped.
- **CI targets `windows-latest` only, no Linux/macOS matrix.** Section 6's own guidance says "CI builds
  Windows at minimum," which a single Windows job satisfies literally; a cross-platform matrix was not
  added because the shipped product is an Avalonia **desktop** app whose Step 004–007 manual demos were
  all performed on native Windows, and PHASE-01 Section 2 says nothing about Linux/macOS as a target for
  this phase.
- **Category filters are a complete, verified partition of the suite (§3), not merely three independent
  subsets that might overlap or miss tests.** This was explicitly checked (9 + 44 + 27 = 80, matching the
  unfiltered total) rather than assumed, because an uncategorized or double-categorized test would make
  CI's "store test reports" guarantee silently incomplete.
- **Real cloud GitHub Actions execution is BLOCKED, not mocked** — disclosed in full in §6, per this
  task's explicit instruction not to pretend a mock is equivalent to the real external environment.

## 10. Unresolved issues / risks carried forward

1. **Evidence-filename convention inconsistency** — still unresolved (carried forward again from
   Steps 002–007).
2. **No native folder-picker dialog** (Step 006 §9/§10, unchanged).
3. **No cross-process file lock for `ProjectService.SaveAsync`'s stale-state guard** (Step 006/007,
   unchanged this step).
4. **No dedicated "recovery screen" UI distinct from `ActionErrorMessage`** (Step 007 §9/§10 item 4,
   unchanged this step — this step adds no UI).
5. **No `schema_migrations` audit table** (Step 007 §9, unchanged).
6. Git Bash remains non-functional in this environment (carried forward from Steps 001–007); all
   commands in this evidence were run via PowerShell.
7. **Pre-migration backup files (`studio.db.bak-v*`) are never pruned** (Step 007 §10 item 7, unchanged).
8. **Real GitHub Actions cloud execution has never actually run** (§6) — this repository has no remote
   and no commits; the workflow file's shape and every command it runs were verified locally and for
   real, but the workflow itself has not executed inside GitHub's infrastructure. Recommend the next
   actor that pushes this repository to a real GitHub remote re-confirm the first real cloud run
   succeeds, since `windows-latest`'s exact preinstalled `.NET` SDK patch version, available disk space,
   and `actions/upload-artifact@v4` quota/retention behavior cannot be verified from this environment.
9. **No `Directory.Build.props`/analyzer-based static-analysis gate was added** (§9) — the "static
   checks" required acceptance is currently satisfied only by `dotnet format --verify-no-changes` plus
   the existing "0 Warning(s)" build; a later step or phase may want to add `TreatWarningsAsErrors` or
   enabled analyzers as its own accepted migration against the now-immutable Steps 001–008 contract.
10. **No code-coverage threshold/gate** — `coverlet.collector` is already referenced by each test
    project (pre-existing, from Step 004's initial scaffolding), but this step does not wire coverage
    collection or a minimum-coverage gate into the CI workflow; flagged as a possible future enhancement,
    not a requirement of this step's literal acceptance text.

## 11. Gate checklist (this step only)

- [x] CI workflow added (`.github/workflows/ci.yml`), builds Windows at minimum, performs tests without
      developer Python (Section 2, Section 9).
- [x] Unit/integration/golden test categorization implemented and verified as a complete partition of
      the full suite (Section 2, Section 5, Section 9).
- [x] Formatting/static checks: `.editorconfig` + `dotnet format --verify-no-changes` gate added and
      proven to pass cleanly against every pre-existing file with zero forced reformatting (Section 2,
      Section 3, Section 5).
- [x] Required acceptance — "Clean checkout builds, runs fixtures, and stores test reports" — proven for
      every command the CI workflow runs, executed for real locally, plus a real `.trx`-on-disk assertion
      (Section 4, Section 5).
- [x] Failure/edge case — "Invalid input, cancellation or stale persisted state must not corrupt the
      project" — proven by three dedicated negative-path tests (Section 5).
- [x] Full existing regression suite re-run: 80/80 passing (76 pre-existing, unmodified behavior, + 4 new
      Step 008 tests), zero regressions.
- [x] Steps 001–007's test files treated as immutable except one additive, disclosed `[Trait(...)]` line
      per file — no assertion, fixture, or production file touched (Section 1, Section 7, Section 9).
- [x] No original/show files touched; nothing pre-existing was at risk (none exist in this repo).
- [x] Architecture deviations disclosed in full (Section 9), per this task's explicit instruction.
- [ ] Real GitHub Actions cloud execution: **BLOCKED**, disclosed in full with reasoning, not
      substituted with a mock (Section 6, Section 10 item 8).

**Step 008 status: PASS** (real cloud CI execution explicitly BLOCKED and disclosed, not claimed —
Section 6). This completes PHASE-01's eight numbered steps; the whole-phase gate demonstration (Section
1: fresh install → create/close/reopen → second project → injected migration failure → recovery/backup/
diagnostic log) is a separate, whole-phase deliverable not re-run by this step-scoped evidence file —
recommend a dedicated phase-gate pass across Steps 001–008 together before PHASE-02 begins.
