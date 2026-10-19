# Test Data and Phase Gate Strategy

## Golden fixture collection
1. `audio/beat-120bpm.wav`: generated metronome with known beat times, silence breaks and sync offset.
2. `audio/melody-c4-d4-e4-g4-e4-d4-c4.wav` plus matching reference MIDI and note events.
3. `audio/repeat-a-b-a.wav`: reproducible sections with repeated motif and altered instrument/velocity.
4. `audio/polyphonic-short.wav`: lead/bass/drums with multiple overlapping note sources; must be licensed or original.
5. `layouts/simple-5-arches/`: five models in ordered locations, plus windows/roofline and model grouping.
6. `layouts/complex/`: irregular positions, groups, submodels, malformed reference variants.
7. `xsq/golden-minimal/`: an editable hand-verified export and expected semantic effect summary.

Store fixtures only if you own rights or the source license permits redistribution. Capture original SHA-256, sample rate, duration, note/beat timing labels, xLights version and any conversion offset. Build fixture generators when possible to avoid music licensing issues.

## Tests
- Deterministic tests: XML/schema, path mapping, time quantization, scopes, undo/redo, caches, loop budgets.
- Scientific comparisons: note/onset F1, beat alignment absolute error, stem separation audio metrics + subjective checks; thresholds agreed from baselines, never fabricated.
- UI tests: menu, selection, editing, keyboard, audio transport, crashes, cancellation and project reopen.
- Integration tests: real xLights import/open/export/render where supported, version matrix and visual attestation.
- Regression: hash/semantic-compare unaffected effects after edits; golden XSQ comparisons tolerate irrelevant XML ordering only when parsed semantics match.

## Gates
Each phase spec has a mandatory demonstration. Stop and record blocker if validation cannot be run. Mark an unexecuted test as **NOT RUN**, not passed. A screenshot alone does not prove timing correctness or native runtime portability.

## Mandatory xLights-specific compatibility fixtures

See `XLIGHTS-SEQUENCE-ENGINEERING-GUIDE.md`, section 11, for a concrete 13-case XSQ fixture suite spanning metadata, effects, palette references, timing, layers, groups, submodels, safe round-tripping, malformed references, and model resolution. Every case must record a tested xLights version and whether a real render was executed.
