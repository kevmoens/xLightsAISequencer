namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 017 ("Build prop rows, group expansion and stable mapping to model IDs"):
/// whether a <see cref="TimelineRowId"/> identifies an imported <see cref="XlightsAI.Studio.Xlights.DisplayModel"/>
/// or an imported <see cref="XlightsAI.Studio.Xlights.DisplayModelGroup"/> — a group is itself a
/// valid, independently addressable row, never merged into its members' identity.
/// </summary>
public enum TimelineRowKind
{
    Model,
    Group,
}
