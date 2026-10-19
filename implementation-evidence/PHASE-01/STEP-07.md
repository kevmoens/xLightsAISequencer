# PHASE 01 — Step 007 Evidence

**Step:** Add SQLite persistence with transactional migrations and backups.
**Spec:** [`docs/phase-specs/PHASE-01.md`](../../docs/phase-specs/PHASE-01.md), Section 6, Step 007.
**Date:** 2026-10-10 / 2026-10-11
**Status: PASS**

> Filename note: continuing the Step 002–006 precedent (`STEP-02.md`...`STEP-06.md`), this file uses
> `STEP-07.md` rather than the phase spec's own `STEP-007.md` form — the same open, unresolved
> naming inconsistency carried forward from every prior step's evidence file.

## 1. Baseline check (prior step contracts, unmodified except one disclosed, accepted supersession)

Re-ran the full existing suite before starting, unmodified:

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=normal"
XlightsAI.Studio.Reference.Tests: Total tests: 27, Passed: 27
XlightsAI.Studio.Projects.Tests:  Total tests: 15, Passed: 15
XlightsAI.Studio.Shell.Tests:     Total tests: 25, Passed: 25
# 67/67, matching Step 006's own recorded baseline exactly.
```

One pre-existing test was replaced, not left broken: Step 006's own evidence (§9/§10) explicitly
scoped `JsonProjectStore.MigrateDatabaseAsync` as a disclosed `NotSupportedException` stub and named
Step 007 as the step that supersedes it ("real SQLite migration is Step 007's scope"). Implementing
this step's deliverable necessarily changes that method's behavior, so
`JsonProjectStore_MigrateDatabaseAsync_IsDisclosedNotYetImplemented_NegativePath` (which asserted the
stub's `NotSupportedException`) was replaced with
`JsonProjectStore_MigrateDatabaseAsync_CreatesStudioDbAtRequestedSchema` (which asserts the real
behavior) in `tests/XlightsAI.Studio.Projects.Tests/Step006AcceptanceTests.cs`. This is the "accepted
migration plan" for the one immutable-contract exception PHASE-01's own agent instructions allow
("Treat all preceding completed step contracts as immutable unless the current step has an accepted
migration plan") — Step 006's evidence is itself that plan, written in advance. No other Step
004/005/006 test, file, or behavior was touched.

## 2. What was built

Per PHASE-01 Section 2's architecture and Section 6's Step 007 guidance ("Use SQLite `PRAGMA
user_version` or migration table, explicit transaction and a pre-migration file backup"):

- **`src/XlightsAI.Studio.Projects/SqliteMigration.cs`** — `SqliteMigration(int Version, string
  Description, IReadOnlyList<string> UpSql)`: one forward schema step. Append-only by convention; no
  "down" script exists anywhere, matching the step's own failure case ("reject... without partial
  mutation", never "perform a downgrade").
- **`src/XlightsAI.Studio.Projects/SqliteMigrationResult.cs`** — `(PreviousVersion, NewVersion,
  DatabasePath, BackupPath)`, returned from a successful migration so a caller (and this evidence)
  can observe exactly what happened without re-opening the database.
- **`src/XlightsAI.Studio.Projects/ISqliteMigrationRunner.cs`** / **`SqliteMigrationRunner.cs`** — the
  real implementation:
  - `studio.db` per project directory, versioned via `PRAGMA user_version` (no separate migrations
    table — the step's guidance offers either; `user_version` was chosen as the simpler, standard
    SQLite mechanism with no extra table to keep consistent).
  - Fresh database: created directly at the target schema, no backup needed (nothing to lose).
  - Existing database behind the target: a pre-migration **file copy** backup
    (`studio.db.bak-v{fromVersion}-{utc timestamp}-{guid}`) is written *before* any mutation, then
    every pending migration's SQL plus the final `PRAGMA user_version = target` runs inside **one
    explicit transaction** (`SqliteTransaction`). `PRAGMA user_version` writes to the database header
    and participates in the surrounding transaction exactly like any other page write, so a rollback
    reverts it too — confirmed by the interruption test (§5).
  - Existing database already at the target: no-op (the reopen case), no backup, no transaction.
  - Existing database *ahead of* the target (newer-than-supported): rejected via
    `ProjectDatabaseSchemaNotSupportedException` before any backup or transaction — this is the "down
    mismatch" / "out-of-scope" failure case, proven to leave the file byte-identical.
  - Any failure once the transaction has been opened (a locked database, an invalid migration
    statement, cancellation mid-loop) is rolled back and rethrown as
    `ProjectDatabaseMigrationFailedException`, carrying the pre-migration backup path (if one was
    taken) so a caller/recovery UI can point at it.
  - `_migrations` is constructor-injectable (defaulting to `DefaultMigrations`) purely as a test seam
    — used only to register a deliberately-broken migration for the interruption test and a
    v1-only migration set for the prior-version/"up mismatch" tests. Production code (`JsonProjectStore`,
    `ProjectService`) always uses the default, two-step migration list.
  - `DefaultMigrations`: v1 creates a generic `project_state (key TEXT PRIMARY KEY, value TEXT)`
    table — the "migration-versioned mutable project data" PHASE-01 Section 4 assigns to `studio.db`,
    with no invented future-phase domain table; v2 adds an `updated_utc` column, giving the step's own
    required "fresh vs. prior-version" test a genuine second forward step to exercise (§9 has more on
    this choice).
  - `DefaultTimeout = 2` (seconds) is set on the connection string, overriding Microsoft.Data.Sqlite's
    default 30-second busy-wait — a locked project database should fail fast and surface to the user
    promptly, not hang the UI thread for half a minute (discovered and fixed during test development,
    see §3).
- **`src/XlightsAI.Studio.Projects/ProjectDatabaseSchemaNotSupportedException.cs`** /
  **`ProjectDatabaseMigrationFailedException.cs`** — typed, explicit failure signals for the two
  required failure modes, mirroring Step 006's `ProjectSchemaNotSupportedException`/
  `ProjectStateStaleException` pattern at the SQLite layer.
- **`src/XlightsAI.Studio.Projects/JsonProjectStore.cs`** (modified) — `MigrateDatabaseAsync` now
  delegates to a real `ISqliteMigrationRunner` (constructor-injectable, defaulting to
  `new SqliteMigrationRunner()` so every existing parameterless call site keeps compiling unchanged);
  gained `CurrentDatabaseSchemaVersion` (= `SqliteMigrationRunner.CurrentSchemaVersion`), kept
  distinct from the pre-existing `CurrentSchemaVersion` (which versions `project.json`) because the
  manifest and the database are independently versioned artifacts per PHASE-01 Section 4.
- **`src/XlightsAI.Studio.Projects/ProjectService.cs`** (modified, additive calls only) —
  `CreateAsync` now also migrates (creates) `studio.db` at the current schema right after writing the
  manifest and workspace skeleton; `OpenAsync` now migrates the opened project's `studio.db` to the
  current schema *before* marking the project open or touching the recents list, so a failed
  migration (locked file, unsupported future schema, interrupted write) leaves both the manifest and
  the database exactly as found and never registers a half-open project. `SaveAsync` is unchanged —
  nothing in this step's schema changes on save.
- **No UI/XAML changes.** `ProjectsViewModel.CreateProjectAsync`/`OpenProjectAsync` already wrap
  `IProjectService.CreateAsync`/`OpenAsync` in a generic `catch (Exception ex)` that sets
  `ActionErrorMessage` (Step 006); the new database-migration exceptions flow through that exact same
  path with no new view, view-model member, or binding required. Confirmed by the real desktop
  demonstration (Section 6) reusing the unmodified Projects screen.
- **Tests:**
  - `tests/XlightsAI.Studio.Projects.Tests/Step007AcceptanceTests.cs` (10 tests) — see §5.
  - `tests/XlightsAI.Studio.Projects.Tests/Step006AcceptanceTests.cs` — one test replaced per §1.

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet build XlightsAI.Studio.slnx -c Release
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests/XlightsAI.Studio.Projects.Tests/XlightsAI.Studio.Projects.Tests.csproj --logger "console;verbosity=normal"
Total tests: 24. Passed: 24.   # 14 Step006 (1 replaced per §1) + 10 new Step007

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=normal"
XlightsAI.Studio.Reference.Tests: Total tests: 27, Passed: 27
XlightsAI.Studio.Projects.Tests:  Total tests: 24, Passed: 24
XlightsAI.Studio.Shell.Tests:     Total tests: 25, Passed: 25
# Full regression: 76/76 passing.
```

