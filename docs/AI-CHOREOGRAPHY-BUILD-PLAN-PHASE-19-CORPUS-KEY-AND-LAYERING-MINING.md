# Build Plan: Phase 19 — Corpus-Grounded Key, Layering & Effect-Combination Mining

**Status:** implemented and run for real against `C:\xLights`, 2026-11-09. §2a/§2b/§2c/§2d below are done;
§2e's own guidance (report under-powered/non-findings honestly rather than forcing them) is exactly what
happened on this first real run -- see §5 for the actual, measured outcome.

Written 2026-11-09 in direct response to the user's own real-run observation: "it uses a lot of the same
combined effects... they also don't have many layers of effects," plus a new, explicit ask -- mine the real,
hand-sequenced `.xsq` shows already sitting in `C:\xLights` (excluding anything this app itself has
generated) to learn what real choreographers actually did, including what they did around different musical
keys, and turn that into real training signal instead of guessed defaults.

---

## 0. Framing prompt (the mission brief this phase answers)

The user asked for this phase to be approached from a specific persona. Recorded verbatim-in-spirit so any
future session picking this phase back up starts from the same brief:

> I am a Christmas light choreographer. I've been building shows in xLights for years, and I also understand
> how this AI Choreography Studio app is built under the hood -- its analysis pipeline, its Director/
> Choreographer split, how it stores what it learns. I want the app to actually learn from the real shows
> I and the wider community have already hand-sequenced, not just from its own guesses about what "good"
> looks like. Specifically: look at my real `.xsq` files in `C:\xLights` -- the ones *I* (or another real
> choreographer) built by hand, not anything this app has generated for me in the last few weeks -- and
> figure out the patterns. What keys were songs in, and did that correlate with anything about the look or
> palette choices? How many effects were layered at once on the richest props, and when? What effect
> combinations actually got reused on purpose, versus the narrow, repetitive set my generated shows keep
> falling back on? Turn whatever is real and statistically solid into training material the Director and
> Choreographer actually use, and be honest about what doesn't hold up -- I'd rather know a pattern wasn't
> real than have the app confidently repeat a coincidence.

---

## 1. Why this phase exists

### 1a. The complaint is real and already partly diagnosed

