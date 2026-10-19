namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 4 ("Save SequenceDocument in versioned JSON/SQLite") persistence boundary for a
/// <see cref="SequenceDocument"/>, independent of any xLights XML representation (Step 012 required
/// acceptance: "Plan is serialized and inspectable independently of XSQ"). Not the XSQ exporter
/// (<c>IXsqExporter</c>, Step 013's own scope) — this is the plan itself, in its own app-owned format.
/// </summary>
public interface ISequenceDocumentStore
{
    /// <summary>
    /// Persists <paramref name="document"/> atomically and returns the real on-disk path it was
    /// written to. Never partially writes a file a concurrent reader could observe mid-write.
    /// </summary>
    Task<string> SaveAsync(SequenceDocument document, CancellationToken ct);

    /// <summary>Reads back a previously saved plan from <paramref name="path"/> for independent inspection/round-trip verification.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="SequenceDocumentCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="SequenceDocument"/>.</exception>
    Task<SequenceDocument> LoadAsync(string path, CancellationToken ct);
}
