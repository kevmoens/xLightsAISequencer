namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Step 087's own new contract (the proposed Section 3 signatures stop at Step 086's
/// <see cref="RenderArtifact"/> -- they do not define a decoded-frame shape). One real, decoded instant
/// of rendered channel-byte data from a <c>.fseq</c> file, with its absolute song-origin timing preserved
/// exactly as Section 2 requires ("internally represent position with an explicit canonical monotonic
/// clock tied to source audio origin... Convert to xLights frames only when explicitly snapping or
/// exporting") -- the reverse conversion, frame index back to <c>TimelineTick</c>, happens exactly once,
/// here, from the file's own declared step time, never re-derived or assumed elsewhere.
/// </summary>
/// <param name="TimeTicks">
/// Absolute song-origin tick (<c>frameIndex * stepMs</c>, in the same 100&#160;ns <c>TimelineTick</c> unit
/// every other contract in this repository uses) this frame represents.
/// </param>
/// <param name="Channels">
/// This frame's raw channel bytes, in on-disk channel order, exactly as decoded -- never color- or
/// model-interpreted here (Section 2: "FSEQ is rendered playback data, not an editable XSQ substitute";
/// see <see cref="FseqRenderedFrameDecoder"/> remarks for why channel-to-model mapping is explicitly out
/// of this step's scope).
/// </param>
public sealed record DecodedRenderFrame(long TimeTicks, IReadOnlyList<byte> Channels);
