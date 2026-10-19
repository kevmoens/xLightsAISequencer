# PHASE 01 — Step 001 Evidence

**Step:** Fork or vendor upstream source at an immutable commit; document license and downstream notices.
**Spec:** [`docs/phase-specs/PHASE-01.md`](../../docs/phase-specs/PHASE-01.md), Section 6, Step 001.
**Date:** 2026-10-10
**Status: PASS**

> **Addendum (2026-10-19):** at the project owner's explicit request, the `reference/xlights-autosequencer`
> git submodule, `.gitmodules`, and its supporting notice/metadata files (`reference/NOTICE.md`,
> `reference/UPSTREAM-REFERENCE.json`, `reference/UPSTREAM-LICENSE-MIT.txt`) described below were later
> removed from the repository, along with every other documentation link and test fixture naming the
> upstream repository owner. This file's body below is left exactly as originally written — the accurate
> historical record of what Step 001 actually did and verified at the time — rather than rewritten to
> pretend the submodule was never added. It should be read as a historical record, not a description of
> the current repository state; `tests/XlightsAI.Studio.Reference.Tests/Step001AcceptanceTests.cs` and its
> fixtures were updated in the same later follow-up to validate the same generic manifest-validator
> contract against synthetic, non-upstream-identifying fixture data instead.

> Filename note: the spec's own evidence template and the step-by-step table in
> `docs/phase-specs/PHASE-01.md` both use the 3-digit form `STEP-001.md` (matching
> steps 002-008); this file follows that so all eight phase steps stay consistently
> named. The task briefing referenced `STEP-01.md` — treated as shorthand, not a
> deliberate divergent instruction.

## 1. What was built

This step has no UI yet (Step 004 is explicitly chartered to create the Avalonia
shell — "do not cross into future features except narrow stable contracts"). The
vertical slice implemented here is:

- **Adapter boundary:** pinned upstream source vendored as a **git submodule**
  (not a full file copy) at `reference/xlights-autosequencer`, locked to an
  immutable commit SHA (detached HEAD, not `main`).
- **Data:** a machine-readable manifest, `reference/UPSTREAM-REFERENCE.json`,
  recording URL, full commit SHA, commit date, license, attribution notice,
  a component inventory, third-party dependency notes, and known license risks.
  A durable copy of the upstream `LICENSE` text lives at
  `reference/UPSTREAM-LICENSE-MIT.txt` so the license text survives even if the
  submodule is never initialized (e.g. a `git clone` without `--recurse-submodules`).
- **Domain:** `XlightsAI.Studio.Reference` class library —
  `UpstreamReferenceManifest`/`UpstreamComponent`/`UpstreamLicense` (pure data
  contracts) and `UpstreamReferenceValidator` (pure rule: full 40-hex commit SHA,
  non-empty URL/license/attribution, at least one audited component).
- **Application:** `UpstreamReferenceService.AuditAsync` — reads + validates +
  produces `UpstreamReferenceAuditReport`, whose `ToDisplayText()` is the
  "observable public output" a future Settings/Diagnostics screen (Step 005)
  can render verbatim.
- **Tests:** `XlightsAI.Studio.Reference.Tests` (xUnit), including the required
  `Step001_Acceptance_Passes` test plus a negative-path test and adapter
  failure-path tests.

### Architecture decision — submodule, not a full vendored copy

**Decision:** reference the upstream repository by pinned git submodule rather
than copying its ~56 MB working tree into this repository.

**Why:** inspection of the pinned commit (see Section 3) found
`vendor/vamp/linux-x86_64/*.so` — compiled native plugin binaries for
well-known Vamp plugin packs (QM Vamp Plugins, BBC Vamp Plugins, BeatRoot, pYIN,
NNLS Chroma/Chordino, Segmentino, vamp-aubio). Those plugin packs are commonly
distributed under GPL-family licenses by their own maintainers, separate from
and not covered by the MIT license at the root of `xlights-autosequencer`.
Copying them into this repository's own git history would have created an
unreviewed license entanglement before Step 002's module-by-module audit has
even happened. A submodule keeps the reference fully inspectable and
reproducible (pinned by SHA, not by a copy that silently drifts from the
record) while deferring the "do we vendor/port this binary" question to the
step chartered to answer it. Recorded as `knownLicenseRisks` in the manifest
and in `reference/NOTICE.md`.

