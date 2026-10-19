namespace XlightsAI.Studio.Rendering;

/// <summary>
/// PHASE-13 Step 087 ("Decode rendered output/frame or channel data into display preview, preserving
/// timing"): inspects a real rendered <c>.fseq</c> artifact (<see cref="RenderArtifact.FseqPath"/>) and
/// decodes it into per-frame channel bytes with timing preserved -- never confusing the rendered playback
/// bytes this reads with an xLights effect definition (Section 2: "FSEQ is rendered playback data, not an
/// editable XSQ substitute").
/// </summary>
public interface IRenderedFrameDecoder
{
    /// <exception cref="FseqDecodeInvalidException">
    /// <paramref name="fseqPath"/> does not exist, is not a well-formed FSEQ v2 file, or declares data
    /// that does not fit inside the file's own actual length.
    /// </exception>
    DecodedRenderOutput Decode(string fseqPath);
}
