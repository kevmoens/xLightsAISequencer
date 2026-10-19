# PHASE 13 — xLights Rendering and Visual Evaluation

**Specification version:** 2.0 / 2026-10-10 | **Steps:** 085–089 | **Status:** Proposed, unimplemented until gate evidence exists

**Standalone specification.** This document contains the relevant product assumptions, contracts, class relationships, business flows, numbered implementation tasks, test designs, risk gates and an agent prompt. It does **not** mean Phase 13 can run without earlier implemented code; it means an architect/agent can plan and implement the phase without consulting a separate high-level brief. Code below expresses **proposed API signatures**, not verified source code. Reconcile them with the actual repository and xLights version before implementation.

## 1. User outcome, release boundary and phase gate

**User story:** As a sequencer, I can see a fast local approximation of a phrase, then run xLights itself to confirm how supported effects actually render before accepting an AI revision.

**Owned boundary:** Lightweight preview, installed xLights version/capability probing, authoritative render command (when supported), frame data extraction, visual quality measures and render evidence.

**Out of scope:** No full reimplementation of the xLights C++ rendering engine, no assumption every installed release supports `--headless`, no controller hardware playback.

**Blocking phase gate:** Preview simple arch stepping, export a temp XSQ and render it using a verified installed xLights mode (headless if supported), compare a frame/time metric with the local approximation and show evidence links.

**Depends on:** Phase 12: Loop-Engineered Refinement.

**Unblocks:** Phase 14: Whole Song Composition and Continuity.

The gate is PASS only with automated tests, persisted-output inspection, and a real UI or xLights demonstration appropriate to the phase. If xLights or the required hardware/backend is unavailable, report BLOCKED or NOT VERIFIED—never silently substitute a mock.

## 2. Architectural assumptions (fully restated for standalone use)

- **Product:** locally installable Avalonia/.NET 10 desktop application; no Python, Docker, Git or mandatory LLM subscription for end users. CPU-first inference; native/ONNX only after a demonstrated and legally redistributable runtime/model fit.
- **Topology:** modular monolith with vertical feature slices. UI (Views/ViewModels), application orchestration, pure domain contracts, and platform/infrastructure adapters. Do not fabricate generic interfaces without a concrete boundary.
- **Timing:** internally represent position with an explicit canonical monotonic clock tied to source audio origin. Preserve sample index/rate for analysis provenance; UI may show milliseconds. All time scopes are half-open `[start,end)`. Convert to xLights frames only when explicitly snapping or exporting.
- **Ownership:** SQLite project metadata and committed user edits; immutable source media (linked/copied with consent); hash-addressed and versioned analysis/preview artifacts on disk. Every cache key includes algorithm/model/settings versions.
- **xLights:** `xlights_rgbeffects.xml` describes models/groups and other show configuration; `xlights_networks.xml` covers controller/output configuration; editable sequence is `.xsq` XML; rendered playback data is `.fseq`. Never overwrite source show files silently. Match layout models by verified name/identity mapping; do not invent xLights effect parameters.
- **Safe editing:** `SequenceDocument` is the editable state. `ChoreographyPlan` is an intent proposal, not a destructive replacement. Keep optimistic revision checks, effect locks, selection scope, immutable before/after patches, undo/redo, and explicit human acceptance.
- **AI trust:** optional local or BYOK cloud AI returns validated structured proposals only. Do not execute LLM-produced code, scripts or XML directly. Treat song lyrics, filenames, descriptions and external metadata as untrusted data. Explicit consent before sending media-derived data to remote services.
- **Verification:** unit tests + fixtures + integration tests + one observable UI/xLights demonstration per phase. An XML parse pass does not prove xLights renders an effect. A numeric musical score does not prove artistic quality. Do not claim tests ran when they did not.
- **Migration:** upstream xLightsAI (MIT repository, with separate dependency/model license checks) is a pinned reference for behavior, data and effect recipes. Python may run only in developer comparison tooling; there is no runtime Python requirement in releases.

### Core architecture at a glance

```mermaid
flowchart LR
  UI[Avalonia Views / ViewModels] --> APP[Feature Commands / Queries]
  APP --> DOMAIN[Pure Domain Contracts + Validators]
  APP --> STORE[SQLite Project + Versioned Cache]
  APP --> AUDIO[Audio + Optional Native/ONNX Engines]
  APP --> XL[Verified xLights XML Adapter]
  APP --> AI[Optional Structured AI Planner]
  XL --> XSQ[Editable XSQ Export]
  XSQ --> REAL[xLights Editor / Real Renderer]
  DOMAIN --> DOC[SequenceDocument + Edit Transactions]
  DOMAIN --> PLAN[MusicAnalysis + ChoreographyPlan]
  PLAN --> DOC
  AI --> DOMAIN
```

