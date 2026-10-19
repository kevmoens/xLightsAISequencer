using System.Text.Json;

namespace XlightsAI.Studio.Reference.Fixtures;

/// <summary>Raised when a manifest file exists but is not well-formed JSON or is missing required shape.</summary>
public sealed class FixtureCorpusManifestFormatException(string path, Exception inner)
    : Exception($"'{path}' is not a well-formed fixture corpus manifest.", inner);

/// <summary>Reads <see cref="FixtureCorpusManifest"/> from a JSON file on disk. Never writes.</summary>
public sealed class JsonFixtureCorpusManifestReader : IFixtureCorpusManifestReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public async Task<FixtureCorpusManifest> ReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture corpus manifest not found at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var manifest = await JsonSerializer.DeserializeAsync<FixtureCorpusManifest>(stream, Options, ct)
                .ConfigureAwait(false);

            return manifest ?? throw new FixtureCorpusManifestFormatException(path,
                new JsonException("Deserialization returned null."));
        }
        catch (JsonException ex)
        {
            throw new FixtureCorpusManifestFormatException(path, ex);
        }
    }
}
