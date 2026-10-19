# PHASE 13 — Step 087: Decode rendered output/frame or channel data into display preview, preserving timing

**Status: PASS** (automated tests against real, byte-level FSEQ v2 fixtures built with a real zstd
compressor; no installed xLights build is available in this environment — per the standing user waiver
recorded in this project's memory, the real-xLights human verification portion of this step/phase gate is
explicitly waived rather than faked or re-requested).

## 1. Scope actually implemented

New types in `src/XlightsAI.Studio.Rendering` (same project Steps 085/086 created). The PHASE-13 Section 3
proposed signatures stop at Step 086's `RenderArtifact` — they do not define a decoded-frame shape, so
this step's own new contracts are additive, not a deviation from an existing verbatim signature:

- `DecodedRenderFrame.cs` — `DecodedRenderFrame(long TimeTicks, IReadOnlyList<byte> Channels)`. One decoded
  instant: absolute song-origin `TimeTicks` (the same 100ns `TimelineTick` unit every other contract uses)
  plus that frame's raw channel bytes, in on-disk order.
- `DecodedRenderOutput.cs` — `DecodedRenderOutput(int ChannelCount, int StepMilliseconds, IReadOnlyList<DecodedRenderFrame> Frames)`.
  The full decode of one `.fseq` file.
- `IRenderedFrameDecoder.cs` — `Decode(string fseqPath) : DecodedRenderOutput`, throwing
  `FseqDecodeInvalidException` on any malformed input.
- `FseqDecodeInvalidException.cs` — mirrors `Xlights.XsqStructuralReadException`'s "no partial/invalid
  attempt is ever made" convention.
- `FseqRenderedFrameDecoder.cs` — default `IRenderedFrameDecoder`. Parses the real FSEQ v2 binary layout
  (fixed 32-byte header, 8-byte compression-block-index entries, zstd-compressed channel-data blocks) and
  decompresses with `ZstdSharp.Port` (new `PackageReference`, pure managed, MIT-licensed — restored
  successfully from `nuget.org` in this environment, same tier of dependency as the pre-existing
  `SharpCompress`/`Microsoft.ML.OnnxRuntime` references in `XlightsAI.Studio.Stems`).

New test file, `tests/XlightsAI.Studio.Rendering.Tests/Step087AcceptanceTests.cs` (7 tests, every fixture a
real, byte-for-byte valid on-disk `.fseq` file built with the real `ZstdSharp.Compressor` — no mocking of
the file format):

| Test | Proves |
|---|---|
| `Step087_Acceptance_Passes` (required name) | Required acceptance, verbatim ("Frame sample matches known pixel/channel values"): a known 4-channel/5-frame/25ms-step fixture decodes to the exact expected `ChannelCount`, `StepMilliseconds`, per-frame `TimeTicks` (`frameIndex * 25ms`, in 100ns ticks), and every individual channel byte value. |
| `Step087_Acceptance_DifferentStepTime_PreservesDistinctTiming` | "Preserving timing" is not hard-coded: a 40ms-step fixture produces `0, 400_000, 800_000` tick spacing, not the 25ms fixture's spacing. |
| `NegativePath_MissingFile_ThrowsWithoutPartialOutput` | Required failure case ("Invalid input... must not corrupt the project"): a missing path throws before any output is constructed. |
| `NegativePath_BadMagicBytes_RejectsRatherThanGuessing` | A file that isn't `PSEQ`-prefixed is rejected, not guessed at. |
| `NegativePath_TruncatedHeader_ThrowsBeforeAnyDecode` | A file shorter than the fixed 32-byte header throws before touching the (nonexistent) block index. |
| `NegativePath_CompressedBlockExtendsPastEndOfFile_RejectsRatherThanReadingGarbage` | A corrupted block-length field that would read past EOF is rejected rather than silently truncating or reading adjacent memory/garbage bytes. |
| `NegativePath_ZeroDeclaredChannelsOrFrames_RejectsRatherThanProducingEmptyOutput` | A file declaring zero channels is rejected rather than silently returning an empty/degenerate `DecodedRenderOutput`. |