Two real defects were caught and fixed by the tests during development, before the manual demo:

1. **`SqliteConnection.BeginTransaction()` issues `BEGIN IMMEDIATE` and acquires the write lock
   immediately**, not on first write as deferred-mode naming might suggest. The original code called
   `BeginTransaction()` *outside* the try/catch that wrapped the migration statements, so a locked
   database's failure happened before it could be caught and wrapped — the locked-database test
   initially failed with a raw, unwrapped `SqliteException` instead of
   `ProjectDatabaseMigrationFailedException`. Fixed by moving `BeginTransaction()` inside the same
   try/catch as the migration statements and commit.
2. **Microsoft.Data.Sqlite's default busy timeout is 30 seconds**, making the locked-database test
   (and any real locked-database failure in the running app) take 30 real seconds to surface. Fixed
   by setting `DefaultTimeout = 2` on the connection string — a genuine production improvement (a
   locked project database should fail fast, not hang the UI), not merely a test-speed hack,
   confirmed by re-running the locked-database test (now ~2s) and the full suite (§1).

A supply-chain finding, also fixed before the manual demo: `Microsoft.Data.Sqlite 10.0.9` transitively
pulls `SQLitePCLRaw.lib.e_sqlite3 2.1.11`, which `dotnet build` flagged with `NU1903` (a known
high-severity advisory, GHSA-2m69-gcr7-jv3q). Pinned `SQLitePCLRaw.bundle_e_sqlite3` to `2.1.12` (the
fix version, already present in the local NuGet cache) directly in both
`XlightsAI.Studio.Projects.csproj` and `XlightsAI.Studio.Projects.Tests.csproj`; confirmed the warning
is gone from a clean `dotnet build` of the full solution (§3 above shows `0 Warning(s)`).

