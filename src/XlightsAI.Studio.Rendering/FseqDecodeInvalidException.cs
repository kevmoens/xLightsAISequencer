namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Thrown by <see cref="IRenderedFrameDecoder"/> when a file is missing, is not a well-formed FSEQ v2
/// file (bad magic, truncated header, a compressed block extending past the end of the file, or a
/// zstd-decompression failure), or declares an impossible channel/frame/step-time combination. Mirrors
/// <c>Xlights.XsqStructuralReadException</c>'s own "no partial/invalid attempt is ever made" convention --
/// no partially-decoded <see cref="DecodedRenderOutput"/> is ever returned.
/// </summary>
public sealed class FseqDecodeInvalidException : Exception
{
    public FseqDecodeInvalidException(string message)
        : base(message)
    {
    }

    public FseqDecodeInvalidException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
