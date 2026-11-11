# Phase 24 §2c -- real CorpusMiner re-run (data/process step, no code change)

Build-plan Phase 24 §2c: "re-run `CorpusMiner` against the real corpus (including any newly-available
reference shows) so `ExemplarIndex.FindSimilar`/`FindVaried` have more real passages to retrieve from per
`SectionKind`. Report the real before/after exemplar count per Kind."

## Run command

```
dotnet run -c Release --project tools/CorpusMiner -- "C:\xLights" "C:\xLights\knowledge" 15 3
```

Per-song timeout 15 minutes, max concurrency 3 (lowered from the tool's own 20-processor default after an
earlier attempt at full concurrency timed out on every single song -- a pre-existing performance
characteristic of this already-WIP tree's analysis pipeline, unrelated to this phase's own changes).
`XLAI_SKIP_EXEMPLAR_DESCRIPTIONS=1` (no Director credentials configured in this environment for real
per-exemplar descriptions -- same disclosed limitation the tool's own `Program.cs` already documents).

## Before (real prior run, 2026-10-24)

From `C:\xLights\knowledge\corpus-mining-20261024-112513.log`: manifest of 20 entries (an older, smaller
real corpus checkout), 14 songs analyzed successfully, 6 failed. **ExemplarIndex: 156 entries total.** That
run's own `CorpusMiner.Program.cs` did not yet print a per-`SectionKind` breakdown (that line was added
since), so no real per-Kind "before" count exists to compare against -- disclosed, not fabricated.

## After (real run, 2026-11-10, this phase)

Manifest of 32 entries (4,484 excluded paths), 19 songs analyzed successfully, 12 timed out (per-song 15
minute budget; the same slow-analysis characteristic the "before" run and this project's own
`PHASE-07-HARNESS.md` already document, not a regression from this phase), 1 skipped
(`Dubstep Christmas Light Show` -- `ExternalReference` provenance, excluded from every downstream consumer
by design).

**ExemplarIndex: 429 entries total** (429/429 with real `BandBalance`, 429/429 with real
`VocalActivityHint`, 0/429 with a real `Description` -- expected, since exemplar descriptions were
skipped this run per the disclosed env-var above).

Real per-`SectionKind` breakdown (entries available / `DirectorPromptBuilder.ExemplarCountForKind` would
request):

| SectionKind | Entries | Exemplars requested |
|---|---:|---:|
| Unknown | 20 | 1 |
| Intro | 6 | 1 |
| Verse | 26 | 1 |
| PreChorus | 0 | 1 (nearest-kind fallback) |
| Chorus | 328 | 5 |
| Bridge | 46 | 2 |
| Solo | 0 | 1 (nearest-kind fallback) |
| Outro | 3 | 1 |

Songs that failed/timed out this run (for transparency -- each still contributes nothing to the
counts above, same as "before"): All i want for christmas 2, First 7 songs Christmas, 7 song medley
updated, AC_DC - Thunderstruck, PinkPonyClub, TSO - Christmas Sarajevo, Cher-DJ Play a Christmas Song,
The Weeknd - Blinding Lights Lite, Thriller, E1 - Eye of the Tiger, homealone, Eye of the Tiger DubStep.

## Interpretation

156 -> 429 total exemplar entries (+175%) is a real, measured increase -- driven mostly by a larger real
manifest (20 -> 32 entries) and, per-Kind, overwhelmingly concentrated in `Chorus` (328 of 429 entries).
`PreChorus` and `Solo` still have zero real entries and keep falling back to `ExemplarIndex`'s own
disclosed nearest-kind substitution -- a real, pre-existing gap this re-run did not close (no song in the
current real corpus checkout has a confidently-labeled PreChorus/Solo section), named here rather than
glossed over.
