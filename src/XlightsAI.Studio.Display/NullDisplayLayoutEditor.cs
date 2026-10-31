using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// Throwing default <see cref="IDisplayLayoutEditor"/>, same "needs a real directory-backed store
/// this layer does not yet own" reasoning as <c>XlightsAI.Studio.Melody.NullNoteTrackEditor</c>.
/// </summary>
public sealed class NullDisplayLayoutEditor : IDisplayLayoutEditor
{
    public static readonly NullDisplayLayoutEditor Instance = new();

    private NullDisplayLayoutEditor()
    {
    }

    public Task<DisplayLayoutCorrections> GetCorrectionsAsync(string sourceLayoutHash, CancellationToken ct) =>
        throw new InvalidOperationException("No real display layout editor is configured for this composition root yet (no corrections-store directory wired up).");

    public Task<DisplayLayoutCorrections> CorrectPositionAsync(SpatialDisplayGraph graph, ModelId nodeId, double newX, double newY, CancellationToken ct) =>
        throw new InvalidOperationException("No real display layout editor is configured for this composition root yet (no corrections-store directory wired up).");

    public Task<DisplayLayoutCorrections> ReorderAsync(SpatialDisplayGraph graph, IReadOnlyList<ModelId> newOrder, CancellationToken ct) =>
        throw new InvalidOperationException("No real display layout editor is configured for this composition root yet (no corrections-store directory wired up).");
}