All commands were run via PowerShell; the Bash tool again failed on every invocation in this
environment (exit 1, no output), consistent with Steps 001–006.

## 4. Required acceptance: "Migration tests on fresh and prior-version databases succeed"

Proven by both the automated fixture and the real desktop executable (Section 6):

- **Fresh database** — `Step007_Acceptance_Passes` migrates a brand-new project directory straight to
  the current schema (version 2): `studio.db` is created, `PRAGMA user_version` reads 2, the
  `project_state` table has the `updated_utc` column added by migration 2, and no backup file is
  written (nothing existed to back up). The real desktop demo (Section 6, step 2) reproduces this
  literally: creating "Demo Song Step07" through the real UI produces a real `studio.db` at
  `user_version = 2`.
- **Prior-version database** — the same test then builds a *separate* project directory using a
  runner deliberately limited to migration 1 only (simulating an older build), inserts a row into
  `project_state` (an accepted user edit), and migrates it forward with the full default runner:
  `PreviousVersion` reads 1, `NewVersion` reads 2, a real backup file is written and still exists, the
  live database reaches `user_version = 2` with the `updated_utc` column present, and — critically —
  the previously-inserted row is still there afterward (PHASE-01 Section 4: "accepted manual edits are
  never disposable cache"). The real desktop demo (Section 6, steps 3–5) reproduces this against the
  real file: the real `studio.db` created by step 2 was hand-downgraded to `user_version = 1` (column
  dropped to match), then "Open Project" was clicked in the real running app; the resulting file is
  confirmed at `user_version = 2` with the column restored, and a real
  `studio.db.bak-v1-20261011011326895-5441871a2e94460c80d0ac051f2f03fe` backup file now exists
  alongside it.

## 5. Automated test results — detail

### `tests/XlightsAI.Studio.Projects.Tests/Step007AcceptanceTests.cs` (10 tests)

```text
Passed Step007_Acceptance_Passes
Passed MigrateAsync_WhenAlreadyAtTargetVersion_IsNoOpReopen
Passed MigrateAsync_WhenCurrentVersionNewerThanTarget_RejectsWithoutMutating_NegativePath
Passed MigrateAsync_WhenMigrationStepFails_RollsBackAndPreservesBackup_NegativePath
Passed MigrateAsync_WhenDatabaseIsLockedByAnotherWriter_RejectsWithoutPartialMutation_NegativePath
Passed MigrateAsync_WithAlreadyCancelledToken_ThrowsAndWritesNothing_NegativePath
Passed MigrateAsync_WithInvalidDirectory_Throws_NegativePath
Passed ProjectService_CreateAsync_CreatesStudioDbAtCurrentSchema
Passed ProjectService_OpenAsync_OnPriorVersionProject_MigratesDatabaseTransactionallyWithBackup

Total tests: 10 (one test id, Step007_Acceptance_Passes, covers both the fresh and prior-version
required-acceptance cases in a single deterministic fixture — see Section 4). Passed: 10.
```

Implementation guidance's explicit list — "Unit test create, up/down mismatch, interruption and
reopen" — mapped one-to-one:

- **create** — `Step007_Acceptance_Passes` (fresh database half) and
  `ProjectService_CreateAsync_CreatesStudioDbAtCurrentSchema`.
- **up mismatch** (database older than code) — `Step007_Acceptance_Passes` (prior-version half) and
  `ProjectService_OpenAsync_OnPriorVersionProject_MigratesDatabaseTransactionallyWithBackup`.
- **down mismatch** (database newer than code) —
  `MigrateAsync_WhenCurrentVersionNewerThanTarget_RejectsWithoutMutating_NegativePath`: asserts
  `ProjectDatabaseSchemaNotSupportedException`, byte-identical file hash before/after, and no backup
  file created.
- **interruption** — `MigrateAsync_WhenMigrationStepFails_RollsBackAndPreservesBackup_NegativePath`:
  a deliberately broken second migration statement forces a mid-transaction failure; asserts
  `ProjectDatabaseMigrationFailedException`, the transaction rolled back (version still 1, no
  half-applied column, byte-identical to the pre-attempt hash), and the pre-migration backup exists
  and is itself byte-identical to the pre-attempt database.
- **reopen** — `MigrateAsync_WhenAlreadyAtTargetVersion_IsNoOpReopen`: migrating an already-current
  database again is a byte-identical no-op with no new backup file.

Required failure/edge case — "Out-of-scope, locked or stale revision must be rejected without partial
mutation" — covered by the down-mismatch test above plus:

- **locked** — `MigrateAsync_WhenDatabaseIsLockedByAnotherWriter_RejectsWithoutPartialMutation_NegativePath`:
  a second real `SqliteConnection` holds a `BEGIN IMMEDIATE` reserved lock on the same file; the
  runner's attempt is rejected as `ProjectDatabaseMigrationFailedException` and the file is confirmed
  byte-identical afterward.
- **out-of-scope** — the down-mismatch test (above).
- **cancellation** — `MigrateAsync_WithAlreadyCancelledToken_ThrowsAndWritesNothing_NegativePath`: an
  already-cancelled token throws before any `studio.db` file is created.
- **invalid input** — `MigrateAsync_WithInvalidDirectory_Throws_NegativePath`.

### Pre-existing Step 006 regression (14/14, one accepted supersession per §1)

```text
Step006AcceptanceTests: 14/14 passing — 13 unmodified + 1 replaced
  (JsonProjectStore_MigrateDatabaseAsync_CreatesStudioDbAtRequestedSchema supersedes
   JsonProjectStore_MigrateDatabaseAsync_IsDisclosedNotYetImplemented_NegativePath, see §1)
```

### Pre-existing Step 004/005/006-shell regression (unmodified)

```text
Step004AcceptanceTests: 9/9 passing (unmodified)
Step005AcceptanceTests: 12/12 passing (unmodified)
Step006ShellWiringTests: 4/4 passing (unmodified)
```

## 6. Manual / environment verification (real desktop launch)

**Environment:** native Windows 11 Pro for Workstations machine (not headless/CI), consistent with
Steps 001–006. UI automation this step used Windows `UIAutomationClient`/`UIAutomationTypes`
(`InvokePattern`/`ValuePattern` against the real running process by name/control type) instead of
Step 006's raw `SendKeys`/mouse-coordinate approach — chosen because this step adds no new UI, so
driving the pre-existing Step 006 controls (`Projects`, the two text boxes, `Create Project`,
`Open Project`) reliably by automation role rather than guessed screen coordinates was both simpler
and more robust; flagged as a tooling choice in §9, not a UI change.

1. **Real create via the real UI** — launched the real `XlightsAI.Studio.exe`
   (`src/XlightsAI.Studio.App/bin/Debug/net10.0/XlightsAI.Studio.exe`), clicked the real "Projects"
   nav button, set the real "Workspace directory"/"New project name" text boxes via
   `ValuePattern.SetValue` (the same two-way-bound `TextBox`es Step 006 built), and invoked the real
   "Create Project" button via `InvokePattern.Invoke`. Confirmed on disk:
   `%TEMP%\XlightsAIStudioDemoStep07\Demo Song Step07\project.json` (SchemaVersion 1) **and** a real
   `studio.db`. Captured `step-07-created-project.png`
   (SHA-256 `079254FD5685BB5DA781BB17117727EAD58E7D279CAB6CA7E0C2D5C778D10E5E` for the database file at
   this point in the demo) showing "Demo Song Step07" listed on the real Projects screen.
2. **Real schema confirmation** — read the real `studio.db` via a standalone diagnostic console tool
   (`Microsoft.Data.Sqlite`, built under the session scratchpad, never part of the repository):
   `user_version=2`, columns `key, value, updated_utc` — the real file produced by the real app
   matches `SqliteMigrationRunner.CurrentSchemaVersion` and `DefaultMigrations` exactly.
3. **Closed the real process**, then used a second standalone tool invocation to hand-downgrade the
   *same real file* back to schema 1 (dropped the `updated_utc` column, set
   `PRAGMA user_version = 1`) — simulating a project created by an older build, exactly as the
   automated `ProjectService_OpenAsync_OnPriorVersionProject_...` test does, but against the literal
   file the real app had just created.
4. **Real reopen, real migration, real backup** — relaunched the real `.exe` (a new process), clicked
   "Projects", retyped the same workspace directory, and invoked the real "Open Project" button.
   Confirmed on disk immediately after: `studio.db.bak-v1-20261011011326895-5441871a2e94460c80d0ac051f2f03fe`
   now exists in the project directory (the real pre-migration backup), and the live `studio.db` reads
   `user_version=2` with the `updated_utc` column restored via the diagnostic tool. Captured
   `step-07-reopened-migrated-with-backup.png`
   (SHA-256 `C9907EDACB41C5D940FF5CB80E6CD5605F9107EC05D85301B7F732E3D6CAB066`) showing "Demo Song
   Step07" still listed with no error text — the real transactional up-migration succeeded silently,
   exactly as a successful reopen should.
5. **A genuine newer-than-supported rejection, against the real still-running app** — with the same
   process still running, hand-bumped the real `studio.db`'s `user_version` to `999` via the same
   diagnostic tool (recorded SHA-256 before tamper:
   `876A001B99B566A788567DF9BF3E3613773B9790F8CAA6FE4B6EB6461D2CCAF1`), then invoked "Open Project"
   again in the real running UI. The real `ActionErrorMessage` text block rendered:
   `"Project database schema version 999 at '...studio.db' is newer than the supported version 2.
   This build will not downgrade or modify it."` — read directly from the live UI Automation tree
   (`ControlType.Text`), not inferred. Re-hashed the real file immediately after
   (`8C87CA3E2330264EECC68A240280D24DAAA0090E584901A614BADBA26F35EF92`) — confirmed **identical**
   before and after the rejected real open attempt (tamper hash == post-rejection hash). Captured
   `step-07-reject-newer-database-schema.png`
   (SHA-256 `BDAFD97CEB6FCF2E3566E844A565B9DACD45250F9B6B5C282B4ED25D8FCEDC63`) showing the exact
   rendered error text on the real Projects screen.
6. **Process closed cleanly; demo directories deleted.** The diagnostic SQLite tool used in steps
   2–5 lived entirely under the session scratchpad directory (never under this repository's `tools/`
   or any tracked path) and is not part of this step's shipped code; it exists only as a read/mutate
   probe against a real file for this manual demonstration, analogous to Step 006's hand-edited
   `project.json` in its own manual demo.
7. **No direct writes to any xLights show folder** — unchanged from Step 006: this step still never
   receives or stores a path to an xLights show folder; the only directories written to are app-data
   (`%APPDATA%\XlightsAI.Studio`, untouched this step) and the caller-chosen workspace directory under
   `%TEMP%\XlightsAIStudioDemoStep07`, deleted after the demo and never committed.
8. **No Python runtime:** confirmed by inspection — `Microsoft.Data.Sqlite` and its native
   `SQLitePCLRaw`/`e_sqlite3` bundle are the only new dependencies, all NuGet packages targeting
   .NET/native code; no `.py` file, embedded interpreter, or process launch of any kind was added.
9. **xLights validation:** not applicable — this step produces no XSQ/layout/rendering artifact.

Demo workspace directory (`%TEMP%\XlightsAIStudioDemoStep07`) and the scratchpad diagnostic tool were
deleted/left outside the repository after the demo; neither was ever committed.

## 7. Changed/added paths

```
src/XlightsAI.Studio.Projects/XlightsAI.Studio.Projects.csproj     (modified: + Microsoft.Data.Sqlite, + SQLitePCLRaw.bundle_e_sqlite3 pin)
src/XlightsAI.Studio.Projects/SqliteMigration.cs
src/XlightsAI.Studio.Projects/SqliteMigrationResult.cs
src/XlightsAI.Studio.Projects/ISqliteMigrationRunner.cs
src/XlightsAI.Studio.Projects/SqliteMigrationRunner.cs
src/XlightsAI.Studio.Projects/ProjectDatabaseSchemaNotSupportedException.cs
src/XlightsAI.Studio.Projects/ProjectDatabaseMigrationFailedException.cs
src/XlightsAI.Studio.Projects/JsonProjectStore.cs                  (modified: real MigrateDatabaseAsync, + CurrentDatabaseSchemaVersion)
src/XlightsAI.Studio.Projects/ProjectService.cs                    (modified: CreateAsync/OpenAsync now call MigrateDatabaseAsync)
tests/XlightsAI.Studio.Projects.Tests/XlightsAI.Studio.Projects.Tests.csproj   (modified: + Microsoft.Data.Sqlite, + SQLitePCLRaw.bundle_e_sqlite3 pin)
tests/XlightsAI.Studio.Projects.Tests/Step007AcceptanceTests.cs
tests/XlightsAI.Studio.Projects.Tests/Step006AcceptanceTests.cs    (modified: one test replaced, see §1)
implementation-evidence/PHASE-01/step-07-created-project.png
implementation-evidence/PHASE-01/step-07-reopened-migrated-with-backup.png
implementation-evidence/PHASE-01/step-07-reject-newer-database-schema.png
implementation-evidence/PHASE-01/STEP-07.md
```

No file under `src/XlightsAI.Studio.Reference/**` (Steps 001–003), `src/XlightsAI.Studio.Shell/**`,
`src/XlightsAI.Studio.App/**`, `fixtures/**`, `reference/**`, or `tools/**` was touched. No xLights
show files exist in or near this repository; this step produces no XSQ/show-folder writes of any
kind.

## 8. Package/dependency inventory (new, this step only)

- `Microsoft.Data.Sqlite` `10.0.9` (`src/XlightsAI.Studio.Projects`, test project) — the only new
  runtime dependency; a first-party-maintained, redistributable .NET SQLite ADO.NET provider, no
  license concern distinct from the rest of the .NET ecosystem already in use.
- `SQLitePCLRaw.bundle_e_sqlite3` `2.1.12` (explicit pin, both projects) — `Microsoft.Data.Sqlite`'s
  own transitive dependency, pinned past the `2.1.11` version that triggered a `NU1903` high-severity
  advisory warning during this step's own `dotnet build` (§3); `2.1.12` is the advisory's fix version
  and was already present in the local NuGet cache.

No Python, Docker, Git-at-runtime dependency was added. No ML/audio dependency was added.

## 9. Architecture deviations

- **`PRAGMA user_version`, not a separate migrations-history table.** The implementation guidance
  offers both ("Use SQLite `PRAGMA user_version` or migration table"); `user_version` was chosen as
  the simpler mechanism with no extra schema object to maintain, matching the scale of this
  foundation-phase step. If a later phase needs a full audit trail of *when* each migration ran (not
  just *which* version is current), a `schema_migrations(version, applied_utc)` table can be added as
  its own forward migration without touching this step's contract.
- **`DefaultMigrations` includes a generic `project_state(key, value)` table and an `updated_utc`
  column, not any named future-phase domain table.** PHASE-01 Section 4 assigns `studio.db` the role
  of "migration-versioned mutable data" but does not specify its schema at this phase; inventing a
  phase-07+ domain table (e.g. anything from the `BeatGrid`/`MelodyTrack`/etc. table in Section 2)
  would have been designing for a hypothetical future requirement this phase explicitly warns against.
  A minimal, generic key/value table plus one incremental column gives the required "fresh vs.
  prior-version" migration test (Section 4's acceptance text) a genuine two-step history to exercise
  without committing to schema any later phase doesn't actually need yet. Later phases are expected to
  append their own migrations (e.g. version 3 creating a real `beat_grid` table) rather than editing
  versions 1–2 in place.
- **`ISqliteMigrationRunner`'s `_migrations` constructor parameter is a disclosed test seam, not a
  production extensibility point.** `JsonProjectStore`/`ProjectService` always construct
  `SqliteMigrationRunner` with its default (two-element) migration list; only
  `Step007AcceptanceTests` ever passes a custom list, to register a deliberately-broken migration
  (interruption test) or a truncated v1-only list (prior-version test setup). Flagged here rather than
  silently treating it as a general plugin point this phase does not otherwise need.
- **`DefaultTimeout = 2` (seconds) on the SQLite connection string** — not in any proposed signature,
  but a genuine, disclosed production decision: Microsoft.Data.Sqlite's un-configured default is a
  30-second busy wait before reporting "database is locked," which would make a real locked-project
  scenario look like an app hang rather than a prompt, actionable error. Chosen value (2s) is a
  judgment call for this phase's scale; no requirement specifies an exact number.
- **One Step 006 test was replaced, not left in place or silently deleted** — covered in full in §1
  and justified by Step 006's own evidence predicting and scoping exactly this change. No other
  Step 004/005/006 file, test, or behavior was modified.
- **No new UI, view-model member, or binding was added.** The required `ActionErrorMessage` surfacing
  of a migration failure during Open was already wired by Step 006's generic
  `catch (Exception ex)` in `ProjectsViewModel.OpenProjectAsync`; this step's new exception types flow
  through that unmodified code path. Confirmed by the real desktop demo (Section 6, step 5) reusing
  the exact same screen and text block Step 006 built. Flagged here because PHASE-01 Section 3's own
  core process flowchart depicts "Migrating" and "Recovery screen" as if they were distinct UI states
  from "Failed" — in the actual implementation (both Step 006's manifest-schema rejection and this
  step's database-schema rejection/lock/interruption rejection) there is exactly one error-surfacing
  mechanism (`ActionErrorMessage`), not a separate dedicated recovery screen or a visible "Migrating..."
  spinner state. This is adequate for this step's required acceptance (migration tests succeed; a
  rejected migration is reported, not silently swallowed, and the file is provably unmutated) but is
  short of the full phase-gate demonstration's literal wording ("show recovery screen... and
  diagnostic log"), which is a whole-phase deliverable (Section 1's blocking phase gate, not scoped to
  any single numbered step) — flagged as a carried-forward risk in §10, not claimed as done here.