### Stable domain names and cross-phase ownership

| Aggregate / contract | First introduced | Sole authority / rule |
|---|---:|---|
| `ProjectManifest` and `ProjectId` | 01 | Project identity/version + root; SQLite and JSON atomically recoverable |
| `DisplayGraph`, `DisplayModel`, `ModelId` | 02 | Imported xLights model identities/group info; reference layout by hash |
| `SequenceDocument`, `EffectInstance` | 02 | Editable effects/locked state provenance; never serialize business behavior directly as XML |
| `EditScope`, `EditTransaction`, `DocumentRevision` | 03 | Exact time + model boundary and user-approved reversibility |
| `SongAsset`, `IAudioTransport` | 04 | Original song hash, timebase and synchronized playback |
| `BeatGrid` | 05 | Analyzed/corrected beats and alternative tempo hypotheses |
| `StemManifest`, `StemAsset` | 06 | Derived separated sources; known model version and sample origin |
| `MelodyTrack`, `NoteEvent` | 07 | Note/onset/contour truth with human corrections |
| `SongStructure`, `MotifLink` | 08 | Related phrase identities and inheritance decisions |
| `SpatialDisplayGraph`, `SpatialPath` | 09 | Artistic routes / normalized geometry separate from imported xLights identities |
| `VisualStory`, `ChoreographyPlan` | 10 | Musical/visual intent before concrete effect rendering |
| `EditProposal` | 11 | Untrusted/optional AI idea validated before `EditTransaction` |
| `LoopRun`, `Candidate`, `QualityReport` | 12 | Bounded attempts and reproducible candidate comparisons |
| `RenderArtifact` | 13 | Actual or clearly-marked approximate preview evidence |
| `WholeSongPlan` | 14 | Section continuity and approved phrase assembly |
| `ReleaseManifest` | 15 | Platform/model assets and delivery integrity |
| `ReleaseReadiness` | 16 | Explicit community compatibility/artistic evidence and release gate |

### Shared canonical C# identifiers

Define these once in `Studio.Contracts`. They are included here so the current phase spec can stand alone. **Do not redefine copies with different namespaces in code.**

```csharp
// Shared across phases; put these in Studio.Contracts (single declarations in the codebase).
namespace XlightsAI.Studio.Contracts;
public readonly record struct ProjectId(Guid Value);
public readonly record struct SongId(Guid Value);
public readonly record struct ModelId(string Value);
public readonly record struct DocumentRevision(long Value);
public readonly record struct TimelineTick(long Value); // 100 ns ticks from song origin
public readonly record struct TimeWindow(long StartTicks, long EndTicks); // [Start, End)
```

## 3. Phase-specific design and contracts

**Inputs:** A candidate `SequenceDocument`, display geometry, exported XSQ, target xLights installation and optional show directory/renderer arguments.

**Outputs:** `RenderArtifact` with success/error/warnings, version/hash evidence; approximate preview frames; optional FSEQ/render sample metrics and A/B comparison.

### Architectural design notes

Maintain two unequal render tiers. **Fast preview** covers guaranteed supported gestures from our compiler and must visually flag approximations. **Authoritative adapter** interrogates the installed xLights executable and supported CLI/renderer version, validates show folder, exports a temporary XSQ, launches a sandboxed process using explicit argument arrays, timeouts and bounded output directory, then gathers rendered data and logs. Upstream xLights source currently documents `--headless -s <showdir> <seq.xsq>`; availability in an installed build is a **capability probe**, not a universal assumption. FSEQ is rendered playback data, not an editable XSQ substitute. For score comparisons choose properties that can be grounded (temporal coverage, population of active props, brightness proxy, onset mismatch), not an unjustified scalar “beauty” claim. Treat render failures as blocking gates.

### Proposed C# signatures

These are explicit API intent and relationship signatures, not an assertion that the existing repository already implements them. Favor the phase-local interfaces shown, adapt names to repository conventions, and record any compatibility decision. Referenced records introduced by prior phases are intentionally not redefined.

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Editing;
namespace XlightsAI.Studio.Rendering;
public enum RenderAuthority { ApproximateStudioPreview, ActualXlights }
public sealed record RenderCapability(string XlightsVersion, string ExecutablePath,
    bool SupportsHeadless, bool SupportsOutputDirectory);
