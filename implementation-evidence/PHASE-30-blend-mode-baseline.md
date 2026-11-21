# Phase 30 step 30.0 — Blend-mode XML confirmation and baseline (`blend-mode-baseline`)

**Result: PASS**, with one honestly-recorded evidence-strength gap (exact target version 2026.03 was not
directly available on the verification machine; see "Version gap" below) and a human-preview item still
open (tracked, not faked).

## What this step had to establish

Per the build plan's own acceptance definition: *"before any blend mode other than the pipeline's current
default ships in generated output, its exact xLights `2026.03` XML key/value is confirmed against a real
XSQ fixture checked into the repo... Until confirmed for a given mode, that mode is excluded from the
generator's choices, not guessed."*

## 1. How the exact XML values were confirmed (not guessed)

**Real xLights install found on the verification machine:** `C:\Program Files\xLights\xlights.exe`,
build **2026.17** (confirmed via `C:\Users\<user>\AppData\Roaming\xLights\Logs\xLights_spdlog.log`:
`Version: 2026.17`) — not the plan's pinned target `2026.03`. See "Version gap" below for how this is
handled.

**Real author-created `.xsq` show files found** at `C:\xLights\*.xsq` (35 files, a real multi-year family
Christmas-light show library, versions `2020.47` through `2026.17` inclusive per each file's own
`<head><version>`). These are genuine human-authored sequences, not Studio-exported fixtures.

**xLights' own `effectmetadata` does NOT enumerate this control's choice list.** The Blending effect
panel's `T_CHOICE_LayerMethod` control is documented in `effectmetadata/shared/Blending.json` only as:

```json
{
    "id": "LayerMethodRow", "controlType": "custom",
    "description": "Custom row: Layer Method choice + '?' help button. Serialized as T_CHOICE_LayerMethod."
}
```

— a custom widget with no enumerated values. Confirmed the repo's own checked-in copy
(`tests/XlightsAI.Studio.Xlights.Tests/Fixtures/effectmetadata/shared/Blending.json`) is **byte-identical**
(`diff` returned nothing) to the real installed file, i.e. this gap is real, not a stale copy in the repo.
This is why the only honest source for the exact values is real authored content, not xLights' own schema
metadata — and why an LLM asked to "just know" these values would be guessing.

**Method:** grepped every real `.xsq` file in `C:\xLights` for literal `T_CHOICE_LayerMethod=<value>`
occurrences (the control is serialized as a `key=value` pair inside a comma-separated effect-settings
string, itself the text content of an `<Effect>` element in the file's `<EffectDB>` dedup table — not a
quoted XML attribute). Full tally across all 35 files (occurrence counts):

| Value found | Count | Candidate this maps to |
|---|---:|---|
| `Average` | 108 | **Average** |
| `Layered` | 53 | (not a build-plan candidate) |
| `1 reveals 2` | 44 | **Reveal 1-on-2** |
| `2 is Mask` | 38 | **Mask** |
| `2 reveals 1` | 36 | **Reveal 2-on-1** |
| `1 is Unmask` | 36 | **Unmask** |
| `Effect 1` | 21 | (not a build-plan candidate) |
| `1 is Mask` | 20 | **Mask** (other-layer spelling) |
| `Brightness` | 19 | (not a build-plan candidate) |
| `1 is True Unmask` | 16 | (not requested; a stronger Unmask variant) |
| `Normal` | 12 | pipeline's existing default |
| `2 is Unmask` | 11 | **Unmask** (other-layer spelling) |
| `Additive` | 5 | (not a build-plan candidate) |
| `Shadow 1 on 2` | 4 | **Shadow 1-on-2** |
| `Bottom-Top` | 4 | (not a build-plan candidate) |
| `Shadow 2 on 1` | 3 | **Shadow 2-on-1** |
| `Min` | 3 | (not a build-plan candidate) |
| `Subtractive` | 2 | **Subtractive** |
| `Left-Right` | 2 | (not a build-plan candidate) |
| `Effect 2` | 2 | (not a build-plan candidate) |
| `2 is True Unmask` | 2 | (not requested) |

**All 8 build-plan candidate modes are confirmed.** No mode is excluded.

Values not among the plan's 8 named candidates (`Layered`, `Effect 1`/`Effect 2`, `Brightness`,
`Additive`, `Bottom-Top`, `Min`, `Left-Right`, the `True Unmask` variants) are real and equally confirmed,
but out of scope for this phase per the plan's own candidate list — recorded here for completeness, not
added to the confirmed catalog.

**Mask/Unmask directionality:** both carry two real spellings depending on which of the two stacked
layers is the mask/unmask layer (`"1 is Mask"`/`"2 is Mask"`, `"1 is Unmask"`/`"2 is Unmask"`). The
catalog (below) carries the `"2 is ..."` spelling for each as the single canonical direction per
candidate name — both spellings are equally real/confirmed, this is a selection for planner simplicity,
not a gap.

### Reconfirmed independently at the installed version (2026.17)

Only 4 of the 35 files are themselves authored at the installed 2026.17 build. One of them
(`First 7 songs Christmas.xsq`) was inspected directly and independently reconfirms, at the exact
installed version:

