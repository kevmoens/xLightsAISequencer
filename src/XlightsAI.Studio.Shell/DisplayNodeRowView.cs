using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only projection of one <see cref="XlightsAI.Studio.Display.DisplayNode"/> for SCREEN 05 DISPLAY
/// PATHS -- same "Avalonia-free Shell layer projects pure domain data into something directly
/// bindable" convention <see cref="PianoRollNoteRowView"/>/<see cref="EffectBlockItemView"/> already
/// established. <see cref="X"/>/<see cref="Y"/> are the node's current normalized preview position
/// (Step 061 "manual position correction" already applied, when present); rows are returned in the
/// list editor's own current display order. <see cref="SelectCommand"/> captures <see cref="NodeId"/>
/// the same way <see cref="PianoRollNoteRowView.SelectCommand"/> already captures <c>NoteId</c>.
/// </summary>
public sealed record DisplayNodeRowView(
    ModelId NodeId,
    string XlightsName,
    double X,
    double Y,
    string ModelKind,
    bool IsSelected,
    DelegateCommand SelectCommand);