public sealed record RenderRequest(Guid SequenceId, string ShowDirectory,
    string ExportedXsqPath, string OutputDirectory, TimeWindow? Selection,
    RenderAuthority RequestedAuthority);
public sealed record RenderArtifact(Guid Id, RenderAuthority Authority, bool Success,
    string RendererVersion, string? FseqPath, IReadOnlyList<string> Warnings,
    string EvidenceHash);
public interface IRenderCapabilityProbe {
    Task<RenderCapability> ProbeAsync(string executablePath, CancellationToken ct);
}
public interface IRenderAdapter {
    Task<RenderArtifact> RenderAsync(RenderRequest request, CancellationToken ct);
}
public interface ILocalPreviewRenderer {
    IAsyncEnumerable<PreviewFrame> PreviewAsync(SequenceDocument sequence,
        SpatialDisplayGraph layout, TimeWindow window, CancellationToken ct);
}
public sealed record PreviewFrame(long TimeTicks, IReadOnlyList<PixelPoint> Illuminated);
public sealed record PixelPoint(ModelId Model, double X, double Y, byte R, byte G, byte B);
```

### Class relationship diagram

```mermaid
classDiagram
  class RenderPreviewViewModel
  class IRenderCapabilityProbe
  class RenderCapability
  class RenderRequest
  class IRenderAdapter
  class ILocalPreviewRenderer
  class RenderArtifact
  class PreviewFrame
  class SequenceDocument
  IRenderCapabilityProbe --> RenderCapability
  IRenderAdapter --> RenderRequest
  IRenderAdapter --> RenderArtifact
  ILocalPreviewRenderer --> SequenceDocument
  ILocalPreviewRenderer --> PreviewFrame
  RenderPreviewViewModel --> IRenderAdapter
  RenderPreviewViewModel --> ILocalPreviewRenderer
```

### Core process flowchart

```mermaid
flowchart TD
  A[Candidate + selected display] --> B[Fast gesture preview]
  B --> C[User chooses authoritative verification]
  C --> D[Probe installed xLights capabilities]
  D --> E{Supported render mode?}
  E -- No --> F[Show manual open/render instructions]
  E -- Yes --> G[Export isolated temp XSQ]
  G --> H[Run xLights with safe args + timeout]
  H --> I{Output valid?}
  I -- No --> J[Collect logs; fail hard gate]
  I -- Yes --> K[Inspect rendered frames or FSEQ]
  K --> L[Compute grounded coverage/motion metrics]
  L --> M[Compare candidate and accepted baseline]
  F --> N[Mark verification manual/pending]
  M --> O[User review before commit]
