# Phase 29.2 — Layered choreography

**PASS.** The active engine resolves overlapping plans in section order, masks base/filler on protected members, and logs losing claims. A takeover ends only the members it replaces. The prompt presents real ordered groups and offers real target sets; the following section receives existing holds when its sustain catalog spans the seam.

The targeted Choreography suite passed 33 tests and the Director prompt/validation suite passed 32. The seam test exported the owning group as one `[2,7)` s effect across the 4 s section boundary and parsed no member effect overlapping that span, despite an adjacent-section claim. The claim produced an ownership diagnostic.

## Follow-up — sparse whole-song export

**PASS — background coverage restored without overriding holds.** The 3:29 p.m. Studio export on November 19 generated all 23 section scripts. The XSQ still had prop-level gaps as long as 35.875 seconds. The final timeline audit showed 11 automatic chorus cues and no authored cues or explicit Off spans; a single automatic cue was enough to disable filler for its entire section. Gap filling now covers ordinary timeline sections, while held targets (including group members), explicit Off windows, and declared-dark looks remain protected. The portable seam XSQ regression verifies that a background prop resumes at the end of its Off window while the neighboring group hold remains intact. All 43 targeted Choreography tests and four scripted whole-song cases passed.

A separate replay of the captured Carrie clip shortened the longest per-prop gap from 9.350 s to 4.175 s (351 to 424 placements). Mean unrelated starts during eligible vocal spans rose from 32.4 to 45.8; restoring background coverage does not pass the unrelated-switch reduction gate.

## Follow-up — sub-frame fragments after sustained notes

**PASS — generated sustains no longer leave unexportable tails.** The November 19 Carol of the Bells run was rejected for a `Matrixes` effect at `368965079–369039383` ticks (7.4304 ms, both boundaries round to 36,900 ms). A compiler-to-XSQ regression reproduced the same error when a vocal sustain consumed all the visible frames of the next base effect. The compiler now drops only the zero-frame result; a short tail that still occupies a frame is preserved. The parsed regression exports preserve the sustain endpoint and the next full effect. The strict exporter behavior for invalid direct input is unchanged.

The 43 targeted choreography, four scripted whole-song, and three exporter frame-grid tests passed. The original failed run was not regenerated.

