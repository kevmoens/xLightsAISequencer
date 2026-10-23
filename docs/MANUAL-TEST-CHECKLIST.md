# Manual Test Checklist

A walkthrough for manually verifying the app works, screen by screen. Check items off as you go. Items marked **(known limitation)** are documented gaps, not bugs — confirming they behave as described (clear error, not a crash) is still a pass.

## Before you start

- [ ] Have a real xLights show folder (contains `xlights_rgbeffects.xml`) with a handful of props.
- [ ] Have a song file in at least two formats: a `.wav` and an `.mp3` (or other non-wav) version of the same song.
- [ ] Have a `.wav` that is **not** 44.1kHz stereo (e.g. 48kHz, or mono) for the rejection test.
- [ ] If possible, have xLights installed so you can open the exported `.xsq` for real.

---

## 1. First run / onboarding

- [ ] Launch the app for the first time (or delete its app-data settings file to simulate first run). The First Run wizard appears.
- [ ] Pick a workspace folder in the wizard. It should be remembered — restart the app and confirm the workspace path is still suggested.
- [ ] If there's an "Install Optional Asset" button on First Run, click it. **(known limitation)** Expect a clear "no installer configured" error, not a crash and not a silent no-op.
- [ ] Check whatever "health check" / diagnostics indicator is on this screen. **(known limitation)** It will always report "zero issues" even if something is actually wrong — don't trust a clean bill of health here.

## 2. Project screen

- [ ] Create a new project: pick a workspace directory + name, click Create. Confirm a project folder actually appears on disk at that location.
- [ ] Confirm the nav rail now shows a "PROJECT: <name>" banner.
- [ ] Close the project via the banner's close link. Banner should disappear.
- [ ] Open an existing project (the one you just created) via "Open existing" + workspace dir. Confirm it loads and the banner reappears with the right name.
- [ ] Fully quit and relaunch the app. Confirm the last project path is pre-filled as suggested text (it should **not** auto-open the project — just suggest the path).

## 3. Show layout (Show Import)

- [ ] Browse to your real xLights show folder and click Discover.
- [ ] Confirm the props/groups from `xlights_rgbeffects.xml` show up with sensible names — compare a few against what's actually in your xLights layout.
- [ ] Point it at a folder with **no** `xlights_rgbeffects.xml` and confirm you get a clear error, not a crash or a silently-empty list.
- [ ] Restart the app and confirm "last show directory" is restored.

## 4. Song (Audio Import)

- [ ] Type/paste the path to your `.wav` file and click "Probe Audio File." It should succeed and show basic info (duration, etc.).
- [ ] Do the same with your non-wav file (e.g. `.mp3`). It should transcode via ffmpeg automatically and then probe successfully.
  - [ ] If no local ffmpeg is installed yet, confirm the screen instead offers a "Download ffmpeg" button, and that clicking it actually downloads and then lets probing proceed.
- [ ] Probe your non-44.1kHz/mono `.wav`. **(known limitation)** Expect a specific rejection error about the format mismatch — not a crash, not a silent pass.
- [ ] Click "Build Waveform Cache" and confirm it completes without error.
- [ ] Open playback, then Play / Pause / Seek. Confirm audio is actually audible and seeking moves playback position.
  - [ ] **Windows only** — on macOS/Linux, confirm you instead get a clear "not supported on this platform" message, not a crash.

## 5. Generate & Export

- [ ] With both Show Layout and Song screens completed, click "Generate Whole-Song Plan" (the real pipeline — not "Generate 3 Effects," which is a legacy demo).
  - [ ] If a stem-separation model isn't installed yet, confirm you're offered a "Download separation model" button and that it works.
  - [ ] Confirm generation finishes (may take a while) and produces a plan without errors.
- [ ] Click "Export XSQ." Confirm a `.xsq` file is actually written to `{project}/exports/sequence.xsq` (or your chosen path).
- [ ] Try exporting again to the same path. By default it should refuse to overwrite (reject-if-exists) rather than silently clobbering the file.
- [ ] If xLights is installed, set the xLights executable path and click "Open in xLights." Confirm xLights actually launches with the file loaded.
- [ ] **In xLights itself**: open the exported sequence against your real show and confirm it renders — effects appear on the right props, roughly timed to the music, and xLights doesn't report corruption/errors on load. This is the one thing the project's own release notes say has never been verified by a human — this is the most valuable test in this whole checklist.

