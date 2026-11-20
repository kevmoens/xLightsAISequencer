# Phase 29.1 — Shared musical anchors and spans

**PASS.** Prompting, validation, and generation compilation use the same section catalog. It preserves numbered salient onsets without reindexing the full list and carries stable note-ID start/end pairs even when a note crosses a section boundary. Estimated word labels are advisory; note ticks define the interval. Energy shifts, beats, transients, and section endpoints remain available without notes.

The targeted Choreography suite passed 32 tests and the Director prompt/validation suite passed 32. The XSQ regression offered `vocalnote-1` for the salient onset at 3 s despite an earlier note in the full list and inspected the exported hold at `[3,7)` s. The separate boundary regression rejected a high-confidence, insufficiently overlapping word estimate and resolved the same note endpoints in both section catalogs.
