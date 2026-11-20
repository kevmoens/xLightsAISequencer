# Phase 29.6 — XSQ verification

**PASS — automated XSQ and round-trip gates.** The scripted whole-song run checks a group hold without accents, with rotating independent accents, and with an explicit replacement at 7.5 s. It parses seam-spanning hold intervals, confirms no member interruption, and checks a three-model structural accent without a dominant hold. It compares chorus targets, recipes, palettes and return variation, traces a mined half-time exemplar through the emitted layers, and verifies missing/disabled, wrong-kind, and legacy fallbacks. Reload and targeted repair retain the seam hold and chorus signature while replacing a structural accent. Separate compiler XSQ tests cover Full-versus-Bright dimming scope. Targeted results: 33 Choreography, 44 Director and 15 Pipeline tests passed.

**PASS — real export comparison executed.** The patched pipeline reused the exact song analyses, fixed layout and Show Bible from 29.0. The comparison below replays the original local Choreographer payloads and parses each resulting XSQ at 25 ms/frame. Missing retries in the historical capture are handled as unavailable model replies, not filled with synthetic ones. Phase 29 counts only actual matching authored placements as declared cues; the baseline necessarily inferred hold intent from the old cue shape.

| Song | Eligible spans | Baseline mean unrelated starts | Patched mean | Patched placements | Patched undeclared interruptions |
|---|---:|---:|---:|---:|---:|
| `carrie-30s-clip.mp3` | 5 | 33.8 | 33.4 | 146 | 0 |
| `chappell-30s-clip.mp3` | 0 | N/A | N/A | 312 | 0 |
| `Carrie Underwood - Away In A Manger Lite.mp3` (161.158 s) | 28 | 74.3571 | 69.7857 | 3,167 | 0 |

Patched per-span starts: Carrie clip `30, 49, 16, 16, 56`; full Carrie `75, 90, 19, 130, 24, 40, 73, 24, 25, 179, 73, 20, 59, 78, 60, 18, 20, 204, 89, 107, 101, 0, 61, 64, 60, 141, 18, 102`. None of the replayed sections supplied a matching explicit note hold, so these zeroes do not prove hold integrity. The fixed layout's SHA-256 remained unchanged. Captures, XSQs and per-span reports are in the local session's `files\phase29-capture` directory.

**BLOCKED — 50% reduction not achieved.** The same-response reductions were 1.2% for the Carrie clip and 6.1% for full Carrie. As a secondary check, fresh `qwen2.5-coder:7b-instruct` replies for the Carrie clip were captured and replayed through the patched compiler: 351 placements, five spans, 32.4 mean unrelated starts (4.1% reduction), zero interruptions and no matched explicit note hold. Fresh Chappell inference was stopped during retries and the fresh full-track run did not start; both have same-response comparison exports. Chappell has no sustain denominator; it is not counted as meeting the reduction target.

**BLOCKED — human preview not performed.** All three songs lack a named reviewer and an actual xLights **2026.03** preview. The tool-generated XSQs are not a substitute. The separate per-effect brightness calibration also remains blocked: a real xLights 2026.03-authored XSQ with known per-effect brightness values is missing, so no brightness setting was added.
