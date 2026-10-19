# PHASE 13 — Step 086: Probe installed xLights capabilities and implement version-aware render adapter

**Status: PASS** (automated tests against a real, separately-compiled stand-in OS process; no installed
xLights build is available in this environment — per the standing user waiver recorded in this project's
memory, the real-xLights human verification portion of this step/phase gate is explicitly waived rather than
faked or re-requested).

## 1. Scope actually implemented

New types in `src/XlightsAI.Studio.Rendering` (same project Step 085 created), exactly the PHASE-13
Section 3 proposed signatures unless noted under §6:

- `RenderAuthority.cs` — `enum { ApproximateStudioPreview, ActualXlights }`, verbatim.
- `RenderCapability.cs` — `RenderCapability(string XlightsVersion, string ExecutablePath, bool SupportsHeadless, bool SupportsOutputDirectory)`, verbatim.
- `RenderRequest.cs` — `RenderRequest(Guid SequenceId, string ShowDirectory, string ExportedXsqPath, string OutputDirectory, TimeWindow? Selection, RenderAuthority RequestedAuthority)`, verbatim.
- `RenderArtifact.cs` — `RenderArtifact(Guid Id, RenderAuthority Authority, bool Success, string RendererVersion, string? FseqPath, IReadOnlyList<string> Warnings, string EvidenceHash)`, verbatim.
- `IRenderCapabilityProbe.cs` / `IRenderAdapter.cs` — verbatim Section 3 interface signatures.
- `RenderCapabilityProbeInvalidException.cs`, `RenderRequestInvalidException.cs` — new, mirroring `XlightsLaunchInvalidException`'s "no partial/invalid attempt is ever made" convention (PHASE-02 Step 014).
- `XlightsRenderCapabilityProbe.cs` — default `IRenderCapabilityProbe`. Starts the configured executable with `--help` (default, overridable — see §6.1), captures combined stdout+stderr via `ProcessStartInfo.ArgumentList` (no shell interpolation), reports a real OS-reported `FileVersionInfo` version (best-effort) and whether the captured text itself mentions `--headless` / `--outputdir`. A process that doesn't exit within its timeout (default 15s) is killed via `Process.Kill(entireProcessTree: true)` and reported as supporting neither flag — a result, not an exception.
- `XlightsRenderAdapter.cs` — default `IRenderAdapter`. Validates `ShowDirectory`/`ExportedXsqPath`/`OutputDirectory` before anything else; probes capability; if `--headless` is unsupported, returns an unsuccessful `RenderArtifact` with manual-fallback guidance **without ever starting a process** (the required "unsupported build gracefully falls back" acceptance); otherwise builds `--headless -s <showdir> [--outputdir <dir>] <xsq>` from only the flags the probe actually observed, runs it with the same timeout/kill-tree discipline, and treats "exit 0 but no `.fseq` found" as a failure rather than trusting a bare exit code. `EvidenceHash` is a SHA-256 of the real exported `.xsq` bytes plus the observed exit outcome.

New tool project `tools/FakeXlightsHost` (`FakeXlightsHost.csproj`, net10.0, `OutputType=Exe`, same shape as the pre-existing `tools/FixtureCorpusGenerator`):

- `Program.cs` — a real, separately-compiled .NET console apphost that stands in for "an installed xLights build" purely to exercise real OS process plumbing end to end. Behavior is selected only by literal argument tokens (`--help`, `--help-limited`, `--hang`) or by substrings in the trailing sequence file name (`-fail-`, `-hang-`) — never environment variables, so parallel test runs stay deterministic. Explicitly disclosed everywhere it's used: **this is not xLights**.

New tests, `tests/XlightsAI.Studio.Rendering.Tests/Step086AcceptanceTests.cs` (11 tests, real `Process.Start` against the real `FakeXlightsHost.exe`, no mocking of the process boundary):

