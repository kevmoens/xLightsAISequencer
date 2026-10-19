using System.Text.Json;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// JSON-file-backed <see cref="IAiProviderSettingsStore"/>. Same atomic temp-file +
/// <see cref="File.Move(string, string, bool)"/> replace pattern <see cref="JsonSelectionContextStore"/>
/// already uses. A missing or corrupt file is never an error here -- <see cref="LoadAsync"/> falls back
/// to <see cref="AiProviderSettings.Disabled"/>, so a damaged settings file can never leave the provider
/// silently (and incorrectly) enabled.
/// </summary>
public sealed class JsonAiProviderSettingsStore : IAiProviderSettingsStore
{
    private readonly string _filePath;

    public JsonAiProviderSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public async Task<AiProviderSettings> LoadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(_filePath))
        {
            return AiProviderSettings.Disabled;
        }

        await using var stream = File.OpenRead(_filePath);
        try
        {
            var settings = await JsonSerializer.DeserializeAsync<AiProviderSettings>(stream, cancellationToken: ct).ConfigureAwait(false);
            return settings ?? AiProviderSettings.Disabled;
        }
        catch (JsonException)
        {
            return AiProviderSettings.Disabled;
        }
    }

    public async Task SaveAsync(AiProviderSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ct.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{_filePath}.tmp-{Guid.NewGuid():N}";
        try
        {
            var json = JsonSerializer.Serialize(settings);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            File.Move(tempPath, _filePath, overwrite: true);
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
}
