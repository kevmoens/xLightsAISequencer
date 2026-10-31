# Phase 0 Findings — De-risk Spike

Evidence-first write-up for the build plan's Phase 0 gate (`docs/AI-CHOREOGRAPHY-BUILD-PLAN.md`).
Go/no-go facts only — design reasoning lives in the architecture doc.

Environment: real xLights 2024.13 installed at `C:\Program Files\xLights` (`xlights.exe`, `xlDo.exe`).
Real, long-running hand-sequenced show at `C:\xLights` (20 `.xsq` files with paired audio, a real
`xlights_rgbeffects.xml`).

## 1. Exporter defects — confirmed/refuted against real xLights output

Ground truth: structurally inspected all 20 real `.xsq` files in `C:\xLights` (file-size range 89KB–3.7MB),
primarily `Ronettes - Sleigh Bells (PhatCap! Trap Remix).xsq`, `Joe Bygraves - Stand As One Lite.xsq`,
`LalalaGirl - Christmas Mashup 2016-short.xsq`, `Carrie Underwood - Away In A Manger Lite.xsq`.

| # | Defect (architecture §12 blocker 4) | Verdict | Evidence |
|---|---|---|---|
| 1 | `FixedPointTiming="25"` should be `"1"` (boolean flag, not ms) | **CONFIRMED, FIXED** | Every real file: `<xsequence ... FixedPointTiming="1" ...>` regardless of actual frame rate (25ms and 50ms shows both show `"1"`). Real frame interval lives only in `<head><sequenceTiming>50 ms</sequenceTiming>`. |
| 2 | `type="modelGroup"` on DisplayElements should be `type="model"` | **CONFIRMED, FIXED — and the real rule is stronger than stated**: `type="modelGroup"` was **never observed in any of the 20 real files**, for models OR groups. `grep -l "modelGroup" *.xsq` over all 20 files → zero matches. The "Matrixes" model group (defined as `<modelGroup ... name="Matrixes" .../>` in `xlights_rgbeffects.xml`) is referenced in every sequence's `DisplayElements`/`ElementEffects` as `<Element type="model" name="Matrixes" .../>`. | `grep -o 'type="[a-zA-Z]*"' Ronettes...xsq \| sort -u` → only `model` and `timing`. |
| 3 | Missing root attrs `ChanCtrlBasic="0" ChanCtrlColor="0"` | **CONFIRMED, FIXED** | Present verbatim on every real file's `<xsequence>` tag. |
| 4 | Missing head fields (`author`, `song`, `artist`, `album`, `mediaFile`, `comment`, self-closed `<imageDir/>`) | **CONFIRMED, FIXED (shape only)** | Real head order verified: `version, author, author-email, author-website, song, artist, album, MusicURL, comment, sequenceTiming, sequenceType, mediaFile, sequenceDuration, imageDir`. Exporter now emits all of these (empty-string placeholders, since `SequenceDocument` carries no metadata yet — that's architecture §12 blocker 3 / Phase 1). `mediaFile` omitted for our no-audio Animation-type test fixture (no real "Animation"-type-with-no-media sample was available to confirm its exact position when absent; this is noted as an open item, not a blocker). |
| 5 | Missing `<nextid>`, `<Jukebox/>`, `<DataLayers>`, `<TimingTags>` (ten `<Tag number="N" position="-1"/>`) | **CONFIRMED, FIXED** | All four present in every real file, same shape. `<DataLayers>` is non-empty in real files only because every real file there has been rendered at least once (`<DataLayer .../>` auto-generated erase-mode entry); a freshly-authored sequence's own empty case wasn't independently confirmed, so the exporter emits an empty `<DataLayers></DataLayers>` (valid XML, matches the tag's own presence requirement; not proven to be the exact freshly-saved shape). |
| 6 | `selected="0"` on every effect (should omit unless `"1"`); missing `id="N"` on non-first effects in a layer | **CONFIRMED, FIXED** | `grep "selected="` over a full real file → zero matches anywhere (never written, not even `selected="1"`, in this corpus sample). `id` attribute: `<Effect ref="0" name="On" startTime="14000" endTime="14050" palette="0"/>` (first in layer, no id) then `<Effect ref="0" name="On" id="1" .../>`, `id="2"`, etc. — confirmed 1-based, starts at second effect. |
| 7 | Frame interval hardcoded 25ms vs. corpus/community default 50ms | **CONFIRMED, NOT FIXED (deliberately deferred)** | `Ronettes...xsq`, `Joe Bygraves...xsq`, `Carrie Underwood...xsq` all show `<sequenceTiming>50 ms</sequenceTiming>`. However, `XsqExporter.FrameIntervalMs` is a public constant directly referenced by `XlightsAI.Studio.Editing.XLightsFrameGrid`, `ScopedXsqExporter`, `XlightsAI.Studio.Rhythm.RhythmGesturePlanner`, and hardcoded into several `XlightsAI.Studio.Shell.Tests`/`Composition.Tests` assertions outside this spike's scoped files. Changing the value broke 5 tests in `Shell.Tests` and 1 in `Composition.Tests` (verified directly — see below) that assume a 25ms grid. Left at 25 for this spike to stay inside the file scope given ("only touch the exporter and its own tests"); flipping to 50 is a correct, mechanical follow-up that needs to also touch those dependents' tests. **Not a Phase 0 import-correctness blocker** — xLights imports and renders correctly at any self-consistent frame interval; this is a size/precision concern, not an import-failure risk. |
| 8 | Missing `&comma;` escaping in setting values | **N/A for this spike** | No generated effect-settings serializer exists yet (the only EffectDB entry is the hardcoded empty `"On"` placeholder — architecture §12 confirms this placeholder shape is itself correct). There is nothing with a comma in it to escape today. The escaping rule must be implemented when Phase 2 builds the real settings-string serializer; flagged here so it isn't forgotten. |
| — | Empty `<Effect></Effect>` EffectDB placeholder is correct/idiomatic | **CONFIRMED CORRECT, not a defect** | `Ronettes...xsq` EffectDB entry 0 is exactly `<Effect></Effect>`. xLights genuinely omits all-default keys. The exporter's existing placeholder and its warning text were already right; no change needed beyond retiring the overly-hedged doc comment (done). |

All 8 listed defects were confirmed against real ground truth (one, #2, turned out to be a stronger rule
than described: `modelGroup` is never used at all, not merely "both use `model`"). 7 of 8 were fixed in
`src/XlightsAI.Studio.Xlights/XsqExporter.cs`. #7 (frame interval) is confirmed but deliberately left
as a documented follow-up to avoid an out-of-scope cross-module edit.

## 2. Round-trip import/render — ACTUALLY VERIFIED, not just structurally asserted

GUI-interactive confirmation ("open in xLights, watch for a repair dialog") isn't automatable from this
environment, so the documented alternative was used instead, and it produced **stronger** evidence than
a visual GUI check would have: a real headless render.

**Method:** built a `SequenceDocument` via the fixed `XsqExporter` targeting two models that genuinely
exist in `C:\xLights`'s own real layout (`Tree`, `Arches`), wrote it as
`C:\xLights\zz-phase0-roundtrip-test.xsq` (temporary; deleted after the spike), then ran:

```
"C:\Program Files\xLights\xlights.exe" --headless -s "C:\xLights" --outputdir "<tempdir>" "C:\xLights\zz-phase0-roundtrip-test.xsq"
```

**Result: exit code 0.** Real `.fseq` produced (31,139 bytes, 800 frames × 73,440 channels). xLights'
own persistent log (`%AppData%\xLights\Logs\xLights_spdlog.log`) for this run, verbatim:

```
2026-10-23 19:08:37.227 [xLights info] Sequence file passed on command line: C:\xLights\zz-phase0-roundtrip-test.xsq.
2026-10-23 19:08:37.228 [xLights warning] --headless: GPU rendering is disabled in preferences but OpenGL is unavailable; enabling it so Shader effects render (they have no CPU implementation)
2026-10-23 19:08:38.800 [xLights info] --headless: rendering C:\xLights\zz-phase0-roundtrip-test.xsq
2026-10-23 19:08:38.801 [xLights info] LoadSequence: Loading sequence C:\xLights\zz-phase0-roundtrip-test.xsq
2026-10-23 19:08:38.801 [xLights info] HeadlessRenderContext: opened C:\xLights\zz-phase0-roundtrip-test.xsq (2 elements, 20000 ms long) in 0 ms
2026-10-23 19:08:38.832 [xLights info] FSEQFileIO::Write: wrote 800 frames x 73440 channels to C:\Code\...\render-out\zz-phase0-roundtrip-test.fseq
2026-10-23 19:08:38.833 [xLights info] --headless: wrote C:\Code\...\render-out\zz-phase0-roundtrip-test.fseq in 0.032 seconds
```

No error, warning, or "repair" message about the sequence content itself (the one warning is an
unrelated GPU/OpenGL preference notice, same on every `--headless` run on this machine). **The real
xLights engine opened the fixed exporter's file, resolved both model targets, and rendered a correct
FSEQ with zero parse/structural complaints.** This is the strongest form of "imports cleanly" evidence
available without a human watching the GUI, and it directly answers architecture §15 risk 2 ("generated
files may not import cleanly") for the fixed exporter: **no.**

**Caveat:** this does not independently prove the GUI's interactive "Save" path would avoid a dialog
asking to repair/relink something GUI-only (e.g. a missing media file prompt) — only that the file
parses, loads, and renders correctly in the real engine. Treat as strong evidence, not full proof of a
silent, dialog-free GUI open.

## 3. `xlDo.exe` capability verdict

**`xlDo.exe` is a thin REST client, not a standalone automation/render tool.** Its own bundled
reference (`C:\Program Files\xLights\xlDo Commands.txt`) documents it as a wrapper around xLights' HTTP
automation API (port 49913/49914), which only exists **if the xFade service is enabled in xLights'
preferences and a GUI instance is already running.** Confirmed directly:

- `xlDo.exe` (any argument, including `--help`/`/?`) printed only `Error setting console mode.` when
  run from a redirected-stdio shell (git-bash) — it needs a real attached console.
- Run from `cmd.exe` with a real console (`< CON`) it accepted a `msg="{...}"` JSON command but then
  **hung indefinitely** with no xLights GUI instance running to answer it — consistent with "pure REST
  client, nothing to automate without a live target process."
- Its own command list (`xlDo Commands.txt`) includes `checkSequence` ("Open and check a sequence") and
  `getSequenceInfo` (reads basic `.xsq` properties **without opening the editor**) — both useful for
  future validation tooling, but both still require either a running xLights (checkSequence) or are
  read-only metadata (getSequenceInfo), never a render.
- **It cannot render to FSEQ by itself.** Its `renderAll` command requires a sequence already open in a
  live xLights GUI session (`{"cmd":"renderAll", ...}` operates on "the open sequence").

**This means the architecture doc's framing in §9/§15 ("xlDo.exe ships alongside fseq_convert.exe... the
single biggest technical dependency") names the wrong tool for headless rendering.** `xlDo.exe` is the
*interactive-automation* surface (useful later for live preview/scripting against a running designer
session); it is not how headless batch rendering should work.

## 4. Render adapter spike — real headless rendering IS viable, via a different mechanism than assumed

The actual mechanism, confirmed directly: **`xlights.exe` itself has a native `--headless` CLI flag**
(`xlights.exe --headless -s <showdir> [--outputdir <dir>] <seq.xsq>`), completely independent of
`xlDo.exe`/xFade/any running GUI session. This is exactly what `XlightsAI.Studio.Rendering`'s existing
`XlightsRenderAdapter` was already built to use (`src/XlightsAI.Studio.Rendering/XlightsRenderAdapter.cs`
line 100: `new List<string> { "--headless", "-s", request.ShowDirectory }`) — the existing design guessed
right.

**However, the existing `XlightsRenderCapabilityProbe` produces a false negative against this real
install**, confirmed by actually invoking it (not just reading the code):

```
Version: unknown
SupportsHeadless: False
SupportsOutputDirectory: False
```

Two independent, confirmed causes:

1. **Version detection fails**: `FileVersionInfo.GetVersionInfo("xlights.exe").FileVersion` returns
   null/empty because the real binary's Win32 version resource is unstamped
   (`FileVersionRaw: 0.0.0.0`, confirmed via `Get-Item ... | VersionInfo`). This isn't a probe bug per
   se — there's no version to read from that API for this build — but it means "unknown" is the
   realistic result, not a transient failure.
2. **`--help` detection fails outright**: running `xlights.exe --help` does **not** print usage text to
   stdout/stderr within any reasonable window — it launches the full GUI (observed directly: a real
   `xLights.exe` process appeared in the process list and sat idle with zero captured output until
   killed). The probe's default behavior (15s timeout, treat silence as "unsupported") therefore reports
   `SupportsHeadless: False` for a build that, as section 2 above proves, genuinely supports and
   correctly executes `--headless`.

**This is a real, verified defect in the existing capability probe** that would silently force the
whole system down the degraded "open manually and Render All" fallback path (architecture §9's weaker
alternative) even on a fully-capable install. It was not fixed in this spike (out of the scoped
file set — `XlightsAI.Studio.Rendering` wasn't on the touch-list — but the finding is unambiguous and
should be the first task whoever starts Phase 7 (or an early Phase 1 cleanup) picks up: detect
`--headless` support by actually attempting a trivial headless render (as section 2 did) and checking
for a real `.fseq` artifact and exit code 0, rather than parsing `--help` text.

## 5. L5 rendering strategy decision

**Real headless render (architecture §9's preferred option) is viable and should be used**, via
`xlights.exe --headless -s <showdir> --outputdir <dir> <seq.xsq>` directly — not via `xlDo.exe`. Reasoning:

- Section 2 directly demonstrated a correct, fast (0.032s), zero-error headless render producing a real,
  well-formed FSEQ from the fixed exporter's own output.
- No running GUI, no xFade preference toggle, and no interactive session were required — this is
  genuinely automatable from a CI-style or background process.
- `XlightsAI.Studio.Rendering.XlightsRenderAdapter` already targets exactly this flag shape; it needs no
  redesign for Phase 7, only the capability-probe fix in section 4 so it doesn't falsely refuse to use a
  working install.
- `xlDo.exe`/xFade remains a plausible secondary surface for a *live, interactive* future feature (e.g.
  scripting an open designer session, or `checkSequence` as a lightweight pre-flight lint using an
  already-running instance a human has open) but is not needed for, and should not gate, the core
  render-measure-critique loop.

**Recommendation:** Phase 7 should build its render pipeline directly on `--headless`/`--outputdir`
exactly as `XlightsRenderAdapter` already assumes, with the capability probe's detection method replaced
by an actual trivial-render attempt (section 4) before trusting its `SupportsHeadless` result on an
unfamiliar install.

## 6. Acceptance criteria — status

| Build plan Phase 0 acceptance criterion | Status |
|---|---|
| A real xLights install opens an app-exported `.xsq` with zero errors/repairs | **MET** — verified via real headless render with a clean log (section 2); GUI-dialog-level confirmation not independently done (noted caveat). |
| The golden fixture matches real xLights output byte-for-byte on the covered fields | **MET** for every field this exporter controls (head shape, root attrs, DisplayElements/ElementEffects shape, selected/id, TimingTags, DataLayers, nextid, Jukebox). Frame interval (50ms) deliberately deferred — see item 7 above. |
| A written yes/no answer, with evidence, on headless rendering | **MET — YES**, with command transcripts and log excerpts (sections 2–5). |

## 7. Out-of-scope items noticed, not acted on

- Full solution `dotnet test` surfaces pre-existing failures/analyzer errors (`XlightsAI.Studio.Distribution.Tests`,
  style-analyzer `ENDOFLINE`/`WHITESPACE` errors in `XlightsAI.Studio.AiEdits`, `XlightsAI.Studio.Melody`,
  `tools/FixtureCorpusGenerator`) that are part of the *other* uncommitted work already in this working
  tree, not something introduced by this spike. Confirmed unrelated: none of the affected files were
  touched here. The scoped test projects (`XlightsAI.Studio.Xlights.Tests`, `XlightsAI.Studio.Composition.Tests`,
  `XlightsAI.Studio.Shell.Tests`, `XlightsAI.Studio.Rendering.Tests`) all pass cleanly (74/74, 78/78,
  311/311, 31/31).
- `FrameIntervalMs` 25→50 (item 7, section 1) — confirmed defect, correct fix identified, deliberately
  not applied because it cascades into `XlightsAI.Studio.Editing`/`Rhythm`/`Shell` test assertions
  outside this spike's file scope.
- `XlightsRenderCapabilityProbe`'s `--help`-text detection (section 4) — confirmed defect, not fixed
  (file outside scope), flagged as the first Phase 7 (or earlier) task.
