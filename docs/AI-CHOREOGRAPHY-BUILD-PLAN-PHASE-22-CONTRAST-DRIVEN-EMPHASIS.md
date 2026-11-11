# Build Plan: Phase 22 — Contrast-Driven Emphasis (Dim/Off → Bright)

**Status:** proposed, not yet implemented. Written 2026-11-09. Third of the 4-phase arc (20-23) -- see
Phase 20 §0 for the full framing prompt. This is the actual feature the user asked for: make a repeated
hook, a lyric, or a beat-locked hit actually FEEL like an accent, by requiring real contrast into it, not
just correct timing.

---

**Design decisions confirmed with the user (2026-11-09):**
- **Enforcement strength (§2b/§2c):** strong Choreographer guidance, never a hard legality gate. §2c below
  is retitled from "if 2b's adoption is weak" to "always advisory" -- there is no escalation path to a
  rejecting/retrying rule in this phase; §2c exists permanently as a visibility/measurement tool, not a step
  toward enforcement.
- **"Repetition" clarified -- not a separate detection feature, folded into this phase's own scope:**
  the user's own clarification: "repetition would be things like beats or notes or words to the user that
  feel like they have emphasis." Read plainly, this means every recurring beat, vocal-note onset, and
  emphasized word throughout a whole song is itself a case needing the contrast treatment -- "repetition" is
  the fact that emphasis-worthy moments recur constantly through a song, not a distinct motif/hook-recognition
  mechanic layered on top. This phase's own scope was already "every real detected emphasis instant" (§2b),
  so no separate mechanic needs designing -- the earlier "two mechanics" framing in this doc's first draft
  is corrected here: there is one mechanic (contrast at emphasis), applied consistently and repeatedly
  everywhere a real emphasis signal recurs, never just an occasional highlight.
- **Scope of a single contrast event (§2a) is not fixed -- it varies, and it travels:** the user's own
  words: "it will be one or the other. Everything, or moving from group of models to a group of models, or
  just a set of models. Mostly a set of models. It feels like going note to note on a keyboard, and that
  movement feels good to watcher." This is a real, specific design requirement, not just "pick one prop and
  dim it" -- §2e below is new, added specifically to capture it: successive emphasis moments should, most of
  the time, travel across *different* sets of models (like playing adjacent keys), not pulse the identical
  set every time, with "the whole show" and "one fixed prop" both being valid but minority cases.
- **Brightness metric:** confirmed in Phase 21 as whole-frame average pixel brightness -- this phase
  consumes `BrightnessTier` as-is from there, no separate metric needed here.
- **§2e's travel order (which set comes next):** confirmed as deliberately unspecified/free -- "it can be
  up to the choreographer or director. It can be random or alphabetical order. Doesn't matter. Change it up
  randomly when generating the sequences." No fixed rotation rule is required; §2e below is simplified to
  match -- randomized selection (excluding whichever set was just used, so two consecutive moments never
  trivially repeat the same set by chance) is the implementation default, left loose by design so repeated
  generations of the same song don't all travel identically.
- **§2d's trigger threshold:** resolved without needing a user decision -- it must catch BOTH an exact
  same-effect repeat AND two different-but-equally-bright effects back to back (the user's own original
  complaint was specifically about the second case), so §2d triggers on any same-`BrightnessTier`
  transition, not only a literal same-effect one.

---

## 1. Why this phase exists

Every emphasis signal this app already detects -- transient hits, vocal-note onsets, timed cues, key
changes, motif-linked repeats -- currently only controls WHEN something happens. Nothing today checks WHAT
kind of visual change happens at that instant. The user's own diagnosis is exactly right: a transition from
one bright/full effect to another bright/full effect does not register as an accent to a viewer, no matter
how precisely it lands on the beat -- only a real intensity drop immediately before the hit (dim, sparse, or
literally off) followed by the hit itself reads as a deliberate accent.

This phase is the first point in the whole pipeline that treats "the moment right before an emphasis point"
as something to actively compose, rather than something that just happens to contain whatever Look was
already running.

## 2. Proposed technical changes

### 2a. A new `EmphasisContrastPlanner` concept, not a Look property
An emphasis moment's own "pre-beat" window (e.g. the ~0.5-1 beat immediately before a detected transient
hit/vocal onset/timed cue) needs its OWN effect/intensity decision, distinct from whatever Look otherwise
spans that section -- this is a brief, deliberate dip, not a property of the section's base Look. Model it
as a thin layer/override the compiler can insert, using Phase 21's `BrightnessTier` to verify (and, where
missing, choose) a `Dim`/`Off`-tier effect for that pre-beat window on the same prop(s) the emphasis itself
will use.

