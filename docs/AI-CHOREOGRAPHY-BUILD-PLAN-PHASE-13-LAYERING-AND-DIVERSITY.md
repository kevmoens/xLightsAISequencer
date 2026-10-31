# Build Plan: Phase 13 — Layering Depth and Effect-Vocabulary Diversity

**Status:** proposed, not yet implemented. Written 2026-10-30 in direct response to
`docs/AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md`'s Findings 3 and 4: a real generated sequence used
much shallower per-prop layering and a much narrower effect vocabulary than the real hand-sequenced
reference for the same song.

This slots in after Phase 10/11 in `AI-CHOREOGRAPHY-BUILD-PLAN.md`'s own numbering, independent of Phase 11
(provider reliability) and Phase 12 (dynamic density) — all three react to the same comparison report but
address different, non-overlapping output properties.

---

## 1. Why this phase exists

### 1a. Confirmed findings (2026-10-30 real-run comparison)

| Metric | Reference | AI-generated |
|---|---|---|
| Max simultaneous `EffectLayer`s, richest prop | 8 (Matrixes) / 7 (Tree) | 3 (Matrixes/Outline) |
| Typical max layers, secondary props | 3 | 0 – 2 |
| Distinct effect names used, whole song | 22 | 9 |
| Share of all effects from the top 2 effect names | ~25% + ~24% | ~37% combined, concentrated in 2 effects |

Both findings point the same direction: the Director's Show Bible is settling on a small number of
shallow, "safe" Looks, and the Choreographer is reusing them across most of the song, rather than drawing on
a wide effect palette or layering props as richly as the reference does (even before counting the
reference's own Faces/Pictures/Video layers, which this comparison deliberately excluded).

This echoes two things already named but not yet closed in Phase 10:
- §3f (corpus mining activation) and §2f (craft rules as grounding) are meant to push toward real,
  corpus-typical group counts/effect combinations, but corpus mining still does not run in practice
  (`Promoted: []` is still hardcoded per Phase 10 §1a) — there is no real grounding signal reaching the
  Director yet.
- §6a/§6b (the real multi-layer Tree/Matrix research already gathered into
  `docs/research/multilayer-template-exemplars/`) identified exactly this "many layers and effects at once"
  pattern in real corpus shows, but that research has not yet been turned into enforced prompt guidance or a
  shippable template catalog.

### 1b. A related, smaller finding: §3e phrase-scoped staging saw zero real adoption

Phase 10 §3e's phrase-scoped sub-section variation is correctly wired and independently tested, but a real
run with six genuinely eligible 2-phrase sections produced zero `PhraseVariations` declarations across 16
real Choreographer calls. This isn't proof of a defect (§2e's own guidance explicitly says staging is
optional and warns against manufacturing it), but it is the same shape of problem as §1a above: the
Choreographer has access to a richer creative tool and is consistently not reaching for it. This phase
folds in the cheapest fix for that alongside the effect-diversity work below, since both are instances of
"make an available option easier for the Choreographer to actually choose."

---

## 2. Proposed technical changes

### 2a. Concrete minimum-layering guidance in the Director prompt
1. Turn the already-gathered `docs/research/multilayer-template-exemplars/` patterns (Phase 10 §6a: the
   "EDM dense-canvas," "quiet-ballad hold-plus-pop," and "mid-energy chase-and-accent" patterns) into
   concrete `DirectorPromptBuilder` guidance with a stated minimum layer count per prop role/energy tier —
   e.g. "a Chorus-kind Look's subject prop should carry at least 3 concurrent layers; a Verse/ballad-kind
   Look's subject prop should carry at least 2 (one steady base, one accent)."
2. This can land ahead of, and independently from, the full seed-template-catalog work in Phase 10 §6b
   (deriving a shippable JSON pattern catalog) — a stated minimum is a much smaller change than a full
   catalog, and should be tried first per this project's own "cheapest fix first" discipline.

### 2b. A whole-song effect-diversity signal
1. Add an "effects used so far, with counts" line to the Choreographer's volatile per-section prompt suffix
   (`SectionScriptPromptBuilder.BuildVolatileSuffix`) — the same reasoning-tax-respecting pattern the existing
   occurrence-index (`occurrenceIndex`/`totalOccurrences`) and motif-link context already use: a cheap,
   stateless-call-compatible signal that lets a given call see it's over-relying on one or two effects without
   needing to see every sibling call's own output.
2. Add a corresponding soft guideline to the prompt text: prefer an effect not yet heavily used this song when
   multiple choices would serve the section equally well.

### 2c. A soft legality check on whole-song effect concentration
1. Add an advisory `ChoreographyLegalityValidator` rule flagging a whole-song compiled effect distribution
   that is too concentrated (e.g. any single effect exceeding some share of all effects in the show) —
   soft/advisory, never a hard rejection, consistent with Phase 10 §2f's own "soft defaults the Director can
   deviate from with reason" framing.

### 2d. Improve real adoption of §3e phrase-scoped staging
1. Make the §2e Choreographer prompt guidance more concrete with a short worked example, not just a rule
   description — Phase 10's own §2d/§3c motif-leitmotif guidance already improved real adoption once it named
   a *specific* earlier section rather than speaking abstractly; try the same concreteness here before
   concluding the feature needs further engineering.
2. Track real adoption rate (declared `PhraseVariations` per eligible multi-phrase section) across a handful
   of real songs as its own explicit metric — the same "dead wiring vs. truncation vs. wired-but-never-invoked"
   discipline Phase 10 §1a already applies to signals, extended to a fourth category this run revealed:
   "wired, invoked, and genuinely available, but never chosen."
3. Only escalate beyond prompt wording (e.g. a legality check nudging a section past some phrase-count
   threshold without declared staging) once adoption data from more real songs confirms it is still
   near zero after §2d.1's own fix.

---

## 3. Sequencing and dependencies

Independent of Phase 11 (provider reliability) and Phase 12 (dynamic density) — all three were motivated by
the same 2026-10-30 comparison but touch non-overlapping parts of the Director/Choreographer prompts and
`ChoreographyLegalityValidator`. Depends loosely on Phase 10 §3f (corpus mining) for a stronger, data-driven
version of §2a/§2c above, but §2a/§2b/§2c/§2d here are each independently valuable without it (manual/
research-derived minimums and a stateless diversity signal, not a corpus-mined one).

## 4. Acceptance criteria

- A real generation on the same reference song shows at least one Chorus-kind section's subject prop using 3
  or more concurrent layers (verified in the compiled IR, not just prompt text).
- The same real generation's whole-song distinct-effect-name count measurably increases from the 9 measured
  in the 2026-10-30 baseline, with no single effect accounting for as dominant a share as the ~37% measured
  there.
- At least one real, eligible multi-phrase section in a real generation declares a `PhraseVariations` entry
  after §2d.1's prompt-wording change — re-measured the same way Phase 10 §3e's own acceptance criterion
  already requires (a real `tools/GenerateOnce` run, inspected, not assumed from prompt text).
