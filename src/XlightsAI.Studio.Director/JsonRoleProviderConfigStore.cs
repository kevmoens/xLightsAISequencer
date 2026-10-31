using System.Text.Json;

namespace XlightsAI.Studio.Director;

/// <summary>JSON-file-backed <see cref="IRoleProviderConfigStore"/>. Mirrors the atomic temp-file + <see cref="File.Move(string, string, bool)"/> pattern every other JSON store in this codebase uses (e.g. <c>JsonVisualStoryStore</c>), one file per role.</summary>
public sealed class JsonRoleProviderConfigStore : IRoleProviderConfigStore
{
    private readonly string _directory;

    public JsonRoleProviderConfigStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(AiRole role) =>
        Path.Combine(_directory, $"ai-provider-{role.ToString().ToLowerInvariant()}.json");

    public async Task<string> SaveAsync(RoleProviderConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(config.Role);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(config);
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

    public async Task<RoleProviderConfig> LoadAsync(AiRole role, CancellationToken ct)
    {
        var path = GetPath(role);
        if (!File.Exists(path))
        {
            return RoleProviderConfig.Disabled(role);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var config = await JsonSerializer.DeserializeAsync<RoleProviderConfig>(stream, cancellationToken: ct).ConfigureAwait(false);
            return config ?? throw new RoleProviderConfigCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new RoleProviderConfigCorruptException(path, ex);
        }
    }
}
