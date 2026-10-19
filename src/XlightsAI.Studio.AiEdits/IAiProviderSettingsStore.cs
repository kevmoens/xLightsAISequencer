namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Persists the single <see cref="AiProviderSettings"/> this installation uses. Deliberately a new,
/// AiEdits-owned store rather than extending Shell's own <c>ISettingsStore</c>/<c>AppSettings</c> --
/// AiEdits cannot reference Shell (established dependency direction, see this project's own
/// <c>.csproj</c>) and provider config is domain-adjacent (it feeds a domain-layer
/// <see cref="IOptionalAiPlanner"/>), not purely UI-display state.
/// </summary>
public interface IAiProviderSettingsStore
{
    /// <summary>Returns the persisted <see cref="AiProviderSettings"/>, or <see cref="AiProviderSettings.Disabled"/> if none has ever been saved, or the saved file is corrupt -- a corrupt provider config falls back to the safe "no outbound calls" default, never a crash.</summary>
    Task<AiProviderSettings> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(AiProviderSettings settings, CancellationToken ct = default);
}