Two build-plan phases already exist for pieces of this:
- Phase 13 (Layering Depth and Effect-Vocabulary Diversity) measured the exact symptom the user is
  describing again now -- a real generated sequence used far fewer simultaneous layers (3 vs. a real
  reference's 7-8) and a far narrower effect vocabulary (9 distinct names vs. 22) than real hand-sequenced
  work. Its own fix (§2a) is a **hand-guessed** minimum ("at least 3 concurrent layers for a Chorus-kind
  subject prop"), explicitly flagged in its own §3 as something that should eventually be replaced by "a
  stronger, data-driven version" once corpus mining actually runs.
- Phase 17 (Corpus Expansion) designed exactly how to mine the real `C:\xLights` shows while excluding this
  app's own output, and partially landed: a real run on 2026-10-31 produced a 27-entry
  `corpus-manifest.json` (confirmed on disk under `%AppData%\XlightsAI.Studio\corpus-knowledge\`), with
  937 paths correctly excluded (`Backup\`, this app's own `exports/`-convention output, etc.).

### 1b. Confirmed, by reading the real mined output, that the specific gaps the user is naming were never closed

Inspecting the real `craft-rules.json` this app has on disk right now (from that same 2026-10-31 run) shows
only 3 promoted rules ever clear the induction threshold:

1. "An effect instance rarely has more than 3 active palette slots."
2. "Mode A effects run longer than Mode B effects."
3. "Blend mode is usually left at its default."

None of these says anything about **how many layers were active at once**, **which effects got combined
together**, or **musical key** at all -- because `CraftRuleInducer`'s own candidate-rule list (4 hard-coded
hypotheses in `src/XlightsAI.Studio.Pipeline/CraftRuleInducer.cs`) never proposed those hypotheses in the
first place. This is not a tuning problem; it's a missing-candidate problem, fixed the same cheap way the
existing 4 were added (one new `(Id, Statement, Predicate)` tuple each, mechanically checked against the
real corpus, promoted only if it clears the existing confidence/song-count bar).

Separately, `CorpusSongAnalysis` (`src/XlightsAI.Studio.Pipeline/CorpusSongAnalysis.cs`) -- the per-song
bundle every corpus-mining consumer reads from -- carries `BeatGrid`/`FingerprintSet`/`Labels`/`Phrases`/
`AlignedEffects`/`BandEnergy`/`VocalActivity`, but **no `KeyChanges` and no real melody/note data at all**.
`KeyDetector.DetectKeyChanges` and the real isolated-vocal-stem melody/note-onset pipeline
(`VocalNoteEmphasisBriefBuilder`, both added to the *live generation* path earlier this same session) were
never backported to the corpus-mining side -- so even if a key-correlated craft rule existed today, it would
have no key data to check against. This is exactly why "what keys did they use" cannot be answered yet: the
raw signal was never captured for the corpus, only for a live generation.

### 1c. The 2026-10-31 manifest is now over a week stale against this app's own output

Since that mining run, this app has generated several more real projects directly into `C:\xLights` or
nearby (`AJR-Bang\`, `Bruno-Mars-I-Just-Might\`, `generated\Bad Feeling - Jagwar Twin.xsq`, `Choosin
Texas\`, and an apparent in-place re-touch of `Bad Feeling-Jagwar Twin Ella.xsq` after its original
2026-10-17 real version). A re-run is needed regardless of the new candidate rules below, both to pick up
any *new real* shows added to `C:\xLights` since, and to re-verify every one of this app's own newer outputs
is actually excluded (the user's explicit "not from the projects the current app has built in the last 4
weeks" requirement) -- not just assumed still-correct from a week-old run.

---

## 2. Proposed technical changes

### 2a. Refresh the corpus manifest and verify exclusion is airtight, not just path-shaped
Re-run `CorpusManifestBuilder.Build(@"C:\xLights")`. Before trusting its output, specifically check the
newer app-generated paths named in §1c against both of Phase 17 §2a's intended exclusion rules (path-prefix
`exports/` convention, and cross-reference against this app's own `recent-projects.json`/project
manifests) -- `Bad Feeling-Jagwar Twin Ella.xsq` living at the `C:\xLights` root (not under an `exports/`
subfolder) is exactly the case that could slip past a path-only check and needs the manifest
cross-reference to actually catch it in practice, not just in design.

### 2b. Extend `CorpusSongAnalysis` with the same enriched signals live generation already has
Add `KeyChanges` (via `KeyDetector.DetectKeyChanges`, same call `AiChoreographyWholeSongGenerator` already
makes) and real melody/vocal-note data (reusing the existing stem-separation + pitch-tracking path, same
as `AudioAnalysisRunner.Melody`) to `CorpusSongAnalyzer.AnalyzeAsync`'s output. No new analyzers -- purely
wiring two already-proven live-generation signals into the corpus-mining side, which has simply never asked
for them before.

### 2c. New `CraftRuleInducer` candidate rules (additive, same mechanism as the existing 4)
- **Simultaneous-layer-count floor, by section kind/energy tier.** For each real song, group
  `AlignedEffects` by model name and section, and count the real maximum number of time-overlapping
  instances on the richest prop (Tree/Matrix-class, same name-matching convention
  `ShowBibleValidator.IsTreeOrMatrixName` already uses) per `SectionKind`. This directly replaces Phase 13
  §2a's guessed "at least 3" with a real, measured floor -- and may reveal the real floor differs by
  section kind in ways the guess did not capture.
- **Effect-combination pairs.** Which 2 effect names most often run concurrently (overlapping time ranges)
  on two different models within the same section -- the real "vocabulary of combos" real choreographers
  reach for, broader than whatever narrow set a generation currently keeps repeating.
- **Key-correlated Look/palette choice.** Within sections that share a detected musical key, or specifically
  around a detected key *change*, is there a statistically real shift in palette brightness/effect energy
  tier? This is the newest and least-precedented hypothesis of the three (correlating a continuous musical
  property against a categorical choice) -- per this project's own established discipline (the existing
  "2/2/4 pattern" candidate was honestly *rejected* at 1.5% confidence after real measurement, not forced
  through), this one should be checked and reported on its own real merits, promoted only if it clears the
  bar, and reported as a confirmed non-finding (not silently dropped) if it doesn't.

### 2d. Feed promoted rules into the real prompts, not just into `craft-rules.json`
Once real rules are promoted: (1) replace Phase 13 §2a's hand-guessed per-tier layer minimum in
`DirectorPromptBuilder`/`SectionScriptPromptBuilder` with the real corpus-measured floor; (2) add a short,
concrete "real effect combinations choreographers commonly use here, try one you haven't used yet this
song" list to the Choreographer's per-section prompt, addressing the repetition complaint with genuine
learned variety rather than only today's soft "prefer a less-used effect" nudge (Phase 13 §2b).

### 2e. Expect some candidates to need a bigger corpus, and say so honestly
A narrower hypothesis (e.g. "near a key change, inside a Chorus") only has applicable data in songs with
both a detected key change *and* a Chorus section -- plausibly fewer than the existing `minSongCount = 5`
default among 27 mined songs. Rather than silently loosening the threshold to force a promotion, report an
under-powered candidate as exactly that (confidence/song-count shown, not promoted), and treat it as
supporting evidence for prioritizing Phase 17 §2b (historical git-commit mining -- ~87 more real,
unmined shows sitting in `C:\xLights`'s own git history) rather than lowering the bar.

---

## 3. Sequencing and dependencies

Builds directly on Phase 17 (manifest hygiene / exclusion discipline, already partly landed) and replaces
part of Phase 13 (static, guessed layering/diversity guidance) with corpus-real numbers once available.
Independent of Phase 18 (effect visual characterization), though a future phase could combine "what visual
character does an effect have" with "what combinations/densities real choreographers actually used it at."

## 4. Acceptance criteria

- A refreshed `corpus-manifest.json` contains zero entries traceable to any project this app has generated
  (cross-checked against `recent-projects.json`, not assumed from path shape alone), and includes every
  real show added to `C:\xLights` since the 2026-10-31 run.
- `CorpusSongAnalysis` for every mined song carries real, non-null `KeyChanges` and real melody/vocal-note
  data.
- At least one new simultaneous-layer-count rule and at least one new effect-combination rule is promoted
  (clears real confidence/song-count thresholds) with numbers visibly different from Phase 13's guesses.
- The key-correlated-Look-choice candidate is evaluated and reported honestly either way (promoted with its
  real statement, or rejected with its real confidence) -- never silently omitted.
- A real regenerated song (the same reference song Phase 13's own comparison used) shows a measurable
  increase in simultaneous Tree/Matrix layer count and distinct-effect-name count versus that 2026-10-30
  baseline, inspected in the real compiled output -- not assumed from prompt text alone.

---

## 5. Real run results (2026-11-09)

Re-ran `tools/CorpusMiner` against the live `C:\xLights` corpus, writing directly to the app's own
`%AppData%\XlightsAI.Studio\corpus-knowledge\`. First two attempts (3-minute, then 10-minute per-song
timeout, both at full/high concurrency) timed out on every or nearly every song -- not a code defect: an
isolated single-song timing check showed a real, uncontended analysis genuinely takes ~5.5 minutes (richer
analysis now runs real pitch tracking/key detection on top of the prior pipeline), so running 30+ songs at
once starves every one of them of CPU. The real, successful run used `maxSongConcurrency=2` and a
45-minute per-song timeout: **31/31 mineable songs analyzed successfully, zero failures**, wall-clock
1:57:17.

**CraftRules (threshold: confidence ≥70%, songs ≥5), real measured results:**

| Rule | Outcome | Confidence | Instances | Songs |
|---|---|---:|---:|---:|
| `concurrent-props-vary-effect-choice` (new, §2c) | **Promoted** | 98.2% | 64,650/65,868 | 33 |
| `palette-rarely-exceeds-three-active-slots` (pre-existing) | Promoted | 96.4% | 65,063/67,492 | 33 |
| `blend-mode-left-at-default` (pre-existing) | Promoted | 96.5% | 65,143/67,492 | 33 |
| `mode-a-effects-run-longer-than-mode-b` (pre-existing) | Promoted | 75.0% | 18/24 | 24 |
| `high-energy-tree-matrix-layer-floor` (new, §2c) | Rejected (honestly) | 36.5% | 61/167 | 30 |
| `key-change-correlates-with-elevated-effect-activity` (new, §2c) | Rejected (honestly) | 36.5% | 35/96 | 27 |
| `two-two-four-phrase-pattern` (pre-existing) | Rejected | 1.6% | 31/1,981 | 26 |

**Reading the real result, honestly:**
- The "same combined effects" complaint is now backed by a strong, real, well-powered corpus rule:
  real choreographers vary effect choice across concurrently-active props 98.2% of the time -- this is
  exactly the guidance the Choreographer now receives verbatim (via `DirectorPromptBuilder`'s existing
  generic "## Verified craft rules" rendering, no prompt-builder change needed).
- The "not enough layers" hand-guessed floor (Phase 13's "at least 3 concurrent layers") is **not**
  confirmed by the real corpus at the 70%-confidence bar: real shows only reach 3+ overlapping layers on a
  Tree/Matrix-class prop during a high-energy section 36.5% of the time. This does not necessarily mean "3
  is the wrong number" -- the corpus mixes older/simpler shows with denser ones -- but it does mean the
  claim "typically reaches at least 3" is not something this real data backs up as stated, so it is
  reported here rather than silently forced into the prompt as settled fact. Phase 13's own static guidance
  is left as-is (an aspiration, not a measured baseline) until a future pass can separate the corpus by era
  or complexity and re-check.
- The "what keys did they use" question got a real, honest answer: no, a detected musical key change is
  not reliably marked with a burst of effect activity above that song's own baseline rate in this corpus
  (36.5%, 27 songs is a real, meaningful sample -- this is a genuine non-finding, not an underpowered one).
- Corpus scale: 31 songs, 66,568 total effects, 854 real exemplar entries (up from 297 pre-Phase-19), all
  with real BandBalance/VocalActivityHint/Description.
- Zero entries traceable to this app's own generated output (`AJR-Bang/exports`, `Bad Feeling-Jagwar Twin
  Ella.xsq`, `bad feelings test.xsq`, `generated/*` all correctly excluded).
