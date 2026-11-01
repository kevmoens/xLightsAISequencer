# Build Plan: Phase 17 — Corpus Expansion (current files + git history)

**Status:** designed (5 design decisions confirmed with the user 2026-10-31 -- §2e and §2f folded in after
Phase 16 landed), not yet implemented. Depended on Phase 16 landing first (see Phase 16 §3) so this mining
work happens once, against corrected retrieval logic and a final schema -- confirmed done: Phase 16 re-run
against the real `C:\xLights` corpus on 2026-10-31 produced 297 exemplars (13/20 songs analyzed; 7 failed
-- see §1c below, a new finding from that same real run) with 100% real BandBalance/VocalActivityHint/
Description coverage. §1c's 5 broken-vendor-copy entries have since been fixed in `CorpusManifestBuilder`
(swapped to working root-level duplicates), and `CorpusMiningRunner` now skips analyzing `ExternalReference`
entries entirely (confirmed via the same real run: the reference show took 20+ minutes and contributes zero
exemplars regardless, since every downstream consumer already excludes that provenance) -- a corpus re-run
with both fixes has not yet completed as of this doc update.

---

## 1. Why this phase exists

The real corpus mined so far is 6 songs (`corpus-manifest.json`'s own `RootDirectory`:
`C:\Code\xLightsAISequencer-scratch\corpus-subset`), producing 181 exemplars and only 2 craft rules
that cleared the confidence/song-count threshold (Phase 16 §1a). There is substantially more real,
hand-sequenced material available than this, in two places.

### 1a. Real shows already sitting in the current `C:\xLights` working tree

`find C:\xLights -iname "*.xsq"` returns 961 files, but the overwhelming majority are noise for this
purpose:
- `Backup\*` — xLights' own repeated auto-backup snapshots of the same handful of shows (hundreds of
  near-duplicate timestamped copies).
