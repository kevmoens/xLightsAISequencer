# PHASE 02 — Step 011 Evidence

**Step:** Validate referenced models, channel/network data where relevant and missing layout elements.
**Spec:** [`docs/phase-specs/PHASE-02.md`](../../docs/phase-specs/PHASE-02.md), Section 6, Step 011.
**Date:** 2026-10-10
**Status: PASS** (includes an accepted, evidence-backed Step 010 migration — see §9a/§9b)

> Filename note: continuing the PHASE-01/PHASE-02 precedent (`STEP-02.md`...`STEP-10.md`), this file
> uses `STEP-11.md` rather than the phase spec's own `STEP-011.md` form, matching the task
> instruction's own spelling ("Step 11" / "STEP-11.md").

## 1. Baseline check (prior step contracts, unmodified)

Re-ran the full existing suite before starting (PHASE-01 Steps 001-008 + PHASE-02 Steps 009-010, the
only prior PHASE-02 steps):

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 22
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 34
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# 112/112, exactly matching Step 010's own recorded final total.
```

No Step 009/010 (or PHASE-01) production `src/**` file's existing behavior was changed; this step's
modifications to `DisplayGraph.cs` and `ShowLayoutImporter.cs` are additive extensions of Step 010's
own already-accepted contracts (new record parameters/new parsing step), proven non-breaking by the
unmodified Step 009/010 test suites still passing in full below.

**Real xLights installation / show files:** none found on this machine. Per the user's mid-task
suggestion to copy local xLights files for testing, I searched `C:\`, `C:\Program Files*` and the
user profile for an xLights install or a `C:\xlights`-style directory; none exists here. Step 011
itself is pure XML validation (no rendering) — Step 014 is the step that actually requires an
installed xLights build. This gap is carried forward unchanged from Step 010's own §10 risk list
(no real large/representative xLights-authored `xlights_rgbeffects.xml` was available then either)
and is re-disclosed in §10 below, not silently worked around.

## 2. What was built

Per PHASE-02 Section 6 Step 011 ("Identify targetable models versus groups/submodels, reject
unresolved refs; read networks for diagnostics only if needed. Support nested/overlapping groups
without flattening.") and the required acceptance ("Fixture with broken references generates
warnings rather than partial destructive output."):

- **`src/XlightsAI.Studio.Xlights/`** (extends the Step 009/010 project; no new project):
  - `DisplayGroupMemberKind.cs` (new) — enum: `Model`, `Group` (nested-group reference), `Submodel`
    (the xLights `ModelName/SubModelName` shape, model half verified), `Unresolved` (broken
    reference).
  - `DisplayGroupMember.cs` (new) — `(string RawName, DisplayGroupMemberKind Kind)`: one raw,
    as-written `<modelGroup>` member entry plus its resolved classification. `RawName` is never
    rewritten/normalized.
  - `DisplayModelGroup.cs` (new) — `(string Name, IReadOnlyList<DisplayGroupMember> Members)`: a
    `<modelGroup>` as its own targetable entity (a group is itself a valid xLights effect
    target, distinct from its members), with its declared member list preserved in declaration
    order, never expanded/flattened.
  - `DisplayGraph.cs` (modified, additive) — gained two new positional members:
    `IReadOnlyList<DisplayModelGroup> Groups` and `IReadOnlyList<string> Warnings`. The only existing
    construction site (`ShowLayoutImporter.ImportReadOnlyAsync`) was updated; no other call site in
    the repository constructs `DisplayGraph` positionally (verified by `grep`, §5).
  - `ShowLayoutImporter.cs` (modified, additive) — new private `ParseGroupsAndValidateReferences`
    method, invoked after the existing `ParseModels` call:
    - Reports a warning (not a thrown exception) when the document has no `<models>` element and/or
      no `<modelGroups>` element at all ("missing layout elements" — the import still succeeds
      with an empty `Models`/`Groups` list, never guessed/invented content).
    - Builds the real set of model names and the real set of group names from this same document,
      then classifies every raw `<modelGroup>` member against both sets plus the `ModelName/SubModelName`
      shape (new `ClassifyGroupMember` helper): a name matching a real model → `Model`; a name
      matching another group's own name → `Group` (nested reference, left exactly as a reference,
      never substituted with that group's own members — proving "Support nested/overlapping groups
      without flattening"); a `ModelName/SubModelName`-shaped name whose model half resolves →
      `Submodel`; anything else → `Unresolved`, which also appends an explicit warning naming both the
      owning group and the broken raw reference.
    - The pre-existing `groupsByModelName`/direct per-model `GroupNames` logic (Step 010's own
      contract) is untouched — this step's validation runs as an independent second pass over the
      same parsed document, not a replacement.
  - No change to `IShowLayoutImporter`'s public signature, `ShowLayoutMalformedException`, or any of
    Step 009/010's thrown-exception failure paths — this step's new findings are warnings on a
    successful `DisplayGraph`, never a new exception type (the one new negative-path test this step
    adds, §4, exercises the *pre-existing* `modelGroup`-missing-`name` failure path, which already
    existed in Step 010's code and was simply not yet covered by its own fixture/test).
- **`src/XlightsAI.Studio.App/Views/MainWindow.axaml`** (additive wiring only): two new bound
  sections inside the existing Step 010 `IsLoaded` panel — a "Groups" list (`ShowImport.Layout.Groups`,
  showing each group's `Name`) and a `Warnings` list (`ShowImport.Layout.Warnings`, same orange-text
  styling Step 009 already established for `Discovery.Warnings`). No existing markup removed/changed.
- **Fixtures** (`tests/XlightsAI.Studio.Xlights.Tests/Fixtures/`, continuing Step 009/010's own
  structural-fixture precedent):
  - `sample-show-broken-references/` — **this step's golden fixture**: two real models; one group
    ("Front-Yard") whose member list includes a broken reference ("Ghost-Prop", no matching model);
    a second group ("All-Props") that nests "Front-Yard" by name and also references a submodel shape
    ("Arch-01/Segment-1") whose model half resolves.
  - `sample-show-no-layout-elements/` — a well-formed, recognized `xrgbEffects` document with
    neither a `<models>` nor a `<modelGroups>` element.
  - `sample-show-modelgroup-missing-name/` — a `<modelGroup>` element missing its required `name`
    attribute (this step's required negative-path assertion).
  - Step 010's own `sample-show-with-geometry` fixture is reused unmodified by this step's own
    "no warnings on a well-formed document" test.

## 3. Commands actually executed

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test tests\XlightsAI.Studio.Xlights.Tests\XlightsAI.Studio.Xlights.Tests.csproj --logger "console;verbosity=normal"
Total tests: 26. Passed: 26. (22 pre-existing Step009/010AcceptanceTests + 4 new Step011AcceptanceTests)
# — this was the count BEFORE the §9a/§9b migration; see below for the post-migration count.

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 26
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 34
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31
# Full regression: 116/116 passing (112 baseline + 4 new), 0 regressions — pre-migration snapshot.

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes
(no output; exit code 0)
```

All commands were run via PowerShell; the Bash tool failed on every invocation in this environment
(exit 1 / no output, including on a bare `cat`/`ls`), consistent with every prior step's evidence
(PHASE-01 Steps 001-008, PHASE-02 Steps 009-010).

### 3a. Commands actually executed — after the §9a/§9b migration (final)

```text
$ dotnet build XlightsAI.Studio.slnx
Build succeeded. 0 Warning(s) 0 Error(s)

$ dotnet test XlightsAI.Studio.slnx
XlightsAI.Studio.Xlights.Tests.dll:   Passed! Total: 33   (26 above + 7 new migration tests, §9c)
XlightsAI.Studio.Shell.Tests.dll:     Passed! Total: 34   (fixture content updated, no assertions changed)
XlightsAI.Studio.Projects.Tests.dll:  Passed! Total: 25   (untouched)
XlightsAI.Studio.Reference.Tests.dll: Passed! Total: 31   (untouched)
# Full, final regression: 123/123 passing (112 PHASE-01/Step009-010 baseline + 4 Step 011 content
# tests + 7 Step 011 migration tests), 0 regressions.

$ dotnet format XlightsAI.Studio.slnx --verify-no-changes
(no output; exit code 0)
```

## 4. Required acceptance: "Fixture with broken references generates warnings rather than partial destructive output"

`Step011_Acceptance_Passes_BrokenReferencesProduceWarningsNotDestructiveFailure`
(`tests/XlightsAI.Studio.Xlights.Tests/Step011AcceptanceTests.cs`) parses the real
`sample-show-broken-references` fixture and asserts, against the real returned `DisplayGraph`:

- **Non-destructive**: the import *succeeds* (no exception) and returns a complete graph —
  `graph.Models.Count == 2`, `graph.Groups.Count == 2` — not a partial/empty one.
- **"Front-Yard"** group's 3 declared members are each classified correctly: `Arch-01` → `Model`,
  `MegaTree-Center` → `Model`, `Ghost-Prop` → `Unresolved`.
- **"All-Props"** group's 2 declared members: `Front-Yard` → `Group` (a nested-group reference, kept
  exactly as a reference — proving "Support nested/overlapping groups without flattening": the test
  does **not** assert `All-Props` somehow also contains `Arch-01`/`MegaTree-Center` by expansion);
  `Arch-01/Segment-1` → `Submodel` (submodel shape, model half "Arch-01" resolves).
- **The broken reference surfaces as an explicit warning**: `graph.Warnings` contains an entry
  naming both the owning group ("Front-Yard") and the broken raw reference ("Ghost-Prop").
- **Step 010's own per-model `GroupNames` contract is unaffected**: `Arch-01.GroupNames == ["Front-Yard"]`,
  exactly the direct membership Step 010 already computed, proving this step's validation is additive,
  not a replacement.

`MissingLayoutElements_ProduceWarnings_NotFailure` proves the complementary "missing layout elements"
half of the requirement: a document with neither `<models>` nor `<modelGroups>` still imports
successfully (`Models`/`Groups` both empty — never guessed) with two explicit warnings, one per
missing element.

`WellFormedReferences_ProduceNoWarnings` proves the required acceptance's implicit converse: Step
010's own golden fixture (no broken references) produces zero warnings under this step's new
validation, and every one of its groups' members classifies as `Model` — this step adds no false
positives against already-accepted, fully-resolved data.

## 5. Automated test results — detail

### `tests/XlightsAI.Studio.Xlights.Tests/Step011AcceptanceTests.cs` (4 new tests, real file-system I/O and real XML parsing, no mocks)

```text
Passed Step011_Acceptance_Passes_BrokenReferencesProduceWarningsNotDestructiveFailure [24 ms]
Passed MissingLayoutElements_ProduceWarnings_NotFailure                              [8 ms]
Passed WellFormedReferences_ProduceNoWarnings                                        [8 ms]
Passed ModelGroupMissingRequiredNameAttribute_FailsSafely_NegativePath               [24 ms]

Total: 4. Passed: 4.
```

- **Failure/edge case required** ("Invalid model/format/version must fail safely without rewriting
  source files") — `ModelGroupMissingRequiredNameAttribute_FailsSafely_NegativePath` against the new
  `sample-show-modelgroup-missing-name` fixture: a `<modelGroup>` element with no `name` attribute
  throws `ShowLayoutMalformedException` (the pre-existing Step 010 `RequireAttribute` check, now
  explicitly covered by its own fixture/test), and the test byte-compares the fixture file before and
  after the failed parse attempt to prove the source file is never rewritten.
- `grep -rn "new DisplayGraph("` across `src/` confirmed exactly one construction site
  (`ShowLayoutImporter.cs:64`) before this step's `DisplayGraph` signature change, so the additive
  4-parameter record change could not silently miss another call site.

### Pre-existing regression (unmodified acceptance text)

```text
XlightsAI.Studio.Xlights.Tests: 22/22 Step009/010AcceptanceTests passing, unmodified.
XlightsAI.Studio.Shell.Tests:   34/34 pre-existing tests (Step 004-010) passing, unmodified.
XlightsAI.Studio.Projects.Tests: 25/25 passing, untouched by this step.
XlightsAI.Studio.Reference.Tests: 31/31 passing, untouched by this step.
# Full regression with this step's new tests included: 116/116, 0 regressions.
```

No PHASE-01 or Step 009/010 test file's assertions were edited (fixture/inline-XML *content* was
edited for the §9a/§9b migration, never an assertion). The Step 010 production files edited were
`DisplayGraph.cs` (two new additive positional record parameters, both with the one real construction
site updated) and `ShowLayoutImporter.cs` (the Step 011 content-validation method, plus the §9b
migration's root-element-acceptance and DOCTYPE-tolerance changes); all are proven non-breaking by
Step 009/010's own still-passing test suite (§3a) — none of those existing tests assert
`DisplayGraph.Groups`/`Warnings` at all, so the additive signature change did not require touching
them, and the root-element/DOCTYPE changes only required updating fixture *content* (never
assertions) where that content is actually parsed for a success path.

## 6. Manual / desktop UI verification — real Avalonia app, real file system

Per the task's "Build the minimum complete UI -> application -> domain -> data/adapter slice" and
"run... manual tests when this step requires them" instructions, the real compiled desktop app
(`src\XlightsAI.Studio.App\bin\Debug\net10.0\XlightsAI.Studio.exe`, started via
`Start-Process`/`dotnet build` — the exact same executable a user double-clicks) was launched for real
and driven via Windows UI Automation (`System.Windows.Automation`, real `InvokePattern`/`ValuePattern`
calls against the real running window — not simulated input, not a test harness substitute). As
Step 010 also recorded: no xLights installation is relevant to this step either (Step 011 is still
read-only XML *parsing/validation*; nothing is exported/opened in xLights yet — that remains Step
013/014's scope). Fixture files were copied to `C:\temp\xlai-step011-*` (outside the build-output
`Fixtures` directory, at the user's suggestion to use locally-copied files for this manual run) before
driving the real app against them.

1. **Broken-reference fixture** —
   `implementation-evidence/PHASE-02/step-011-broken-references-warnings.png`: navigated to Show
   Import via a real `InvokePattern.Invoke()` on the real "Show Import" button, set the real textbox
   (via `ValuePattern.SetValue`) to `C:\temp\xlai-step011-broken-refs`, clicked the real Discover
   button. The screen shows both real parsed groups "Front-Yard"/"All-Props" and the real orange
   warning line `Model group 'Front-Yard' references unresolved target 'Ghost-Prop'.` alongside the
   already-successful Models list and a real computed `Layout hash: 2E10A424...` — real end-to-end UI
   evidence that a broken reference produces a warning on an otherwise fully-populated, non-destructive
   screen, not a blank screen or a crash.
2. **Missing-layout-elements fixture** —
   `implementation-evidence/PHASE-02/step-011-missing-layout-elements-warnings.png`: same real flow
   against `C:\temp\xlai-step011-no-layout` (a well-formed document with neither `<models>` nor
   `<modelGroups>`). The screen shows an empty Models list, an empty Groups list, and the two real
   orange warning lines `'...xlights_rgbeffects.xml' has no '<models>' element.` /
   `'...xlights_rgbeffects.xml' has no '<modelGroups>' element.` — not an error screen, not an
   unhandled-exception crash.

**Disclosed side effect**: same as Step 009/010's own disclosure — this manual verification ran the
real app against the real `%AppData%\XlightsAI.Studio\settings.json` on this machine. That file's
`LastShowDirectory` now reflects this session's last successful Discover
(`C:\temp\xlai-step011-no-layout`). No project file, show file, or test fixture under
`tests/.../Fixtures/` was touched/rewritten by this step's manual verification — only the throwaway
`C:\temp\xlai-step011-*` copies (outside the repository) were read.

## 7. Fixture hashes (this step's new fixtures)

```text
sample-show-broken-references/xlights_rgbeffects.xml      SHA256=2E10A4249DB70DB7C688923BDF84CE299E9CC49A3C2FF74EE72C9A5F5A324CAB
sample-show-no-layout-elements/xlights_rgbeffects.xml      SHA256=5EE4F857C45FF5F4EC39D6AC816FA2BB9C366D1AC724EA94BDE1233952111BEE
sample-show-modelgroup-missing-name/xlights_rgbeffects.xml SHA256=33F1863176E9E7B970B1D1B06CBF20DDD1D31AA0D57432A1D652869A3E6AE918
```

Computed via `Get-FileHash -Algorithm SHA256` directly against the checked-in fixture files (not the
build-output copies). The UI screenshot in §6 independently shows the importer's own computed
`SourceLayoutHash` for `sample-show-broken-references` (`2E10A4249DB70DB7C688923BDF84CE299E9CC49A3C2FF74EE72C9A5F5A324CAB`)
matching this table exactly.

## 8. Changed/added paths

```
src/XlightsAI.Studio.Xlights/DisplayGroupMemberKind.cs                                        (new)
src/XlightsAI.Studio.Xlights/DisplayGroupMember.cs                                            (new)
src/XlightsAI.Studio.Xlights/DisplayModelGroup.cs                                             (new)
src/XlightsAI.Studio.Xlights/DisplayGraph.cs                                                  (modified: +Groups, +Warnings positional members)
src/XlightsAI.Studio.Xlights/ShowLayoutImporter.cs                                            (modified: +ParseGroupsAndValidateReferences, +ClassifyGroupMember; §9b migration: +AcceptedRootElementNames, +StripBenignDoctypeIfPresent, updated root-element check, updated DisplayGraph construction)
src/XlightsAI.Studio.App/Views/MainWindow.axaml                                               (modified: +2 bound sections inside the existing IsLoaded panel, no existing markup changed)
tests/XlightsAI.Studio.Xlights.Tests/Step011AcceptanceTests.cs                                (new; includes 4 content tests + 7 §9b migration tests)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-broken-references/xlights_rgbeffects.xml           (new; §9b: root renamed to xrgb)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-no-layout-elements/xlights_rgbeffects.xml          (new; §9b: root renamed to xrgb)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-modelgroup-missing-name/xlights_rgbeffects.xml     (new; §9b: root renamed to xrgb)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-legacy-xlights-root/xlights_rgbeffects.xml         (new, §9b migration fixture)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-stray-doctype/xlights_rgbeffects.xml                (new, §9b migration fixture)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-doctype-external-reference/xlights_rgbeffects.xml   (new, §9b migration fixture)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-complete/xlights_rgbeffects.xml             (modified, §9b: root renamed xrgbEffects → xrgb; reused Step 009/010 fixture)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-with-geometry/xlights_rgbeffects.xml        (modified, §9b: root renamed; Step 010's own golden fixture)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-model-missing-name/xlights_rgbeffects.xml   (modified, §9b: root renamed)
tests/XlightsAI.Studio.Xlights.Tests/Fixtures/sample-show-missing-networks/xlights_rgbeffects.xml     (modified, §9b: root renamed; Step 009's own fixture)
tests/XlightsAI.Studio.Shell.Tests/Step009ShellWiringTests.cs                                 (modified, §9b: one inline fixture string's root renamed; no assertion changed)
tests/XlightsAI.Studio.Shell.Tests/Step010ShellWiringTests.cs                                 (modified, §9b: two inline fixture strings' roots renamed; no assertion changed)
implementation-evidence/PHASE-02/STEP-11.md                                                  (new, this file)
implementation-evidence/PHASE-02/step-011-broken-references-warnings.png                      (refreshed after §9b migration)
implementation-evidence/PHASE-02/step-011-missing-layout-elements-warnings.png                (refreshed after §9b migration)
implementation-evidence/PHASE-02/step-011-migration-legacy-xlights-root.png                   (new, §9b)
implementation-evidence/PHASE-02/step-011-migration-stray-doctype-tolerated.png               (new, §9b)
```

No PHASE-01 file and no Step 009/010 test *assertion* was altered. Four pre-existing Step 009/010
fixtures and two pre-existing Shell test files' inline fixture *content* (never assertions) were
updated as part of the explicitly user-accepted §9a/§9b migration — disclosed in full above, not
silently changed.

## 9. Architecture deviations

- **"Reject unresolved refs" is implemented as a non-destructive warning on an otherwise-successful
  `DisplayGraph`, not a thrown exception.** The implementation guidance text ("reject unresolved
  refs") could be read as implying a thrown/blocking failure. This step instead follows the step's
  own, more specific **required acceptance** text verbatim — "Fixture with broken references generates
  warnings rather than partial destructive output" — which explicitly rules out a destructive/blocking
  response to a broken reference. "Reject" is read here as "do not treat the broken name as a resolved
  model/group" (i.e., it is excluded from `Model`/`Group`/`Submodel` classification and tagged
  `Unresolved` instead) rather than "reject the whole import." This mirrors Step 009/010's own
  established pattern of optional/non-required problems (missing optional `xlights_networks.xml`,
  missing geometry) surfacing as warnings, while only structurally fatal problems (malformed XML,
  wrong root element, a required attribute entirely absent) throw.
- **`xlights_networks.xml` is not parsed in this step.** The implementation guidance's "read networks
  for diagnostics only if needed" is conditional, and Step 011's own required acceptance text never
  mentions networks data at all — only "broken references" and "missing layout elements" against
  `xlights_rgbeffects.xml`. No real xLights-authored `xlights_networks.xml` sample was available to
  verify an actual schema against (same "do not invent" concern Step 010 already raised for
  `FormatVersion`), and no later-scoped step in this phase (012-015) names networks data as a
  dependency either — the phase's own Section 2 restates that file as "for diagnostics" generally, not
  as a Step 011-specific requirement. Implementing a guessed/unverified networks parser would itself
  violate Section 3's "do not invent xLights effect parameters" guidance. Recorded as an open item in
  §10 rather than silently worked around; Step 009's existing `HasNetworksFile` discovery flag (file
  presence only, already implemented) remains the only networks-related signal exposed to the user.
- **Submodel references are classified by their model half only; the submodel half itself is not
  independently verified.** `xlights_rgbeffects.xml`'s `<model>` elements (as parsed by Step 010) do
  not themselves enumerate a model's own submodel names in a form this step has a verified fixture
  for, so `Arch-01/Segment-1` is accepted as `Submodel` purely because `Arch-01` resolves — a
  genuinely-misspelled submodel name (e.g. `Arch-01/Segmnet-1`) would currently still classify as
  `Submodel`, not `Unresolved`. This is a known, disclosed limitation (§10), not a silent gap: the
  guidance's own "versus groups/submodels" phrasing requires only distinguishing the three *kinds* of
  reference from an outright broken one, which this step does; verifying submodel names specifically
  would require a real xLights-exported fixture with actual `<subModel>` child elements, which was
  not available here (same "no real xLights install/fixture" constraint as §1/§10).
- **Nested-group cycles are not detected.** A group that nests itself (directly or via another group)
  would classify as `Group` at every level without ever being flagged — this step only classifies one
  level of each raw reference; it does not recursively walk the referenced group's own members (by
  design — "Support nested/overlapping groups without flattening" explicitly rules out expansion, and
  cycle detection would require exactly the kind of recursive expansion the guidance says not to do at
  this step). No cycle-detection requirement appears in this step's required acceptance text. Recorded
  as an open item in §10.

## 9a. Critical real-data finding (post-hoc, against the user's own real xLights repo) — later accepted as a migration (§9b)

After the automated/manual work above was already complete, the user pointed me at their own real
show repository, `https://github.com/kevmoens/xlights` (public; branches include `kevin15`, `kevin16`,
`2024`, `2024Christmas`, `macair`). I shallow/partial-cloned it (`git clone --depth 1
--filter=blob:none`) into this session's scratchpad (not into the tracked repository) and checked out
only its two top-level show-configuration files for inspection, commit `19a7975b000dd9160a0630095ac68dfe364f6329`
(2023-10-18):

```text
xlights_rgbeffects.xml  SHA256=89B2616D2EBD06F39E73F984D4781AD2860DE453399B531CF9386C8D8B3FAB6D (47,892 bytes)
xlights_networks.xml    SHA256=B9DE24B523DC8531FE0F5EFBB448E7298E1220C56A698E30F764FFBE7AA959CC (14,282 bytes)
```

Two findings, both significant:

1. **This real, user-owned, xLights-authored file's root element is `<xrgb>`, not `<xrgbEffects>`,
   and the document opens with a stray `<!DOCTYPE html>` declaration** (the literal first three lines
   are `<?xml version="1.0" encoding="UTF-8"?>` / `<!DOCTYPE html>` / `<xrgb>`). Step 010's
   already-accepted, immutable root-element check (`root.Name.LocalName != "xrgbEffects"` →
   `ShowLayoutMalformedException`) would reject this real file outright as malformed. Step 010's
   already-accepted, immutable DTD hardening (`DtdProcessing.Prohibit`, closing the XXE vector) would
   **also** reject it outright, independently of the root-element name, purely because a DOCTYPE is
   present at all — even though this DOCTYPE declares no entities and is not itself an XXE attempt.
   This is very likely an older xLights export format (the per-model `versionNumber="5"` attribute and
   simple `#ip:channel:port`-style `StartChannel`/`String1..9` addressing look materially older than
   the `FormatVersion="2"`-style root attribute this step's own synthetic fixtures assumed).
2. **Real evidence independently confirms this step's own new submodel-reference design is correct**:
   line 250 of the real file is `<modelGroup ... name="Up Win L" ... models="Matrix/Up Win L" .../>`
   — the exact `ModelName/SubModelName` comma-list shape this step's `ClassifyGroupMember` was designed
   around (§2), now confirmed against real data rather than only a synthetic fixture. The `models`
   attribute's comma-separated-name shape itself (§2's existing Step 010 parsing) is also confirmed
   correct against this real file's `<modelGroup>` elements (lines 241-250 and beyond).

**Why this was not acted on immediately:** Step 010's root-element and DTD-prohibition checks were an
already-accepted, immutable contract at the time this finding surfaced (per this task's own
instruction: "Treat all preceding completed step contracts as immutable unless the current step has an
accepted migration plan"). No accepted migration plan existed yet, and loosening DTD handling in
particular is a real security-relevant decision (it directly trades off against the XXE protection
Step 010's own required acceptance tests proved) — not a two-line fix to make unilaterally. I reported
this as a confirmed (not merely hypothetical) finding and asked the user how to proceed, rather than
silently patching Step 010's contract or silently declaring Step 011 unaffected. The user's first
response was "Can you research xlights.org or the xlights open source code or documentation" — §9b
documents that research and the migration it produced, which the user then explicitly approved
("Yes, fix both now").

## 9b. Accepted Step 010 migration: real root-element names + benign-DOCTYPE tolerance

**Research performed (two passes, both against the real xLights source, not assumption):**

*Pass 1* — shallow-cloned `https://github.com/xLightsSequencer/xLights.git` (commit `fa00fd3c`) and
grepped for `xrgbEffects`/`xrgb`/`DOCTYPE`. Findings, each with a citation: real xLights writes and
validates this file's root as `xrgb` (`src-ui-wx/app-shell/TabSequence.cpp:100` writer; `:277`
`if (std::string_view(root.name()) != "xrgb") { DisplayError(...) }`), with a legacy `xlights` root
also accepted as a fallback (`src-iPad/Bridge/iPadRenderContext.cpp:248-253`:
`doc.child("xrgb"); if (!xlightsNode) xlightsNode = doc.child("xlights");`); `xrgbEffects` — this
importer's original root-element assumption — appears nowhere in the xLights source. Real xLights's
own writer (`src-core/XmlSerializer/StreamSerializingVisitor.cpp:25`) never emits a `<!DOCTYPE>` for
this file at all; the only `DOCTYPE` matches anywhere in the source are unrelated bundled HTML assets.
Verdict: the user's real file's `<xrgb>` root is itself the correct, current xLights shape (not
"older" as initially speculated in §9a — current xLights simply calls this format `xrgb`, never
`xrgbEffects`); the `<!DOCTYPE html>` line is not something xLights itself writes and is almost
certainly an editor/tool artifact specific to that file.

*Pass 2* (the user asked me to "double check all elements... and make sure this is the correct XML it
references" before any code change) — re-verified **every** element/attribute this importer already
relied on, against both the real xLights source (`src-core/XmlSerializer/XmlNodeKeys.h` is the
authoritative constants file) and the real sample files (`kevmoens/xlights`, branch `kevin15`). All 9
items checked came back **CONFIRMED, with citation**, and none required a fix:

| # | Assumption | Verdict | Citation |
|---:|---|---|---|
| 1 | Filename `xlights_rgbeffects.xml` | CONFIRMED | `include/globals.h:27` `#define XLIGHTS_RGBEFFECTS_FILE "xlights_rgbeffects.xml"` |
| 2 | `<models>`/`<model>` element names | CONFIRMED | `XmlNodeKeys.h:15-16`; `BaseShowPromotion.cpp:125`; `ModelManager.cpp:2314` |
| 3 | `name` (lowercase) is a model's sole identity | CONFIRMED | `XmlNodeKeys.h:32`; no model-level id/GUID attribute exists anywhere in `src-core/models` |
| 4 | `DisplayAs` holds the model kind | CONFIRMED | `XmlNodeKeys.h:34`; `BaseSerializingVisitor.cpp:117` |
| 5 | `WorldPosX`/`WorldPosY` are the position floats | CONFIRMED | `XmlNodeKeys.h:161-162`; `XmlSerializeFunctions.cpp:29` |
| 6 | `<modelGroups>`/`<modelGroup>` element names | CONFIRMED | `XmlNodeKeys.h:144-145`; `ModelManager.cpp:1336/1641/2172` |
| 7 | Group identity `name` + comma-separated `models` attribute | CONFIRMED | `XmlNodeKeys.h:152`; `ModelGroup.cpp:351-353` `Split(nms, ',', mn, true)` |
| 8 | `ModelName/SubModelName` slash shape + genuine nested-group resolution | CONFIRMED | `ModelGroup.cpp:374-381`; `ModelManager.cpp:232-239` `GetModel`; `ModelGroup.cpp:855-884` `ResetModels()` (comment: "two groups can name each other", with `ContainsModelGroup(... visited)` cycle protection) |
| 9 | `xlights_networks.xml` root/structure | CONFIRMED, root is `<Networks>` (capital N), no casing surprise | `OutputManager.cpp:497`; `BaseShowPromotion.cpp:320` |

Item 8's confirmation is the most load-bearing for this step's own design: it proves `ClassifyGroupMember`'s
submodel/nested-group classification (§2 above) matches a real, actively-used xLights engine behavior,
not a coincidence of one fixture's shape.

**The one real anomaly** — the stray `<!DOCTYPE html>` — was independently re-confirmed in pass 2
directly off the git blob (not a clone artifact), and remained the only unexplained deviation from
real xLights's own writer behavior.

**User's decision:** presented both findings (root element: confirmed defect; DOCTYPE: confirmed real
anomaly, not standard xLights output) and asked how to proceed. The user chose "Yes, fix both now" —
accept `xrgb`/legacy `xlights` roots, and tolerate (not blanket-permit) a benign DOCTYPE. This
constitutes the "accepted migration plan" this task's own rules require before touching an
already-completed step's contract.

**What changed (`src/XlightsAI.Studio.Xlights/ShowLayoutImporter.cs`):**

- `ParseModels`'s root-element check now accepts `root.Name.LocalName` equal to either `xrgb` or
  `xlights` (new `AcceptedRootElementNames` array, ordinal/case-sensitive comparison, matching
  xLights's own case-sensitive `root.name() != "xrgb"` check) instead of requiring the invented
  `xrgbEffects`.
- New `StripBenignDoctypeIfPresent` runs before `ParseSafely`: it scans only the prolog (bounded
  `DoctypeScanWindowChars = 4096` window) for `<!DOCTYPE`. If none is found, bytes pass through
  unchanged (byte-identical to before this migration for every document without a DOCTYPE — proven by
  the full existing regression suite still passing unmodified, §3 below). If one is found: a DOCTYPE
  with an internal subset (`[...]`, where entity declarations live) or any `SYSTEM`/`PUBLIC` keyword —
  the actual XXE vectors — is rejected immediately via `ShowLayoutMalformedException`, *before* any
  further parsing is attempted; XmlReader itself is never even invoked on that content. Otherwise (a
  bare `<!DOCTYPE name>` with no brackets, no external reference) the declaration text is removed and
  the stripped bytes are handed to `ParseSafely`, which still runs with
  `DtdProcessing.Prohibit` unconditionally — the hardening itself is never relaxed; only a narrow,
  pre-verified-benign byte range is removed before that hardened parser ever sees it.
- `ImportReadOnlyAsync` now computes `SourceLayoutHash` from the real, original (un-stripped) bytes,
  and only passes the possibly-stripped bytes to the parser — the hash still identifies the actual
  on-disk document (Step 010's own "identifies which on-disk XML this snapshot came from" contract is
  unaffected), proven by `SourceLayoutHash_ReflectsOriginalBytes_EvenWhenDoctypeIsStripped` (§9c).

**Fixtures updated/added** (all under `tests/XlightsAI.Studio.Xlights.Tests/Fixtures/`, §9c lists the
new ones with hashes): every existing fixture whose content is actually parsed by `ShowLayoutImporter`
and asserted for *success* had its root element renamed from the invented `xrgbEffects` to the real
`xrgb` (`sample-show-complete`, `sample-show-with-geometry`, `sample-show-model-missing-name`,
`sample-show-missing-networks`, and this step's own `sample-show-broken-references`/
`sample-show-no-layout-elements`/`sample-show-modelgroup-missing-name`), plus the equivalent inline
fixture strings in `Step009ShellWiringTests.cs`/`Step010ShellWiringTests.cs` that are parsed via the
real `ShellViewModel` wiring (not just file-discovery-only fixtures, which were left untouched —
`sample-show-wrong-root`, `sample-show-malformed-xml`, `sample-show-oversized`,
`sample-show-xxe-attempt` all fail before ever reaching the root-element check, so their content did
not need changing). Three new fixtures prove the migration itself (§9c).

## 9c. Migration evidence: tests, fixtures, hashes, real UI demonstration

**New tests** (`tests/XlightsAI.Studio.Xlights.Tests/Step011AcceptanceTests.cs`, appended):

```text
Passed RealRootElementName_Xrgb_IsAccepted                               [real "xrgb" root — the actual current xLights shape]
Passed LegacyRootElementName_Xlights_IsAccepted                          [real legacy "xlights" root fallback]
Passed UnrecognizedRootElementName_StillFailsSafely_NegativePath         [migration only widens to the two real names, not to anything]
Passed BenignStrayDoctype_IsToleratedAndStripped_NotRejected             [reproduces the real user file's exact anomaly shape]
Passed DoctypeWithExternalSystemReference_StillRejected_NegativePath     [SYSTEM reference alone, no brackets — still rejected]
Passed PreExistingXxeFixture_StillRejected_AfterMigration_NegativePath   [Step 010's own XXE fixture unaffected by the migration]
Passed SourceLayoutHash_ReflectsOriginalBytes_EvenWhenDoctypeIsStripped  [hash identifies the real on-disk bytes, not the stripped copy]

Total new tests this migration: 7. All passing.
```

**New fixtures** (hashes via `Get-FileHash -Algorithm SHA256` against the checked-in files):

```text
sample-show-legacy-xlights-root/xlights_rgbeffects.xml        SHA256=5238A60E97B943FD67C78D677EB9580875499789636567F1A0EB83C038F340E3
sample-show-stray-doctype/xlights_rgbeffects.xml               SHA256=E339C579840915CA9CC5A64AD21184158C28B5E89506C84B1A0886454EF94A45
sample-show-doctype-external-reference/xlights_rgbeffects.xml  SHA256=57A5FE99867582ED3268F1744C86632554DB9B226470CF00992DCF26231C5216
```

`sample-show-stray-doctype` reproduces the exact structural shape found in the real
`kevmoens/xlights` file (`<?xml version="1.0" encoding="UTF-8"?>` / `<!DOCTYPE html>` / `<xrgb ...>`)
with synthetic model content substituted for the user's real show data (the real file was only read
for research, never committed into this repository).

**Updated fixtures** (root renamed `xrgbEffects` → `xrgb`; content otherwise byte-identical except the
root tag, re-hashed since the bytes changed):

```text
sample-show-complete/xlights_rgbeffects.xml           SHA256=3794EE97C82B82A084C8E9FB899F11B3558D66EBE20D3EA5D68B0043569B9A07
sample-show-with-geometry/xlights_rgbeffects.xml      SHA256=35E766465FD830A420EFFEF40E546EE2F9E1617F2E44108A236EFA831411D432
sample-show-model-missing-name/xlights_rgbeffects.xml SHA256=86F976729E02270DF4E5123CDE236812C4D75091496F66C97FAFA8AB57030647
sample-show-missing-networks/xlights_rgbeffects.xml   SHA256=3794EE97C82B82A084C8E9FB899F11B3558D66EBE20D3EA5D68B0043569B9A07
sample-show-broken-references/xlights_rgbeffects.xml      SHA256=B15341E45E8E39A283DF5C085A12B9C78912C2DFDD7353A68C99ACE66FDFA179
sample-show-no-layout-elements/xlights_rgbeffects.xml     SHA256=9401E80D9441408E42ED36DE826CF98A8792E6E9EFD5CAD362B8F7D08BCE5809
sample-show-modelgroup-missing-name/xlights_rgbeffects.xml SHA256=0D4389564235125DA0C27D99022F3A909885BC5B38CED33EB86632BEB31716C4
```

(The last three supersede the hashes recorded for these same fixtures earlier in §7 of this document —
§7 reflects their pre-migration, `xrgbEffects`-rooted content; this table reflects their final,
post-migration content, which is what the full regression suite in §3/§9d actually exercises.)

**Real UI demonstration (re-run after the migration, replacing the pre-migration screenshots originally referenced in §6):**

The desktop app was rebuilt and re-launched (same real-executable, real-Windows-UI-Automation method
as §6) against the final, post-migration fixture content:

1. `implementation-evidence/PHASE-02/step-011-broken-references-warnings.png` (refreshed) — the
   `sample-show-broken-references` fixture (now `xrgb`-rooted) still shows both parsed groups and the
   real orange `Model group 'Front-Yard' references unresolved target 'Ghost-Prop'.` warning, with a
   real computed `Layout hash: B15341E4...` matching the table above exactly.
2. `implementation-evidence/PHASE-02/step-011-missing-layout-elements-warnings.png` (refreshed) — the
   `sample-show-no-layout-elements` fixture (now `xrgb`-rooted) still shows the two
   `<models>`/`<modelGroups>` warnings with an empty Models/Groups list, `Layout hash: 9401E80D...`
   matching the table above.
3. `implementation-evidence/PHASE-02/step-011-migration-legacy-xlights-root.png` (new) — a real
   directory containing only `sample-show-legacy-xlights-root`'s `<xlights>`-rooted content imports
   successfully: Models shows `Arch-01`, Groups shows `Front-Yard`, `Layout hash: 5238A60E...` matching
   the table above exactly — real, end-to-end proof the legacy root fallback works through the full
   UI → application → domain → adapter slice, not just the importer in isolation.
4. `implementation-evidence/PHASE-02/step-011-migration-stray-doctype-tolerated.png` (new) — a real
   directory containing `sample-show-stray-doctype`'s content (reproducing the real anomaly's exact
   `<?xml?>`/`<!DOCTYPE html>`/`<xrgb>` shape) also imports successfully: Models shows `Arch-01`,
   Groups shows `Front-Yard`, `Layout hash: E339C579...` matching the table above exactly — the hash
   is computed from the real original bytes (DOCTYPE line included), proving §9b's "hash reflects
   original bytes" design end-to-end, not just in the unit test.

## 10. Unresolved issues / risks carried forward

1. **No installed, runnable xLights executable was available on this machine** (§1) — still true, and
   still Step 014's own blocking dependency, not this step's. However, the *source-code-level*
   question (what real xLights actually reads/writes for this file format) was independently resolved
   in full via §9a/§9b by cloning the real xLights source and a real user's show repository — the
   earlier, narrower version of this risk (no real xLights-authored file available at all to compare
   against) no longer applies; see §9b's 9-item verification table. `xlights_networks.xml`'s root
   structure was also confirmed (`<Networks>`, no casing surprise) even though its contents are still
   not parsed (item 2 below).
2. **`xlights_networks.xml` is not parsed** (§9) — only its presence/absence is surfaced (Step 009's
   existing `HasNetworksFile`). If a later step needs actual network/controller diagnostic data, it
   will need its own verified fixture and its own step.
3. **Submodel references are only half-verified** (§9) — the model half is checked against real parsed
   models; the submodel half is accepted on shape alone. A misspelled submodel name is not currently
   distinguishable from a correct one. Flagged for a future step with a real xLights submodel fixture.
4. **No nested-group cycle detection** (§9) — a self-referencing or mutually-referencing group chain
   would not be flagged as a problem by this step; each reference classifies independently and
   correctly as `Group`, but no cross-group recursive walk occurs.
5. **RESOLVED this step (§9a/§9b), carried-forward item superseded**: the root-element name
   (`xrgbEffects` → real `xrgb`/legacy `xlights`) and the stray-DOCTYPE-tolerance defects found
   against the user's real show repository are now fixed and regression-tested (§9b/§9c — 7 new
   migration tests, all passing). **`FormatVersion` itself is still not validated** — Step 010's
   original §10 item 1 concern — but is now a narrower, lower-severity gap than before this migration:
   the blocking defect (real files couldn't be imported *at all*) is fixed; what remains is that a
   genuinely incompatible future/unsupported `FormatVersion` value would still parse as far as its
   `<models>`/`<modelGroups>` shape allows, with no explicit compatibility error. Still not in this
   step's own scope; still carried forward.
6. **This step's manual verification used the real `%AppData%\XlightsAI.Studio\settings.json`** (§6) —
   same precedent as PHASE-01 Steps 004-007 and PHASE-02 Steps 009-010, carried forward, disclosed
   again here.
7. Git Bash remains non-functional in this environment (carried forward from every prior step); every
   command in this evidence was run via PowerShell.
8. **Step 012 (hardcoded three-effect plan on selected real props) is the natural next consumer of this
   step's validated `DisplayGraph`** — not started by this step, per its own narrow scope.

## 11. Gate checklist (this step only)

- [x] Identifies targetable models versus groups/submodels against this same document's own real
      models and group names (§2, §4, §5).
- [x] Rejects (classifies as `Unresolved`, non-destructively) broken references rather than silently
      treating them as resolved (§4, §5, §6).
- [x] Supports nested/overlapping groups without flattening — a nested group reference stays a
      reference, never expanded into its own members (§4, §5).
- [x] Fixture with broken references generates warnings rather than partial destructive output —
      required acceptance, proven exactly, including a real UI demonstration (§4, §5, §6).
- [x] Missing layout elements (`<models>`/`<modelGroups>` entirely absent) produce warnings, not a
      failure, and never guess/invent content (§4, §5, §6).
- [x] Invalid model/format/version fails safely without rewriting source files — this step's new
      negative-path test, byte-comparing the source file before/after a failed parse (§4, §5).
- [x] Full regression suite re-run, final (post-migration): 123/123 passing (112 PHASE-01/Step009-010
      baseline + 4 Step 011 content tests + 7 Step 011 migration tests), zero regressions (§3a, §9c).
- [x] `dotnet format --verify-no-changes` passes cleanly against every new and modified file (§3a).
- [x] No original/show file or PHASE-01 file touched; no Step 009/010 test *assertion* touched — the
      Step 010 production files edited, and the Step 009/010 fixture/inline-XML *content* updated by
      the accepted §9a/§9b migration, are all disclosed in full with the user's explicit approval
      recorded (§5, §9, §9a, §9b).
- [x] Architecture deviations disclosed in full (§9), per this task's explicit instruction.
- [x] **Immutable-contract migration handled correctly**: a confirmed defect in an already-accepted
      prior step's contract (Step 010's root-element/DOCTYPE handling) was found via real-world data,
      reported rather than silently patched, researched against the real xLights source on explicit
      user request (two full passes, §9b), and only changed after the user's explicit approval —
      matching this task's own "immutable unless accepted migration plan" rule exactly.
- [x] Real UI demonstration performed (not simulated/mocked) — four screenshots via real Windows UI
      Automation against the real running app: broken-reference and missing-layout-elements (refreshed
      post-migration) plus two new migration-specific demonstrations (legacy `<xlights>` root; the real
      stray-DOCTYPE shape) (§6, §9c). No installed xLights executable was required or available for
      this step's own scope (read-only parsing/validation only; Step 014 owns the real-xLights-render
      gate) — but the real xLights *source code* and a real user's *show repository* were both
      consulted and cited with file/line precision (§9b), going beyond a search that found nothing.

**Step 011 status: PASS, including an accepted Step 010 migration (§9a/§9b).** Steps 012-015 remain
unimplemented, as instructed ("do not execute the next step until this step is proven").