- `1 reveals 2`, `2 is Mask`, `2 reveals 1` (plus non-candidate `Effect 1`, `Layered`)

`Average`, `Unmask`, `Subtractive`, `Shadow 1 on 2`, `Shadow 2 on 1` were not present in any of the four
2026.17-authored files sampled — they remain confirmed only via the older-version files (back to
2020.47). The exact string spelling has been identical across every version sampled (2020.47–2026.17,
spanning 6 years of real authored content), which is strong evidence of format stability, but this is
recorded honestly as weaker evidence than a direct same-version hit, not silently treated as equivalent.
See `ConfirmedLayerBlendMode.ConfirmedAtInstalledVersion` in the catalog (below), which carries this
distinction per mode rather than asserting uniform confidence.

### Version gap: 2026.03 (plan's pinned target) vs 2026.17 (installed)

The build plan pins verification to xLights `2026.03` (matching Phase 29's own fixtures). No exact
2026.03 install was available on the verification machine — the real install present is 2026.17, and the
35 real authored files span 2020.47–2026.17 with no file landing exactly on 2026.03. This gap is recorded
transparently rather than closed by assumption:

- The xLights automation REST API (`xFade`/MCP, documented in the installed `xlDo Commands.txt`) was
  explored as a way to construct and round-trip a purpose-built fixture directly against a running
  install, which would have let this step verify against whatever exact build was targeted. It did not
  pan out in this environment: the default automation ports (49913/49914) were already bound by unrelated
  local tooling (`RemoteDesktopCompanion.exe`, part of this remote session's own infrastructure — not
  touched), and after reassigning the port (`xFadePort` in both the registry and
  `%AppData%\xLights\settings.json`) and restarting, the listener still never bound on the new port with
  no corresponding log activity — the toggle this build needs is not reachable through either config
  surface alone, and no GUI-automation tool was available in this environment to find it the other way.
  All environment changes made in pursuit of this (`xFadePort` in two places) were reverted to their
  original values (`0`) afterward; no lasting change was left on the machine.
- Given that, this step relies on the real-authored-file evidence above. The fixture
  (`tests/XlightsAI.Studio.Xlights.Tests/Fixtures/blend-modes/blend-mode-exemplars.xsq`) is a minimal,
  Studio-constructed 2-layer example (not a real authored show file — real shows are large, licensed,
  multi-song family content, not appropriate to check into this repo even in part) using the exact
  confirmed string values and the same `EffectDB`-dedup/`ref`-indexed `EffectLayer` grammar this repo's
  own `XsqExporter`/`XsqStructuralReader`/golden fixture already use and real files confirm. Its
  `<head><version>` is stamped `2026.03` to match the plan's pinned target, identical to the existing
  `golden-three-effect-sequence.xsq` convention (that fixture is also Studio-constructed with a stamped
  `2026.03` version, not literally authored in a 2026.03 install) — this is documented inline in the
  fixture's own `<comment>` element, not hidden.
- **Net effect:** the exact string values are real and confirmed; the specific xLights build version they
  were drawn from is 2026.17, not the pinned 2026.03, and that substitution is the one piece of this
  step's evidence that is "same format, adjacent version" rather than "exact version." Given the format
  has been byte-identical across 6 years of real content spanning both sides of 2026.03, this is judged
  sufficient to unblock step 30.1, but is flagged here for whoever performs the human preview gate (below)
  to double-check if a true 2026.03 install becomes available.

## 2. Confirmed catalog (what later steps may actually emit)

`src/XlightsAI.Studio.Knowledge/ConfirmedLayerBlendModes.cs` — `ConfirmedLayerBlendModeCatalog`:

| Candidate | Confirmed XML value | Confirmed at installed version (2026.17)? |
|---|---|---|
| Mask | `2 is Mask` | yes |
| Unmask | `2 is Unmask` | no (confirmed 2020.47–2024.13 era only) |
| Reveal 1-on-2 | `1 reveals 2` | yes |
| Reveal 2-on-1 | `2 reveals 1` | yes |
| Average | `Average` | no |
| Subtractive | `Subtractive` | no |
| Shadow 1-on-2 | `Shadow 1 on 2` | no |
| Shadow 2-on-1 | `Shadow 2 on 1` | no |

`ConfirmedLayerBlendModeCatalog.IsConfirmedXmlValue(...)` is the gate a later step (30.1's
`LayerBlendPlanner`) must consult before emitting any `T_CHOICE_LayerMethod` value — any string not in
this table (including a plausible-looking non-candidate real value like `"Brightness"` or `"Bottom-Top"`)
is rejected. No mode is excluded from this phase's candidate list; all 8 are confirmed.

## 3. Fixture checked in

`tests/XlightsAI.Studio.Xlights.Tests/Fixtures/blend-modes/blend-mode-exemplars.xsq` — one model
("Everything") with two `EffectLayer`s: a base "On" effect spanning the full 8s duration, and a second
layer with 8 back-to-back 1s "Bars" effects, each referencing a distinct `EffectDB` entry carrying one of
the 8 confirmed `T_CHOICE_LayerMethod` values — i.e. each candidate mode is exercised between two known,
time-overlapping layers, per the acceptance definition's own phrasing.

## 4. Tests written and run

`tests/XlightsAI.Studio.Xlights.Tests/Phase30BlendModeFixtureTests.cs` — **19/19 pass.** Verifies: the
fixture's own two-layer/overlapping-span structure; each of the 8 confirmed catalog values appears,
spelled exactly, in the fixture's `EffectDB` (via the existing `XsqStructuralReader`, not a new parser);
the catalog contains exactly the 8 build-plan candidates and no more; each mode's
`ConfirmedAtInstalledVersion` flag matches the evidence above; `IsConfirmedXmlValue` accepts every
catalog value plus the default and rejects a real-but-non-candidate value (`"Brightness"`) and a
plausible-but-wrong guess (`"Bottom-Top"`, which is itself real but not a build-plan candidate).

Full `XlightsAI.Studio.Xlights.Tests` project: **120/120 pass** (no regressions).

## 5. Baseline measurement (current generator output, before any blend-mode behavior ships)

`tests/XlightsAI.Studio.Director.Tests/Phase30BlendModeAndEffectTypeBaselineTests.cs` — **1/1 pass**
(3m43s). Reuses the existing synthetic/scripted-AI-response harness convention (same shape as
`Phase6EndToEndRealTrackTests`/`Phase29TimelineGenerationTests`/`tools/Phase29Baseline`): real audio
analysis of the two existing baseline clips (`carrie-30s-clip.mp3`, `chappell-30s-clip.mp3`, the same two
songs `PHASE-29-baseline-and-blockers.md` already used for baseline measurement), a hand-authored Show
Bible, and a deterministic scripted Choreographer client (no live LLM, no network) producing a real
exported XSQ via `XsqExporter`, parsed back with the existing `XsqStructuralReader`. Hit and fixed the
same out-of-bounds-beat issue Phase 29's own baseline run recorded for these two clips.

Full non-integration `XlightsAI.Studio.Director.Tests` suite: **317/317 pass** (no regressions;
integration-tagged tests requiring a live local Ollama were not run, unrelated to this change).

Real measured results (also written to `implementation-evidence/PHASE-30-BASELINE-RUN.md`):

| Song | Total effect placements | `T_CHOICE_LayerMethod` settings | Raw effect-type counts (pre-family-grouping) |
|---|---:|---:|---|
| Carrie Underwood - Away In A Manger Lite (30s clip) | 36 | 0 | On=18, Bars=8, Twinkle=6, SingleStrand=4 |
| Christmas Tree Hot to Go - Chappell Roan (30s clip) | 36 | 0 | On=13, Twinkle=11, Bars=4, SingleStrand=4, Color Wash=2, Butterfly=2 |

**Blend-mode baseline: 0 `T_CHOICE_LayerMethod` settings across both songs — 100% implicit default**,
matching the acceptance definition's "expect ~100% default" exactly, confirmed empirically against real
exported XSQ output (not just cited from `XsqExporter.BuildSettingsString`'s own doc comment, which
independently documents the same thing: `BlendMode`/`EffectTransition` are "deliberately NOT serialized
here... They remain IR-only").

**Effect-family concentration baseline: not computed, and not invented.** The build plan's own current-state
section already flags this precisely: `EffectVisualCharacterCatalog`/`EffectVisualCharacter` (Phase 18/21)
carries per-effect brightness tier but **no family-grouping field at all** — confirmed by reading
`src/XlightsAI.Studio.Knowledge/EffectVisualCharacter.cs` in full. Step 30.4 is where that field and a real
family taxonomy belong; inventing one here to produce a "concentration" number would be exactly the kind
of guessed/fabricated infrastructure this build plan's own discipline forbids. What's recorded instead is
the simpler, honestly-labeled thing that IS computable today: raw per-effect-name placement counts per
song (table above) — step 30.4 can regroup these same counts by family once that field exists, which is
the "for later comparison" baseline the acceptance definition actually asks for, scoped to what exists.

## 6. Human preview gate

**Not performed — recorded as open, not faked.** Per the acceptance definition, blend-mode variety (not
yet generated — that's 30.1) and this step's own fixture are meant to be eventually judged by a named
person previewing in real xLights. No reviewer/preview has been supplied for this step. Consistent with
Phase 29's own precedent (`PHASE-29-xsq-verification.md`: "human preview not performed... The
tool-generated XSQs are not a substitute"), this is not treated as blocking 30.0's own automated gates,
which are about XML confirmation and baseline measurement, not about generated creative output (nothing
has been generated with blend-mode variety yet — that begins at 30.1).

## Gate check (per the build plan's own wording)

> "every blend mode used in later steps has a confirmed fixture; modes without confirmation are recorded
> as excluded, not silently skipped."

All 8 build-plan candidate modes are confirmed with exact XML values, backed by a checked-in fixture and
a passing test. **No mode is excluded.** Step 30.1 (blend-mode mining and selection) may proceed.
