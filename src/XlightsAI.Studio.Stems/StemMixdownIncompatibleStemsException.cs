namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown by <see cref="StemMixdownRenderer.RenderMixAsync"/> when the stems being mixed do not share
/// the same sample rate / sample count / source origin (so they cannot be summed sample-for-sample
/// and stay aligned with the original source), or when a stem's on-disk file no longer matches its
/// own declared <see cref="StemAsset"/> metadata (stale/corrupt stem). PHASE-06 Step 042's own
/// failure/edge case, verbatim (shared phase-wide boilerplate, reinterpreted for this step's own
/// mixdown boundary): "Missing or incompatible model, cancellation and offline mode must yield a
/// recoverable state" -- no mixdown file is ever written before this check runs.
/// </summary>
public sealed class StemMixdownIncompatibleStemsException : Exception
{
    public StemMixdownIncompatibleStemsException(string message) : base(message)
    {
    }
}
