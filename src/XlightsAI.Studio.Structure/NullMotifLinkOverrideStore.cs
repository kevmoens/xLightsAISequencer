using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Throwing default <see cref="IMotifLinkOverrideStore"/>, same "needs a real directory-backed store
/// this layer does not yet own" reasoning as <c>XlightsAI.Studio.Melody.NullNoteTrackEditor</c>.
/// </summary>
public sealed class NullMotifLinkOverrideStore : IMotifLinkOverrideStore
{
    public static readonly NullMotifLinkOverrideStore Instance = new();

    private NullMotifLinkOverrideStore()
    {
    }

    public string GetPath(SongId songId) =>
        throw new InvalidOperationException("No real motif link override store is configured for this composition root yet (no analysis-cache directory wired up).");

    public Task<IReadOnlyList<MotifLinkOverride>> LoadAsync(SongId songId, CancellationToken ct) =>
        throw new InvalidOperationException("No real motif link override store is configured for this composition root yet (no analysis-cache directory wired up).");

    public Task SaveAsync(SongId songId, IReadOnlyList<MotifLinkOverride> overrides, CancellationToken ct) =>
        throw new InvalidOperationException("No real motif link override store is configured for this composition root yet (no analysis-cache directory wired up).");
}
