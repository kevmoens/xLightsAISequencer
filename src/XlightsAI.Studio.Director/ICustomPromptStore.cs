using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Director;

/// <summary>Seam for <see cref="JsonCustomPromptStore"/>, same reason <see cref="IRoleProviderConfigStore"/> exists -- lets <c>CustomPromptEditorViewModel</c> be unit-tested without real file I/O.</summary>
public interface ICustomPromptStore
{
    string GetPath();

    Task<string> SaveAsync(CustomGenerationPrompt prompt, CancellationToken ct);

    /// <summary>Returns <see cref="CustomGenerationPrompt.Empty"/> when no file exists yet -- same "disabled is a valid, non-error default" convention as <see cref="IRoleProviderConfigStore.LoadAsync"/>.</summary>
    Task<CustomGenerationPrompt> LoadAsync(CancellationToken ct);
}
