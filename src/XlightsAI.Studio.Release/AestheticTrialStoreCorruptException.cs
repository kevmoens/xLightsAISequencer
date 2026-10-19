namespace XlightsAI.Studio.Release;

/// <summary>
/// Thrown when the on-disk aesthetic-trial store exists but is not well-formed JSON (disk corruption,
/// an external non-atomic edit, a crash mid-write by something other than
/// <see cref="FileAestheticTrialCollector"/>'s own atomic temp-then-rename write). Step 099's own
/// required failure/edge case, verbatim: "...stale persisted state must not corrupt the project."
/// Unlike <see cref="XlightsAI.Studio.Projects.RecentProjectsStore"/>'s own disposable-cache precedent,
/// a human's already-recorded rating is accepted data (Section 4: "accepted manual edits are never
/// disposable cache") — a corrupt store is reported loudly rather than silently replaced, so no
/// already-recorded rating is ever silently discarded.
/// </summary>
public sealed class AestheticTrialStoreCorruptException : Exception
{
    public string StorePath { get; }

    public AestheticTrialStoreCorruptException(string storePath, Exception inner)
        : base($"Aesthetic trial store at '{storePath}' is not well-formed JSON and could not be read.", inner)
    {
        StorePath = storePath;
    }
}