- This app's own prior AI-generated output (e.g. `Choosin Texas\export1\sequence.xsq`, and likely
  `bad feelings test.xsq`/`WISCANTHEM.xsq`, matching project names this app itself has generated into —
  confirmed against `recent-projects.json`). **These must be excluded from any corpus-mining input** —
  mining the app's own prior generations would feed its existing patterns back into its own reference
  material, reinforcing sameness rather than introducing real variety (explicit user direction: "skip
  any exports from projects of our app").

After excluding both, roughly **20–28 distinct real hand-sequenced shows** remain unmined (exact count
needs a proper dedup pass — several titles appear at both the `C:\xLights` root and nested inside their
own project subfolder, almost certainly the same file copied twice).

### 1b. Real shows that no longer exist in the working tree at all, but survive in git history

`C:\xLights` is itself a git repository (`git -C C:\xLights log` — 18 commits, branch `2024Christmas`,
spanning "2022 and prior shows from kevin15" through "2025 christmas updates"). Several seasons' worth
of real shows were later **removed** from the working tree as newer seasons replaced them (e.g. commit
`4f0d3a7 remove 2023 show files` immediately follows `749d18c 2023 show`) — meaning they are invisible
to any mining pass that only scans the live directory, but are fully recoverable from history.

Per the user's request, commit `749d18cdeaca44d24fe0f746908d5ebb678ee869` ("2023 show") was inspected
directly as a concrete example. It alone adds **17 real, fully-paired `.xsq`+`.mp3`(+`.fseq`) shows**
that are gone from today's working tree:

```
AC_DC - Thunderstruck (Official Video).xsq
All I Want For Christmas Is You shortened.xsq      (also still in the current tree, under a different title)
Alvin and the Chipmunks.xsq
Baby Sharks Christmas.xsq
Carol Of The Bells (Instrumental).xsq
Carol of the Bells.xsq
Cartman Christmas.xsq
Eagles -Please Come Home for Christmas.xsq
Existence (VIP) - Shortened.xsq
Eye of the Tiger DubStep.xsq
Nutcracker Suite.xsq
Pac Man Sequence.xsq
Taylor Swift 25112023 Mash Up.xsq
Toy Sack.xsq
winter-wonderland-lyrics-pitch-perfect-2.xsq
```
(plus incremental edits to `Grandma Got Runover.xsq`/`Mr Grinch2018final.xsq`, already present earlier).

The repo's full commit list (oldest to newest) suggests at least 3-4 more seasons' worth of similarly
recoverable material, especially the earliest commit (`8932a18 "2022 and prior shows from kevin15"`),
not yet individually inspected.

**Update 2026-10-31 (systematic scan, not just the one-commit spot check above):** a full `git ls-tree`
pass across all 10 relevant historical commits (`8932a18`, `372e9b9`, `2dabbea`, `3c70b9b`, `da8cd81`,
`749d18c`, `cfc240e`, `964584a`, `48c2dea`, `49ef88d`), diffed by filename against every real `.xsq`
in the current working tree (excluding `Backup\`/`RenderCache\`), found **87 distinct `.xsq` filenames
that exist somewhere in history but nowhere in the current tree** -- substantially more than the
17-from-one-commit figure above. Includes `Existence (VIP) - Shortened.xsq` (named by the user, §2f) plus
`Alvin and the Chipmunks.xsq`, `Carol of the Bells.xsq`, `Thriller.xsq`, `Nutcracker Suite.xsq`,
`Memories - Maroon 5.xsq`, `LET IT GO.xsq`, and 80 more spanning every season from 2022 through 2025.
This is a filename-only diff (no content-hash dedup yet -- that's §2b's own job once this phase actually
extracts and mines these commits), so the real distinct-show count after dedup will be somewhat lower, but
the opportunity is clearly much larger than originally scoped.

### 1c. 7 of 20 working-tree songs failed/timed out on the real 2026-10-31 run, and git history may fix some

Confirmed against the real Phase 16 verification run (20-song `C:\xLights` corpus, concurrency 6,
20-minute per-song timeout): 13/20 analyzed successfully, 7 did not --

- 5 real `XsqStructuralReadException`s, all the same root cause: a timing-track mark with a
  non-positive interval (`[X, X)`, zero-width) -- `Mashup.xsq` ('Beats'), `Nutcracker Dubstep.xsq`
  ('Beat Count'), `The Weeknd AG - Save Your Tears HD.xsq` ('Abel2'), `The Weeknd - Blinding Lights3.xsq`
  ('Beats'), `Christmas Eve Sarajevo.xsq` ('Sarajevo'). This is a real parser bug in
  `XsqStructuralReader`, not a Phase 16 regression -- out of this phase's scope to fix directly, but §2b's
  historical-commit extraction gives a free second chance: an earlier season's commit may carry a version
  of the same show saved before whatever edit introduced the zero-width mark.
- 2 timeouts (`PinkPonyClub`, `Dubstep Christmas Light Show`) that were still queued/running when the run
  was nearly done -- plausibly just unlucky scheduling under concurrency (last to get a slot) rather than
  a real per-song hang; re-running each alone (no contention) is the cheap way to tell the two apart
  before concluding either needs a data fix.

### 1d. No way today for a user to see or curate what the Director actually draws from

Separately raised by the user (2026-10-31, after seeing Phase 16's real per-entry `Description` field
land): `ExemplarIndex`/`PatternLibrary`/`CraftRules` are already plain JSON on disk (never hardcoded --
`JsonExemplarIndexStore`/`JsonPatternLibraryStore`/`JsonCraftRulesStore`, read fresh by
`AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync` on every generation), but there is no UI to
browse them, and `ExemplarIndex.FindSimilar` always searches every entry -- there is no way for a user to
exclude a specific song's material, or a specific bad exemplar, from influencing generation.

---

## 2. Proposed technical changes

Three design decisions were made with the user, in order; each subsection is one of them.

### 2a. Decision: exclude this app's own output via path convention AND project-manifest cross-reference

Run (or extend) `CorpusManifestBuilder.Build` (`src/XlightsAI.Studio.Knowledge/CorpusManifestBuilder.cs`)
against `C:\xLights`, excluding `Backup\` (path-prefix exclude) and this app's own generated output via
**both** of: (1) any `.xsq` living under a path segment named `exports` (this app's own convention,
confirmed in `ChoreographyGenerationPipeline`'s `exportsDir`/`XsqExporter` output location), and (2)
cross-referencing this app's own `recent-projects.json`/project manifests to exclude any show directory
this app itself created, even if a generated file was later copied/moved outside its original `exports/`
folder. Catches more cases than either check alone.

### 2b. Decision: extract and mine one historical commit per season, then content-hash dedup

`CorpusManifestBuilder.Build(rootDirectory)` only scans a live directory — it has no git awareness, and
should not gain any (keeps it a pure, testable, I/O-simple builder). Instead: for each of the ~6
season-representative commits (one each from roughly `8932a18`, `749d18c`, `cfc240e`, `964584a`,
`48c2dea`, `49ef88d` — not every one of the repo's 18 commits), export that commit's tree into a scratch
directory (`git archive <commit> | tar -x -C <scratch-dir>`, or a `git worktree add` at that commit) and
point the existing, *unmodified* `CorpusManifestBuilder`/mining pipeline at that scratch directory — same
code path as mining any other folder, just pointed at a historical snapshot instead of the live one.
After mining all seasons, deduplicate by content hash so the same unedited show carried across multiple
seasons' commits produces exemplars only once — extending the existing
`CorpusManifestEntry.DuplicatePaths` concept (already present in the current `corpus-manifest.json`)
rather than inventing a new mechanism.

### 2c. Decision: record the source commit on every historically-mined entry

Extend `CorpusManifestEntry`'s own provenance shape to also carry a commit hash (or an explicit
"working tree" marker for non-historical entries), so any exemplar can be traced back to exactly which
real file, season, and commit produced it — useful for debugging a wrong-looking exemplar later, or for
re-mining just one season without touching the rest.

### 2d. Re-run the full mining pipeline once, against Phase 16's corrected schema

Per Phase 16 §3's sequencing note — do this after Phase 16 lands, not before, so every one of these
~20-28+ newly-discovered working-tree songs plus however many historical ones get mined once, with
working `FindSimilar` section-kind filtering, real per-entry descriptions, and real band-energy/
vocal-activity analysis already run as part of this same pass (Phase 16 §2c).

### 2e. Decision: a user-curatable exemplar library, folded into this phase

Per §1d: add a persisted, per-exemplar enabled/disabled flag (keyed by a stable identity -- song title +
bar range, since an entry has no other natural key) stored *separately* from `exemplar-index.json` (e.g.
`exemplar-overrides.json`, same "overrides survive a re-mine" convention
`JsonSectionLabelOverrideStore`/`JsonPhraseBoundaryOverrideStore` already establish elsewhere in this
codebase) so re-running corpus mining never silently resets a user's curation choices. `ExemplarIndex.FindSimilar`
(or a thin wrapper around it) excludes disabled entries from candidates before ranking. A new Shell screen
lists every entry (song, bar range, section kind, tempo, energy percentile, and -- thanks to Phase 16 §2b
-- its real description) with a toggle, grouped by source song so a user can disable/enable a whole show's
worth of material in one action as well as individual entries. Exact screen layout/filtering (by kind,
by song, search) not yet designed -- a reasonable first cut is a flat sortable list, refined once a user
has tried it against the real ~300-entry index.

### 2f. Decision: a "favorite song" gets heavier weight in CraftRuleInducer, not just retrieval priority

Per the user (2026-10-31, naming `Existence (VIP) - Shortened.xsq` -- a real show recoverable only from
§1b's git history, not present in the current working tree at all, confirmed by a full-tree search finding
zero matches): a song marked "favorite" should count extra toward `CraftRuleInducer.InduceAndVerify`'s own
confidence/song-count statistics (e.g. counted as if it were N songs instead of 1), not only get a
retrieval-ranking boost in `ExemplarIndex.FindSimilar`. This is explicitly broader than §2e's per-entry
enable/disable flag -- a favorite is a positive multiplier on a whole song's statistical weight in the
*induced craft rules themselves* (the baseline guidance every generation gets), not just which worked
examples get shown for a given section kind. Mechanism and exact weight not yet designed (e.g. literally
duplicating a favorited song's `CorpusSongAnalysis` N times before calling `CraftRuleInducer`, vs. a new
first-class per-song weight parameter `CraftRuleInducer` accounts for directly) -- needs its own design
pass before implementation, and the disclosed overfitting risk (a craft rule becoming confidently "induced"
from what is really one show's own idiosyncrasy, amplified) should be weighed explicitly when that happens.
Blocked on §2b/§1b actually landing first, since the specific named example doesn't exist in the corpus
until historical-commit mining recovers it.

---

## 3. Open questions still not decided

- Expected final exemplar count and per-`SectionKind` distribution once mining completes, to confirm the
  Solo/PreChorus gap (Phase 16 §1a) is actually closed and not just diluted.
- Exact content-hash dedup strategy for §2b (byte-identical file hash vs. a fuzzier "same song, minor
  re-edit" similarity check — a show re-opened and lightly touched between seasons would have a
  different exact file hash despite being substantially the same real sequence).

## 4. Acceptance criteria

- The corpus-mining pipeline runs, unmodified in its own core logic, against both the current working
  tree (minus `Backup\`/this app's own output, excluded per §2a's two rules) and the season-representative
  historical commits from §2b, producing a combined manifest with zero entries sourced from this app's
  own prior generations.
- Every historically-mined exemplar entry records its real source commit hash (§2c).
- The resulting exemplar index measurably closes the Solo/PreChorus/Intro/Outro gaps found in Phase 16
  §1a (nonzero real entries for each, not just more Chorus material).
- No duplicate exemplar entries from the literal same unedited show appearing under two different
  seasons' commits.
- A user can disable an individual exemplar entry or a whole source song's entries, and a subsequent
  generation's Director prompt never includes a disabled entry; re-running corpus mining does not reset
  that choice (§2e).
- At least some of the 5 `XsqStructuralReadException` failures (§1c) are resolved by mining an earlier
  commit's version of the same show instead of (or in addition to) the current broken working-tree copy.