```

## 4. Data contract, state ownership, migrations and caching

Store renderer executable/version, capability probe results, hashed XSQ/FSEQ artifacts, parameterized render command, exit code, execution time, warnings and derived metrics. Keep preview caches separate from authoritative render evidence.

### Contract compatibility rules

- Keep IDs stable across UI views, SQLite and exported artifacts; never persist Avalonia object instances or XML DOM nodes as domain objects.
- Schema changes require versioned migrations, fixture updates and tests against the preceding project version.
- Analysis caches may be rebuilt if their source/model/version hash changes; accepted manual edits are never disposable cache.
- All edits to `SequenceDocument` pass a revision-aware command boundary. Preview artifacts and AI proposals cannot update the accepted document directly.
- Logs include operation/fixture correlation IDs and error codes. External model names and XML payloads are treated as untrusted input.

## 5. User interface / screen behavior

Use the existing Avalonia shell and apply this phase incrementally; do not present empty future functionality as implemented. These wireframe images are conceptual design references, not pixels that must be copied literally.

- **SCREEN 16 EXPORT** — [Visual wireframe](../assets/wireframes/SCREEN-16-EXPORT.png). The screen must show loading/empty/error states, responsive resizing, keyboard navigation and an explicit version/status indicator when data is derived.
- **SCREEN 15 COMPARE** — [Visual wireframe](../assets/wireframes/SCREEN-15-COMPARE.png). The screen must show loading/empty/error states, responsive resizing, keyboard navigation and an explicit version/status indicator when data is derived.

UI behavior must always show *which song, time range, props, analysis source, version and approval state* the user is operating on when those concepts are available at this phase.

## 6. Numbered build slices — implement in order

Each numbered item is a complete vertical test cut: UI/command -> domain rule -> persistence or adapter -> observable result -> automated test. Avoid collecting multiple unverified tasks into one mega-commit.

### Step 085 — Build fast lightweight internal preview for supported primitive gestures

**User-visible outcome:** Avalonia screenshot or repeatable desktop interaction recording with saved-state verification.

**Implementation guidance:** Build pure renderer for known simple gestures (step, hold, pop) on imported geometry; show an unmistakable “approximate” indication for unsupported effects.

**Required acceptance:** Manual known fixtures match approximate rendered path.

**Failure/edge case to prove:** Invalid input, cancellation or stale persisted state must not corrupt the project.

**Suggested automated acceptance test:** `Step085_Acceptance_Passes`. Implement a deterministic fixture and assert persisted domain state plus public output, not only a mock invocation. Include a negative-path assertion.

**Evidence:** Write `implementation-evidence/PHASE-13/STEP-085.md` with commands actually executed, input hashes, actual results, screen/output artifact, unresolved issues, and an explicit PASS/BLOCKED status.

- [ ] Implementation / tests / observable demo / evidence all complete before next numbered step.

### Step 086 — Probe installed xLights capabilities and implement version-aware render adapter

**User-visible outcome:** Installer/bundle artifact with clean-machine smoke results and checksums.

**Implementation guidance:** Probe installed xLights version/help and optional headless render; use robust quoting/argument list and output directory isolation; fall back to manual verification.

**Required acceptance:** Supported build renders fixture; unsupported build gracefully falls back.

**Failure/edge case to prove:** Clean machine and unsupported runtime must produce user-readable diagnostics.

**Suggested automated acceptance test:** `Step086_Acceptance_Passes`. Implement a deterministic fixture and assert persisted domain state plus public output, not only a mock invocation. Include a negative-path assertion.

**Evidence:** Write `implementation-evidence/PHASE-13/STEP-086.md` with commands actually executed, input hashes, actual results, screen/output artifact, unresolved issues, and an explicit PASS/BLOCKED status.

- [ ] Implementation / tests / observable demo / evidence all complete before next numbered step.

### Step 087 — Decode rendered output/frame or channel data into display preview, preserving timing

**User-visible outcome:** Avalonia screenshot or repeatable desktop interaction recording with saved-state verification.

**Implementation guidance:** Inspect render data using version-aware FSEQ parser or available frame output; do not confuse rendered playback bytes with effect definitions.

**Required acceptance:** Frame sample matches known pixel/channel values.

**Failure/edge case to prove:** Invalid input, cancellation or stale persisted state must not corrupt the project.

**Suggested automated acceptance test:** `Step087_Acceptance_Passes`. Implement a deterministic fixture and assert persisted domain state plus public output, not only a mock invocation. Include a negative-path assertion.

**Evidence:** Write `implementation-evidence/PHASE-13/STEP-087.md` with commands actually executed, input hashes, actual results, screen/output artifact, unresolved issues, and an explicit PASS/BLOCKED status.

- [ ] Implementation / tests / observable demo / evidence all complete before next numbered step.

### Step 088 — Compute display-level metrics for brightness, coverage, spatial motion and density

**User-visible outcome:** Versioned, inspectable project output plus a reproducible test report.

**Implementation guidance:** Calculate coverage, brightness distribution, movement trajectory and temporal change using documented metric definitions and synthetic expected outputs.

**Required acceptance:** Designed render samples produce sensible metric order.

**Failure/edge case to prove:** Invalid input, cancellation or stale persisted state must not corrupt the project.

**Suggested automated acceptance test:** `Step088_Acceptance_Passes`. Implement a deterministic fixture and assert persisted domain state plus public output, not only a mock invocation. Include a negative-path assertion.

**Evidence:** Write `implementation-evidence/PHASE-13/STEP-088.md` with commands actually executed, input hashes, actual results, screen/output artifact, unresolved issues, and an explicit PASS/BLOCKED status.

- [ ] Implementation / tests / observable demo / evidence all complete before next numbered step.

### Step 089 — Add render/evaluation gate to refinement and report limitations

**User-visible outcome:** Versioned, inspectable project output plus a reproducible test report.

**Implementation guidance:** Integrate xLights render results as separate hard and soft evaluation evidence in loop; require user acceptance even when score improves.

**Required acceptance:** Broken/unrenderable proposal cannot auto-apply; clear diagnostics.

**Failure/edge case to prove:** Invalid input, cancellation or stale persisted state must not corrupt the project.

**Suggested automated acceptance test:** `Step089_Acceptance_Passes`. Implement a deterministic fixture and assert persisted domain state plus public output, not only a mock invocation. Include a negative-path assertion.

**Evidence:** Write `implementation-evidence/PHASE-13/STEP-089.md` with commands actually executed, input hashes, actual results, screen/output artifact, unresolved issues, and an explicit PASS/BLOCKED status.

- [ ] Implementation / tests / observable demo / evidence all complete before next numbered step.

## 7. Full-phase test plan and quality gate matrix

| # | Test scenario / invariant | Type | Required evidence |
|---:|---|---|---|
| 1 | Approximate StepAlongPath preview emits expected active nodes | Unit + property | Actual result, input hash/fixture and pass/fail |
| 2 | Unsupported effect is labeled approximate/unsupported | Integration/golden | Actual result, input hash/fixture and pass/fail |
| 3 | Capability probe handles missing executable and unsupported version | Desktop/manual | Actual result, input hash/fixture and pass/fail |
| 4 | Render process times out and kills child cleanly | Unit + property | Actual result, input hash/fixture and pass/fail |
| 5 | Bad XSQ fails rather than becoming good score | Integration/golden | Actual result, input hash/fixture and pass/fail |
| 6 | Valid tested fixture yields a traceable render artifact | Desktop/manual | Actual result, input hash/fixture and pass/fail |
| 7 | Metrics agree with synthetic known active-frame patterns | Unit + property | Actual result, input hash/fixture and pass/fail |
| 8 | Manual verification path remains possible without headless support | Integration/golden | Actual result, input hash/fixture and pass/fail |

### Phase gate demonstration — exact user workflow

Preview simple arch stepping, export a temp XSQ and render it using a verified installed xLights mode (headless if supported), compare a frame/time metric with the local approximation and show evidence links.

### Failure handling and known technical risks

Version-dependent CLI flags, proprietary/unstable FSEQ variants, relative media paths, headless shader differences, render process hang, antivirus/file permissions and huge channel arrays.

### Cross-cutting negative cases

- **Invalid/stale input:** reject and show the source of the problem; do not commit partial project state.
- **Cancellation:** propagate `CancellationToken`, dispose native/audio resources and leave resumable or safely rolled-back job state.
- **Compatibility:** detect actual versions/capabilities. No undocumented claim that all xLights versions or ML models behave identically.
- **Non-destructive writes:** if a user selects a show directory or existing XSQ, default to creating a new app-owned artifact rather than silently overwriting.
- **Regression:** rerun all previous phase gates affected by changed domain contracts or serializer versions.

## 8. Implementation checkpoints, decisions and handoff

### Recommended internal development order

1. Inspect the current committed code and pinned upstream reference; enumerate differences from signatures in this document.
2. Create fixtures and tests for the *first* numbered step; document exact external dependencies before writing the adapter.
3. Build one end-to-end slice, run the targeted tests, demonstrate the new behavior, then record evidence and proceed.
4. Update architecture decision records for genuine tradeoffs and migration decisions. Do not generalize everything into an interface.
5. At completion, run full automated regression and the phase demonstration; record blocker status truthfully.

### Explicit ADR questions to resolve during this phase

- **Primary design choice:** Is the proposed xlights rendering and visual evaluation contract compatible with existing repository conventions and user-approved state?
- **Runtime/source truth:** Which behaviors can be verified locally, and which require a specific xLights build, external model, audio device, or human judgment?
- **Recovery and rollback:** What happens on crash, cancellation, stale file version or invalid external content?
- **Dependency and licenses:** Are required upstream algorithms, model files or native libraries version-pinned and redistributable?

### Required final gate report

- [ ] All named numbered steps show independently recorded evidence.
- [ ] Compiles; full unit/integration/golden regression run records actual commands.
- [ ] Desktop interaction was exercised or explicitly reported blocked.
- [ ] Required xLights/manual/inference test ran against a recorded version, if applicable.
- [ ] Missing assumptions, compatibility limits, technical debt and migration impacts recorded.
- [ ] Previous accepted sequences/project files are unchanged except approved migrations/edits.
- [ ] Gate result = PASS or BLOCKED. Next phase begins only after PASS or an explicit scoped architecture waiver.

## 9. Copy/paste coding-agent prompt (phase-local)

```text
You are acting as a senior .NET/Avalonia architect and implementation agent on the xLights AI Choreography Studio repository.
Implement the PHASE 13 (xLights Rendering and Visual Evaluation) only, in numbered-step order. This spec is the source of truth for the current phase and contains the necessary contract and verification context. Read the existing repository and baseline history first; **do not copy sample signatures over working established contracts blindly**. Resolve any older/newer contract discrepancy with a short architecture decision and compatibility migration.

