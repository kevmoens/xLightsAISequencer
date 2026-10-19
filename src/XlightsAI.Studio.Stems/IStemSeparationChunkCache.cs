namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 041 ("Add chunking/overlap, caching, progress, cancellation and bounded resource
/// usage"): a resumable cache of already-computed, per-chunk inference output. Keyed by an opaque
/// <paramref name="cacheKey"/> the caller derives deterministically from source/model/recipe identity
/// (never random), so re-invoking separation against the same inputs after a cancellation naturally
/// resumes from whichever chunks already completed, instead of recomputing the whole song.
/// </summary>
public interface IStemSeparationChunkCache
{
    /// <summary>
    /// Attempts to read a previously-cached chunk's raw (not yet overlap-add-merged) stem output.
    /// Returns <c>false</c> -- a cache miss, never a thrown exception -- both when nothing is cached yet
    /// and when a cached entry exists but is corrupt or structurally stale (wrong declared shape): a
    /// miss always safely falls back to recomputing that one chunk, satisfying this step's own
    /// failure/edge case text verbatim ("stale persisted state must not corrupt the project").
    /// </summary>
    /// <param name="stemSamples">Row-major <c>[stemCount, sampleCount]</c> array on a hit; <c>null</c> on a miss.</param>
    /// <param name="discardedCorruptEntry">True when a cached entry existed but was discarded as corrupt/stale (for caller-visible disclosure), false on a plain first-time miss.</param>
    bool TryGet(string cacheKey, int chunkIndex, int stemCount, int sampleCount, out float[,]? stemSamples, out bool discardedCorruptEntry);

    /// <summary>Persists one chunk's completed, verified-shape raw inference output so a future resumed run can skip recomputing it.</summary>
    void Put(string cacheKey, int chunkIndex, float[,] stemSamples);
}
