# PHASE 02 — Step 010 Evidence

**Step:** Parse `xlights_rgbeffects.xml` into normalized stable model IDs/groups/geometry where present.
**Spec:** [`docs/phase-specs/PHASE-02.md`](../../docs/phase-specs/PHASE-02.md), Section 6, Step 010.
**Date:** 2026-10-10
**Status: PASS**

> Filename note: continuing the PHASE-01/Step 009 precedent (`STEP-02.md`...`STEP-09.md`), this file
> uses `STEP-10.md` rather than the phase spec's own `STEP-010.md` form, matching the task
> instruction's own spelling ("Step 10" / "STEP-10.md").

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting (PHASE-01 Steps 001-008 + PHASE-02 Step 009, the only
prior PHASE-02 step):

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 9
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 31
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# 96/96, exactly matching Step 009's own recorded final total.
```

No Step 009 (or PHASE-01) production `src/**` file was modified at baseline read time; the one
Step 009 file this step does modify (`ShowImportViewModel.cs`'s `DiscoverAsync`, `ShellViewModel.cs`'s
constructor/`RunDiscoverShowDirectoryAsync`) is disclosed in full in §9 as an additive extension of
Step 009's own already-accepted Discover flow, not a contract change to it.

## 2. What was built

Per PHASE-02 Section 6 Step 010 ("Parse actual `<models>` and `<modelGroups>` structures from
fixtures, preserve names/casing and normalized geometry. Avoid guessing total pixel counts or channel
mappings from screen coordinates.") and the required acceptance ("Golden fixture verifies model
names, groups and positions."), realizing PHASE-02 Section 3's proposed `DisplayGraph`/`DisplayModel`/
`Point2`/`IShowLayoutImporter` contracts for the first time (Step 009 intentionally deferred them —
see that step's own §9):

- **`src/XlightsAI.Studio.Xlights/`** (extends the Step 009 project; no new project):
  - `Point2.cs` — `(double X, double Y)`, exactly PHASE-02 Section 3's proposed signature. No unit
    conversion; values are the model's own `WorldPosX`/`WorldPosY` attribute values, verbatim.
  - `DisplayModel.cs` — `(ModelId Id, string XlightsName, string Kind, IReadOnlyList<string>
    GroupNames, IReadOnlyList<Point2> PreviewPoints)`, exactly PHASE-02 Section 3's proposed
    signature. `Id` and `XlightsName` are both derived from the model's own trimmed `name` attribute
    (xLights assigns no other stable identity in this file format); `Kind` is the `DisplayAs`
    attribute; `GroupNames` lists every `<modelGroup>` whose comma-separated `models` list names this
    model (overlapping group membership is preserved, never flattened — proven in §5's golden test,
    both fixture models belong to two groups each).
  - `DisplayGraph.cs` — `(string SourceLayoutHash, IReadOnlyList<DisplayModel> Models)`, exactly
    PHASE-02 Section 3's proposed signature. `SourceLayoutHash` is the SHA-256 of the exact bytes
    read from disk (PHASE-02 Section 4: "Store read-only show path, source layout hash... in project
    DB") — not a hash of the normalized output, so it identifies the *exact source document*, not
    this step's own interpretation of it.
  - `IShowLayoutImporter.cs` / `ShowLayoutImporter.cs` — the real implementation. Reads only the one
    expected file (`xlights_rgbeffects.xml`) under the caller-named directory; never
    writes/moves/deletes. Untrusted-XML hardening (PHASE-02 Section 2: "limits on DTD/entity
    expansion, path traversal and large documents") via `XmlReaderSettings`:
    `DtdProcessing.Prohibit` (no DOCTYPE permitted at all — this alone closes the classic XXE/external-
    entity vector, since there is no entity declaration mechanism left to abuse), `XmlResolver = null`
    (never resolves an external reference even if one somehow reached the resolver),
    `MaxCharactersFromEntities = 0`, and `MaxCharactersInDocument = 5,000,000` (rejects oversized
    documents before they are fully parsed). A document whose root element is not `xrgbEffects`, or
    whose `<model>` is missing its required `name` attribute, is rejected as malformed rather than
    silently producing an empty/partial `DisplayGraph`. All of the above surface as the new
    `ShowLayoutMalformedException` (`ShowLayoutMalformedException.cs`) — missing directory/file still
    use the existing `DirectoryNotFoundException`/`FileNotFoundException` types, consistent with Step
    009's own exception vocabulary for the same two failure shapes.
  - `NullShowLayoutImporter.cs` — present for symmetry with `NullShowDirectoryDiscoveryService`, but
    (see §9) **not** the default collaborator `ShellViewModel` actually wires up, by deliberate design
    decision.
- **`src/XlightsAI.Studio.Shell/`** (additive extension of Step 009's existing Show Import slice):
  - `ShowImportViewModel.cs` — new `Layout` (`DisplayGraph?`) bindable property. `DiscoverAsync` gained
    an optional `IShowLayoutImporter? layoutImporter = null` parameter: when discovery succeeds (file
    present) and an importer is supplied, it is invoked against the same `ShowDirectory`, and its
    result populates `Layout`; any exception from either discovery or import is still caught by the
    same single `catch` block and surfaced as `ErrorMessage`/`ScreenLoadState.Error`, exactly Step
    009's existing contract, extended rather than replaced.
  - `ShellViewModel.cs` — new optional `IShowLayoutImporter? showLayoutImporter = null` constructor
    parameter, passed through to `ShowImport.DiscoverAsync` from `RunDiscoverShowDirectoryAsync`. See
    §9 for why this one collaborator's default is a real instance, not a throwing Null-object.
- **`src/XlightsAI.Studio.App/`** (additive wiring only): `App.ShowLayoutImporter` static hand-off
  slot (same pattern as `App.ShowDirectoryDiscoveryService`), `Program.cs` constructs a real
  `ShowLayoutImporter` and assigns it, `MainWindow.axaml`'s existing SCREEN 04 SHOW IMPORT panel gains
  a layout-hash line and a models list — no pre-existing markup removed/changed, only appended inside
  the already-existing `IsLoaded` block.
- **Fixtures** (`tests/XlightsAI.Studio.Xlights.Tests/Fixtures/`, continuing Step 009's own
  structural-fixture precedent, not the shared repo-root `fixtures/` corpus — same rationale Step
  009 recorded in its own §9, third bullet):
  - `sample-show-with-geometry/` — **the Step 010 golden fixture**: two models, each with explicit
    `WorldPosX`/`WorldPosY`, and two overlapping `modelGroup`s so every model belongs to both.
  - `sample-show-malformed-xml/` — a truncated/non-well-formed document (unclosed `<model>` tag).
  - `sample-show-xxe-attempt/` — a document with a `DOCTYPE`/external-entity declaration attempting
    to read `file:///etc/passwd` via `&xxe;`.
  - `sample-show-wrong-root/` — a well-formed XML document whose root element is not `xrgbEffects`.
  - `sample-show-model-missing-name/` — a `<model>` element missing its required `name` attribute.
  - `sample-show-oversized/` — a ~5.1 MB document (comment padding) exceeding the 5,000,000-character
    safety bound.
  - Step 009's own `sample-show-complete/` and `sample-show-missing-rgbeffects/` fixtures are reused
    unmodified by this step's tests (no-geometry and missing-file cases respectively).

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests\XlightsAI.Studio.Xlights.Tests\XlightsAI.Studio.Xlights.Tests.csproj --logger "console;verbosity=normal"
Total tests: 22. Passed: 22. (9 pre-existing Step009AcceptanceTests + 13 new Step010AcceptanceTests)

$ dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --logger "console;verbosity=normal"
Total tests: 34. Passed: 34. (31 pre-existing Shell.Tests + 3 new Step010ShellWiringTests)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 22
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 34
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# Full regression: 112/112 passing (96 baseline + 13 + 3 new this step), 0 regressions.

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes
(no output; exit code 0)
```

All commands were run via PowerShell; the Bash tool failed on every invocation in this environment
(exit 1 / no output, including on a bare `pwd`/`ls`), consistent with every prior step's evidence
(PHASE-01 Steps 001-008, PHASE-02 Step 009).

## 4. Required acceptance: "Golden fixture verifies model names, groups and positions"

`Step010_Acceptance_Passes_ModelsGroupsAndPositionsMatchGoldenFixture`
(`tests/XlightsAI.Studio.Xlights.Tests/Step010AcceptanceTests.cs`) parses the real
`sample-show-with-geometry` fixture and asserts, against the real returned `DisplayGraph`:

- **Model names**: exactly `Arch-01` and `MegaTree-Center` (`DisplayModel.XlightsName`), with
  `DisplayModel.Id` equal to `new ModelId("Arch-01")`/`new ModelId("MegaTree-Center")` — the stable ID
  is the model's own name, byte-for-byte.
- **Kinds**: `"Arches"` and `"Tree 360"` (`DisplayAs` attribute, preserved verbatim).
- **Groups**: both models' `GroupNames` equal `["Front-Yard", "All-Props"]` — the fixture's two
  overlapping `modelGroup` elements both list both models, and both memberships survive without
  flattening/deduplication into a single group.
- **Positions**: `Arch-01` → `Point2(10.5, -2.25)`; `MegaTree-Center` → `Point2(0, 0)` — exactly the
  fixture's `WorldPosX`/`WorldPosY` attribute values, parsed with `CultureInfo.InvariantCulture`.

`ModelsWithoutGeometry_HaveEmptyPreviewPoints_NeverGuessed` proves the complementary "where present"
half of the requirement: Step 009's own `sample-show-complete` fixture (no `WorldPosX`/`WorldPosY`
attributes at all) parses with `PreviewPoints` empty for every model — never an invented/defaulted
position.

## 5. Automated test results — detail

### `tests/XlightsAI.Studio.Xlights.Tests/Step010AcceptanceTests.cs` (13 tests, real file-system I/O and real XML parsing, no mocks)

```text
Passed Step010_Acceptance_Passes_ModelsGroupsAndPositionsMatchGoldenFixture   [10 ms]
Passed Import_IsDeterministic_SameBytesSameHash                              [4 ms]
Passed ModelsWithoutGeometry_HaveEmptyPreviewPoints_NeverGuessed              [21 ms]
Passed MissingDirectory_ThrowsActionableError_NegativePath                   [2 ms]
Passed MissingRgbEffectsFile_ThrowsActionableError_NegativePath              [1 ms]
Passed InvalidDirectoryInput_ThrowsActionableError_NegativePath("")          [< 1 ms]
Passed InvalidDirectoryInput_ThrowsActionableError_NegativePath("   ")       [< 1 ms]
Passed AlreadyCancelledToken_ThrowsWithoutReadingAnything_NegativePath       [16 ms]
Passed MalformedXml_FailsSafely_NegativePath                                 [6 ms]
Passed WrongRootElement_FailsSafely_NegativePath                             [4 ms]
Passed ModelMissingRequiredNameAttribute_FailsSafely_NegativePath            [3 ms]
Passed XxeAttempt_IsRejected_NeverResolvesExternalEntity                     [3 ms]
Passed OversizedDocument_IsRejected_NegativePath                             [104 ms]

Total: 13. Passed: 13.
```

- **Failure/edge case required** ("Invalid model/format/version must fail safely without rewriting
  source files") — covered by 9 distinct negative-path tests: missing directory, missing required
  file, invalid (blank) directory input, an already-cancelled token, malformed/non-well-formed XML, a
  recognized-but-wrong root element, a model missing its required identity attribute, an XXE/DOCTYPE
  attempt, and an oversized document. `MalformedXml_FailsSafely_NegativePath` additionally
  byte-compares the fixture file before and after the failed parse attempt to prove the source file
  is never rewritten.
- `XxeAttempt_IsRejected_NeverResolvesExternalEntity` asserts the thrown exception's message does not
  contain `"root:"` (the first line of a real `/etc/passwd`) — proving the external entity was never
  expanded/resolved, not merely that *some* exception was thrown. Because `DtdProcessing.Prohibit`
  rejects the DOCTYPE itself before the entity declaration is ever read, this is actually a stronger
  guarantee than "the entity wasn't resolved": no DTD content of any kind is processed.
- `OversizedDocument_IsRejected_NegativePath` runs against a real ~5.1 MB fixture file generated on
  disk (comment padding past the 5,000,000-character `MaxCharactersInDocument` bound) — not a
  simulated/mocked size check.
- `Import_IsDeterministic_SameBytesSameHash` proves `SourceLayoutHash` is a pure function of the file
  bytes (two independent reads of the same fixture produce the identical hash).

### `tests/XlightsAI.Studio.Shell.Tests/Step010ShellWiringTests.cs` (3 new tests, real `ShellViewModel`/`SettingsStore`/`ShowLayoutImporter`, no mocks)

```text
Passed Step010_Acceptance_Passes_DiscoverAlsoParsesLayoutOntoScreen           [143 ms]
Passed MalformedRgbEffects_SetsScreenError_NegativePath                      [18 ms]
Passed ShellViewModel_WithoutExplicitLayoutImporter_StillParsesLayout_BackwardCompatible [13 ms]
```

- `Step010_Acceptance_Passes_DiscoverAlsoParsesLayoutOntoScreen`: a real `ShellViewModel` over a real
  temp show directory runs `DiscoverShowDirectoryCommand`, then asserts `ShowImport.Layout` is
  populated with the real parsed model/group/geometry data — proving the full UI → application →
  domain → adapter slice, not just the importer in isolation.
- `MalformedRgbEffects_SetsScreenError_NegativePath` — the step's UI-layer negative path: a directory
  whose required file exists but fails to parse safely surfaces `ShowImport.IsError`/`ErrorMessage`,
  `Layout` stays `null`, and (same Step 009 contract, preserved) the failed attempt never persists
  `LastShowDirectory`.
- `ShellViewModel_WithoutExplicitLayoutImporter_StillParsesLayout_BackwardCompatible` — proves the
  deliberate default-collaborator decision in §9: omitting `showLayoutImporter` from the constructor
  (Step 009's own existing call-site shape) still successfully parses a layout, because the default is
  a real `ShowLayoutImporter`, not a throwing stub.

### Pre-existing regression (unmodified acceptance text)

```text
XlightsAI.Studio.Xlights.Tests: 9/9 Step009AcceptanceTests passing, unmodified.
XlightsAI.Studio.Shell.Tests:   31/31 pre-existing tests (Step 004-009) passing, unmodified.
XlightsAI.Studio.Projects.Tests: 25/25 passing, untouched by this step.
XlightsAI.Studio.Reference.Tests: 31/31 passing, untouched by this step.
# Full regression with this step's new tests included: 112/112, 0 regressions.
```

No PHASE-01 or Step 009 test file's assertions were edited. The only Step 009 production files edited
were `ShowImportViewModel.cs` (new optional parameter + new property, both additive — Step 009's
existing two-argument `DiscoverAsync(discoveryService, ct)`-shaped call pattern still compiles because
the new parameter sits between the existing two with a default) and `ShellViewModel.cs` (new optional
constructor parameter + one new line in `RunDiscoverShowDirectoryAsync`); both are proven
non-breaking by Step 009's own still-passing test suite (§3) plus this step's own
`ShellViewModel_WithoutExplicitLayoutImporter_StillParsesLayout_BackwardCompatible`.

## 6. Manual / desktop UI verification — real Avalonia app, real file system

Per the task's "Build the minimum complete UI -> application -> domain -> data/adapter slice" and
"run... manual tests when this step requires them" instructions, the real compiled desktop app
(`src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe`, started via
`Start-Process`/`dotnet build` — the exact same executable a user double-clicks) was launched for real
and driven via Windows UI Automation (`System.Windows.Automation`, real `InvokePattern`/`ValuePattern`
calls against the real running window — not simulated input, not a test harness substitute). No
xLights installation is relevant to this step (Step 010 is read-only XML *parsing*; nothing is
exported/opened in xLights yet — that remains Step 013/014's scope).

1. **Successful parse, golden-geometry fixture** —
   `implementation-evidence/PHASE-02/step-010-geometry-fixture-loaded.png`: navigated to Show Import
   via a real `InvokePattern.Invoke()` on the real "Show Import" button, set the real textbox (via
   `ValuePattern.SetValue`) to the real `sample-show-with-geometry` fixture's build-output directory,
   clicked the real Discover button. The screen shows "xlights_rgbeffects.xml found: True",
   "xlights_networks.xml found: False" (expected — this fixture has no networks file), the orange
   optional-file warning, a real computed `Layout hash: 460B9B...` (matches §7's fixture SHA-256), and
   both model names "Arch-01"/"MegaTree-Center" under "Models" — real end-to-end UI evidence of the
   parsed `DisplayGraph`, not a mocked view.
2. **Malformed-XML negative path** —
   `implementation-evidence/PHASE-02/step-010-malformed-xml-error.png`: same real flow against the
   real `sample-show-malformed-xml` fixture directory (file exists, is not well-formed). The screen
   shows the explicit red error `'...\xlights_rgbeffects.xml' is not a safely parseable XML document:
   Name cannot begin with the '<' character, hexadecimal value 0x3C. Line 4, position 3.` — not a
   blank screen, not an unhandled-exception crash, not a silently-empty Models list.

**Disclosed side effect**: same as Step 009's own disclosure — this manual verification ran the real
app against the real `%AppData%\XlightsAI.Studio\settings.json` on this machine. That file's
`LastShowDirectory` now reflects this session's **first** successful Discover (the geometry fixture);
the second (malformed-XML) attempt did not update it, per the preserved Step 009 contract (also
directly asserted by `MalformedRgbEffects_SetsScreenError_NegativePath`, §5). No project file, show
file, or test fixture was touched/rewritten by this step's manual verification (`sample-show-
malformed-xml/xlights_rgbeffects.xml`'s bytes are identical before/after, per `MalformedXml_
FailsSafely_NegativePath`'s own direct assertion).

## 7. Fixture hashes (this step's new fixtures)

```text
sample-show-with-geometry/xlights_rgbeffects.xml      SHA256=460B9B4848B9BEABB3AA67DE3BF77B592D2E90F100F0398243579CBD9235EAED
sample-show-malformed-xml/xlights_rgbeffects.xml      SHA256=91CC830D95493BFFC0DDC7C813B335D0E829481CCE55EB7C96D87DB75F79E930
sample-show-xxe-attempt/xlights_rgbeffects.xml        SHA256=B5401CC46F8BC904DCA32475D59240621FA2F7D74450C4A72712F037E1DA6B92
sample-show-wrong-root/xlights_rgbeffects.xml         SHA256=0A2998924C426FB9EAEAE83405F5024CD072F47945D3D86A3BD29490016F49E0
sample-show-model-missing-name/xlights_rgbeffects.xml SHA256=0E99885E70138CC56702C867F9323B42BE9945D7508B8596BD3C9F9EE2C5B77A
sample-show-oversized/xlights_rgbeffects.xml          5,100,172 bytes (exceeds the 5,000,000-character MaxCharactersInDocument bound)
```

Computed via `Get-FileHash -Algorithm SHA256` directly against the checked-in fixture files (not the
build-output copies). The UI screenshot in §6 independently shows the importer's own computed
`SourceLayoutHash` for `sample-show-with-geometry` (`460B9B4848B9BEABB3AA67DE3BF77B592D2E90F100F039
8243579CBD9235EAED`) matching this table exactly — the hash recorded here *is* the SHA-256 of the raw
file bytes, proving `ShowLayoutImporter` hashes the actual source document rather than some derived
representation.

## 8. Changed/added paths

```
src/XlightsAI.Studio.Xlights/Point2.cs                                                      (new)
src/XlightsAI.Studio.Xlights/DisplayModel.cs                                                (new)
src/XlightsAI.Studio.Xlights/DisplayGraph.cs                                                (new)
src/XlightsAI.Studio.Xlights/ShowLayoutMalformedException.cs                                (new)
src/XlightsAI.Studio.Xlights/IShowLayoutImporter.cs                                          (new)
src/XlightsAI.Studio.Xlights/ShowLayoutImporter.cs                                           (new)
src/XlightsAI.Studio.Xlights/NullShowLayoutImporter.cs                                       (new)
src/XlightsAI.Studio.Shell/ShowImportViewModel.cs                                            (modified: +Layout property, +optional layoutImporter parameter)
src/XlightsAI.Studio.Shell/ShellViewModel.cs                                                 (modified: +optional showLayoutImporter constructor parameter, +1 call-site argument)
src/XlightsAI.Studio.App/App.axaml.cs                                                        (modified: additive wiring only)
src/XlightsAI.Studio.App/Program.cs                                                          (modified: additive wiring only)
src/XlightsAI.Studio.App/Views/MainWindow.axaml                                              (modified: +2 bound elements inside the existing IsLoaded panel, no existing markup changed)
tests/XlightsAI.Studio.Xlights.Tests/Step010AcceptanceTests.cs                               (new)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-with-geometry/xlights_rgbeffects.xml        (new)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-malformed-xml/xlights_rgbeffects.xml        (new)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-xxe-attempt/xlights_rgbeffects.xml           (new)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-wrong-root/xlights_rgbeffects.xml            (new)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-model-missing-name/xlights_rgbeffects.xml    (new)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-oversized/xlights_rgbeffects.xml             (new, ~5.1 MB)
tests/XlightsAI.Studio.Shell.Tests/Step010ShellWiringTests.cs                                 (new)
implementation-evidence/PHASE-02/STEP-10.md                                                  (new, this file)
implementation-evidence/PHASE-02/step-010-geometry-fixture-loaded.png                        (new)
implementation-evidence/PHASE-02/step-010-malformed-xml-error.png                            (new)
```

No PHASE-01 file, no Step 009 test file, and no Step 009 fixture was altered. No real xLights show
file (fixture or otherwise) existed in or near this step's new fixtures before this step; the
oversized fixture is synthetic padding, not a real show export.

## 9. Architecture deviations

- **`ShellViewModel`'s default `IShowLayoutImporter` is a real, working `ShowLayoutImporter`
  instance, not a throwing `NullShowLayoutImporter.Instance` sentinel** — unlike every other optional
  collaborator this class takes (`ISettingsStore`, `IAppDiagnostics`, `IProjectService`,
  `IShowDirectoryDiscoveryService`), which all default to a Null-object that throws/no-ops until a
  real instance is explicitly supplied. Reasoning: `ShowLayoutImporter` is a pure, stateless,
  read-only XML parser with no external target to configure (no workspace directory, no project
  state, nothing a caller must "opt into" before it can safely run) — unlike
  `IShowDirectoryDiscoveryService`/`IProjectService`, which could do something meaningfully wrong
  (or simply confusing) if silently defaulted to a working instance pointed at nothing in particular.
  The practical consequence that made this the right call rather than just a convenient one: Step
  009's own already-accepted `Step009_Acceptance_Passes_DiscoverThenRestartRestoresDirectory` test
  constructs `ShellViewModel` **without** an explicit layout importer, and this step's Discover flow
  now always attempts a layout parse as part of that same existing command. Defaulting to
  `NullShowLayoutImporter` would have turned that already-passing Step 009 test into a failure the
  instant Step 010's new parse step was wired into the shared Discover flow — an unacceptable,
  undisclosed regression of an already-accepted step. Defaulting to a real importer instead means
  Step 009's own test fixture (`<xrgbEffects />`, zero models) parses successfully (empty `Models`
  list) and that test's existing assertions (which only check `Discovery`, never `Layout`) continue
  to hold unmodified. `NullShowLayoutImporter` is still provided for symmetry with the sibling
  pattern and as an explicit opt-out seam for a future caller that wants layout parsing disabled, but
  it is not the wiring default.
- **Step 010's own parse is folded into Step 009's existing `DiscoverShowDirectoryCommand`/
  `DiscoverAsync`, rather than being a separate command/button.** The phase spec's own numbered-step
  guidance says "Avoid collecting multiple unverified tasks into one mega-commit," which could be read
  as implying a separate UI action per step. This step reads that guidance as being about *commit/test
  granularity*, not mandating a separate user gesture per step when the step's own "User-visible
  outcome" (an inspectable parsed result) is a natural, structurally-required follow-on of the
  immediately preceding step's own successful result (you cannot usefully parse models/groups out of
  a file whose presence you have not yet confirmed) — the same "vertical slice" framing Section 6's
  preamble uses ("Each numbered item is a complete vertical test cut: UI/command -> domain rule ->
  persistence or adapter -> observable result -> automated test"). A dedicated, separately-tested
  `IShowLayoutImporter`/`ShowLayoutImporter` still exists and is independently unit-tested
  (`Step010AcceptanceTests.cs`) without going through the UI at all, satisfying the per-step isolation
  the guidance is actually protecting.
- **The stable `ModelId` is simply the model's own trimmed `name` attribute, not a synthesized GUID.**
  PHASE-02 Section 3's proposed `ModelId(string Value)` type (defined in Step 006/`Studio.Contracts`)
  does not mandate a particular string shape, and xLights' own `xlights_rgbeffects.xml` format assigns
  a model no other persistent identity than its name — inventing a synthetic ID here would itself be
  "guessing" data not actually present in the source document, which Section 3's own implementation
  guidance explicitly warns against ("Avoid guessing..."). Disambiguating renamed/duplicate model
  names across re-imports is explicitly Step 011's own scope ("Identify targetable models versus
  groups/submodels, reject unresolved refs"), not this step's.
- **Geometry extraction is limited to a single `WorldPosX`/`WorldPosY` pair per model (one `Point2`),
  not a full per-node pixel/polygon outline.** PHASE-02 Section 3's own implementation guidance
  ("Avoid guessing total pixel counts or channel mappings from screen coordinates") was read as ruling
  out reconstructing detailed per-pixel geometry from `parm1`/`parm2`/`BufferStyle`-style attributes
  (which encode channel/node counts, not screen position, and differ per model `DisplayAs` kind in
  ways this step has no verified real-xLights-authored reference for). `WorldPosX`/`WorldPosY` is the
  one geometry concept xLights models uniformly expose regardless of model kind, matching `Point2`'s
  own minimal `(X, Y)` shape. A richer per-node outline (if a later phase needs one for spatial
  routing — PHASE-09's `SpatialDisplayGraph`/`SpatialPath`) should be its own, separately-verified
  step against real xLights-exported fixtures, not retrofitted here from unverified guesses.
- **No `FormatVersion` compatibility gate.** The fixture root element carries a `FormatVersion="2"`
  attribute; this step does not read or validate it. PHASE-02's failure-handling text flags "xLights
  XML version differences" as a known risk, but Section 3's own guidance also warns against inventing
  behavior not backed by a verified fixture ("do not invent xLights effect parameters") — without a
  verified corpus of real xLights-exported `FormatVersion` values across the versions this Studio
  intends to support, hard-coding an accept/reject list here would itself be an unverified guess.
  Recorded as an open risk in §10, to be resolved once Step 011+ (or a dedicated compatibility step)
  has real multi-version fixtures to test against.

## 10. Unresolved issues / risks carried forward

1. **No `FormatVersion` validation** (§9, last bullet) — a real xLights file from an unsupported/
   future format version would currently parse as far as its `<models>`/`<modelGroups>` shape allows,
   with no explicit compatibility error. Flagged, not silently resolved.
2. **Duplicate model names across the document are not disambiguated** — if two `<model>` elements
   somehow shared the same `name` (not possible in a real xLights-authored file, but not structurally
   prevented by this parser), both would produce `DisplayModel` records with the identical `ModelId`.
   Step 011 ("Identify targetable models versus groups/submodels, reject unresolved refs... produce
   disambiguation warnings") is the step that explicitly owns this.
3. **Single-point geometry only** (§9) — no per-node pixel outline, matrix shape, or multi-point
   preview path is extracted, by design for this step; carried forward as a known gap for whichever
   later phase/step needs richer spatial data.
4. **The 5,000,000-character `MaxCharactersInDocument` bound is a judgment call, not derived from a
   real xLights show's measured maximum file size.** No real large xLights layout fixture was
   available to calibrate against; the bound is generous relative to the small fixtures this step
   actually tested, but should be revisited once a real, large, xLights-exported
   `xlights_rgbeffects.xml` is available for comparison.
5. **This step's manual verification used the real `%AppData%\XlightsAI.Studio\settings.json`**
   (§6) — same precedent as PHASE-01 Steps 004-007 and PHASE-02 Step 009, carried forward, disclosed
   again here.
6. Git Bash remains non-functional in this environment (carried forward from every prior step); every
   command in this evidence was run via PowerShell.
7. **Step 011 (validate referenced models/channels, reject unresolved refs, nested/overlapping group
   handling) is the natural next consumer of this step's `DisplayGraph`** — not started by this step,
   per its own narrow scope.

## 11. Gate checklist (this step only)

- [x] Parses actual `<models>`/`<modelGroups>` structures from real fixtures; preserves names/casing
      verbatim (§4, §5).
- [x] Normalized geometry extracted only "where present," never guessed/invented (§4, §5 —
      `ModelsWithoutGeometry_HaveEmptyPreviewPoints_NeverGuessed`).
- [x] Golden fixture verifies model names, groups and positions — required acceptance, proven exactly
      (§4, §5).
- [x] Invalid model/format/version fails safely without rewriting source files — 9 distinct negative
      paths, including a byte-for-byte source-file-untouched assertion (§5).
- [x] Untrusted-XML hardening: DTD/XXE prohibited outright, oversized documents rejected (§2, §5).
- [x] Full existing regression suite re-run: 112/112 passing (96 pre-existing baseline + 16 new Step
      010 tests), zero regressions (§3, §5).
- [x] `dotnet format --verify-no-changes` passes cleanly against every new and modified file (§3).
- [x] No original/show file, PHASE-01 file, or Step 009 test assertion touched except the two
      disclosed, additive, backward-compatible `ShowImportViewModel`/`ShellViewModel` extensions
      (§5, §9).
- [x] Architecture deviations disclosed in full (§9), per this task's explicit instruction.
- [x] Real UI demonstration performed (not simulated/mocked) — successful-parse and malformed-XML
      negative-path screenshots, both via real Windows UI Automation against the real running app
      (§6). No xLights installation was required for this step's own scope (read-only parsing only;
      Step 014 owns the real-xLights-render gate).

**Step 010 status: PASS.** Steps 011-015 remain unimplemented, as instructed ("do not execute the
next step until this step is proven").
