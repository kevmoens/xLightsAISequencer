namespace XlightsAI.Studio.Melody;

/// <summary>
/// Throwing default <see cref="INoteTrackEditor"/>, same "needs a real directory-backed store this
/// layer does not yet own" reasoning as <c>XlightsAI.Studio.Shell.NullWholeSongGenerator</c> — a real
/// <see cref="NoteTrackEditor"/> needs an <see cref="IMelodyTrackRevisionStore"/> rooted at a real
/// on-disk directory, which only a real composition root can supply.
/// </summary>
public sealed class NullNoteTrackEditor : INoteTrackEditor
{
    public static readonly NullNoteTrackEditor Instance = new();

    private NullNoteTrackEditor()
    {
    }

    public Task<MelodyTrackRevisionState> BeginEditingAsync(MelodyTrack analyzedTrack, GestureClassificationSettings classificationSettings, CancellationToken ct) =>
        throw new InvalidOperationException("No real note track editor is configured for this composition root yet (no melody-revision-store directory wired up).");

    public Task<MelodyTrackRevisionState?> GetStateAsync(Guid trackId, CancellationToken ct) =>
        throw new InvalidOperationException("No real note track editor is configured for this composition root yet (no melody-revision-store directory wired up).");

    public Task<MelodyTrackRevisionState> CorrectAsync(Guid trackId, CorrectNoteCommand command, CancellationToken ct) =>
        throw new InvalidOperationException("No real note track editor is configured for this composition root yet (no melody-revision-store directory wired up).");

    public Task<MelodyTrackRevisionState> SetNoteLockedAsync(Guid trackId, SetNoteLockedCommand command, CancellationToken ct) =>
        throw new InvalidOperationException("No real note track editor is configured for this composition root yet (no melody-revision-store directory wired up).");
}
