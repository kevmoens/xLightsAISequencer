using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 017: one node of the row hierarchy built from a <see cref="XlightsAI.Studio.Xlights.DisplayGraph"/>
/// — either a leaf model row or a group row with its own nested children, mirroring the imported
/// layout's real group nesting (PHASE-02 Section 3: "Groups may overlap; do not flatten user group
/// membership" — this applies equally here: a group appearing under two different parents is two
/// distinct <see cref="TimelineRowNode"/> instances sharing the same <see cref="Id"/>, never merged
/// into one). <see cref="ModelId"/> is populated only for a genuine model row; <see langword="null"/>
/// for a group row or an unresolved/submodel reference that could not be matched to a real imported
/// model (still shown as a visible row rather than silently dropped).
/// </summary>
public sealed record TimelineRowNode(
    TimelineRowId Id,
    string DisplayName,
    bool IsGroup,
    ModelId? ModelId,
    IReadOnlyList<TimelineRowNode> Children);
