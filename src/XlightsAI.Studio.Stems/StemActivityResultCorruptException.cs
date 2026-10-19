namespace XlightsAI.Studio.Stems;

/// <summary>Mirrors <c>XlightsAI.Studio.Rhythm.EnergyEnvelopeCorruptException</c>: a persisted stem-activity result file exists but failed to deserialize.</summary>
public sealed class StemActivityResultCorruptException : Exception
{
    public StemActivityResultCorruptException(string path, Exception inner)
        : base($"Stem activity cache at '{path}' is corrupt or not a valid serialized StemActivityResult.", inner)
    {
        Path = path;
    }

    public string Path { get; }
}
