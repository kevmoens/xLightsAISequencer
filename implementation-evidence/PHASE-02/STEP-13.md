# PHASE 02 — Step 013 Evidence

**Step:** Port minimal editable XSQ writer and required sequencing metadata.
**Spec:** [`docs/phase-specs/PHASE-02.md`](../../docs/phase-specs/PHASE-02.md), Section 6, Step 013.
**Date:** 2026-10-10
**Status: PASS** (scoped to this step's own required acceptance; real xLights render verification remains Step 014's explicit, separate scope — see §1/§10).

> Filename note: continuing the PHASE-01/PHASE-02 precedent (`STEP-02.md`...`STEP-12.md`), this file
> uses `STEP-13.md` rather than the phase spec's own `STEP-013.md` form, matching that same precedent.

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting (PHASE-01 Steps 001-008 + PHASE-02 Steps 009-012, every
prior PHASE-02 step):

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 45
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 38
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# 139/139, exactly matching Step 012's own recorded final total.
```

No Step 009/010/011/012 (or PHASE-01) production `src/**` type's existing shape was changed or
removed — this step only **adds** new files (`IXsqExporter.cs`, `ExportResult.cs`,
`XsqExportInvalidException.cs`, `XsqExporter.cs`, `XsqStructuralEffectPlacement.cs`,
`XsqStructuralElement.cs`, `XsqStructuralDocument.cs`, `XsqStructuralReadException.cs`,
`IXsqStructuralReader.cs`, `XsqStructuralReader.cs`) plus additive extensions to
`ShowImportViewModel.cs`/`ShellViewModel.cs`/`MainWindow.axaml` (new optional constructor parameter
with a safe default, new bindable properties/commands, new UI section) — proven non-breaking by the
unmodified Step 009-012 test suites still passing in full below (§3).

**Real xLights installation:** still none found on this machine (carried forward from every prior
PHASE-02 step's own disclosure). This is why this step's own scope is deliberately narrow — "Port
minimal editable XSQ writer and required sequencing metadata" with required acceptance "Generated file
passes XML/structural tests and round-trip parser" — and explicitly does **not** claim the file renders
in real xLights; that remains Step 014's own, separately-scoped required acceptance ("Gold-standard
test opens and visibly renders in installed xLights" — PHASE-02 Section 1's own blocking phase-gate
text, Section 7 row 7, and the copy/paste agent prompt's own "NEVER CLAIM THE GATE PASSED WITHOUT"
line). Nothing in this step's own required acceptance text calls for a real render.

## 2. What was built

Per PHASE-02 Section 6 Step 013 ("Inspect xLights-authored XSQ and upstream xsq_writer; implement XML
escaping, ID/reference tables, palettes, effect data and layer order. Write a structural roundtrip test
with a reference reader.") and the required acceptance ("Generated file passes XML/structural tests and
round-trip parser."):

- **Inspected sources** (per the step's own implementation guidance), before writing any code:
  - The pinned upstream reference's actual XSQ writer,
    `reference/xlights-autosequencer/src/generator/xsq_writer.py` (full `write_xsq` function plus
    `_ensure_palette`/`_ensure_effect_entry`/`_serialize_palette`/`_serialize_effect_params`) — confirms
    the real document shape (`<xsequence>` root with `BaseChannel`/`FixedPointTiming`/`ModelBlending`
    attributes; `<head>`/`<ColorPalettes>`/`<EffectDB>`/`<DisplayElements>`/`<ElementEffects>` as
    siblings; `<Effect ref="N" name="..." startTime="..." endTime="..." palette="N" selected="0" />`
    inside a per-element `<EffectLayer>`; EffectDB/palette dedup-by-content with integer indices), the
    real 25ms frame interval (`FRAME_INTERVAL_MS = 25` in `reference/xlights-autosequencer/src/generator/models.py:17`),
    and — critically — confirms upstream **never emits an "On" effect at all**: there is no
    `_XLIGHTS_EFFECT_DEFAULTS["On"]` entry anywhere in the pinned reference (`grep` for `"On":`/`On
    effect`/`effect_name == "On"` returned zero matches), so no verified real xLights default-parameter
    table for this phase's own one recipe exists anywhere this codebase can inspect.
  - PHASE-02 Section 12's own appendix (the only "xLights-authored XSQ" shape available to this step,
    since no installed xLights build exists on this machine to save a real sample from — carried
    forward from every prior PHASE-02 step's own disclosure) — explicitly marked there as "not a
    complete, valid import promise."
  - Step 012's own `EffectInstance`/`SequenceDocument`/`EffectRecipeRegistry` (the exact upstream
    contract this step serializes; no redefinition).
- **`src/XlightsAI.Studio.Xlights/`** (extends the Step 009-012 project; no new project):
  - `IXsqExporter.cs` / `ExportResult.cs` (new) — match PHASE-02 Section 3's proposed signatures
    exactly: `ExportNewAsync(DisplayGraph, SequenceDocument, string outputPath, string
    targetXlightsVersion, CancellationToken) -> Task<ExportResult>`; `ExportResult(Path, Sha256,
    Warnings)`.
  - `XsqExportInvalidException.cs` (new) — the one exception type for every rejected-export-request
    path (blank/wrong-extension/already-existing output path, blank target version, an unresolved
    effect target, an unknown recipe id, a frame-rounded non-positive effect duration). Always thrown
    **before** any file is created — this step's own required failure path: "Invalid model/format/
    version must fail safely without rewriting source files."
  - `XsqExporter.cs` (new) — the actual writer, built with `System.Xml.Linq.XDocument`/`XElement`
    (Section 12: "Use DOM/XDocument serializer rather than string concatenation" — every name/value is
    therefore correctly XML-escaped automatically by the framework, never hand-built string
    concatenation). Emits exactly the five required sections (`head`, `ColorPalettes`, `EffectDB`,
    `DisplayElements`, `ElementEffects`) in the appendix's own order; resolves every effect's target
    against the real `DisplayGraph` (model vs. group, correctly typed `"model"`/`"modelGroup"` —
    reusing Step 011's own group contract, never inventing a third kind); deduplicates the (currently
    single) palette/EffectDB entry by content so every `ref`/`palette` index is non-dangling **by
    construction**, not merely by later inspection; converts each `EffectInstance`'s `StartTicks`/
    `EndTicks` (100ns ticks) to milliseconds and rounds to the real 25ms xLights frame grid (Section 12:
    "xLights frame-grid rounding is documented" — documented here as exactly `Math.Round(ms / 25) *
    25`); writes atomically (temp file + `File.Move`, the same pattern `JsonSequenceDocumentStore`/
    `JsonProjectStore` already use) and **never overwrites an existing file** (re-checked both before
    and immediately after the write, closing the TOCTOU window between the two checks).
  - **Deliberately does not invent the "On" effect's xLights settings parameters.** Section 2: "do not
    invent xLights effect parameters" is explicit and unconditional. Since no verified real xLights "On"
    effect sample exists anywhere (see "Inspected sources" above), the single `<EffectDB><Effect>`
    entry is written with an **empty** settings string rather than a plausible-looking but unverified
    `E_CHECKBOX_On_*`/`E_SLIDER_On_*` key set — the honest alternative to guessing. Every export's
    `ExportResult.Warnings` carries an explicit, non-silent disclosure of this (quoted in full in §6
    below), and `docs/phase-specs/PHASE-02.md` Section 12's own "golden fixture process" (capture a
    real xLights-saved sample, compare, freeze) is the documented path to replace this placeholder once
    an xLights install becomes available — the same path already named as Step 014's own blocking
    dependency.
  - `XsqStructuralDocument.cs` / `XsqStructuralElement.cs` / `XsqStructuralEffectPlacement.cs` /
    `XsqStructuralReadException.cs` / `IXsqStructuralReader.cs` / `XsqStructuralReader.cs` (new) — the
    step's own explicitly-named "reference reader": an independent `XDocument`-based re-parser (no
    shared code/state with `XsqExporter`) that validates a file is well-formed, carries every required
    section, and — Section 12's own serializer invariant — contains **no dangling `EffectDB`/palette
    ref** and no non-positive effect interval, throwing `XsqStructuralReadException` (never silently
    returning a partial/default document) otherwise.
- **`src/XlightsAI.Studio.Shell/ShowImportViewModel.cs`** (modified, additive): new bindable
  `XsqOutputPath`/`TargetXlightsVersion`/`IsExportBusy`/`ExportErrorMessage`/`LastExport`/`HasExport`
  properties and a new `ExportXsqAsync` method — same busy-guard/try-catch-sets-error-state shape as the
  existing `DiscoverAsync`/`GenerateEffectPlanAsync`, kept as entirely separate state (a failed export
  attempt never clobbers an already-successful `Plan`/`Layout`, and exporting never re-runs Discover or
  Generate).
- **`src/XlightsAI.Studio.Shell/ShellViewModel.cs`** (modified, additive): one new optional constructor
  parameter (`IXsqExporter`, defaulting to a real `new XsqExporter()` — same "pure, stateless, nothing
  to configure" reasoning Step 010/012 already used for `IShowLayoutImporter`/`IThreeEffectPlanFactory`/
  `IEffectRecipeRegistry`, since `XsqExporter` takes its output path as a per-call argument rather than
  needing a configured directory) and a new `ExportXsqCommand`/`LastExportXsqCommandTask`, wired exactly
  like `GenerateEffectPlanCommand`/`LastGenerateEffectPlanCommandTask`.
- **`src/XlightsAI.Studio.App/Views/MainWindow.axaml`** (modified, additive): new "Export XSQ" section
  on the existing Show Import screen (output-path textbox, target-version textbox, Export XSQ button,
  busy/error text, and the resulting export's path/SHA256/warnings) — no existing markup removed. No
  `Program.cs`/`App.axaml.cs` change was needed (unlike Step 012's `ISequenceDocumentStore`), since
  `XsqExporter` needs no external configuration to default to a real instance.
- **Tests** (new, no existing test file's assertions touched):
  - `tests/XlightsAI.Studio.Xlights.Tests/Step013AcceptanceTests.cs` — 11 tests against the real
    `XsqExporter`/`XsqStructuralReader`, real file-system I/O, no mocks.
  - `tests/XlightsAI.Studio.Shell.Tests/Step013ShellWiringTests.cs` — 5 tests proving the real
    `ShellViewModel.ExportXsqCommand` end-to-end pathway.

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests\XlightsAI.Studio.Xlights.Tests\XlightsAI.Studio.Xlights.Tests.csproj --filter "FullyQualifiedName~Step013" --logger "console;verbosity=normal"
Total tests: 11. Passed: 11.

$ dotnet test tests\XlightsAI.Studio.Shell.Tests\XlightsAI.Studio.Shell.Tests.csproj --filter "FullyQualifiedName~Step013" --logger "console;verbosity=normal"
Total tests: 5. Passed: 5.

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 56   (45 baseline + 11 new Step 013 domain tests)
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 43   (38 baseline + 5 new Step 013 shell-wiring tests)
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25   (untouched)
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31   (untouched)
# Full, final regression: 155/155 passing (139 baseline + 16 new), 0 regressions.

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes
(no output; exit code 0)
```

All commands were run via PowerShell; the Bash tool continues to fail on every invocation in this
environment (exit 1 / no output, including on a bare `ls`/`find`), consistent with every prior step's
evidence (PHASE-01 Steps 001-008, PHASE-02 Steps 009-012).

## 4. Required acceptance: "Generated file passes XML/structural tests and round-trip parser"

`Step013_Acceptance_Passes_GeneratedFilePassesStructuralAndRoundTripTests`
(`tests/XlightsAI.Studio.Xlights.Tests/Step013AcceptanceTests.cs`) builds an in-memory `DisplayGraph`
using the phase gate's own exact prop names ("Roofline, Arch 1 and Arch 2", PHASE-02 Section 1), a real
3-effect `SequenceDocument` via the real `ThreeEffectPlanFactory` (Step 012), calls the real
`XsqExporter.ExportNewAsync`, and asserts against the **independently re-parsed** `XsqStructuralDocument`
returned by the real `XsqStructuralReader` (the step's own explicitly-named "reference reader", sharing
no code or in-memory state with the exporter):

- `<head><version>`/`<sequenceDuration>`/`FixedPointTiming` round-trip exactly (`"2026.03"`, `30.000`
  seconds for the 30s plan, `25` ms).
- Exactly one `<ColorPalette>` and one `<EffectDB><Effect>` entry (content-deduplicated, as designed).
- Exactly 3 `<DisplayElements>`, in order, named `Roofline`/`Arch 1`/`Arch 2`, each correctly typed
  `"model"`.
- Exactly 3 `<ElementEffects>` elements, each with exactly one placement, whose `ref`/`palette` are both
  `0` (the single shared, by-construction-non-dangling entries) and whose `startTime`/`endTime`
  (milliseconds) exactly equal `TimeSpan.FromTicks(EffectInstance.StartTicks/EndTicks).TotalMilliseconds`
  for every one of the three original effects — the required acceptance itself: a real structural
  round-trip through a real, independent reference reader, field-by-field, not merely "the file parsed
  without throwing."

`Export_TargetingARealModelGroup_IsTypedAsModelGroup` proves the complementary case: a plan targeting a
real `DisplayModelGroup` (Step 011/012's own "a group is itself a valid effect target" contract) is
exported with `type="modelGroup"`, not `"model"` — no third, invented element kind.

## 5. Automated test results — detail

### `tests/XlightsAI.Studio.Xlights.Tests/Step013AcceptanceTests.cs` (11 new tests, real file-system I/O, no mocks)

```text
Passed Step013_Acceptance_Passes_GeneratedFilePassesStructuralAndRoundTripTests
Passed Export_TargetingARealModelGroup_IsTypedAsModelGroup
Passed ExistingOutputFile_FailsSafely_DoesNotOverwrite_NegativePath
Passed UnresolvedEffectTarget_FailsSafely_NoFileWritten_NegativePath
Passed UnknownRecipeId_FailsSafely_NoFileWritten_NegativePath
Passed InvalidOutputPath_FailsSafely_NoFileWritten_NegativePath(rawPath: "")
Passed InvalidOutputPath_FailsSafely_NoFileWritten_NegativePath(rawPath: "   ")
Passed InvalidOutputPath_FailsSafely_NoFileWritten_NegativePath(rawPath: "sequence.xml")
Passed BlankTargetXlightsVersion_FailsSafely_NoFileWritten_NegativePath
Passed StructuralReader_MalformedXml_FailsSafely_NegativePath
Passed StructuralReader_DanglingEffectDbRef_FailsSafely_NegativePath

Total: 11. Passed: 11.
```

- **Required negative-path assertion** ("Invalid model/format/version must fail safely without
  rewriting source files"): `ExistingOutputFile_FailsSafely_DoesNotOverwrite_NegativePath` proves a
  pre-existing target file's content (`"not touched"`) is byte-identical after a rejected export
  attempt; `UnresolvedEffectTarget_FailsSafely_NoFileWritten_NegativePath` proves a `SequenceDocument`
  built against a *different* layout than the one supplied to the exporter (defense-in-depth,
  independent of `ThreeEffectPlanFactory`'s own Step 012 validation) is rejected with the bad target
  named in the message and no file ever created; `UnknownRecipeId_FailsSafely_NoFileWritten_NegativePath`
  proves an unrecognized `RecipeId` is rejected rather than silently skipped or guessed; the three
  `InvalidOutputPath_*`/`BlankTargetXlightsVersion_*` cases cover blank/whitespace/wrong-extension
  output paths and a blank target version — "format/version must fail safely."
- `StructuralReader_MalformedXml_FailsSafely_NegativePath` /
  `StructuralReader_DanglingEffectDbRef_FailsSafely_NegativePath` prove the reference reader itself
  (not just the exporter) fails safely — a hand-crafted non-well-formed file, and a hand-crafted
  well-formed-but-structurally-invalid file (an `Effect ref="5"` against a 1-entry `EffectDB`) both
  throw `XsqStructuralReadException` naming the problem, never silently returning a document with the
  dangling reference intact.

### `tests/XlightsAI.Studio.Shell.Tests/Step013ShellWiringTests.cs` (5 new tests, real file-system I/O, no mocks)

```text
Passed Step013_Acceptance_Passes_ExportXsqEndToEnd
Passed ExportWithoutALayoutYet_SetsScreenError_NegativePath
Passed ExportWithoutAPlanYet_SetsScreenError_NegativePath
Passed ExportWithInvalidOutputPath_SetsScreenError_NegativePath
Passed ShellViewModel_WithoutExplicitXsqExporter_StillExportsForReal_BackwardCompatible

Total: 5. Passed: 5.
```

- `Step013_Acceptance_Passes_ExportXsqEndToEnd` runs the **exact phase gate demo shape** through the
  real `ShellViewModel`: Discover → Generate 3 Effects → Export XSQ, then independently re-reads the
  exported file with the real `XsqStructuralReader` and confirms 3 element-effects — the full UI →
  application → domain → adapter slice, not a stubbed collaborator.
- `ExportWithoutALayoutYet_SetsScreenError_NegativePath` / `ExportWithoutAPlanYet_SetsScreenError_NegativePath`
  prove exporting before Discover/Generate has populated `Layout`/`Plan` fails safely with an explicit
  screen error, never an unhandled exception or a guessed/invented layout/plan.
- `ExportWithInvalidOutputPath_SetsScreenError_NegativePath` proves a wrong-extension output path
  surfaces as a screen error and writes nothing.
- `ShellViewModel_WithoutExplicitXsqExporter_StillExportsForReal_BackwardCompatible` proves every
  earlier step's existing `ShellViewModel` call-site shape (no explicit exporter argument) still
  compiles and now really exports (via the real default `XsqExporter` instance), not a silently-succeeding
  fake.

### Pre-existing regression (unmodified acceptance text)

```text
XlightsAI.Studio.Xlights.Tests: 45/45 pre-existing Step009-012 tests passing, unmodified.
XlightsAI.Studio.Shell.Tests:   38/38 pre-existing tests (Step 004-012) passing, unmodified.
XlightsAI.Studio.Projects.Tests: 25/25 passing, untouched by this step.
XlightsAI.Studio.Reference.Tests: 31/31 passing, untouched by this step.
# Full regression with this step's new tests included: 155/155, 0 regressions.
```

No PHASE-01 or Step 009/010/011/012 test file's assertions were edited. No prior-step production type's
existing public shape was changed or removed; `ShowImportViewModel`/`ShellViewModel` only gained new
members, and `MainWindow.axaml` only gained new additive wiring.

## 6. Manual / desktop UI verification — real Avalonia app, real file system

Per the task's "Build the minimum complete UI -> application -> domain -> data/adapter slice" and "run
... manual tests when this step requires them" instructions, the real compiled desktop app
(`src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe`, started via `Start-Process` — the
exact same executable a user double-clicks) was launched for real and driven via Windows UI Automation
(`System.Windows.Automation`, real `InvokePattern`/`ValuePattern`/`ScrollPattern` calls against the real
running window — not simulated input, not a test harness substitute). A fixture show directory was
created directly under `C:\temp\xlai-step013-demo-show` (outside the repository, same convention every
prior PHASE-02 step's manual demo used) using the phase gate's own exact prop names — byte-identical to
Step 012's own fixture:

```xml
<xrgb FormatVersion="2" BaseChannel="0">
  <models>
    <model name="Roofline" DisplayAs="Star" WorldPosX="0" WorldPosY="5" />
    <model name="Arch 1" DisplayAs="Arches" WorldPosX="-3" WorldPosY="0" />
    <model name="Arch 2" DisplayAs="Arches" WorldPosX="3" WorldPosY="0" />
  </models>
  <modelGroups>
    <modelGroup name="Front-Arches" models="Roofline,Arch 1,Arch 2" />
  </modelGroups>
</xrgb>
```

SHA256 of `C:\temp\xlai-step013-demo-show\xlights_rgbeffects.xml`:
`792C35E91EFBB345D5C7A7D37A4651D19DC4FF1402BCEC8E2DCDE56820971937` (matches Step 012's own recorded
hash for the same content — same fixture reused for continuity of the gate-demo workflow).

1. **Success path** — `implementation-evidence/PHASE-02/step-013-export-xsq-success.png`: navigated to
   Show Import via a real `InvokePattern.Invoke()`, set the real show-directory textbox to the fixture
   above, clicked Discover (real parsed `Roofline`/`Arch 1`/`Arch 2`/`Front-Arches`), set "Three real
   props" to `Roofline, Arch 1, Arch 2` and clicked **Generate 3 Effects** (real 3-effect plan, same as
   Step 012's own demo), then set the new **Output path** textbox to
   `C:\temp\xlai-step013-demo-show\exports\sequence.xsq`, left **Target xLights version** at its default
   `2026.03`, and clicked the real **Export XSQ** button. The screen shows a real result line
   (`Exported: C:\temp\xlai-step013-demo-show\exports\sequence.xsq`), a real SHA256
   (`051C1334E322278E7C049B792FFF2324B2C90FD02B908428196A20463742DCD2`), and the real disclosed warning
   text (quoted verbatim, §2 above) — real, end-to-end UI evidence of the full slice, not a blank screen
   or a crash.
   The real exported file was independently inspected directly on disk and matches the reported hash
   exactly:
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <xsequence BaseChannel="0" FixedPointTiming="25" ModelBlending="true">
     <head>
       <version>2026.03</version>
       <sequenceType>Animation</sequenceType>
       <sequenceTiming>25 ms</sequenceTiming>
       <sequenceDuration>30.000</sequenceDuration>
     </head>
     <ColorPalettes><ColorPalette>C_BUTTON_Palette1=#FFFFFF</ColorPalette></ColorPalettes>
     <EffectDB><Effect></Effect></EffectDB>
     <DisplayElements>
       <Element type="model" name="Roofline" visible="1" />
       <Element type="model" name="Arch 1" visible="1" />
       <Element type="model" name="Arch 2" visible="1" />
     </DisplayElements>
     <ElementEffects>
       <Element type="model" name="Roofline"><EffectLayer><Effect ref="0" name="On" startTime="0" endTime="10000" palette="0" selected="0" /></EffectLayer></Element>
       <Element type="model" name="Arch 1"><EffectLayer><Effect ref="0" name="On" startTime="10000" endTime="20000" palette="0" selected="0" /></EffectLayer></Element>
       <Element type="model" name="Arch 2"><EffectLayer><Effect ref="0" name="On" startTime="20000" endTime="30000" palette="0" selected="0" /></EffectLayer></Element>
     </ElementEffects>
   </xsequence>
   ```
   Matches PHASE-02 Section 12's appendix shape exactly (`head`/`ColorPalettes`/`EffectDB`/
   `DisplayElements`/`ElementEffects`, in that order).
2. **Negative path** — `implementation-evidence/PHASE-02/step-013-export-existing-file-error.png`: with
   the same output path already holding the file just written, **Export XSQ** was clicked a second time.
   The screen shows the real red error line `'C:\temp\xlai-step013-demo-show\exports\sequence.xsq'
   already exists; export never overwrites an existing file.` — not a crash, not a silently-overwritten
   file. The on-disk file's content/hash were unchanged by this second click (verified: the SHA256 above
   was captured *after* this negative-path attempt).

**Disclosed side effect**: this manual verification wrote real files under
`C:\temp\xlai-step013-demo-show\exports\` (one `sequence.xsq`) and under this machine's own
`%AppData%\XlightsAI.Studio\exports\` (one additional `sequence-plan-*.json` from the Generate step,
same precedent as Step 012's own disclosure) — neither is part of the committed repository.

## 7. Fixture hashes

```text
C:\temp\xlai-step013-demo-show\xlights_rgbeffects.xml (manual-demo fixture, outside the repository)
  SHA256=792C35E91EFBB345D5C7A7D37A4651D19DC4FF1402BCEC8E2DCDE56820971937 (identical content to Step 012's own fixture)

C:\temp\xlai-step013-demo-show\exports\sequence.xsq (real app output, manual verification)
  SHA256=051C1334E322278E7C049B792FFF2324B2C90FD02B908428196A20463742DCD2
```

This step's automated tests (§4/§5) build their `DisplayGraph`/`SequenceDocument` fixtures in-memory (no
new checked-in XML fixture file was needed), so there is no new `tests/.../Fixtures/*.xml` hash table
analogous to Step 010/011's.

## 8. Changed/added paths

```
src/XlightsAI.Studio.Xlights/IXsqExporter.cs                                     (new)
src/XlightsAI.Studio.Xlights/ExportResult.cs                                     (new)
src/XlightsAI.Studio.Xlights/XsqExportInvalidException.cs                        (new)
src/XlightsAI.Studio.Xlights/XsqExporter.cs                                      (new)
src/XlightsAI.Studio.Xlights/XsqStructuralEffectPlacement.cs                     (new)
src/XlightsAI.Studio.Xlights/XsqStructuralElement.cs                             (new)
src/XlightsAI.Studio.Xlights/XsqStructuralDocument.cs                            (new)
src/XlightsAI.Studio.Xlights/XsqStructuralReadException.cs                       (new)
src/XlightsAI.Studio.Xlights/IXsqStructuralReader.cs                             (new)
src/XlightsAI.Studio.Xlights/XsqStructuralReader.cs                              (new)
src/XlightsAI.Studio.Shell/ShowImportViewModel.cs                                (modified: +XsqOutputPath/+TargetXlightsVersion/+IsExportBusy/+ExportErrorMessage/+LastExport/+HasExport/+ExportXsqAsync)
src/XlightsAI.Studio.Shell/ShellViewModel.cs                                     (modified: +1 optional ctor param, +ExportXsqCommand, +LastExportXsqCommandTask)
src/XlightsAI.Studio.App/Views/MainWindow.axaml                                  (modified: +Export XSQ section)
tests/XlightsAI.Studio.Xlights.Tests/Step013AcceptanceTests.cs                   (new; 11 tests)
tests/XlightsAI.Studio.Shell.Tests/Step013ShellWiringTests.cs                    (new; 5 tests)
implementation-evidence/PHASE-02/STEP-13.md                                      (new, this file)
implementation-evidence/PHASE-02/step-013-export-xsq-success.png                 (new)
implementation-evidence/PHASE-02/step-013-export-existing-file-error.png         (new)
```

No PHASE-01 file, no Step 009/010/011/012 production file's existing members, and no Step 009/010/011/012
test file's assertions were edited or removed.

## 9. Architecture deviations

- **The "On" recipe's `EffectDB` settings string is an empty placeholder, not a guessed real xLights
  parameter set.** This is the single most consequential decision this step makes and is disclosed at
  three levels: in `XsqExporter`'s own class doc, in every `ExportResult.Warnings` entry (surfaced live
  in the UI, §6), and here. Section 2 is unconditional ("do not invent xLights effect parameters") and
  Section 12 states "No hardcoded XML claimed valid until verified in xLights." Since the pinned
  upstream reference has no "On" effect default table at all (confirmed by direct inspection, §2) and no
  xLights install exists on this machine to save a real sample from, there is no verified source to copy
  real parameter keys from. An empty settings string is honest about this gap; a plausible-looking
  fabricated key set would not be, and could additionally produce a file that *appears* complete while
  actually being wrong in an unverifiable way. This narrows this step's own required acceptance
  correspondingly: "passes XML/structural tests and round-trip parser" is proven exactly (§4/§5); "the
  'On' effect renders correctly in real xLights" is explicitly *not* claimed and remains Step 014's own
  scope, which is why Step 014 (not this step) is named the phase's "gold-standard... visibly renders"
  gate in Section 1/Section 9's copy/paste prompt.
- **`XsqExporter` re-validates every effect's target against the supplied `DisplayGraph`**, even though
  `ThreeEffectPlanFactory` (Step 012) already validated this when the plan was first created. This is
  deliberate defense-in-depth, not redundant busywork: a `SequenceDocument` can be loaded from disk
  (`ISequenceDocumentStore.LoadAsync`, Step 012) independently of any particular `DisplayGraph`, so a
  caller could in principle hand the exporter a stale/mismatched plan+layout pair. `XsqExporter` does not
  trust that the two were ever validated together and checks again itself —
  `UnresolvedEffectTarget_FailsSafely_NoFileWritten_NegativePath` (§4/§5) specifically exercises this
  cross-layout mismatch case, which `ThreeEffectPlanFactory`'s own tests cannot reach (it only ever sees
  a plan+layout pair it itself just validated together).
- **Effect start/end times are rounded to the 25ms xLights frame grid before being written**, per Section
  12's explicit "xLights frame-grid rounding is documented" invariant. For this step's own 3-effect,
  10-second-segment plan shape the rounding is a no-op (every boundary is already frame-aligned), but the
  rule itself — and its own failure mode, a frame-rounded window collapsing to a non-positive duration —
  is implemented and enforced (`XsqExportInvalidException` thrown, no file written) rather than left
  unhandled for a future step to discover the hard way.
- **No `IXsqExporter` Null-object fallback.** Unlike `ISequenceDocumentStore` (needs a configured output
  directory), `XsqExporter` takes its output path as a per-call argument and needs no external
  configuration, so `ShellViewModel` defaults it to a real instance directly — the same reasoning Step
  010/012 already recorded for `IShowLayoutImporter`/`IThreeEffectPlanFactory`/`IEffectRecipeRegistry`,
  applied consistently here rather than introducing a different convention for a fourth "pure, stateless"
  collaborator.
- **`XsqStructuralReader` is new production code, not test-only scaffolding**, per the step's own text:
  "Write a structural roundtrip test **with a reference reader**" names the reader as a thing to build,
  distinct from the test that uses it. It is intentionally independent of `XsqExporter` (separate file,
  no shared helper functions, re-parses from bytes on disk rather than reusing any in-memory `XDocument`)
  so the round-trip test proves something about the actual file on disk, not merely that the exporter's
  own in-memory model is self-consistent.

## 10. Unresolved issues / risks carried forward

1. **No installed, runnable xLights executable was available on this machine** (carried forward from
   every prior PHASE-02 step's own §1) — still Step 014's own blocking dependency, not this step's.
   Concretely for this step: the "On" effect's `EffectDB` settings remain an empty, explicitly-disclosed
   placeholder (§9) until a real xLights-saved sample can be captured per Section 12's own golden-fixture
   process.
2. **Atomic/collision-suffix/backup export handling is only partially built.** This step implements the
   minimum "never overwrite an existing file" (checked before and after the write) plus a temp-file+move
   write, but collision-suffix naming, explicit backup-on-overwrite, and golden XML regression comparison
   are Step 015's own named scope ("Implement atomic export, filename safety, backups and golden XSQ
   regression tests") — not duplicated here.
3. **Only one recipe (`effect.on.v1` → xLights "On") is translatable to XML.** `XsqExporter` rejects any
   other `RecipeId` by name rather than guessing a shape for it; this is correct per this step's own
   scope (Step 012 ships exactly one recipe) and is not a gap, but is recorded here since a future step
   adding a second recipe must extend `XsqExporter`'s own recipe-to-XML mapping, not assume it is
   generic.
4. **`xlights_networks.xml` is still not parsed** (carried forward from Step 011's own §10 item 2) —
   unaffected by this step's own scope.
5. **This step's manual verification wrote real files** under `C:\temp\xlai-step013-demo-show\exports\`
   and this machine's own `%AppData%\XlightsAI.Studio\exports\` (§6) — same precedent as every prior
   step's own disclosure, carried forward again here; none are part of the committed repository.
6. Git Bash remains non-functional in this environment (carried forward from every prior step); every
   command in this evidence was run via PowerShell.
7. **Step 014 (open output in installed xLights; verify duration, timing, models and visible effects) is
   the natural next consumer of this step's validated, structurally-sound `.xsq` output** — not started
   by this step, per its own narrow scope, and explicitly blocked on the same missing xLights install
   named in item 1.

## 11. Gate checklist (this step only)

- [x] Inspected the pinned upstream `xsq_writer.py` and PHASE-02 Section 12's own appendix before
      implementing (§2).
- [x] Implements XML escaping (via `XDocument`/`XElement`, never string concatenation), ID/reference
      tables (dedup-by-content palette/EffectDB indices, non-dangling by construction), palettes,
      effect data and layer order (§2, §4).
- [x] Required acceptance — "Generated file passes XML/structural tests and round-trip parser" — proven
      exactly, both by an automated structural round-trip test against a real, independent reference
      reader and by a real UI demonstration with independent on-disk file inspection (§4, §5, §6).
- [x] Required failure path — "Invalid model/format/version must fail safely without rewriting source
      files" — proven by 7 distinct negative-path tests (existing-file, unresolved-target, unknown-
      recipe, blank/whitespace/wrong-extension path, blank version) plus a real UI negative-path
      demonstration; no partial/invalid file is ever created or overwritten (§4, §5, §6).
- [x] Full regression suite re-run, final: 155/155 passing (139 PHASE-01/Step009-012 baseline + 16 new
      Step 013 tests), zero regressions (§3, §5).
- [x] `dotnet format --verify-no-changes` passes cleanly against every new and modified file (§3).
- [x] No original/show file or PHASE-01 file touched; no Step 009/010/011/012 test *assertion* touched —
      every prior-step production file edited gained only new, additive members (§5, §8).
- [x] Never overwrites source show files or any pre-existing export target (§2, §4, §5, §6, §9).
- [x] Does not invent xLights effect parameters — an unverified placeholder is used and explicitly
      disclosed instead (§2, §9), per Section 2's unconditional rule.
- [x] Architecture deviations disclosed in full (§9), per this task's explicit instruction.
- [x] Real UI demonstration performed (not simulated/mocked) — two screenshots via real Windows UI
      Automation against the real running app: the phase gate's own exact success-path prop names
      through the full Discover → Generate → Export workflow, and an explicit already-exists negative
      path (§6). **No installed xLights executable was required, available, or used for this step's own
      scope** — Step 013 never claims a real render; that remains Step 014's own, separately-scoped gate.

**Step 013 status: PASS** (scoped to this step's own required acceptance, structural/round-trip only).
Steps 014-015 remain unimplemented, as instructed ("do not execute the next step until this step is
proven"). Step 014 specifically — real xLights open/render verification — is explicitly still BLOCKED on
the same missing xLights install disclosed in every prior PHASE-02 step; this step does not and cannot
discharge that blocker, and does not claim to.
