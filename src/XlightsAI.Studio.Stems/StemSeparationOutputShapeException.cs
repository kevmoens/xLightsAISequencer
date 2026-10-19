namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown by <see cref="OnnxStemSeparator.SeparateAsync"/> when the loaded ONNX model's actual output
/// tensor rank/shape does not match what the <see cref="StemSeparationRecipe"/> declares (4-D
/// <c>[1, stemCount, channels, sampleCount]</c>). PHASE-06 Step 040's own failure/edge case, verbatim:
/// "Missing or incompatible model ... must yield a recoverable state" -- an incompatible model is
/// rejected explicitly rather than silently reshaped, truncated, or padded.
/// </summary>
public sealed class StemSeparationOutputShapeException : Exception
{
    public StemSeparationOutputShapeException(string message) : base(message)
    {
    }
}