For EACH numbered step: (1) inspect only relevant repo files and pinned upstream sources; (2) write acceptance/failure tests first or alongside code; (3) implement the smallest complete UI -> application -> domain -> persistence/export pathway; (4) compile/run targeted and existing tests; (5) execute the manual demo or report explicitly why it could not run; (6) update completion/evidence notes in `implementation-evidence/PHASE-13/STEP-XXX.md`; (7) commit the step if permitted. Do not cross into future features except narrow stable contracts. Hard gates cannot be traded for weighted scoring.

Before ending the phase, produce a Gate Report showing exact tests/commands, observed results, fixture IDs/hashes, screenshots/XSQ artifacts, unverified assumptions, known risks and whether the phase gate is PASS/BLOCKED. Do not claim test success without execution. Do not start the next phase when blocked.

CURRENT SCOPE: Phase 13: xLights Rendering and Visual Evaluation. Implement only numbered steps 085, 086, 087, 088, 089.
CORE DEMO REQUIRED: Preview simple arch stepping, export a temp XSQ and render it using a verified installed xLights mode (headless if supported), compare a frame/time metric with the local approximation and show evidence links.
NEVER CLAIM THE GATE PASSED WITHOUT: Manual verification path remains possible without headless support
```

## 10. Developer test evidence format

```yaml
step: 085
phase: 13
implementation_commit: TO_BE_FILLED
source_fixture_hashes: []
automated_tests:
  - name: TO_BE_FILLED
    command: TO_BE_FILLED
    result: NOT_RUN
