namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only projection of one <see cref="XlightsAI.Studio.Melody.NoteEvent"/> for SCREEN 10 PIANO ROLL
/// — same "Avalonia-free Shell layer projects pure domain data into something directly bindable"
/// convention <see cref="EffectBlockItemView"/>/<see cref="TimelineRulerTickView"/> already established.
/// <see cref="X"/>/<see cref="Width"/> render the note's time span (Step 049 implementation guidance:
/// "Render note lengths"); <see cref="Y"/> renders its pitch row ("...and pitch rows") — higher
/// <see cref="MidiNote"/> values render with a smaller <see cref="Y"/> (higher on screen), the standard
/// piano-roll convention. <see cref="IsLowConfidence"/> is this step's own named "low-confidence
/// markers." <see cref="SelectCommand"/> captures <see cref="NoteId"/> the same way
/// <see cref="EffectBlockItemView.SelectCommand"/> already captures <c>EffectId</c>.
/// </summary>
public sealed record PianoRollNoteRowView(
    Guid NoteId,
    int MidiNote,
    double X,
    double Width,
    double Y,
    bool IsLowConfidence,
    bool IsUserCorrected,
    bool IsLocked,
    bool IsSelected,
    DelegateCommand SelectCommand);