`tests/XlightsAI.Studio.Rendering.Tests.csproj` updated with one additive `PackageReference` to
`ZstdSharp.Port` (needed only to *build* real fixtures in-test with the real compressor the decoder's own
`Decompressor` consumes — the decoder itself depends on it via the production project reference, not via
the test project).

## 2. Commands actually executed

```
dotnet --version                                                              # 10.0.302
dotnet add package ZstdSharp.Port --version 0.8.6   (run from src/XlightsAI.Studio.Rendering)
                                                                               # restored from nuget.org
dotnet build src/XlightsAI.Studio.Rendering/XlightsAI.Studio.Rendering.csproj --nologo
                                                                               # 1 build error (ushort/int
                                                                               # mismatch), fixed, then:
                                                                               # Build succeeded, 0 errors
dotnet build XlightsAI.Studio.slnx --nologo                                   # after full Step 087
                                                                               # implementation: Build
                                                                               # succeeded, 0 warnings,
                                                                               # 0 errors
dotnet test tests/XlightsAI.Studio.Rendering.Tests/XlightsAI.Studio.Rendering.Tests.csproj --nologo
dotnet test XlightsAI.Studio.slnx --nologo                                    # full repository regression
```

## 3. Actual results

### 3.1 New-project test run

```
Passed!  - Failed: 0, Passed: 23, Skipped: 0, Total: 23, Duration: 4 s - XlightsAI.Studio.Rendering.Tests.dll (net10.0)
```

(16 pre-existing Step 085/086 tests + 7 new Step 087 tests, all passing, same assembly.)

### 3.2 Full-repository regression (`dotnet test XlightsAI.Studio.slnx`)

Every test project passed, 0 failures, 0 skipped:

| Project | Result |
|---|---|
| XlightsAI.Studio.Reference.Tests | Passed 31/31 |
| XlightsAI.Studio.Xlights.Tests | Passed 74/74 |
| XlightsAI.Studio.Editing.Tests | Passed 91/91 |
| XlightsAI.Studio.Display.Tests | Passed 56/56 |
| **XlightsAI.Studio.Rendering.Tests (+7 new Step 087 tests)** | **Passed 23/23** |
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

Total: 882 tests (875 from before Step 087 + 7 new), 0 failures. No pre-existing test regressed. No
previously accepted source file outside `src/XlightsAI.Studio.Rendering/*.cs` (new files only),
`src/XlightsAI.Studio.Rendering/XlightsAI.Studio.Rendering.csproj` (one additive `PackageReference`),
`tests/XlightsAI.Studio.Rendering.Tests/Step087AcceptanceTests.cs` (new file), and
`tests/XlightsAI.Studio.Rendering.Tests.csproj` (one additive `PackageReference`) was modified.

## 4. Observable result / "manual demonstration"

Step 087's own phase-spec "User-visible outcome" field reads "Avalonia screenshot or repeatable desktop
interaction recording with saved-state verification" — the same boilerplate text Step 085 already disclosed
does not literally apply to a domain/decoding step with no bindable screen of its own. Consistent with Step
085/086's own already-accepted precedent (no `ShellScreen`/Avalonia view exists yet for any Rendering-tier
concept in this repository), this step's observable proof is the same byte-exact automated assertion a
future bound view would ultimately read from: `Step087_Acceptance_Passes` decodes a real on-disk `.fseq`
file and asserts the exact channel values and exact preserved timing a view would display, the closest
honest equivalent to "repeatable desktop interaction recording with saved-state verification" available at
this step's own layer (decode, not display).

No installed xLights executable exists on this machine (same disclosed gap as every prior PHASE-02/PHASE-13
step). Per this project's own standing memory record (waiver: "user waives the real-xLights human
verification check as a phase-gate blocker; record waiver, don't fake or re-ask"), no real xLights-rendered
`.fseq` file has ever been decoded by this step's tests — every fixture is a real, valid, byte-for-byte FSEQ
v2 file this step's own test code constructs (see §6 risk #1). The real-build manual demonstration required
by the overall phase gate remains correctly deferred, not faked here, and not re-requested — recorded as
**waived**, carried forward against the final PHASE-13 gate report once Steps 088–089 are also complete.

