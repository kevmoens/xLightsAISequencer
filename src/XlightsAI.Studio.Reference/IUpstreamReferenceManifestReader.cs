namespace XlightsAI.Studio.Reference;

/// <summary>Adapter boundary for reading the pinned upstream reference manifest from disk.</summary>
public interface IUpstreamReferenceManifestReader
{
    Task<UpstreamReferenceManifest> ReadAsync(string path, CancellationToken ct);
}
