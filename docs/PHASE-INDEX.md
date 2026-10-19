# Phase Index — 16 Self-Contained Build Specifications

Each phase document includes the full set of design decisions, public signatures, Mermaid class relationships, a Mermaid core flowchart, screen references, implementable steps and validation gates. This index is optional; the phase files are standalone specs.

| Phase | Development steps | Architect specification | Coding-agent prompt | User-visible gate |
|---:|---|---|---|---|
| 01 | 001–008 | [Phase 01](phase-specs/PHASE-01.md) | [Prompt](prompts/PHASE-01-PROMPT.md) | Start a fresh install; create “Demo Song”, close/reopen; open a second project; inject a migration f… |
| 02 | 009–015 | [Phase 02](phase-specs/PHASE-02.md) | [Prompt](prompts/PHASE-02-PROMPT.md) | Import a sample layout with Roofline, Arch 1 and Arch 2; click Generate 3 Effects; save into app exp… |
| 03 | 016–023 | [Phase 03](phase-specs/PHASE-03.md) | [Prompt](prompts/PHASE-03-PROMPT.md) | Open a three-effect sample, drag Arch 1 effect, resize Roofline, undo/redo both operations, lock Arc… |
| 04 | 024–030 | [Phase 04](phase-specs/PHASE-04.md) | [Prompt](prompts/PHASE-04-PROMPT.md) | Import a click track, display waveforms, drag an effect to a click, loop 1 second, seek to song midp… |
| 05 | 031–037 | [Phase 05](phase-specs/PHASE-05.md) | [Prompt](prompts/PHASE-05-PROMPT.md) | Analyze a click/drum sample, see beat markers, correct an intentionally wrong downbeat, generate res… |
| 06 | 038–044 | [Phase 06](phase-specs/PHASE-06.md) | [Prompt](prompts/PHASE-06-PROMPT.md) | On CPU-only Windows, install model through app (or use offline pre-provisioned asset), separate a sh… |
| 07 | 045–051 | [Phase 07](phase-specs/PHASE-07.md) | [Prompt](prompts/PHASE-07-PROMPT.md) | Import trusted MIDI or simple isolated melody, view notes on piano roll, correct an incorrect note, … |
| 08 | 052–058 | [Phase 08](phase-specs/PHASE-08.md) | [Prompt](prompts/PHASE-08-PROMPT.md) | Analyze a song with repeated chorus; show two matching phrases highlighted; accept the first chorus … |
| 09 | 059–065 | [Phase 09](phase-specs/PHASE-09.md) | [Prompt](prompts/PHASE-09-PROMPT.md) | Load a layout, inspect suggested arch and roofline paths, reverse the arch route manually, preview C… |
| 10 | 066–071 | [Phase 10](phase-specs/PHASE-10.md) | [Prompt](prompts/PHASE-10-PROMPT.md) | Choose lead piano on arches, kick/snare on roofline and bass on tree for a 20-second song excerpt; p… |
| 11 | 072–078 | [Phase 11](phase-specs/PHASE-11.md) | [Prompt](prompts/PHASE-11-PROMPT.md) | Select 42–48 seconds on five arches, ask for rising piano pops, inspect affected models/times and be… |
| 12 | 079–084 | [Phase 12](phase-specs/PHASE-12.md) | [Prompt](prompts/PHASE-12-PROMPT.md) | Start with a knowingly over-busy 8-bar phrase, generate three candidates, inspect density/timing met… |
| 13 | 085–089 | [Phase 13](phase-specs/PHASE-13.md) | [Prompt](prompts/PHASE-13-PROMPT.md) | Preview simple arch stepping, export a temp XSQ and render it using a verified installed xLights mod… |
| 14 | 090–093 | [Phase 14](phase-specs/PHASE-14.md) | [Prompt](prompts/PHASE-14-PROMPT.md) | Take a song with Intro/Verse/Chorus/Verse/Chorus/Bridge/Final chorus; show section intensity arc and… |
| 15 | 094–097 | [Phase 15](phase-specs/PHASE-15.md) | [Prompt](prompts/PHASE-15-PROMPT.md) | Install on a clean Windows x64 machine, import sample show/song, generate/edit a simple XSQ, optiona… |
| 16 | 098–100 | [Phase 16](phase-specs/PHASE-16.md) | [Prompt](prompts/PHASE-16-PROMPT.md) | Give a non-developer tester only the installer and demo show; observe install, import, song analysis… |

## Execution algorithm

1. Read the entire selected phase specification, not merely its steps table.
2. Compare proposed signatures to current repo. Preserve backwards compatibility and record ADRs for deviations.
3. For each step, write/prove tests and show observable output; record evidence at `implementation-evidence/PHASE-NN/STEP-XXX.md`.
4. Run full regression and manual phase gate. If external xLights/native inference unavailable, mark blocked, not passed.
5. Only then start next phase.

For all code and hardware constraints see [Master System Architecture](MASTER-SYSTEM-ARCHITECTURE.md).
