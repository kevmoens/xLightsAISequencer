namespace XlightsAI.Studio.Director.Tests;

public sealed class JsonRoleProviderConfigStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase5-provider-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_NeverSaved_ReturnsDisabledDefault_ForThatRole()
    {
        var store = new JsonRoleProviderConfigStore(_root);

        var config = await store.LoadAsync(AiRole.Critic, CancellationToken.None);

        Assert.Equal(AiRole.Critic, config.Role);
        Assert.False(config.Enabled);
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTrips_AndIsIndependentPerRole()
    {
        var store = new JsonRoleProviderConfigStore(_root);
        var directorConfig = new RoleProviderConfig(
            AiRole.Director, Enabled: true, AiProviderType.Anthropic, "https://api.anthropic.com", "claude-opus-5",
            "director-cred", 60, 2, 262_144);

        await store.SaveAsync(directorConfig, CancellationToken.None);

        var loadedDirector = await store.LoadAsync(AiRole.Director, CancellationToken.None);
        var loadedChoreographer = await store.LoadAsync(AiRole.Choreographer, CancellationToken.None);

        Assert.Equal(directorConfig, loadedDirector);
        Assert.False(loadedChoreographer.Enabled); // untouched -- each role's own independent file.
    }
}
