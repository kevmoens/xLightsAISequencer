namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown by <see cref="StemActivityDetector.DetectAsync"/> when it cannot proceed because its inputs
/// are invalid -- either a <see cref="StemActivityDetectionSettings"/> that does not describe a usable
/// hysteresis window, or a stem whose on-disk file no longer matches its own declared
/// <see cref="StemAsset"/> metadata (stale/corrupt stem, the same "stale persisted state must not
/// corrupt the project" guarantee Steps 041/042 already established for their own boundaries). Caught
/// before any decode or cache file is written -- PHASE-06 Step 043's own failure/edge case, reinterpreted
/// for this step's own activity-detection boundary (shared phase-wide boilerplate: "Malformed proposal...
/// must not auto-commit").
/// </summary>
public sealed class StemActivityDetectionInvalidException : Exception
{
    public StemActivityDetectionInvalidException(string path, string reason)
        : base($"Cannot detect stem activity for '{path}': {reason}")
    {
        Path = path;
        Reason = reason;
    }

    public string Path { get; }

    public string Reason { get; }
}
