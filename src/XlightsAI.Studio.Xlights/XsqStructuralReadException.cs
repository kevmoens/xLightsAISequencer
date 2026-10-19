namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Thrown by <see cref="IXsqStructuralReader"/> when a file is not well-formed XML, is missing a
/// required section this phase's appendix documents (<c>head</c>, <c>ColorPalettes</c>,
/// <c>EffectDB</c>, <c>DisplayElements</c>, <c>ElementEffects</c>), or contains a dangling
/// <c>EffectDB</c>/palette reference (Section 12: "Serializer invariants: no dangling palette or
/// EffectDB refs").
/// </summary>
public sealed class XsqStructuralReadException : Exception
{
    public XsqStructuralReadException(string message)
        : base(message)
    {
    }

    public XsqStructuralReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