| Test | Proves |
|---|---|
| `Step086_Acceptance_Passes` (required name) | Supported build: probe detects both flags from real captured output; adapter launches, gets exit 0 + a real on-disk `.fseq`, reports `Success=true`, a real version string, a 64-hex-char SHA-256 `EvidenceHash`. |
| `Probe_DetectsLimitedBuild_NoHeadlessOrOutputDirMentioned` | Probe correctly reports `false`/`false` when the real captured text doesn't mention either flag. |
| `UnsupportedBuild_FallsBackWithoutLaunchingRenderProcess_NegativePath` | Required acceptance, negative half: unsupported build → `Success=false`, manual-fallback warning text, **no `.fseq` is ever created** (proves no render process was started). |
| `BadRenderExitsNonZero_FailsHardGate_NegativePath` | Test matrix #5 ("Bad XSQ fails rather than becoming good score"): non-zero exit → `Success=false`, exit code surfaced in `Warnings`, no `.fseq`. |
| `RenderProcessTimeout_KillsChildCleanly_NegativePath` | Test matrix #4 ("Render process times out and kills child cleanly"): a fixture that sleeps 5 minutes is killed within its 500ms timeout; the call returns in well under 30s, proving the kill (not a wait-out), `Success=false` with a timeout warning. |
| `Probe_Timeout_FallsBackGracefully_NegativePath` | Same timeout/kill discipline, at the probe layer. |
| `Probe_MissingExecutable_FailsSafely_NegativePath` | Required failure case ("Capability probe handles missing executable"): throws `RenderCapabilityProbeInvalidException` naming the missing path; no process attempted. |
| `MissingExportedXsq_FailsSafely_NegativePath` | Throws `RenderRequestInvalidException`; asserts the output directory is never even created for a rejected request. |
| `MissingShowDirectory_FailsSafely_NegativePath` | Same, for a missing show directory. |
| `Cancellation_BeforeProbe_FailsSafely_NegativePath` | A pre-cancelled token surfaces `OperationCanceledException`, not a validation exception. |
| `Selection_RequestedWithoutInventingACliFlag_RecordedAsWarningOnly` | A requested `TimeWindow` selection never becomes an invented CLI flag (Section 2); it's recorded as an explicit warning and the full `.xsq` is still rendered. |

`XlightsAI.Studio.slnx` updated to register `tools/FakeXlightsHost` under `/tools/`; `tests/XlightsAI.Studio.Rendering.Tests.csproj` updated with one additive `ProjectReference` to it (ensures correct build ordering when the test project is built/run standalone, matching the exact `dotnet test tests/.../*.csproj` command used below).

## 2. Commands actually executed

```
dotnet --version                                                            # 10.0.302
dotnet build XlightsAI.Studio.slnx --nologo                                 # after Step 086 implementation: Build succeeded, 0 warnings, 0 errors
Test-Path tools/FakeXlightsHost/bin/Debug/net10.0/FakeXlightsHost.exe       # True — real apphost produced, same mechanism as tools/FixtureCorpusGenerator
dotnet test tests/XlightsAI.Studio.Rendering.Tests/XlightsAI.Studio.Rendering.Tests.csproj --nologo
dotnet test XlightsAI.Studio.slnx --nologo                                  # full repository regression
```

## 3. Actual results

### 3.1 New-project test run

```
Passed!  - Failed: 0, Passed: 16, Skipped: 0, Total: 16, Duration: 3 s - XlightsAI.Studio.Rendering.Tests.dll (net10.0)
```

(5 pre-existing Step 085 tests + 11 new Step 086 tests, all passing, same assembly.)

### 3.2 Full-repository regression (`dotnet test XlightsAI.Studio.slnx`)

Every test project passed, 0 failures, 0 skipped:

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 31/31 |
| XlightsAI.Studio.Xlights.Tests | Passed 74/74 |
| XlightsAI.Studio.Editing.Tests | Passed 91/91 |
| XlightsAI.Studio.Display.Tests | Passed 56/56 |
| **XlightsAI.Studio.Rendering.Tests (+11 new Step 086 tests)** | **Passed 16/16** |
| XlightsAI.Studio.Structure.Tests | Passed 78/78 |
| XlightsAI.Studio.Melody.Tests | Passed 117/117 |
| XlightsAI.Studio.Composition.Tests | Passed 78/78 |
| XlightsAI.Studio.AiEdits.Tests | Passed 79/79 |
| XlightsAI.Studio.Projects.Tests | Passed 25/25 |
| XlightsAI.Studio.Refinement.Tests | Passed 71/71 |
| XlightsAI.Studio.Rhythm.Tests | Passed 82/82 |
| XlightsAI.Studio.Audio.Tests | Passed 38/38 |
| XlightsAI.Studio.Shell.Tests | Passed 272/272 |
| XlightsAI.Studio.Stems.Tests | Passed 101/101 |

