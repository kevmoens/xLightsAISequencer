# Build Plan: Phase 23 — Corpus-Validated Cue-to-Effect-Shape Correlation

**Status:** proposed, not yet implemented. Written 2026-11-09. Fourth and last of the 4-phase arc (20-23)
-- see Phase 20 §0 for the full framing prompt. A follow-up validation pass once Phase 22 has real
generation data to check, replacing Phase 22's own hand-tuned contrast thresholds/effect choices with real
corpus evidence where it exists -- same "verify honestly, promote only if real" discipline Phase 19
established.

---

## 1. Why this phase exists

Phase 22 has to start from reasoned defaults (e.g. "a hard hit wants a short, discrete effect; a sustained
emphasis wants a held one") because that correlation has not actually been checked against the real corpus
yet. Phase 19 already proved this kind of check is possible and honest (one new rule promoted at 98.2%, two
honestly rejected) -- this phase applies the same method to a new, more specific question: do real
choreographers systematically pick a DIFFERENT shape of effect (short/discrete vs. sustained/held) depending
on whether a moment is a hard transient hit versus a sustained emphasis window, and does a real
intensity-contrast pattern (dim/off immediately before a hit) already show up naturally in the corpus the
same way Phase 22 is being asked to engineer deliberately?

## 2. Proposed technical changes

### 2a. A new `CraftRuleInducer` candidate: pre-hit contrast already exists in real shows
Using Phase 21's `BrightnessTier` (once effects in the real corpus's own `AlignedEffects` can be matched
against it by name) and Phase 19's own transient/onset detection reused against the corpus's own audio,
check whether a real detected hard hit's immediately-preceding effect measurably differs in `BrightnessTier`
from the one at/after it, corpus-wide -- honestly promoted or rejected like every other Phase 19 rule.

### 2b. A new candidate: short/discrete vs. sustained effect choice correlates with hit type
Using `ParsedEffectInstance` duration (short vs. long) against `TimedCueComplianceChecker`'s own
"qualifying short/discrete effect" convention (already established, `MaxQualifyingEffectDuration`), check
whether real hard-hit-adjacent moments in the corpus skew toward short effects more than baseline.

### 2c. Feed any promoted results back into Phase 22's own defaults
Where a real corpus rule promotes, replace Phase 22's reasoned-default thresholds/effect-family choices with
the real measured ones; where a candidate is honestly rejected, leave Phase 22's own defaults in place and
say so plainly (not everything needs to be corpus-provable to be good craft -- Phase 13 §2a's own guessed
layering minimum stands on its own merits independent of corpus confirmation).

## 3. Sequencing and dependencies
Depends on Phase 21 (`BrightnessTier`) and benefits from Phase 22 already having run at least once for real
(so there is a real generation to compare against, not just the raw corpus in isolation). Not blocking --
Phase 22 ships and works without this phase; this phase only sharpens it with real evidence afterward.

## 4. Acceptance criteria
- Both new candidate rules are evaluated against the real (by then likely further-grown) corpus and
  reported honestly either way, with real confidence/song-count numbers.
- Any promoted result is traced to the specific Phase 22 default it replaces, with the before/after value
  recorded.
- No candidate is force-promoted by loosening `minConfidence`/`minSongCount` below Phase 19's own
  established defaults.
