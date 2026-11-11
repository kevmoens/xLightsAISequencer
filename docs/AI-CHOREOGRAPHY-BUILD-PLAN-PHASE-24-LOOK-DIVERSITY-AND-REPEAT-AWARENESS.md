# Build Plan: Phase 24 — Look Diversity & Repeat-Aware Structure

**Status:** implemented 2026-11-10. §2a/§2b/§2d landed and verified against a real run (and a real-run
regression this work surfaced -- a Director generation exhausting its retry budget against §2a's own new
rule -- was fixed by making the prompt spell out the eligibility mechanism explicitly, not just the
post-failure correction text; see `DirectorPromptBuilder`'s own `REQUIRED (build-plan Phase 24 §2a...)`
line). §2c (real `CorpusMiner` re-run) completed -- see `implementation-evidence/PHASE-24-CORPUS-REMINING.md`
for the real before/after exemplar counts per Kind. Follows directly from the Phase 20-23 arc
(Phase 20 builds the reusable multi-layer template library; Phase 22 built contrast-driven emphasis timing)
but is its own, independent concern: a song can have perfect per-hit contrast timing and a rich template
library and STILL feel repetitive if the same one or two Looks get reused for every repeat of a given
section Kind. This phase targets that specific complaint directly.

---

## 0. Framing (the user's own follow-up, 2026-11-10)

> My concern is every xsq sequence feels very similar because of the small amount of effects that seem to
> be used/reused between generating a sequence... Shouldn't it know how many bars are in the chorus, verse,
> bridge, etc... Do we need to make more looks? Have more effect examples with more layers? More information
> on how existing music works with a song's sequence? What should we do next to come up with changes that
> will make sequences feel more unique and yet keep these on beat/note/word hits tight.

Two real, separate root causes were confirmed by direct inspection (not guessed):
1. The Director's prompt already receives every section's real bar range, but nothing in the Show Bible
   schema or its validator ever requires a Kind that repeats several times to actually USE more than one or
   two distinct Looks across those repeats -- `ShowBibleValidator.MinLooks`/`MaxLooks` (8-20) is a
   whole-song total, with no per-Kind floor.
2. The Director is told, in prose, to "favor restraint... hold back... escalate" across repeats (existing
   baseline guidance), but is never shown the repeat structure as DATA -- it sees a flat section list, not
   "Chorus repeats 5 times, here are all 5 bar ranges, design real variation across them."

## 1. Why this phase exists
Phase 20's expanded template library (once built) gives the Director more raw material to draw from, but
raw material alone does not guarantee it gets SPREAD across a Kind's own repeats -- nothing currently checks
or requires that. A song with 5 Choruses can satisfy every existing validator rule while using the exact
same one Chorus-tagged Look for all 5. This phase closes that gap with a real, checkable requirement plus
the data the Director needs to actually meet it, and a real measurement so "did this help" is answerable
from a real compiled show, not an impression.

## 2. Proposed technical changes

### 2a. A per-Kind Look-diversity floor in `ShowBibleValidator`
New rule: for any `SectionKind` with N real occurrences in `music.Sections.Labels` where N >= 3, the Show
Bible's own declared `AllowedSectionKinds` must name at least `ceil(N / 2)` distinct Looks for that Kind
(e.g. 5 Choruses -> at least 3 distinct Chorus-eligible Looks). N of 1-2 is exempt (a Kind that barely
repeats has nothing real to diversify). This is a hard validation failure (same tier as `MinLooks`/`MaxLooks`
today), not advisory -- the whole reason the existing "spread Look design" prose guidance isn't working is
that nothing ever actually checks it.

### 2b. Surface each Kind's own real repeat structure as data, not prose
Extend `DirectorPromptBuilder.BuildVolatileSuffix`'s existing section list: group by `SectionKind`, and for
any Kind with 3+ real occurrences, add an explicit line naming every one of that Kind's own bar ranges
together (e.g. "Chorus repeats 5 times: bars [48,64), [112,128), [176,192), [240,256), [304,328) (note: the
last one is 24 bars, longer than the others) -- design at least 3 visibly distinct Looks across these 5
occurrences, not one Look reused"). This is the one piece of cross-section awareness a Director call can
cheaply have (it already sees the whole song's section list; this just re-groups and calls out what it
means), unlike the Choreographer's own necessarily-stateless per-section calls.