## 6. Timeline

- [ ] After generating a plan, open the Timeline screen. Confirm generated effects are plotted against the waveform.
- [ ] Zoom in/out and scroll along the timeline — confirm the waveform and effect blocks stay in sync and nothing clips/disappears.
- [ ] Select an effect block and confirm it highlights correctly.
- [ ] Test loop/snap controls if present — confirm playback loops the selected region and snapping aligns to the expected grid (beats/bars).

## 7. Piano roll (Refine)

- [ ] Open Piano Roll after a song has been analyzed. Confirm detected notes render as a pitch-vs-time grid matching roughly what you'd expect from the melody.
- [ ] Find a note marked "low confidence" and confirm it's visually distinguished from normal notes.
- [ ] Select a note, edit its pitch/start/end, and click Correct. Confirm the grid updates to reflect your edit.
- [ ] Try an invalid correction (e.g. end time before start time, or outside the track bounds). Confirm it's rejected with a visible error and the grid is left unchanged.
- [ ] Lock a note, then try to edit it again — confirm it's protected (locked notes shouldn't accept further edits, or at least should visibly indicate they're locked).
- [ ] Close and reopen the project — confirm your corrections persisted.

## 8. Structure (phrase relationships)

- [ ] Open Structure. Confirm detected phrases are listed in time order, each tagged with a motif group.
- [ ] Unlink a phrase from its group and confirm only that phrase moves — its former group-mates are unaffected.
- [ ] Link two phrases that weren't automatically grouped together and confirm the override takes effect and is visually reflected.
- [ ] If a "choreography template" / preview-onto-target-phrase feature is present, try defining a template from one phrase and previewing it on another — confirm the preview reflects the source phrase's effects adapted onto the target's timing.
- [ ] Reopen the project and confirm link/unlink overrides persisted.

## 9. Display paths (Refine)

- [ ] Open Display Path Editor. Confirm it lists the real props from your imported show layout with their X/Y positions.
- [ ] Select a node, type new X/Y values, and confirm the node's position updates (this is numeric-field "dragging," not literal mouse drag — don't expect to click-and-drag with the mouse).
- [ ] Create a named custom path, add it to the reorder list, and try reversing it. Confirm the order/direction changes as expected.
- [ ] Assign a role (lead/rhythm/ambient/hero/accent) to a prop and confirm it's saved/reflected.
- [ ] Lock a node and confirm it can't be moved.
- [ ] Trigger route validation (if there's a visible "validate" action) on a setup with an obvious issue (e.g. overlapping/invalid path) and confirm it flags a blocking issue — and that Export XSQ actually refuses to proceed while a blocking issue is unresolved.
- [ ] Reopen the project and confirm your position/role/path edits persisted.

## 10. Settings / app behavior

- [ ] Confirm there's no way to edit ffmpeg or xLights executable paths from a Settings screen **(known limitation)** — the xLights path is a per-session text field on the Generate & Export screen you'll need to retype each run; this is expected, not a bug.
- [ ] If you have an older/previous version's settings file, confirm the app still launches cleanly against it (schema is meant to be additive/backward-compatible) rather than crashing on startup.
- [ ] Restart the app and confirm it reopens on the last-viewed screen.

## 11. Things that are intentionally not there yet

Don't file bugs for these — they're known-unbuilt, not broken:
- No UI for: Stem Mixer, Role Routing Editor, Visual Budget Editor, Visual Story Editor, Transition Recipe Editor, Selection Context Editor, Structured Command Quick Actions, AI Edit Request/Review panels, Motif Propagation Panel, Candidate Compare, Render Preview.
- No phrase-seam continuity, no motif variation beyond a flat on/off effect, no AI refinement/scoring/repair loop.
- No macOS/Linux audio playback.
