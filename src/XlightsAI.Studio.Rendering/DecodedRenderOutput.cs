namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Step 087's own new contract: the full, real decode of one on-disk <c>.fseq</c> rendered
/// playback artifact (<see cref="RenderArtifact.FseqPath"/>) -- every frame's raw channel bytes with its
/// preserved absolute timing, ready for a future Step 088 metrics pass to consume without re-parsing the
/// binary file itself.
/// </summary>
/// <param name="ChannelCount">The file's own declared channel count; every <see cref="DecodedRenderFrame.Channels"/> in <see cref="Frames"/> has exactly this many entries.</param>
/// <param name="StepMilliseconds">The file's own declared per-frame step time in milliseconds -- the real source of <see cref="DecodedRenderFrame.TimeTicks"/>' spacing, kept here too so a caller can re-derive frame boundaries without re-deriving it from consecutive frames.</param>
/// <param name="Frames">Every decoded frame, in ascending <see cref="DecodedRenderFrame.TimeTicks"/> order, one entry per the file's own declared frame count.</param>
public sealed record DecodedRenderOutput(int ChannelCount, int StepMilliseconds, IReadOnlyList<DecodedRenderFrame> Frames);
