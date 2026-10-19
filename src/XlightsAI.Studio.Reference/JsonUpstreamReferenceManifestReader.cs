using System.Text.Json;

namespace XlightsAI.Studio.Reference;

/// <summary>Raised when a manifest file exists but is not well-formed JSON or is missing required shape.</summary>
public sealed class UpstreamReferenceManifestFormatException(string path, Exception inner)
    : Exception($"'{path}' is not a well-formed upstream reference manifest.", inner);

/// <summary>Reads <see cref="UpstreamReferenceManifest"/> from a JSON file on disk. Never writes.</summary>
public sealed class JsonUpstreamReferenceManifestReader : IUpstreamReferenceManifestReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<UpstreamReferenceManifest> ReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Upstream reference manifest not found at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var manifest = await JsonSerializer.DeserializeAsync<UpstreamReferenceManifest>(stream, Options, ct)
                .ConfigureAwait(false);

            return manifest ?? throw new UpstreamReferenceManifestFormatException(path,
                new JsonException("Deserialization returned null."));
        }
        catch (JsonException ex)
        {
            throw new UpstreamReferenceManifestFormatException(path, ex);
        }
    }
}