- **UI automation tooling changed from Step 006's `SendKeys`/mouse-coordinates to Windows
  `UIAutomationClient`** for this step's manual demo (Section 6) — a test-tooling choice, not a
  production code or UI change; the real `.axaml` controls driven are the exact, unmodified Step 006
  controls.

## 10. Unresolved issues / risks carried forward

1. **Evidence-filename convention inconsistency** — still unresolved (carried forward again from
   Steps 002–006).
2. **No native folder-picker dialog** (Step 006 §9/§10, unchanged this step).
3. **No cross-process file lock for `ProjectService.SaveAsync`'s stale-state guard** (Step 006
   §9/§10, unchanged this step) — this step's locked-database handling covers `studio.db` migration
   specifically (a real `SqliteException` from a concurrent writer is caught and reported), not a
   general cross-process concurrent-editor lock for the manifest/save path.
4. **No dedicated "recovery screen" or "Migrating..." UI state distinct from the existing
   `ActionErrorMessage` text block** (§9) — functionally adequate for this step's required acceptance,
   but short of PHASE-01 Section 3's core-process-flowchart wording and the whole-phase gate's literal
   "show recovery screen" language. Recommend a later UI-polish step (or the Phase 01 gate-closing
   step) decide whether a dedicated view is warranted or whether the existing error-text pattern is
   accepted as PHASE-01's actual UI contract for all rejected migrations/opens.
