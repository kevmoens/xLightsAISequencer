namespace XlightsAI.Studio.Rhythm;

/// <summary>Mirrors <see cref="OnsetAnalysisInvalidException"/>: thrown when an <see cref="IBandEnergyAnalyzer"/> run cannot proceed because its inputs are invalid.</summary>
public sealed class BandEnergyAnalysisInvalidException : Exception
{
    public BandEnergyAnalysisInvalidException(string path, string reason)
        : base($"Cannot build a band energy envelope for '{path}': {reason}")
    {
        Path = path;
        Reason = reason;
    }

    public string Path { get; }

    public string Reason { get; }
}
