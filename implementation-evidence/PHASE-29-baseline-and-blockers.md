# Phase 29.0 — Baseline and blocking fixes

**PASS — the pre-change baseline was captured before generator changes.** The capture runner in `tools\Phase29Baseline` calls the current `ChoreographyGenerationPipeline`, records actual local model replies by section and attempt, and can replay them without contacting a provider. The measurement reads the exported XSQ, expands groups using the fixed display layout, and retains per-span counts alongside the export.

Pre-change revision: `e9f869ba4f565108ee7d5b247db0d389097e433d`. Fixed layout: `C:\xLights\xlights_rgbeffects.xml` (SHA-256 `7CD446DF4AAF725C111A3E88A238AB224D79228C1FBBCEE4F44327C21701D2AC`). The measured leg used local `qwen2.5-coder:7b-instruct` Choreographer replies and one fixed, previously AI-generated Show Bible (SHA-256 `6B74A996050B00C807C6D95326C696A065733BAF14D76F28BE646490C335582B`). The first fresh Director request timed out; a second local model did not return usable output before the switch to a saved Show Bible. No cloud provider was used.

| Song | Duration | Sustain-eligible notes | Mean unrelated starts | Undeclared interruptions |
|---|---:|---:|---:|---:|
| `carrie-30s-clip.mp3` | 30 s | 5 | 33.8 | 0 (no declared holds on these spans) |
| `chappell-30s-clip.mp3` | 30 s | 0 | N/A (no eligible spans) | 0 (no eligible spans) |
| `C:\xLights\Carrie Underwood - Away In A Manger Lite.mp3` | 161.158 s | 28 | 74.3571 | 0 (one matched declared hold) |

Both clips' first exports failed because the analysed beat grids included a beat past the 30 s audio end (Carrie: 30.142 s; Chappell: 30.256 s). The measurement replayed the recorded responses on the same analyses with just those out-of-song beats removed at the runner's input; no production timing or export code was changed. The resulting 2026.03-targeted XSQs parsed at 25 ms/frame with 147 and 739 placements respectively. Carrie's five span counts were `30, 51, 16, 16, 56`. The zero interruptions are not a hold-integrity result.

Pre-change regression check: all 20 existing `SectionTimeline` and `EmphasisContrast` tests passed. This is not a PASS for any Phase 29 behavior.

The full-track XSQ parsed at 25 ms/frame with 3,301 placements. Its 28 span counts were `79, 94, 25, 138, 24, 34, 77, 24, 25, 187, 74, 19, 63, 81, 64, 26, 20, 216, 116, 103, 109, 0, 55, 64, 65, 153, 18, 129`. Analysis snapshots, raw response captures, XSQs, and tick-by-tick span reports are in the local session's `files\phase29-capture` directory.

**PASS — duration, contrast and explicit overlap decisions.** The targeted Choreography filter passed 30 tests. Portable XSQ round-trips verified full Dim/Bright/Full holds, no Off placement, an independent accent and a takeover at the declared anchor. A group/member collision was rejected. In the back-to-back accent exports, Full dimmed both non-held models, Bright dimmed only its own model, and the held model retained a single unmodified placement.

**BLOCKED — per-effect brightness fixture.** The checked-in `golden-three-effect-sequence.xsq` copies are Studio export fixtures targeting 2026.03; there is no real xLights 2026.03-authored XSQ with known per-effect brightness values to establish the exact key and range. No brightness XML setting was added.

The final human gate requires a named reviewer to preview the comparison songs in xLights 2026.03; no such preview has been supplied or performed.