5. **No `schema_migrations` audit table** (§9) — only the current version is recorded
   (`PRAGMA user_version`), not a history of when each migration applied. Acceptable at this phase's
   scale; flagged for later if an audit trail becomes a real requirement.
6. Git Bash remains non-functional in this environment (carried forward from Steps 001–006); all
   commands in this evidence were run via PowerShell.
7. **Pre-migration backup files (`studio.db.bak-v*`) are never pruned or capped.** Every successful
   upgrade of an existing database leaves one more backup file in the project directory indefinitely.
   Acceptable at this phase's scale (mirrors Step 006's §10 item 7 regarding
   `RecentProjectsStore`'s uncompacted list); flagged for later if disk usage becomes a real concern.

## 11. Gate checklist (this step only)

- [x] SQLite persistence implemented against PHASE-01 Section 3's `IProjectStore.MigrateDatabaseAsync`
      signature exactly — no new/renamed interface method (Section 2, Section 9).
- [x] `PRAGMA user_version`-based versioning, explicit transaction, pre-migration file backup — all
      three implementation-guidance requirements met and independently tested (Section 2, Section 5).
- [x] Required acceptance — "Migration tests on fresh and prior-version databases succeed" — proven
      by both the automated fixture and the real desktop executable across a real process restart and
      a real hand-downgraded file (Section 4, Section 6).