manual_demo:
  instructions: TO_BE_FILLED
  observed: NOT_RUN
xlights_version: NOT_TESTED
artifact_paths: []
blocking_failures: []
architectural_decisions: []
status: NOT_STARTED
```

## 11. Authoritative documentation and verification sources

The URLs below are references for implementation research, not substitute proof that our code or any installed xLights version works. Always pin relevant upstream revisions and record observed behavior.

- xLights source / headless rendering design (verify installed builds separately): https://github.com/xLightsSequencer/xLights/blob/master/AGENTS.md
- xLights manual glossary (layout/sequence formats): https://manual.xlights.org/xlights/appendicies/glossary
- xLights timing tracks: https://manual.xlights.org/xlights/chapters/chapter-four-sequencer/timing-tracks
- xLights model groups: https://manual.xlights.org/xlights/chapters/chapter-four-tabs/model-groups
- xLights importing and re-rendering: https://manual.xlights.org/xlights/chapters/chapter-five-menus/import
- xLights official source repository: https://github.com/xLightsSequencer/xLights
- Real public XSQ example (for observation; verify against target version): https://github.com/FRC3620/xlights.sign1/blob/main/2026%20Doug.xsq
- xLightsAI upstream raw XSQ generator path: `src/generator/xsq_writer.py` in pinned upstream checkout.

### Nearby package material (optional supporting context; this phase is complete without it)

- [`MASTER-SYSTEM-ARCHITECTURE.md`](../MASTER-SYSTEM-ARCHITECTURE.md)
- [`XLIGHTS-SEQUENCE-ENGINEERING-GUIDE.md`](../XLIGHTS-SEQUENCE-ENGINEERING-GUIDE.md)
- [`UI-UX-SCREEN-BLUEPRINTS.md`](../UI-UX-SCREEN-BLUEPRINTS.md)
- [`TEST-DATA-AND-ACCEPTANCE.md`](../TEST-DATA-AND-ACCEPTANCE.md)
- [`prompts/PHASE-13-PROMPT.md`](../prompts/PHASE-13-PROMPT.md)

## 12. xLights render compatibility evidence

Upstream source development guidance currently documents a headless mode in the form:

```text
xLights --headless -s <show-directory> <sequence.xsq>
xLights --headless -s <show-directory> --outputdir <temporary-directory> <sequence.xsq>
```

**Not a universal user-install contract.** Probe installed binary/version and test exit code/artifact; unsupported builds must fall back to instructing the user to open the sequence, Render All, and save. Never run arbitrary shell text assembled from a user pathname; use `ProcessStartInfo.ArgumentList`, isolated output folder, timeout and process-tree cancellation. Track generated `.fseq` as a render artifact and do not use it to replace the editable source document. Source: https://github.com/xLightsSequencer/xLights/blob/master/AGENTS.md .
