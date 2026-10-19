using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only projection of one <see cref="PhraseCandidate"/> for the phrase relationship/propagation
/// inspector (PHASE-08 Step 058) -- same "Avalonia-free Shell layer projects pure domain data into
/// something directly bindable" convention <see cref="PianoRollNoteRowView"/>/<see cref="EffectBlockItemView"/>
/// already established. <see cref="MotifGroupId"/> is <see langword="null"/> exactly when this phrase is
/// not a member of any effective (post-override) motif group -- a distinct passage, or a phrase the user
/// explicitly unlinked from every other occurrence it once shared a group with.
/// </summary>
public sealed record PhraseRelationshipRowView(
    Guid PhraseId,
    int StartBarIndex,
    Guid? MotifGroupId,
    IReadOnlyList<Guid> LinkedPhraseIds,
    bool IsTemplateSource,
    bool IsSelected,
    DelegateCommand SelectCommand);
