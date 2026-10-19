# Architecture and Non-Negotiable Guardrails

## Structure
Use a small modular monolith: `Desktop` (Avalonia feature views/viewmodels), `Core` (music/display/choreography/edit contracts), `Infrastructure` (SQLite, audio, native inference, xLights adapters), and `Tests`. Prefer feature-local classes and avoid abstraction for hypothetical backends.

## Data boundaries
- **ShowImport:** reads existing xLights show; never mutates source by default.
- **SongAnalysis:** precise timed events, source stem, confidence and fingerprint; cached by original audio hash + algorithm/model version.
- **DisplayGraph:** stable identifiers and spatial ordered paths; manual overrides retained.
- **ChoreographyPlan:** renderer-independent semantic gestures, roles, timing, conditions and motif links.
- **SequenceDocument:** editing source of truth with editable effects, locks, scope and change history.
- **XSQAdapter:** explicit target-version writer/parser. Treat `.fseq` as rendered channel/frame output, not editable source.
- **RenderAdapter:** version- and capability-aware xLights integration; test real installed builds before assuming headless features exist.

## Scope protection
Use half-open time intervals `[startMs, endMs)` in canonical time; expose UI selection semantics clearly. An edit can touch only effect IDs/models/intersections authorized by a validated `EditScope`. Approved/locked effects cannot be changed by automatic routines. Persist before/after diffs, expected document version, provenance and undo transaction. Atomic file writes and collision-safe export paths are mandatory.

## Loop execution
Use a state machine: `Queued -> Analyzing -> Planning -> Candidate -> Validating -> PreviewReady -> Accepted/Rejected/Blocked`. Persist candidate seeds, scores, model versions, selected scope and best-so-far. Bounded attempts with explicit stop conditions. Deterministic validators own correctness; optional LLM agents propose creative modifications through strict schemas.

## CI and UX
Require tests for headless data transformations, golden fixtures, Avalonia viewmodel behavior, SQLite migrations and xLights-generated artifacts. Have a reusable sample layout and licensed/created sample audio. Every completed slice must be demonstrated through UI or real xLights output.

## Security, privacy and licensing
No hidden upload of songs/show files, no telemetry without opt-in, no API keys in project files/logs. Download native/ML assets via verified URLs and checksums; maintain attributions for all dependencies and model weights. Distinguish code's license from model licenses. Avoid promising open-source redistribution of incompatible dependencies until reviewed.

## Platform coverage
The product is Windows-first today (the App project is `WinExe` with a Windows `app.manifest`, and native adapters like audio/inference are built against Windows-only APIs as each phase needs them), but Avalonia itself and the rest of the architecture (domain contracts, SQLite, XSQ adapters, view models) are already OS-agnostic — nothing here is meant to permanently exclude macOS or Linux.

Whenever a step ships a concrete, OS-specific implementation of an interface (native P/Invoke, a platform SDK, a vendor-specific executable path, etc.) and no cross-platform equivalent exists yet for the other desktop platforms:
- **Never leave the other platforms unhandled.** A composition root must not let an unsupported platform reach a raw native failure (a missing-library exception, a null pointer, a silent no-op) when a user on that platform exercises the feature.
- **Add a stub implementation of the same interface** for the uncovered platform(s) that throws one clear, specific, typed exception identifying exactly what is missing (name the feature and the platform) the moment the feature is actually used — not at construction/composition time, since merely wiring up the stub must never itself fail.
- **Select the real vs. stub implementation via an explicit `OperatingSystem.IsWindows()` (or equivalent) guard** at the composition root (e.g. `Program.cs`/`App.axaml.cs`, and any view model's own default-collaborator fallback), not a ternary buried deep in unrelated logic — this also keeps the CA1416 platform-compatibility analyzer clean without blanket warning suppression.
- **Record the gap** in that step's own `implementation-evidence/PHASE-XX/STEP-YY.md` (§3 architecture deviations, §6 known gaps) and, if it is a new class of gap, here.
- A stub's read-only/status members (e.g. `IsPlaying`, `CurrentSong`) should return safe neutral defaults rather than throw, so simply binding a view to the stub never crashes the UI; only the actions that would require the missing real backend should throw.

First concrete example: `XlightsAI.Studio.Audio.UnsupportedPlatformAudioTransport` (PHASE-04 Step 025) is the stub `IAudioTransport` for every non-Windows platform, paired with the real `WaveOutAudioTransport` (Windows `winmm.dll`). See `implementation-evidence/PHASE-04/STEP-25.md` for the worked example of this pattern.
