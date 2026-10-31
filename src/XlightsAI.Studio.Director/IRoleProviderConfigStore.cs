namespace XlightsAI.Studio.Director;

/// <summary>Persistence boundary for one <see cref="AiRole"/>'s own <see cref="RoleProviderConfig"/>.</summary>
public interface IRoleProviderConfigStore
{
    /// <summary>The deterministic on-disk path <paramref name="role"/>'s config would be saved at, whether or not it has been saved yet.</summary>
    string GetPath(AiRole role);

    /// <summary>Persists <paramref name="config"/> atomically and returns its path.</summary>
    Task<string> SaveAsync(RoleProviderConfig config, CancellationToken ct);

    /// <summary>Loads <paramref name="role"/>'s own config, or <see cref="RoleProviderConfig.Disabled"/> if none has ever been saved.</summary>
    Task<RoleProviderConfig> LoadAsync(AiRole role, CancellationToken ct);
}
