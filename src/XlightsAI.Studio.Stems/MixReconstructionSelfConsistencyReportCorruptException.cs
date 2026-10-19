namespace XlightsAI.Studio.Stems;

/// <summary>Thrown when a persisted <see cref="MixReconstructionSelfConsistencyReport"/> JSON file cannot be deserialized -- the same "stale/corrupt cache is never silently accepted" precedent <see cref="SeparationQualityReportCorruptException"/> already established.</summary>
public sealed class MixReconstructionSelfConsistencyReportCorruptException : Exception
{
    public MixReconstructionSelfConsistencyReportCorruptException(string path, Exception innerException)
        : base($"Mix reconstruction self-consistency report at '{path}' is corrupt and could not be loaded.", innerException)
    {
    }
}
