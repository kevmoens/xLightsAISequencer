using System.Text.Json;
using System.Text.Json.Serialization;

namespace XlightsAI.Studio.Reference;

/// <summary>Raised when a module audit matrix file exists but is not well-formed JSON or shape.</summary>
public sealed class ModuleAuditMatrixFormatException(string path, Exception inner)
    : Exception($"'{path}' is not a well-formed module audit matrix.", inner);

/// <summary>Reads <see cref="ModuleAuditMatrix"/> from a JSON file on disk. Never writes.</summary>
public sealed class JsonModuleAuditMatrixReader : IModuleAuditMatrixReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<ModuleAuditMatrix> ReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Module audit matrix not found at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var matrix = await JsonSerializer.DeserializeAsync<ModuleAuditMatrix>(stream, Options, ct)
                .ConfigureAwait(false);

            return matrix ?? throw new ModuleAuditMatrixFormatException(path,
                new JsonException("Deserialization returned null."));
        }
        catch (JsonException ex)
        {
            throw new ModuleAuditMatrixFormatException(path, ex);
        }
    }
}
