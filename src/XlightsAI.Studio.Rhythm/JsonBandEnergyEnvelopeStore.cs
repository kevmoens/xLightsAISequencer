using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>JSON-file-backed <see cref="IBandEnergyEnvelopeStore"/>. Mirrors <see cref="JsonOnsetAnalysisStore"/>'s atomic-write pattern exactly.</summary>
public sealed class JsonBandEnergyEnvelopeStore : IBandEnergyEnvelopeStore
{
    private readonly string _directory;

    public JsonBandEnergyEnvelopeStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"band-energy-{songId.Value:N}.json");

    public async Task<string> SaveAsync(BandEnergyEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(envelope.SongId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(envelope);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, finalPath, overwrite: true);
            return finalPath;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public async Task<BandEnergyEnvelope> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No band energy envelope cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var envelope = await JsonSerializer.DeserializeAsync<BandEnergyEnvelope>(stream, cancellationToken: ct).ConfigureAwait(false);
            return envelope ?? throw new BandEnergyAnalysisCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new BandEnergyAnalysisCorruptException(path, ex);
        }
    }
}