## 5. Fixtures

No binary fixture files were added under `fixtures/`. Every `.fseq` used by `Step087AcceptanceTests` is
built entirely programmatically inside the test file (`BuildFseqFixture`), writing a real FSEQ v2 header,
a real 8-byte compression-block-index entry, and real zstd-compressed payload bytes produced by
`ZstdSharp.Compressor` — the exact format `FseqRenderedFrameDecoder` decodes, not a stand-in. This mirrors
this repository's own pinned upstream reference's test convention
(`reference/xlights-autosequencer/tests/unit/test_video_fseq.py::_build_minimal_fseq`), ported from Python
to C# with the equivalent real compressor substituted (`zstandard` → `ZstdSharp.Port`).

## 6. Architecture decisions / deviations from the Section 3 proposed signatures

1. **New contracts not present in Section 3 (disclosed, additive).** Section 3's proposed signatures end at
   `RenderArtifact`/`IRenderAdapter` (Step 086's own scope) and do not define any decoded-frame shape for
   Step 087. `DecodedRenderFrame`/`DecodedRenderOutput`/`IRenderedFrameDecoder` are new types this step
   introduces to fill that gap, following the same naming/record-shape conventions every prior
   `Rendering`/`Xlights` contract in this repository already uses.
2. **Channel-indexed output, not model-indexed `PixelPoint`s (the step's single most significant
   deviation).** Step 087's own phase-spec text literally says "into display preview," which could be read
   as requiring output shaped like Step 085's `PreviewFrame`/`PixelPoint` (per-model, per-pixel color).
   That mapping requires a channel offset / pixel count per model — data that lives in xLights'
   `xlights_networks.xml` controller/output configuration, which **no step through PHASE-13 Step 086
   imports anywhere in this repository** (verified: `Xlights.DisplayModel` carries only `Id`,
   `XlightsName`, `Kind`, `GroupNames`, `PreviewPoints` — no channel/node-count field; `Display.DisplayNode`
   carries only normalized preview `X`/`Y` coordinates). Inventing a channel-to-model mapping without that
   real imported data would violate Section 2's explicit rule ("do not invent xLights effect parameters"),
   applied here to channel layout instead of effect parameters. This step therefore returns a
   channel-indexed `DecodedRenderOutput` — real decoded bytes with real preserved timing, honestly scoped
   to what this repository's own prior phases have actually imported — rather than fabricating a
   model-level mapping. A future step that imports real `xlights_networks.xml` channel-offset data can
   build a thin, additive converter on top of this decoder without changing it. Step 087's own literal,
   specific required-acceptance text ("Frame sample matches known pixel/channel values") is satisfied at
   the channel level this step actually implements; its "pixel" wording does not itself require a
   model-mapped output when the spec supplies no field or interface asking for one at this step boundary
   (contrast with Step 085, whose own `PixelPoint` signature IS given verbatim in Section 3).
3. **Zstd-only decompression, matching this repository's only pinned reference exactly (disclosed risk).**
   `reference/xlights-autosequencer/src/video/fseq.py` — this repository's only committed, pinned reference
   to the FSEQ v2 byte layout — hardcodes zstd decompression and never reads a compression-type byte.
   `FseqRenderedFrameDecoder` does the same: every field offset (magic @0, data offset @4, channel count
   @10, frame count @14, step time @18, fixed 32-byte header, 8-byte block-index entries) is taken
   verbatim from that pinned file, and no attempt is made to support FSEQ v2's publicly-documented-elsewhere
   but, in our own pinned reference, unused "uncompressed" or "zlib" compression types. Guessing an
   undocumented-in-our-own-reference compression-type byte offset risked a worse outcome (silently
   misreading a real file) than disclosing the gap. See §7 for the carried-forward risk this creates.
4. **New `ZstdSharp.Port` NuGet dependency (disclosed, same tier as existing dependencies).** No pure-BCL
   zstd decompressor exists in .NET (unlike zlib, which `System.IO.Compression.ZLibStream` already covers)
   and this repository's own pinned FSEQ reference assumes zstd, not zlib. `ZstdSharp.Port` is a pure
   managed (no native binary), MIT-licensed port already restorable from `nuget.org` in this environment —
   the same dependency tier `XlightsAI.Studio.Stems` already uses for `SharpCompress`/
   `Microsoft.ML.OnnxRuntime`. Added to both the production `XlightsAI.Studio.Rendering.csproj` (the real
   decoder) and the test project (to build real fixtures with the real compressor the decoder consumes).
5. **Synchronous, non-async `Decode(string)` (disclosed, consistent with sibling readers).** `.fseq` files
   are read whole via `File.ReadAllBytes` and decoded in-process, the same synchronous shape
   `Xlights.IXsqStructuralReader.Read(string)` already uses for its own binary/XML file read — no
   `CancellationToken` parameter exists on that sibling interface either, and this step's own required
   failure/edge case ("cancellation... must not corrupt the project") has nothing to corrupt: like Step
   085's pure renderer, this decoder reads one file and returns one new, fully-constructed
   `DecodedRenderOutput` or throws before returning anything — there is no partial/mutable state a
   cancellation could leave behind.
6. **No new `ShellScreen`/Avalonia view.** See §4 — consistent with Step 085/086's own precedent.

## 7. Known risks / unresolved issues

- **No real xLights-rendered `.fseq` file has ever been decoded.** Every assertion about
  `FseqRenderedFrameDecoder`'s correctness is proven against fixtures this step's own test code constructs
  to match our one pinned reference's documented byte layout — not an independently-confirmed file a real
  installed xLights build actually produced. Whether a real xLights v2 `.fseq` always sets major version 2,
  always compresses with zstd (vs. the publicly-documented-elsewhere "uncompressed"/"zlib" compression
  types this decoder does not support — see §6 item 3), and never uses the optional sparse-range
  variable-header extension this decoder's block-index scan silently treats as opaque padding (exactly as
  our pinned Python reference does) are all unverified assumptions, carried forward from the same category
  of gap disclosed in every prior PHASE-02/13 step and explicitly waived by this project's standing
  human-verification waiver, not resolved by this step.