### 2b. Strong prompt-level guidance (the primary mechanism -- confirmed, not just "try first")
Add explicit, emphatic Choreographer prompt guidance: when this section's own emphasis signals (already
rendered in the volatile suffix -- transient hits, vocal-note onsets, timed cues) include a real moment, the
effect/prop state active immediately before it should be a different, measurably dimmer (or literally off)
one than what follows -- never the identical effect/brightness carried through. Phrased as strong,
repeated, worked-example guidance (same concreteness discipline Phase 10 §2d/§3c and Phase 13 §2d.1 already
found improves real adoption over abstract rule statements), not a one-line aside. This is the confirmed
primary mechanism, not a cheap-first-attempt to be superseded -- §2c/§2d exist alongside it, not above it.

### 2c. A permanently-advisory `ChoreographyLegalityValidator` rule (visibility, never enforcement)
Add an advisory legality rule: for each real detected emphasis instant in the compiled IR, check whether the
effect/brightness active immediately before it and the effect active at/after it differ in `BrightnessTier`
by at least one tier (using Phase 21's catalog) -- flag, never block or trigger a retry, the ones that
don't. Confirmed by the user: this stays advisory permanently, purely so real adoption is measurable and
visible in the compiled report -- it is not step one of an escalation toward a hard gate.

### 2d. A deterministic fallback for a confirmed-missed moment (not an "enforcement" step)
Where §2c's own measurement shows a real emphasis moment still landed flat (no real brightness contrast),
a bounded, deterministic pass may substitute in a brief `Dim`/`Off`-tier filler on the specific prop(s)
involved, immediately before that one moment -- short enough (sub-bar) to respect the existing
"always-moving"/no-model-frozen-longer-than-one-bar rule, framed as a deliberate breath. This is a quality
backstop, not a validator-triggered reject/regenerate cycle (per the user's own "strong guidance, not a hard
legality" instruction) -- it repairs the specific missed instant directly, the same "never retry the whole
section for one fixable gap" philosophy `TargetedSectionRegenerator` already embodies elsewhere, just
applied as a direct patch instead of a re-ask.

### 2e. Spatial travel across models between successive emphasis moments ("note to note on a keyboard")
Confirmed design requirement: a contrast event's own scope is not fixed to "the whole show" or "one fixed
prop" -- most of the time it should be **a set of models**, and across a RUN of successive emphasis moments
(e.g. consecutive vocal-note onsets within one phrase, or a string of transient hits), that set should
usually be a *different* one each time, producing a traveling/sequential feel rather than the same prop
pulsing repeatedly. The user explicitly left HOW the next set is chosen unspecified ("random or alphabetical
order, doesn't matter, change it up randomly") -- so the implementation default is: group `LayoutSummary`'s
real groups/models into candidate "emphasis sets," and for a run of emphasis moments within one section,
randomly select the next set each time, excluding whichever set the immediately-previous moment just used
(so two consecutive moments never land on the same set purely by chance) -- "the whole show" and "one
single fixed prop" both remain valid, minority outcomes of the same random choice. Re-randomized on every
generation, by design, so repeated runs of the same song don't all travel the same way.

## 3. Sequencing and dependencies
Hard dependency on Phase 21 (`BrightnessTier`). Benefits from Phase 20's wider template vocabulary (more
real dim-capable layers to alternate with) but does not strictly require it.

## 4. Acceptance criteria
- A real regenerated song's compiled IR shows a measurable rise in emphasis instants where the preceding
  effect's `BrightnessTier` differs from the following one, versus a baseline run without this phase's
  changes.
- 2b's own real adoption rate is measured (not assumed, via 2c's advisory report), confirming it as the
  primary working mechanism before 2d's deterministic fallback is ever invoked.
- Across a real run of 3+ consecutive emphasis moments within one section, the real compiled IR shows the
  contrast/accent landing on more than one distinct model/group across that run (the §2e "travels, doesn't
  pulse the same prop" requirement), not just brightness contrast at a single fixed prop.
- Never introduces a model frozen longer than one bar as a side effect of a deliberate dip (re-verified
  against the existing always-moving legality rule).
- §2c's advisory rule never blocks a generation or triggers a retry, confirmed by a real run that still
  completes and exports even when it flags missed-contrast instants.

## 5. Open questions

None remaining -- §2e's travel order, §2d's trigger threshold, and the "repetition" scope question from
this doc's first draft are all resolved above (2026-11-09). Implementation can proceed directly from the
kickoff prompt once Phase 21 (`BrightnessTier`) lands.