**Trade-off accepted:** a plain `git clone` of this repository (without
`--recurse-submodules` / `git submodule update --init`) leaves
`reference/xlights-autosequencer/` empty. This is mitigated by keeping the
license text and all manifest facts (URL, SHA, license, attribution,
component inventory) in files that are tracked directly by this repository
and require no submodule checkout to read.

## 2. Commands actually executed

```text
$ git --version
git version 2.46.0.windows.1

$ dotnet --version
10.0.302

$ git clone --no-checkout https://github.com/derwin12/xlights-autosequencer.git tools/_upstream-probe
Cloning into 'tools/_upstream-probe'...
$ git log -1 --format="%H %aI %s"
e3d5b062b0e338f2fbeec47c795386dae33e110e 2026-10-04T13:46:40-04:00 chore: bump version to 0.1.11
$ git symbolic-ref refs/remotes/origin/HEAD
refs/remotes/origin/main

# probe clone removed after recording the pin; replaced with the real submodule:
$ git submodule add https://github.com/derwin12/xlights-autosequencer.git reference/xlights-autosequencer
$ git -C reference/xlights-autosequencer checkout e3d5b062b0e338f2fbeec47c795386dae33e110e --quiet
$ git -C reference/xlights-autosequencer rev-parse HEAD
e3d5b062b0e338f2fbeec47c795386dae33e110e

# Solution / project scaffolding
$ dotnet new sln -n XlightsAI.Studio                      # -> XlightsAI.Studio.slnx (net10 SDK default)
$ dotnet new classlib -n XlightsAI.Studio.Reference -o src/XlightsAI.Studio.Reference -f net10.0
$ dotnet new xunit -n XlightsAI.Studio.Reference.Tests -o tests/XlightsAI.Studio.Reference.Tests -f net10.0
$ dotnet sln XlightsAI.Studio.slnx add src/XlightsAI.Studio.Reference/XlightsAI.Studio.Reference.csproj tests/XlightsAI.Studio.Reference.Tests/XlightsAI.Studio.Reference.Tests.csproj
$ dotnet add tests/XlightsAI.Studio.Reference.Tests/XlightsAI.Studio.Reference.Tests.csproj reference src/XlightsAI.Studio.Reference/XlightsAI.Studio.Reference.csproj

$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=detailed"
Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: 161 ms

# "Clean checkout resolves reference" manual verification
$ git submodule deinit -f reference/xlights-autosequencer
Cleared directory 'reference/xlights-autosequencer'
Submodule 'reference/xlights-autosequencer' ... unregistered for path 'reference/xlights-autosequencer'
$ Test-Path reference\xlights-autosequencer\LICENSE
False
$ git submodule update --init -- reference/xlights-autosequencer
Submodule path 'reference/xlights-autosequencer': checked out 'e3d5b062b0e338f2fbeec47c795386dae33e110e'
$ git -C reference/xlights-autosequencer rev-parse HEAD
e3d5b062b0e338f2fbeec47c795386dae33e110e
$ Test-Path reference\xlights-autosequencer\LICENSE
True
```

All commands were run from PowerShell on this machine; the Bash tool in this
environment failed on every invocation (exit 1, no output, even on `echo hi`) —
Git Bash is not usable here, so PowerShell was used for all shell work.

## 3. Pinned reference facts (recorded in `reference/UPSTREAM-REFERENCE.json`)

| Field | Value |
|---|---|
| Repository URL | `https://github.com/derwin12/xlights-autosequencer.git` |
| Commit SHA (full, immutable) | `e3d5b062b0e338f2fbeec47c795386dae33e110e` |
| Commit message | `chore: bump version to 0.1.11` |
| Commit date (UTC) | `2026-10-04T17:46:40Z` |
| Default branch at pin time | `main` (explicitly NOT what is pinned/used) |
| License | MIT, Copyright (c) 2026 Rob Freitag |
| Working-tree size at pinned commit | 1,492 files, 55.78 MB (`.git` 33.28 MB) |

## 4. Component inventory (summary — full list in the manifest)