### 2c. Grow the exemplar corpus
No new mechanism -- `CorpusMiner` (`tools/CorpusMiner`) already exists and already feeds `ExemplarIndex`.
Disclosed as a process/data step, not a code change: re-run corpus mining including any newly-available real
reference shows (e.g. the "Existence (VIP) - Shortened" comparison from this same conversation) so
`ExemplarIndex.FindSimilar`/`FindVaried` have more real passages to retrieve from per `SectionKind`. Report
the real before/after exemplar count per Kind so this is a measured change, not an assumed one.

### 2d. A real, committed Look/effect diversity measurement
Build-plan Phase 22 §4 established the pattern (a dedicated real-run test writing measured numbers to
`implementation-evidence/`) -- reuse it here. A new test: regenerate a real song end-to-end, then for every
`SectionKind` with 3+ real occurrences, measure the real count of distinct Looks actually used (via
`SectionScript.LookId` per section) and the real count of distinct xLights effect names actually compiled
across that Kind's own sections. Report both, per Kind, to `implementation-evidence/PHASE-24-LOOK-DIVERSITY.md`.
This is the direct, measured answer to "does this still feel repetitive," not an impression.

## 3. Sequencing and dependencies
Independent of Phase 20/21/22 (different concern: WHICH Look gets picked across repeats, not what a Look
itself can contain or how it times to a hit) -- can land before or after Phase 20, but is substantially more
useful once Phase 20's own expanded template library gives the Director more distinct Looks to actually
spread across a Kind's repeats. §2a's validator floor should land after (or alongside) Phase 20 for exactly
that reason -- requiring 3 distinct Chorus Looks is a much easier, more natural bar to clear once the
template library itself has grown past 3 entries.

## 4. Acceptance criteria
- `ShowBibleValidator` rejects a real Show Bible where a 3+-occurrence Kind names fewer than `ceil(N/2)`
  distinct allowed Looks, and a real regeneration against that rule produces more distinct Looks per
  repeating Kind than before this phase.
- A real Director prompt log shows the new per-Kind repeat-structure line for any song with a 3+-occurrence
  Kind.
- `implementation-evidence/PHASE-24-LOOK-DIVERSITY.md` exists from a real regenerated song and shows, per
  repeating Kind, the real measured distinct-Look and distinct-effect counts -- compared against a baseline
  run from before this phase on the same song.

## 5. Open questions
- The `ceil(N/2)` diversity floor in §2a is a reasonable, defensible default, not a user-confirmed number --
  worth a quick gut-check before implementation (too strict a floor on a short, simple song could force
  Looks that do not actually differ meaningfully, just to clear a count).

  **Gut-check, resolved 2026-11-10:** the floor is exempt entirely below N=3, and at the smallest
  non-exempt counts it is lenient, not strict: N=3/4 -> 2 distinct Looks required, N=5/6 -> 3, N=7/8 -> 4.
  A real short/simple song's own repeating Kind rarely exceeds 4-6 occurrences (confirmed against
  `fixtures/FIXTURE-CORPUS-MANIFEST.json` and the real corpus song list under `C:\xLights`), so the floor
  this phase actually enforces on a short song is 2-3 distinct Looks -- against a Show Bible that already
  must declare 8-20 Looks total (`ShowBibleValidator.MinLooks`/`MaxLooks`), nowhere near a count that would
  force meaningless churn just to clear a number. The real e2e run in
  `implementation-evidence/PHASE-24-LOOK-DIVERSITY.md` (§2d) also surfaced a real, disclosed finding this
  gut-check did not anticipate: `AllowedSectionKinds` eligibility is advisory to the real Choreographer call,
  not a hard filter at generation time -- so this floor's own real effect today is to widen how many Looks a
  repeating Kind's Show Bible entry legitimately offers (and to make the Director design that many), not yet
  to guarantee the Choreographer actually draws from all of them every run. Closing that remaining gap is
  disclosed follow-up work, not part of this phase's own scope.
