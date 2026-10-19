# Coding Prompt — Phase 04: Audio Waveform and Synchronized Playback

This prompt is paired with [the fully standalone phase specification](../phase-specs/PHASE-04.md). **Read that complete Markdown first**. All required design facts, interfaces, class diagram, process flowchart, individual steps, tests and gate information are inside it.

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 04 (Audio Waveform and Synchronized Playback) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-04/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

Build phase 04 only (024–030).
Working product behavior: As a sequencer, I can hear a song while zooming into its waveform and precisely align a light effect with a transient, without losing sync when I seek or loop.
Explicit exclusions: No automatic beat detection or stem separation; no assumption that waveform amplitude alone represents tempo.
Inputs: User-chosen local media (MP3/WAV initially), project media policy, existing editor timeline.
Outputs: `SongAsset` and playback metadata; cached waveform pyramid; unified position/seek/loop clock exposed to timeline.
Mandatory gate: Import a click track, display waveforms, drag an effect to a click, loop 1 second, seek to song midpoint and back, close/reopen and confirm saved media identity and correct timing.
Do not start Phase 05 without recorded validation.
```

## Steps to implement

- [ ] **024** Import supported local audio and compute canonical audio metadata and hash. **Acceptance:** Invalid/corrupt files fail clearly; duration/sample-rate verified.
- [ ] **025** Add audio playback abstraction and actual desktop audio backend. **Acceptance:** Play/pause/seek works without Python; missing output device handled.
- [ ] **026** Generate zoom-independent multiresolution waveform peaks cache. **Acceptance:** Cache reopens without recompute; envelope sample checks.
- [ ] **027** Render synchronized waveform beneath ruler. **Acceptance:** Waveform position matches timeline coordinates.
- [ ] **028** Use one master media time clock for playback and playhead. **Acceptance:** Repeated seeks do not accumulate drift.
- [ ] **029** Add zoom-to-selection, A/B loop playback and keyboard navigation. **Acceptance:** Selected short range loops within agreed timing tolerance.
- [ ] **030** Add frame/beat/off-grid snapping and timing-diagnostics panel. **Acceptance:** Time-to-frame conversion and boundary rounding golden tests.

## Tests specifically required

- [ ] WAV and supported MP3 probe metadata match independently verified duration
- [ ] Waveform pyramid selects correct resolution after zoom
- [ ] Seek to 0/50%/99% preserves common playhead and effect alignment
- [ ] Loop [2.5s, 3.5s) audibly and visually repeats without accumulating drift
- [ ] Invalid or missing media cannot corrupt project
- [ ] Audio backend device loss is surfaced and recoverable
- [ ] Waveform cache invalidates when content hash changes

## Evidence

Create one step evidence Markdown per numbered task and a phase gate report. If source xLights, an inference backend, licensed media or a clean installation is absent, mark that gate blocked rather than treating a mock as success.