`src/analyzer`, `src/grouper`, `src/effects`, `src/variants`, `src/themes`,
`src/story`, `src/generator`, `src/review`, `src/cli`, `src/evaluation`,
`src/microscope`, `src/validation` (unaudited), `src/video` (unaudited),
`src/packaging` (unaudited), `layout`, `vendor/vamp` (license risk — see below),
`design_handoff_xonset`, `packaging/tauri` (upstream's own desktop shell,
informational only). The module-by-module reuse/port/replace/omit decision for
each of these is explicitly Step 002's scope, not this step's.

## 5. Known risk recorded this step

`vendor/vamp/linux-x86_64/*.so` bundles compiled native plugin binaries for QM
Vamp Plugins, BBC Vamp Plugins, BeatRoot, pYIN, NNLS Chroma/Chordino,
Segmentino, and vamp-aubio. These are commonly GPL-family licensed by their own
maintainers and are **not** covered by the MIT license at the root of
`xlights-autosequencer`. No binaries from this directory have been copied into
this repository (submodule reference only). Recorded in
`UPSTREAM-REFERENCE.json.knownLicenseRisks` and `reference/NOTICE.md`; must be
resolved per-plugin before any future phase vendors/redistributes them.

## 6. Fixture and artifact hashes (SHA-256)

| File | SHA-256 |
|---|---|
| `reference/UPSTREAM-REFERENCE.json` | `7B0DAA39101AD97898A6CBBF4EA1C69BB54C125F794DE45605C390E6B4E27374` |
| `reference/UPSTREAM-LICENSE-MIT.txt` | `B32B8DF20A72242C911571033F9AF0C943ED3D4B3B347179053C9AA5E3E586A6` |
| `tests/.../Fixtures/valid-manifest.json` (byte-identical copy of the real manifest) | `7B0DAA39101AD97898A6CBBF4EA1C69BB54C125F794DE45605C390E6B4E27374` |
| `tests/.../Fixtures/invalid-manifest.json` (deliberately broken, negative-path fixture) | `132C6DDD95E5F9A85C78FD667031853641478C64E9BA9678EC21AD4FEB37DC19` |
| `tests/.../Fixtures/malformed.json` (truncated JSON, negative-path fixture) | `94EA28C1B3671611F69ECC2D819489E8A8CF1C55DB432DD226783BCA4939C407` |
| `reference/xlights-autosequencer/LICENSE` (pinned-commit upstream LICENSE, as checked out) | `E062927E49CE08C443E8EEF59B2185DC155404D49DAD374E57C25C35D7E5BB82` |

## 7. Automated test results

```text
dotnet test XlightsAI.Studio.slnx --logger "console;verbosity=detailed"

Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.Validator_AcceptsAFullyPopulatedManifest
Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.Validator_RejectsAnythingOtherThanAFullCommitSha(commitSha: "main")
Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.Validator_RejectsAnythingOtherThanAFullCommitSha(commitSha: "")
Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.Validator_RejectsAnythingOtherThanAFullCommitSha(commitSha: null)
Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.Validator_RejectsAnythingOtherThanAFullCommitSha(commitSha: "e3d5b06")
Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.AuditAsync_MalformedJson_FailsSafelyWithoutThrowing
Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.AuditAsync_MissingManifestFile_FailsSafelyWithoutThrowing
Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.Step001_Acceptance_Passes
Passed XlightsAI.Studio.Reference.Tests.Step001AcceptanceTests.Step001_Acceptance_Passes_RejectsInvalidManifest_NegativePath

Test Run Successful.
Total tests: 9
     Passed: 9
```

- **`Step001_Acceptance_Passes`** — reads the real, deterministic manifest fixture
  (byte-identical to `reference/UPSTREAM-REFERENCE.json`) through
  `UpstreamReferenceService`, and asserts persisted domain state (URL, full
  commit SHA ≠ `main`, SPDX license id, non-empty attribution, non-empty
  audited component list, recorded license risk) **and** the service's public
  output text (`ToDisplayText()` contains the SHA/URL/license) — not just that
  a mock was invoked.
- **Negative-path assertions** (required by the step): an invalid manifest
  (abbreviated SHA, empty license/attribution, zero components) is rejected
  with specific field-level errors and never throws; a missing manifest file
  and a malformed-JSON file both fail safely (`IsPinned == false`, clear error
  text, no unhandled exception) — directly proving the step's "invalid input
  ... must not corrupt the project" edge case. A parameterized validator test
  independently proves the "never rely on main at build time" rule by
  rejecting `"main"`, an abbreviated SHA, empty string, and null.

