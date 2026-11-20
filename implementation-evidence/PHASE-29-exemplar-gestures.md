# Phase 29.4a — Exemplar gestures

**PASS — extraction and versioned index round trip.** The index retains placements overlapping a phrase, normalizes clipped intervals against measured variable-tempo beats, and stores continuation, role, cadence, layer, and overlap information. A serialized-index regression verifies a foundation that began before the phrase and an overlapping accent; all nine targeted statistics tests passed. Re-run the existing corpus mining operation to rebuild a knowledge directory; old indexes still deserialize without gesture data.
