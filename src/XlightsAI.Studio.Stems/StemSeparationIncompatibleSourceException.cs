namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown by <see cref="OnnxStemSeparator.SeparateAsync"/> when the source song's sample rate or
/// channel count does not match the recipe's declared expectation. PHASE-06 Step 040's own
/// failure/edge case, verbatim: "Missing or incompatible model ... must yield a recoverable state" --
/// no partial stem files are ever written before this check runs.
/// </summary>
public sealed class StemSeparationIncompatibleSourceException : Exception
{
    public StemSeparationIncompatibleSourceException(string message) : base(message)
    {
    }
}