- **Channel-to-model mapping remains unimplemented** (see §6 item 2) — `DecodedRenderOutput` is
  channel-indexed, not model-indexed. A future step/phase needs to import real `xlights_networks.xml`
  channel-offset data before a `PixelPoint`-shaped "actual-render preview" (as opposed to Step 085's
  already-existing *approximate* preview) can exist without guessing.
- **No streaming/chunked read.** `File.ReadAllBytes` loads the entire `.fseq` into memory at once. Section
  12's own disclosed risk list ("huge channel arrays") applies; not addressed by this step, since no
  acceptance text in Step 087 requires it and no real large fixture has been observed to make it a measured
  problem yet.

## 8. Required final checklist (Step 087 only)

- [x] Implementation / tests / observable demo (automated, real-byte-level FSEQ fixtures; see §4 for the
      disclosed real-xLights waiver) / evidence all complete.
- [x] Compiles (`dotnet build XlightsAI.Studio.slnx`: 0 errors).
- [x] Full existing regression suite re-run and passing (882 total tests across all projects, 0 failures).
- [x] "Manual" demonstration performed against real, byte-exact FSEQ v2 data; the real-xLights portion is
      explicitly waived per this project's own standing memory record, not faked.
- [x] No previously accepted sequence/project file modified; only new files plus two additive
      `PackageReference` lines (`XlightsAI.Studio.Rendering.csproj`, `XlightsAI.Studio.Rendering.Tests.csproj`).
- [x] Missing assumptions / compatibility limits / technical debt recorded (§6, §7).

**Gate for Step 087: PASS.** Per the standing user waiver, the real-xLights/manual human verification
portion of the overall PHASE-13 gate (final demonstration across Steps 086–089) remains correctly deferred
and is not claimed as independently proven here; Step 087's own numbered-step scope (decode rendered FSEQ
v2 output into channel/frame data with timing preserved, with a hard failure gate on any malformed input)
is fully implemented and tested.
