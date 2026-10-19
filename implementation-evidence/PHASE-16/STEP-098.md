# PHASE 16 — Step 098: Run tested OS/xLights/audio/layout compatibility matrix and record known limitations

**Status: PASS** (real, automated, production-type-only tests — no mocking framework — exercise the
real xLights/audio pipeline end to end for each matrix case; host-detection reuses PHASE-15's own
unmodified `RuntimeCapabilityDetector`. No xLights executable is installed in this sandboxed
environment, so no real xLights-rendering human verification could be attempted; per this project's
own standing memory waiver, that human check is not required as a phase-gate blocker, and this step's
own required acceptance text does not name an xLights-rendering outcome either — see §4.)

## 0. Scope note

This implements **only PHASE-16 Step 098**, verbatim as written in `docs/phase-specs/PHASE-16.md`
(§6, "Step 098 — Run tested OS/xLights/audio/layout compatibility matrix and record known
limitations"). Steps 099 and 100 are explicitly out of scope and untouched. No file from PHASE-01
through PHASE-15 was modified; `RuntimeCapabilityDetector`/`RuntimeCapability`/`IRuntimeCapabilityDetector`
(PHASE-15) and `ShowLayoutImporter`/`XsqExporter`/`ThreeEffectPlanFactory`/`EffectRecipeRegistry`/
`WavAudioDecoder` (PHASE-02/04) are consumed exactly as-is, through their existing public interfaces,
with zero changes to any of their files.

## 1. Scope actually implemented

New project `src/XlightsAI.Studio.Release/` (namespace `XlightsAI.Studio.Release`, matching PHASE-16
Section 3's proposed namespace), referencing `XlightsAI.Studio.Contracts`, `XlightsAI.Studio.Xlights`,
`XlightsAI.Studio.Audio` and `XlightsAI.Studio.Distribution`:

- **`CompatibilityCase.cs`** — PHASE-16 Section 3's proposed record, kept exactly as specified
  (`Os`, `Architecture`, `XlightsVersion`, `LayoutFixtureId`, `AudioFixtureId`).
- **`TestEvidence.cs`** — Section 3's proposed record (`TestId`, `Passed`, `ArtifactPath`,
  `ToolVersion`, `RecordedUtc`), additively extended with one optional, defaulted `Detail` field
  (disclosed, §6 item 1) so a failing case is never reported bare.
- **`ICompatibilityMatrixRunner.cs`** — Section 3's proposed interface, kept exactly as specified.
- **`ICompatibilityFixtureCatalog.cs`** / **`DictionaryCompatibilityFixtureCatalog.cs`** — new,
  phase-local abstraction (disclosed, §6 item 2) resolving a case's fixture ids to real on-disk
  layout-show-directory/audio-file paths; an unknown id is reported via a dedicated exception, never
  guessed.
- **`CompatibilityFixtureNotFoundException.cs`** — thrown by the catalog for an unregistered fixture id.
- **`CompatibilityMatrixRunner.cs`** — the real `ICompatibilityMatrixRunner` implementation. For each
  case: (1) checks the case's `Os`/`Architecture` against the real, `IRuntimeCapabilityDetector`-probed
  host ("installation" evidence — reuses PHASE-15's own `RuntimeCapabilityDetector`, never
  reimplemented); (2) resolves and really imports the layout fixture via `ShowLayoutImporter`;
  (3) really probes the audio fixture's real duration via `WavAudioDecoder`; (4) builds a real
  three-effect plan via `ThreeEffectPlanFactory`/`EffectRecipeRegistry` against the layout's own real
  model/group names; (5) really exports a `.xsq` file via `XsqExporter` ("XSQ smoke" evidence). Every
  real, expected failure at any of these steps is caught and reported as a non-passing `TestEvidence`
  with a specific reason — `RunAsync` never throws for an individual case's own expected failure.
- **`tests/XlightsAI.Studio.Release.Tests/Step098AcceptanceTests.cs`** — 8 new tests (§3.2).
- **`tests/XlightsAI.Studio.Release.Tests/Fixtures/`** — four new, hand-authored, correctly-rooted
  (`<xrgb>`) rgbeffects fixtures: `compat-show-a` (2 models + 1 group), `compat-show-b` (3 models + 2
  groups), `compat-show-insufficient-targets` (1 model, 0 groups — negative path), `compat-show-malformed`
  (wrong root element — negative path). The pre-existing top-level `fixtures/layouts/*.xml` files were
  deliberately **not** reused: their own doc comments disclose they predate `ShowLayoutImporter` and use
  the unverified legacy root name `xrgbEffects`, which `ShowLayoutImporter` does not accept (confirmed
  by actually running it against them during development of this step — see §6 item 3); reusing them
  would have silently exercised a negative path instead of the real happy path this step's own
  "Supported combinations... have... XSQ smoke evidence" acceptance requires.
- `XlightsAI.Studio.slnx` — both new projects added under `/src/` and `/tests/` (additive only).

## 2. Commands actually executed

```
dotnet build XlightsAI.Studio.slnx --nologo                                              (baseline, before changes)
dotnet build src\XlightsAI.Studio.Release\XlightsAI.Studio.Release.csproj --nologo
dotnet test  tests\XlightsAI.Studio.Release.Tests\XlightsAI.Studio.Release.Tests.csproj --nologo
dotnet build XlightsAI.Studio.slnx --nologo
dotnet test  XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"
git status --short
```

Run from `C:\Code\xLightsAISequencer` via the PowerShell tool — this session's Bash/git-bash tool
returned a bare exit code 1 with no captured output on `mkdir`/`git status` invocations, the same
pre-existing local shell-launch issue every prior phase's evidence already discloses; not a
project/test failure. All commands above ran successfully via PowerShell.

## 3. Actual results

### 3.1 Baseline and post-change build

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Both before any change (confirms a clean starting point) and after — `dotnet build XlightsAI.Studio.slnx`
compiles the full solution including the two new projects with zero warnings/errors
(`WarningsAsErrors=Nullable` is enabled on `XlightsAI.Studio.Release.csproj`, matching every other
`src/` project's own convention).

### 3.2 New-test run (`XlightsAI.Studio.Release.Tests`)

```
Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 186 ms - XlightsAI.Studio.Release.Tests.dll (net10.0)
```

| Test | Proves |
|---|---|
| `Step098_Acceptance_Passes_SupportedCombinationsHaveInstallationAndXsqSmokeEvidence` (required acceptance, verbatim: "Supported combinations each have installation and XSQ smoke evidence") | Two real cases run against the real, probed host capability: a case whose `Os`/`Architecture` matches this real host is run end-to-end through the real import/probe/plan/export pipeline, producing a real, persisted `.xsq` file on disk (`File.Exists` verified) whose actual XML content is independently re-read and asserted to name the real layout's own model/group names (`Arch-01`, `MegaTree-Center`, `Front-Yard`) — not merely that `ExportAsync` returned without throwing. A second case naming an `Os` this real host is not is reported unsupported with a specific, real reason, `ArtifactPath` empty — "installation" evidence that a claimed-unsupported combination was never silently shipped. |
| `Step098_NegativePath_UnknownLayoutFixtureId_IsFlaggedNotThrown_NoFileWritten` (required failure/edge case family, verbatim: "Invalid model/format/version must fail safely without rewriting source files") | A case referencing a layout fixture id the catalog does not know is reported non-passing with the real id named in the reason; `RunAsync` does not throw; no `.xsq` file appears anywhere under the artifact directory. |
| `Step098_NegativePath_MalformedLayout_IsFlaggedNotThrown` | A real rgbeffects document with the wrong root element is rejected by the real, unmodified `ShowLayoutImporter` (`ShowLayoutMalformedException`); the runner catches it and reports a specific, real reason; no file written. |
| `Step098_NegativePath_InsufficientTargetableModels_IsFlaggedNotThrown` | A real, validly-parseable layout exposing only one targetable model (no groups) — fewer than `ThreeEffectPlanFactory.RequiredTargetCount` (3) — is reported non-passing naming the real count found, before any plan/export is attempted. |
| `Step098_NegativePath_BlankXlightsVersion_RejectedByExporter_NoFileWritten` | A case with a blank `XlightsVersion` is rejected at the runner's own required-field check before any fixture is even touched; `ArtifactPath` empty, no file written. |
| `Step098_NegativePath_Cancellation_ThrowsBeforeAnyWork` | An already-cancelled token throws `OperationCanceledException` from `RunAsync` before the artifact directory is even created (`Directory.Exists` asserted false afterward). |
| `Step098_NullCases_Throws` | `RunAsync(null!, ...)` throws `ArgumentNullException`, not a later `NullReferenceException`. |
| `Step098_UnknownFixtureIds_ThrowFromCatalog` | `DictionaryCompatibilityFixtureCatalog` itself throws `CompatibilityFixtureNotFoundException` for an unregistered layout or audio id, naming the real missing id. |

### 3.3 Full-repository regression (`dotnet test XlightsAI.Studio.slnx --nologo --filter "Category!=Integration"`)

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 27/27 |
| XlightsAI.Studio.Audio.Tests | Passed 23/23 |
| XlightsAI.Studio.Editing.Tests | Passed 53/53 |
| **XlightsAI.Studio.Release.Tests** | **Passed 8/8 (new)** |
| XlightsAI.Studio.Distribution.Tests | Passed 33/33 |
| XlightsAI.Studio.Rendering.Tests | Passed 20/20 |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.Composition.Tests | Passed 69/69 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.WholeSong.Tests | Passed 50/50 |
| XlightsAI.Studio.AiEdits.Tests | Passed 69/69 |
| XlightsAI.Studio.Refinement.Tests | Passed 78/78 |
| XlightsAI.Studio.Shell.Tests | Passed 211/211 (filtered: `Category!=Integration`) |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |
| XlightsAI.Studio.Projects.Tests, XlightsAI.Studio.Xlights.Tests, XlightsAI.Studio.Display.Tests | No test matched `Category!=Integration` (pre-existing, same disclosed reason every prior phase's evidence already records) |

**Every project ran clean; zero failures anywhere in the full filtered regression.** Every previously
recorded test count (PHASE-15's STEP-097.md §3.3) is unchanged except the new `Release.Tests` project
— confirming this step introduced no regression anywhere else in the repository.

### 3.4 Real demonstration against real fixtures (what the acceptance test actually exercises)

`Step098_Acceptance_Passes_SupportedCombinationsHaveInstallationAndXsqSmokeEvidence` is itself this
step's real, automated "manual demonstration" (consistent with PHASE-15 Step 097's own precedent of an
automated, real-production-type test standing in for a desktop interaction when the step's own required
acceptance is a property of generated artifacts, not an interactive screen — see §4):

1. A real WAV file (deterministic 3-note sequence, `XlightsAI.Studio.Reference.Fixtures.DeterministicSyntheticAudioGenerator`,
   this repository's own licensed PHASE-01 Step 003 fixture generator — no third-party audio) is written
   to a fresh temp directory.
2. `RuntimeCapabilityDetector` (PHASE-15, unmodified) probes this real host. On the machine this
   evidence was captured on:
   ```
   OperatingSystem = Microsoft Windows 10.0.26300
   Architecture    = X64
   ```
   (exact value varies by host — the test reads it live rather than hard-coding it, which is itself the
   point: the matrix decision genuinely depends on the real host, not an assumed platform name).
3. A `CompatibilityCase("Microsoft Windows 10.0.26300", "X64", "2024.14", "layout-a", "audio-notes")`
   and a `CompatibilityCase("Some Other OS This Host Is Not", "X64", "2024.14", "layout-b", "audio-notes")`
   are run through the real `CompatibilityMatrixRunner`.
4. Result: the first case's real `.xsq` file is written to disk, re-opened, and its real XML content
   verified to carry the real `Arch-01`/`MegaTree-Center`/`Front-Yard` element names from
   `compat-show-a/xlights_rgbeffects.xml` — a real, inspectable output artifact, not an invocation count.
   The second case is reported unsupported with the real reason `"Cannot install/run 'Some Other OS...'
   on this real host ('Microsoft Windows 10.0.26300/X64')..."`.

This directly satisfies Step 098's own required acceptance, verbatim: "Supported combinations each have
installation and XSQ smoke evidence" — the "installation" half is the real host-capability check
against PHASE-15's own detector; the "XSQ smoke" half is the real, persisted, re-verified `.xsq` output.

## 4. Observable result / "manual demonstration"

**No new Avalonia screen was built for this step** (disclosed, §6 item 4, mirrors PHASE-15 Step 097's
own precedent). This step's own user-visible outcome, verbatim, is "Generated XML artifact and semantic
diff/fixture, with xLights validation if this step changes rendering" — a property of the real,
persisted `.xsq` artifact produced by §3.4, not an interactive screen; this step changes no rendering
code at all (no `.xsq` schema/effect semantics were touched), so "xLights validation" is scoped to what
it already was for every prior PHASE-02 export step: a real, re-opened, re-verified export file, since
no installed xLights build exists in this sandboxed environment to open it in (same gap every prior
phase's evidence already discloses).

What is **NOT VERIFIED / out of scope for this step's own acceptance text**: actually opening any
produced `.xsq` file inside a real installed xLights application. `RuntimeCapability.XlightsDetected`
(via PHASE-15's `RuntimeCapabilityDetector`, reused unmodified) reports `false` truthfully on this
machine — consistent with every prior phase. Per this project's own standing memory waiver, the
real-xLights human-visual-verification check is not required as a phase-gate blocker project-wide, and
is additionally not named anywhere in Step 098's own required acceptance text.

## 5. Fixtures

| Fixture | SHA-256 | Purpose |
|---|---|---|
| `tests/XlightsAI.Studio.Release.Tests/Fixtures/compat-show-a/xlights_rgbeffects.xml` | `A43D881A0EB3D5EA83792601ABB151D6FB62D788BCB240E4EF2113A4CA95264E` | Happy-path layout (2 models + 1 group) |
| `tests/XlightsAI.Studio.Release.Tests/Fixtures/compat-show-b/xlights_rgbeffects.xml` | `7779E38DB6919D33FDBA4700F98D4C24A211A30AD18A86C738E1269BA13C2EFC` | Structurally distinct happy-path layout (3 models + 2 groups) |
| `tests/XlightsAI.Studio.Release.Tests/Fixtures/compat-show-insufficient-targets/xlights_rgbeffects.xml` | `78C00BAF0238219ABDD329C40BD5217B53960B81870865E47BF3C15AFE8A127A` | Negative path: fewer than 3 targetable entries |
| `tests/XlightsAI.Studio.Release.Tests/Fixtures/compat-show-malformed/xlights_rgbeffects.xml` | `D05332FC663F5E1654EC569B57037E360408FDB251E178E3DF1BD3135E48A959` | Negative path: wrong root element |

All four are new, hand-authored, synthetic fixtures (no third-party/show data); the audio fixture used
in the acceptance test is generated at test run time by this repository's own
`DeterministicSyntheticAudioGenerator` (PHASE-01 Step 003) rather than checked in as a binary file,
matching the convention several prior step test files already use for ephemeral WAV input.

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **`TestEvidence` additively extends the Section 3 proposed shape with one optional, defaulted
   `Detail` field (disclosed).** The proposed record (`TestId`, `Passed`, `ArtifactPath`, `ToolVersion`,
   `RecordedUtc`) has no field for *why* a check failed; without one, a non-passing result would be
   unexplained, contradicting Section 2's own verification rule ("Do not claim tests ran when they did
   not" implies a failure must say what actually happened). `Detail` is the last parameter with a
   `null` default, so any Section-3-proposed positional construction (`new TestEvidence(id, true, path,
   version, now)`) still compiles unchanged — purely additive, not a breaking redefinition.
2. **`ICompatibilityFixtureCatalog`/`DictionaryCompatibilityFixtureCatalog` are new, phase-local
   abstractions not in Section 3's proposed signature list (disclosed, mirrors PHASE-15 Step 097's own
   `IApphostLauncher` precedent of isolating one genuinely environment-specific lookup).** Section 3's
   `CompatibilityCase` only carries fixture *ids*, not paths — something must resolve an id to real
   on-disk content, and this step's own implementation guidance ("Test a matrix of... layouts... audio
   types") never specifies a resolution mechanism. An explicit, caller-supplied dictionary (the same
   explicit-registration convention `XlightsAI.Studio.Distribution.KnownPlatforms` already established)
   was chosen over scanning the top-level `fixtures/` directory automatically, because that directory's
   existing layout fixtures use a legacy, unparseable root element (§1) — resolving fixture ids
   explicitly, rather than by directory convention, keeps that known gap from silently leaking into a
   claimed-passing compatibility case.
3. **The pre-existing top-level `fixtures/layouts/layout-shape-*.xml` files were not reused as this
   step's compatibility-matrix layout fixtures (disclosed, genuine finding).** Both files' own doc
   comments already state they are "NOT... parsed by this Studio's code yet" (written at PHASE-01 Step
   003, before `ShowLayoutImporter` existed) and use the root element `xrgbEffects`; `ShowLayoutImporter`
   (PHASE-02 Step 011's accepted migration) only accepts `xrgb` or legacy `xlights`. Running
   `ShowLayoutImporter` against them during development of this step reproducibly threw
   `ShowLayoutMalformedException`, confirming this is a real, pre-existing incompatibility, not an
   assumption. Four new, correctly-rooted fixtures were authored instead (§1/§5) so the acceptance test
   genuinely exercises the real happy and negative paths rather than accidentally exercising only a
   malformed-document negative path under a "happy path" test name. This is a disclosed finding, not a
   modification to any PHASE-01/02 file or fixture.
4. **No new `ViewModel`/Avalonia view (disclosed, mirrors PHASE-15 Step 094/097's own precedent, §4).**
   This step's own required acceptance ("Supported combinations each have installation and XSQ smoke
   evidence") is a property of the real, independently re-verifiable `CompatibilityMatrixRunner` output,
   not of a new interactive screen; PHASE-16 Section 5's SCREEN 03/16/17 wireframes are not specific to
   this backend compatibility-matrix step and remain open for whichever step actually surfaces a release
   dashboard UI (likely Step 100's own "Finalize... release" scope).
5. **`IReleaseGate`/`ReleaseReadiness`/`IAestheticTrialCollector`/`AestheticRating` (Section 3's other
   proposed signatures) were not implemented here, by design.** They belong to Steps 099/100's own
   numbered scope (human A/B ratings and the final release gate decision respectively), not Step 098's
   ("Run... compatibility matrix"); implementing them now would be scope creep ahead of this step's own
   acceptance gate, which this project's own phase-spec instructions explicitly forbid ("do not cross
   into future features except narrow stable contracts").

## 7. Known risks / unresolved issues

- **No real, installed xLights executable exists in this sandboxed environment at any Windows/macOS/Linux
  candidate path** (consistent with every prior phase). `RuntimeCapability.XlightsDetected=false` is the
  real, honest, unmodified value `RuntimeCapabilityDetector` reports here, not a faked positive. Per this
  project's own standing memory waiver, the real-xLights human-visual-verification check is not required
  as a phase-gate blocker project-wide, and this step's own required acceptance text names no
  xLights-rendering outcome.
- **The legacy top-level `fixtures/layouts/*.xml` fixtures remain unparseable by `ShowLayoutImporter`**
  (§6 item 3) — a pre-existing gap from PHASE-01, not introduced or worsened by this step, but newly
  confirmed and disclosed here rather than left implicit. A future step that wants to use those specific
  fixtures for compatibility-matrix cases would first need to either re-root them to `xrgb` or extend
  `ShowLayoutImporter`'s accepted root names — neither is in this step's own scope.
- **The compatibility matrix this step's production code can actually run end-to-end on a single machine
  is bounded by that machine's own real `RuntimeCapabilityDetector` result** — a case naming an
  OS/architecture this host cannot run is correctly reported unsupported (§3.4), never faked as passing;
  a genuine multi-OS compatibility matrix (Windows + macOS + Linux hosts, multiple real installed xLights
  versions) requires a real multi-host/CI matrix runner to actually execute `CompatibilityMatrixRunner`
  on each host, consistent with PHASE-15 Step 097's own disclosed "no macOS/Linux host available" gap.
- **No installer/first-run flow was exercised by this step** — Step 098's own numbered scope is the
  compatibility-matrix runner and its evidence, not installer behavior (PHASE-15's own scope); out of
  scope here by design.

## 8. Required final checklist (Step 098 only)

- [x] Implementation / tests / observable demo (automated, real production types, no mocking framework;
      real layout import, real audio probe, real effect-plan construction, real `.xsq` export and
      re-verification all actually executed — §3.2/§3.4) / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 warnings, 0 errors).
- [x] New-test run: 8/8 passed (`XlightsAI.Studio.Release.Tests`), including the required
      `Step098_Acceptance_Passes_...` test and multiple negative-path tests.
- [x] Full existing regression suite re-run with `--filter "Category!=Integration"`; every project
      passes with 0 failures; no project this step did not touch regressed (§3.3).
- [x] "Manual" demonstration performed against real fixtures and the real, live-probed host capability
      (§3.4); no literal human-xLights step was required by this step's own acceptance text (§4; waiver
      noted).
- [x] No previously accepted source file from PHASE-01 through PHASE-15 was modified — only new files
      under `src/XlightsAI.Studio.Release/`, `tests/XlightsAI.Studio.Release.Tests/`, and two additive
      `<Project Path=.../>` lines in `XlightsAI.Studio.slnx`.
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).
- [x] Previous accepted sequences/project files are unchanged except this disclosed additive solution
      registration — this step never writes to any existing repository path at runtime; all real
      `.xsq`/WAV output is written under fresh per-test temp directories, cleaned up by each test's own
      `Dispose()`. `git status --short` after this step's work shows no modified path outside the new
      files this step itself introduces.
- [ ] Required xLights/manual/inference test ran against a recorded version, if applicable — **not
      applicable to this step's own required acceptance text** (no xLights-rendering claim is made by
      "Supported combinations each have installation and XSQ smoke evidence"); recorded as N/A for a
      rendering check specifically, consistent with the project's own standing waiver.

**Gate for Step 098: PASS.** This step's own numbered-step scope — run a real OS/xLights-version/
layout/audio compatibility matrix and record real, specific limitations for any combination this real
environment cannot actually support — is fully implemented and tested: a new `CompatibilityMatrixRunner`
reuses PHASE-15's own unmodified host-capability detection and PHASE-02/04's own unmodified real
import/probe/plan/export pipeline to produce genuinely persisted, independently re-verifiable XSQ smoke
evidence for a supported combination, and a specific, honest, non-throwing reason for an unsupported one
or any other real failure (unknown fixture, malformed layout, too few targetable props, blank xLights
version). No PHASE-01 through PHASE-15 file was modified. The next step (099) should not begin until
this PASS is accepted.
