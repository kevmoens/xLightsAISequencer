using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Director;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>Build-plan Phase 5: the Settings screen's "connect an AI provider" editor, backed by the real atomic config store and the real DPAPI-backed credential store.</summary>
public sealed class AiProviderSettingsEditorViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-phase5-settings-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class InMemoryCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public Task StoreAsync(string name, string secret, CancellationToken ct = default)
        {
            _secrets[name] = secret;
            return Task.CompletedTask;
        }

        public Task<string?> TryGetAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(_secrets.TryGetValue(name, out var secret) ? secret : null);

        public Task DeleteAsync(string name, CancellationToken ct = default)
        {
            _secrets.Remove(name);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LoadAsync_NeverSaved_ShowsDisabledDefault_NoStoredCredential()
    {
        var vm = new AiProviderSettingsEditorViewModel(new JsonRoleProviderConfigStore(_root), new InMemoryCredentialStore());
        vm.SelectedRole = AiRole.Director;

        await vm.LoadAsync();

        Assert.False(vm.CurrentConfig.Enabled);
        Assert.False(vm.HasStoredCredential);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task SaveAsync_WithCredentialInput_PersistsConfigAndEncryptedCredential_IndependentlyPerRole()
    {
        var configStore = new JsonRoleProviderConfigStore(_root);
        var credentialStore = new InMemoryCredentialStore();
        var vm = new AiProviderSettingsEditorViewModel(configStore, credentialStore);
        vm.SelectedRole = AiRole.Director;
        await vm.LoadAsync();

        vm.CurrentConfig = vm.CurrentConfig with
        {
            Enabled = true,
            ProviderType = AiProviderType.Anthropic,
            Endpoint = "https://api.anthropic.com",
            ModelName = "claude-opus-5",
        };
        vm.CredentialInput = "sk-ant-secret";

        var saved = await vm.SaveAsync();

        Assert.True(saved);
        Assert.True(vm.HasStoredCredential);
        Assert.Equal("", vm.CredentialInput); // cleared after save -- never lingers in view-model state.
        Assert.Null(vm.ErrorMessage);

        var reloaded = await configStore.LoadAsync(AiRole.Director, CancellationToken.None);
        Assert.True(reloaded.Enabled);
        Assert.Equal("claude-opus-5", reloaded.ModelName);
        Assert.Equal("sk-ant-secret", await credentialStore.TryGetAsync(reloaded.CredentialName!));

        // Choreographer's own config is untouched by saving Director's.
        var choreographerConfig = await configStore.LoadAsync(AiRole.Choreographer, CancellationToken.None);
        Assert.False(choreographerConfig.Enabled);
    }

    [Fact]
    public async Task SaveAsync_BlankCredentialInput_LeavesAlreadyStoredCredentialUntouched()
    {
        var configStore = new JsonRoleProviderConfigStore(_root);
        var credentialStore = new InMemoryCredentialStore();
        var vm = new AiProviderSettingsEditorViewModel(configStore, credentialStore);
        vm.SelectedRole = AiRole.Director;
        await vm.LoadAsync();
        vm.CurrentConfig = vm.CurrentConfig with { Enabled = true, ModelName = "claude-opus-5" };
        vm.CredentialInput = "sk-first-secret";
        await vm.SaveAsync();

        // Second save, no new credential typed -- just changing the model name.
        vm.CredentialInput = "";
        vm.CurrentConfig = vm.CurrentConfig with { ModelName = "claude-sonnet-5" };
        await vm.SaveAsync();

        Assert.Equal("sk-first-secret", await credentialStore.TryGetAsync("ai-provider-director"));
    }

    [Fact]
    public async Task DisableAndClearCredentialAsync_RemovesCredential_AndDisablesConfig()
    {
        var configStore = new JsonRoleProviderConfigStore(_root);
        var credentialStore = new InMemoryCredentialStore();
        var vm = new AiProviderSettingsEditorViewModel(configStore, credentialStore);
        vm.SelectedRole = AiRole.Critic;
        await vm.LoadAsync();
        vm.CurrentConfig = vm.CurrentConfig with { Enabled = true };
        vm.CredentialInput = "sk-secret";
        await vm.SaveAsync();

        var result = await vm.DisableAndClearCredentialAsync();

        Assert.True(result);
        Assert.False(vm.CurrentConfig.Enabled);
        Assert.False(vm.HasStoredCredential);
        Assert.Null(await credentialStore.TryGetAsync("ai-provider-critic"));
    }
}