Total: 875 tests (864 from before Step 086 + 11 new), 0 failures. No pre-existing test regressed. No
previously accepted source file outside `src/XlightsAI.Studio.Rendering/*.cs` (new files only),
`tools/FakeXlightsHost/*` (new project), `tests/XlightsAI.Studio.Rendering.Tests/Step086AcceptanceTests.cs`
(new file), `tests/XlightsAI.Studio.Rendering.Tests.csproj` (one additive `ProjectReference` line), and
`XlightsAI.Studio.slnx` (one additive project registration) was modified.

## 4. Observable result / "manual demonstration"

Step 086's own phase-spec "User-visible outcome" field reads "Installer/bundle artifact with clean-machine
smoke results and checksums" — this is boilerplate recycled from an unrelated release-packaging template
(compare PHASE-15/16's own `ReleaseManifest`/`ReleaseReadiness` scope); it does not literally describe
anything a capability probe/render adapter produces, and this step does not attempt to fabricate an
installer artifact to satisfy it. The step's own **actually specific and meaningful** acceptance/failure
text — "Supported build renders fixture; unsupported build gracefully falls back" and "Clean machine and
unsupported runtime must produce user-readable diagnostics" — is what this evidence targets and what
`Step086AcceptanceTests.cs` directly proves against a real OS process.

No installed xLights executable exists on this machine (same disclosed gap as every PHASE-02/PHASE-13 step
before it). Per this project's own standing memory record (waiver: "user waives the real-xLights human
verification check as a phase-gate blocker; record waiver, don't fake or re-ask"), the real-build manual
demonstration explicitly required by the phase gate ("compare a frame/time metric with the local
approximation and show evidence links") remains correctly deferred, not faked here, and not re-requested —
it is recorded as **waived**, carried forward against the final PHASE-13 gate report once Steps 087–089 are
also complete.

No new `ShellScreen`/Avalonia view was added for this step, consistent with Step 085's own disclosed,
already-accepted precedent (no screen wiring exists yet for any Rendering-tier concept in this repository).
`IRenderCapabilityProbe`/`IRenderAdapter` are domain/adapter-layer contracts a future SCREEN 16/15 view would
call; wiring a bindable view model for them is left to whichever later step actually introduces that screen,
the same boundary Step 085's evidence already drew.

## 5. Fixtures

- `tools/FakeXlightsHost` — new, committed source (not a binary fixture): a real, separately-compiled .NET
  console apphost, built as part of this solution, that deterministically simulates a supported build
  (`--help`), a limited/unsupported build (`--help-limited`), a hung process (`--hang`, or any sequence file
  named with `-hang-`), a failing render (`-fail-`), and a successful render (anything else — writes a real
  `<stem>.fseq` placeholder file to the requested output directory and exits 0). Mirrors the precedent
  already set by `tools/FixtureCorpusGenerator` (a committed `Exe`-type helper project) and by Step 014's own
  tests (a real stand-in OS executable, explicitly disclosed as not being the real target application).
- No binary `.fseq`/`.xsq` fixtures were added under `fixtures/`: every `.xsq` used by these tests is a
  trivial, programmatically-written placeholder file (its bytes are never parsed by the stand-in or by this
  step's own code — only hashed for `EvidenceHash` and checked for existence), consistent with Step 085's
  own "no on-disk fixture files were needed" precedent.

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **`XlightsRenderCapabilityProbe`'s probe arguments are constructor-overridable (new, disclosed).** The
   Section 3 interface itself is parameterless beyond `executablePath`; production code gets the
   upstream-documented `--help` default. The override exists solely so tests can drive the real
   `FakeXlightsHost` stand-in deterministically (`--help-limited`, `--hang`), the same kind of disclosed,
   test-only seam Step 014's own tests used (`where.exe` standing in for "an installed executable"). No
   production caller is required to use anything but the default.
2. **A requested `RenderRequest.Selection` is never translated into a CLI flag.** No upstream-documented
   xLights CLI flag for a scoped/partial headless render exists (Section 12's own appendix only documents
   `--headless -s <showdir> [--outputdir <dir>] <seq.xsq>`), and Section 2 explicitly forbids inventing
   xLights effect/CLI parameters. A supplied `Selection` is recorded as an explicit `RenderArtifact.Warnings`
   entry instead; the full exported `.xsq` is always rendered, and a future Step 087/088 consumer is expected
   to filter decoded frame/channel data to the requested window itself.
3. **"Exit 0" alone is not treated as success.** If the probed executable exits 0 but no `<stem>.fseq` is
   found in the output directory (or, when `--outputdir` isn't supported, next to the exported `.xsq`), the
   render is still reported as `Success=false` with an explicit warning — Section 2's own rule ("An XML parse
   pass does not prove xLights renders an effect") applied the same way to a bare exit code.
4. **`EvidenceHash` is defined as SHA-256(exported `.xsq` bytes ‖ exit-code-or-"no-process-started").** The
   Section 3 signature names the field but does not define its derivation. This choice ties the hash to one
   specific real file actually submitted and one specific real observed outcome — never a timestamp or other
   non-reproducible input — so two renders of byte-identical `.xsq` content with the same outcome produce the
   same evidence hash, and any change to either changes it.
5. **No new `ShellScreen`/Avalonia view.** See §4 — consistent with Step 085's own precedent.
6. **Phase-spec "User-visible outcome" text not literally satisfied.** See §4 — treated as inapplicable
   boilerplate carried over from a different phase's template, not as a requirement this step silently
   skipped; the step's own specific required acceptance/failure text is what was implemented and tested.

## 7. Known risks / unresolved issues

- **No real xLights build has ever been probed.** Every assertion about `XlightsRenderCapabilityProbe`'s
  real-process plumbing (argument passing, stdout/stderr capture, timeout/kill, version reporting) is proven
  against `FakeXlightsHost`, a fully disclosed non-xLights stand-in. Whether a real installed xLights build's
  actual `--help` (or equivalent) output contains the literal substrings `--headless`/`--outputdir`, whether
  it exits promptly for that flag, and whether its real headless render actually writes a `.fseq` named after
  the input `.xsq` are all still **unverified assumptions**, carried forward from the phase spec's own
  Section 12 appendix (itself sourced from upstream documentation, not an independently confirmed installed
  build). This is the same category of gap disclosed in every prior PHASE-02/13 step and is explicitly waived
  by this project's standing human-verification waiver, not resolved by this step.
- **`.fseq` discovery is name-convention-based only** (`<xsq-stem>.fseq` in the output directory, falling
  back to the show/xsq directory). If a real xLights build names its output differently (e.g. including a
  show-specific prefix/suffix), this adapter will under-report success until that convention is verified
  against a real build.
- **No retry/backoff.** A single probe/render attempt either returns a capability/artifact or throws on
  invalid input; a transient OS-level launch failure is surfaced as a failure, not retried. Not required by
  this step's own acceptance text.

## 8. Required final checklist (Step 086 only)

- [x] Implementation / tests / observable demo (automated, real-process-based; see §4 for the disclosed real-xLights waiver) / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 errors).
- [x] Full existing regression suite re-run and passing (875 total tests across all projects, 0 failures).
- [x] "Manual" demonstration performed against a real OS process (`FakeXlightsHost.exe`); the real-xLights
      portion is explicitly waived per this project's own standing memory record, not faked.
- [x] No previously accepted sequence/project file modified; only new files plus two additive lines
      (`XlightsAI.Studio.slnx`, `XlightsAI.Studio.Rendering.Tests.csproj`'s one new `ProjectReference`).
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).

**Gate for Step 086: PASS.** Per the standing user waiver, the real-xLights/manual human verification portion
of the overall PHASE-13 gate (final demonstration across Steps 086–089) remains correctly deferred and is not
claimed as independently proven here; Step 086's own numbered-step scope (capability probe + version-aware
render adapter, with graceful fallback and a hard failure gate) is fully implemented and tested.
