# Build Plan: Phase 12 — Section-Energy-Driven Change Density

**Status:** proposed, not yet implemented. Written 2026-10-30 in direct response to the headline finding in
`docs/AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md`: a real generated sequence's own rate of visual
change barely varies across the whole song, while the real hand-sequenced reference for the same song swings
nearly 5x between its quietest and most intense sections.

This slots in after Phase 10/11 in `AI-CHOREOGRAPHY-BUILD-PLAN.md`'s own numbering, independent of Phase 11
(the provider-reliability fix) — this phase is about the choreography engine's own behavior, not request
plumbing — though re-measuring against Phase 11's fix first (rather than a possibly-misconfigured baseline)
is recommended before tuning the specifics in §2 below.

---

## 1. Why this phase exists

### 1a. Confirmed finding (2026-10-30 real-run comparison)

Bucketing every effect's own start time into 10-second windows across a full real song:

| | Reference (human-sequenced) | AI-generated |
|---|---|---|
| Starts/10s, full song range | 32 – 149 (**4.7x** spread) | 296 – 357 (**1.2x** spread) |

The human reference's own density swings sharply with the song's structure — audibly quiet stretches
(bridge/breakdown) measurably show far fewer effect starts per second than the chorus/drop. The AI-generated
version's density is essentially flat regardless of section — `LookCompiler`'s own always-moving/discrete
cadence machinery (`AlwaysMovingGapSplitter`, `DutyCycleTable`) is successfully keeping every prop in motion,
but nothing currently pushes that motion's own *rate* up or down with the song's own energy, so a listener
hears a dynamic shift the light show does not visually register.

A second, related finding from the same comparison: the AI-generated version's longest single effect anywhere
was under 7 seconds, versus the reference's longest running close to the full song length (a steady
color-wash/VU-meter-style base layer). The reference's short accent effects sit *on top of* a long anchor
layer; the AI version has no anchor layer at all, which likely compounds the flat-density problem (there is
nothing slow for the pace-of-change metric to settle toward during a quiet section).

---

## 2. The craft brief: what "density should track energy" means concretely

- A section's own already-computed energy signal (RMS from `BandEnergyEnvelope`, section-kind-driven
  ceilings already in `EnergyCeilingByKind`) should translate into a **measurable ceiling and floor on how
  often effects change** within that section — not just which effect/Look is picked, which is already
  energy-aware via `Look.EnergyCeiling`/`SectionMusicContext.EnergyCeiling`.
- A quiet section's own subject layer should be allowed (and, ideally, pushed toward) a long-duration Mode A
  bound layer — the "hold is a steady state, never a dead one" rule this project's own prompt guidance
  already states — rather than only ever compiling as many short Mode B discrete chunks.
- This is a *section-level* ceiling on top of `LookLayer.Duty`/`Timescale`'s own existing per-layer choices,
  not a new compilation mechanism — the same "reuse what already exists, add one more constraint" discipline
  every other Phase 10/11 change already follows.

## 3. Proposed technical changes

### 3a. A measured per-section target change-rate
1. Compute a per-section target change-rate (effect starts per second) derived from that section's own
   energy signal — e.g. a simple monotonic mapping from `EnergyCeilingByKind`/measured RMS to a change-rate
   band, calibrated against the ~5x real-world spread measured in §1a (a quiet section's target should be a
   clear fraction of a loud section's, not a rounding difference).
2. Surface this target to `LookCompiler`'s existing duty-cycle/timescale selection as a section-level
   ceiling/floor — e.g. clamping how short a Mode B layer's own inter-mark duty cycle may go, or how many of
   a Look's own layers may run in fast/discrete mode at once — without inventing a new compiled-IR concept.

### 3b. A "steady base layer" expectation
1. Add Director prompt guidance (and, once real data on what "too short" looks like exists, a
   `ChoreographyLegalityValidator` check) encouraging every section's own subject layer to include at least
   one layer whose own duration covers a large fraction of that section's own window — directly closing the
   "no long anchor layer anywhere" gap measured in §1a.
2. This is additive to, not a replacement for, the existing short accent/overlay layers — the reference shows
   both coexisting (a long base layer plus short accents on top), not one replacing the other.

### 3c. A new legality check: declared vs. measured dynamics
1. Add a `ChoreographyLegalityValidator` rule comparing measured change-density across a whole compiled song
   against each section's own declared energy ceiling — e.g. flag a show whose quietest declared section is
   not measurably quieter (by change-rate) than its loudest declared section, the same "guardrail, not a
   quality score" framing every existing rule in that validator already uses (a finding to review, not an
   automatic rejection).
2. Treat this the same way `RuleDeclaredDarkness` already treats near-zero coverage: a soft, inspectable
   signal a human or a later critique pass can act on, not a hard compile failure.

---

## 4. Sequencing and dependencies

Builds on Phase 10 §3a/§3d (real `BandEnergyEnvelope`/vocal-activity wiring, already landed) for the energy
signal this phase's change-rate mapping consumes. Independent of Phase 11 (provider-reliability fix) and
Phase 13 (layering/diversity) — but re-measuring against Phase 11's fix is recommended before finalizing the
specific change-rate calibration in §3a, since the current ~1.2x-vs-4.7x gap may partly reflect a
misconfigured call rather than the model's own ceiling.

## 5. Acceptance criteria

- A real generation on the same reference song shows a measurably wider change-density spread across its own
  sections than the ~1.2x measured in the 2026-10-30 baseline — ideally approaching the reference's own ~4.7x
  range, verified the same way every other Phase 10/11 change was verified: a real `tools/GenerateOnce` run,
  inspected with the same bucketed-start-time method `AI-CHOREOGRAPHY-REAL-RUN-COMPARISON-2026-10-30.md` used,
  not just prompt text.
- At least one generated section's subject layer includes a long-duration base layer covering most of that
  section's own window, confirmed in the compiled IR.
- The new legality check runs against a real compiled show and produces an inspectable, non-crashing result
  either way (no violations, or a real, named violation).
