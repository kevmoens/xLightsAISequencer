namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// Thrown by <see cref="IMotifVariationPlanStore.SaveAsync"/> when the caller's own
/// <c>expectedCurrentVersion</c> does not match the version actually persisted on disk right now -- this
/// step's own required failure/edge case, verbatim: "...stale persisted state must not corrupt the
/// project." The attempted write is never performed; the file on disk is left exactly as it was. Mirrors
/// <see cref="SongDirectionStaleException"/>'s own convention.
/// </summary>
public sealed class MotifVariationStaleException : Exception
{
    public MotifVariationStaleException(int? expectedCurrentVersion, int? actualCurrentVersion)
        : base(
            $"Motif variation plan save rejected: caller expected current version " +
            $"'{(expectedCurrentVersion.HasValue ? expectedCurrentVersion.Value.ToString() : "none (no prior record)")}' " +
            $"but the actually persisted version is " +
            $"'{(actualCurrentVersion.HasValue ? actualCurrentVersion.Value.ToString() : "none (no prior record)")}'. " +
            "No write was performed.")
    {
        ExpectedCurrentVersion = expectedCurrentVersion;
        ActualCurrentVersion = actualCurrentVersion;
    }

    public int? ExpectedCurrentVersion { get; }

    public int? ActualCurrentVersion { get; }
}
