namespace XlightsAI.Studio.Rendering;

/// <summary>
/// Thrown by <see cref="IRenderAdapter"/> when a requested render is malformed -- a missing show directory,
/// a missing exported <c>.xsq</c>, a missing output directory, or an OS refusal to start the configured
/// executable. Always thrown before any render process is started. A render process that starts and then
/// fails, times out, or produces no <c>.fseq</c> is reported as an unsuccessful <see cref="RenderArtifact"/>
/// instead (Step 086's own required failure path treats that as a hard gate result, not an exception).
/// </summary>
public sealed class RenderRequestInvalidException : Exception
{
    public RenderRequestInvalidException(string message)
        : base(message)
    {
    }
}
