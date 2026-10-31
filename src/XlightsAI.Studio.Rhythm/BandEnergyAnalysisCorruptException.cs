namespace XlightsAI.Studio.Rhythm;

/// <summary>Mirrors <see cref="OnsetAnalysisCorruptException"/>: a persisted band energy envelope file exists but failed to deserialize.</summary>
public sealed class BandEnergyAnalysisCorruptException : Exception
{
    public BandEnergyAnalysisCorruptException(string path, Exception inner)
        : base($"Band energy envelope cache at '{path}' is corrupt or not a valid serialized BandEnergyEnvelope.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
