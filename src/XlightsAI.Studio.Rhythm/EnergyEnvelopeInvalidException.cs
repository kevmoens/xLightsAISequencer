namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown when an <see cref="IEnergyEnvelopeAnalyzer"/> run cannot proceed because its inputs are
/// invalid — either <see cref="RhythmSettings"/> that do not fit the source song (e.g. a requested
/// analysis sample rate the decoder cannot actually produce), or a decoded signal whose real
/// dimensions do not match the song's own declared metadata. Caught before any cache file is written,
/// mirroring <c>WaveformPyramidInvalidException</c>'s own "never silently cache as if correct" rule.
/// </summary>
public sealed class EnergyEnvelopeInvalidException : Exception
{
    public EnergyEnvelopeInvalidException(string path, string reason)
        : base($"Cannot build an energy envelope for '{path}': {reason}")
    {
        Path = path;
        Reason = reason;
    }

    public string Path { get; }

    public string Reason { get; }
}