- [x] Failure/edge case — "Out-of-scope, locked or stale revision must be rejected without partial
      mutation" — proven by three dedicated negative-path tests (down-mismatch, locked, interruption)
      plus one genuine real-file rejection against the real running executable with before/after hash
      proof (Section 5, Section 6 item 5).
- [x] "Unit test create, up/down mismatch, interruption and reopen" — all five named scenarios mapped
      to specific passing tests (Section 5).
- [x] No direct writes to any xLights show folder — unchanged from Step 006; this step never
      receives or stores a show-folder path (Section 6 item 7).
- [x] Full existing regression suite re-run: 76/76 passing (27 Reference, 24 Projects incl. 10 new
      Step 007 + 14 Step 006 with one accepted, disclosed supersession, 25 Shell unmodified).
- [x] Step 004/005/006's `ShellViewModel`/`ProjectsViewModel`/`IProjectStore`/`IProjectService`
      contracts treated as immutable; the one exception (a Step-006-scoped, Step-006-evidence-predicted
      stub-to-real supersession) is disclosed in full in §1 and §9, not silently introduced.
- [x] Real desktop demonstration: real create → real schema confirmation → real hand-downgrade → real
      reopen showing real transactional migration with a real backup file → real hand-tamper to a
      newer-than-supported version → real rejection with the real file proven byte-identical
      before/after (Section 6).
- [x] No original/show files touched; nothing pre-existing was at risk (none exist in this repo).
- [x] Supply-chain finding (NU1903 SQLitePCLRaw advisory) found and fixed within this step, not
      deferred or ignored (Section 3, Section 8).
- [ ] Not yet done, out of this step's scope: CI/test-report publishing (Step 008), dedicated
      recovery-screen UI distinct from `ActionErrorMessage` (§9/§10 item 4 — flagged as a whole-phase
      gate question, not claimed resolved here), cross-process manifest write-lock (Step 006 §10 item 3,
      unchanged).

**Step 007 status: PASS.** Proceed to Step 008 only after this is reviewed.