## 8. Manual / environment verification

- `git submodule deinit` + `git submodule update --init` (Section 2) proves a
  clean checkout deterministically resolves `reference/xlights-autosequencer`
  back to the pinned commit `e3d5b062b0e338f2fbeec47c795386dae33e110e` — this
  is the step's "clean checkout resolves reference" acceptance criterion.
- **BLOCKED (not required by this step, noted for transparency):** a true
  fresh `git clone` of *this* repository was not performed, because this
  repository has no commits yet (`git status` shows "No commits yet") and the
  user's standing instruction is to only commit when explicitly asked. The
  submodule-deinit/reinit check above exercises the same resolution mechanism
  (`.gitmodules` + the staged gitlink SHA) that a fresh clone would use, but a
  true end-to-end "new machine, `git clone`, done" demonstration should be
  repeated once the first commit lands.
- No xLights installation or UI shell exists yet at this step (Step 004 adds
  the Avalonia shell) — there is nothing xLights-observable to demonstrate for
  Step 001, consistent with the phase spec's own step ordering.

## 9. Changed/added paths

```
.gitignore
.gitmodules
XlightsAI.Studio.slnx
reference/xlights-autosequencer/          (git submodule, pinned commit e3d5b062b0e338f2fbeec47c795386dae33e110e)
reference/UPSTREAM-REFERENCE.json
reference/UPSTREAM-LICENSE-MIT.txt
reference/NOTICE.md
src/XlightsAI.Studio.Reference/UpstreamReferenceManifest.cs
src/XlightsAI.Studio.Reference/UpstreamReferenceValidator.cs
src/XlightsAI.Studio.Reference/IUpstreamReferenceManifestReader.cs
src/XlightsAI.Studio.Reference/JsonUpstreamReferenceManifestReader.cs
src/XlightsAI.Studio.Reference/UpstreamReferenceService.cs
src/XlightsAI.Studio.Reference/XlightsAI.Studio.Reference.csproj
tests/XlightsAI.Studio.Reference.Tests/Step001AcceptanceTests.cs
tests/XlightsAI.Studio.Reference.Tests/Fixtures/valid-manifest.json
tests/XlightsAI.Studio.Reference.Tests/Fixtures/invalid-manifest.json
tests/XlightsAI.Studio.Reference.Tests/Fixtures/malformed.json
tests/XlightsAI.Studio.Reference.Tests/XlightsAI.Studio.Reference.Tests.csproj
implementation-evidence/PHASE-01/STEP-001.md
```

No pre-existing show files, effect locks, or timelines exist in this
repository yet, so there was nothing for this step to risk overwriting;
no original xLights show directory was touched.

## 10. Unresolved issues / risks carried forward

1. **GPL-family Vamp plugin binaries** under the pinned reference's `vendor/`
   tree (Section 5) — resolve per-plugin licensing before any phase considers
   porting/vendoring them.
2. **No end-to-end fresh-clone demonstration yet** (Section 8) — repeat once
   this repository has its first commit and is cloned on a second machine/path.
3. Per-dependency SPDX license identifiers for the upstream project's own
   third-party Python/JS dependencies are **not** asserted here — deferred to
   Step 002's module-by-module audit, as that is its explicit scope.
4. Git Bash is not functional in this environment (every Bash tool invocation
   returned exit 1 with no output, even `echo hi`); all commands in this
   evidence were run via PowerShell instead. Flagging in case later steps
   assume a POSIX shell is available.

## 11. Gate checklist (this step only)

- [x] URL, SHA, license and attribution recorded in a machine-readable manifest.
- [x] Clean checkout resolves the reference (manual verification, Section 8).
- [x] Deterministic fixture + automated test asserting persisted domain state
      and public output (`Step001_Acceptance_Passes`).
- [x] Negative-path assertion(s) included and passing.
- [x] `dotnet build` and `dotnet test` executed and recorded (Section 2/7).
- [x] No original/show files touched; nothing to preserve was at risk.
- [ ] Not yet done, out of this step's scope: UI surface (Step 004), SQLite
      persistence (Step 007), CI (Step 008).

**Step 001 status: PASS.** Proceed to Step 002 only after this is reviewed.
