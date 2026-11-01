using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Director;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// Throwing-on-save default <see cref="ICustomPromptStore"/>, same "needs a real directory this
/// layer does not yet own" reasoning as <see cref="NullWholeSongGenerator"/>/<see cref="NullSongAssetStore"/>.
/// <see cref="LoadAsync"/> returns <see cref="CustomGenerationPrompt.Empty"/> rather than throwing
/// (same "disabled is a valid, non-error default" convention <see cref="NullMotifLinkOverrideStore"/>
/// and friends already use for reads) -- an unconfigured composition root should still show an empty,
/// usable text field, not an error banner, the first time the screen opens.
/// </summary>
public sealed class NullCustomPromptStore : ICustomPromptStore
{
    public static readonly NullCustomPromptStore Instance = new();

    private NullCustomPromptStore()
    {
    }

    public string GetPath() => throw new InvalidOperationException(
        "No real custom-prompt store is configured for this composition root yet (no settings directory wired up).");

    public Task<string> SaveAsync(CustomGenerationPrompt prompt, CancellationToken ct) => throw new InvalidOperationException(
        "No real custom-prompt store is configured for this composition root yet (no settings directory wired up).");

    public Task<CustomGenerationPrompt> LoadAsync(CancellationToken ct) => Task.FromResult(CustomGenerationPrompt.Empty);
}
