namespace XlightsAI.Studio.Rhythm;

/// <summary>Mirrors <c>WaveformPyramidCorruptException</c>: a persisted energy envelope file exists but failed to deserialize.</summary>
public sealed class EnergyEnvelopeCorruptException : Exception
{
    public EnergyEnvelopeCorruptException(string path, Exception inner)
        : base($"Energy envelope cache at '{path}' is corrupt or not a valid serialized EnergyEnvelope.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
