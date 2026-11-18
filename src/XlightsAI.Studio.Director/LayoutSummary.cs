using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>One group/model entry condensed for the Director prompt -- a prop reference plus its own pixel weight and resolution tier, not the full geometry record.</summary>
/// <param name="RelativePosition">
/// Build-plan Phase 28a §4b: one of <see cref="LayoutSummary.RelativePositionLeft"/>/.../<see cref="LayoutSummary.RelativePositionRight"/>
/// (5 buckets), bucketed from this entry's own real <c>DisplayBounds.X</c> (a group's own average over
/// its real, bounded members) relative to the layout's own real X span -- the same real position data
/// <see cref="Choreography.Look.Travel"/>'s internal sort already reads, finally exposed as text. <see langword="null"/>
/// when no real bounds are known for this entry -- never a guessed position.
/// </param>
public sealed record LayoutSummaryEntry(string Reference, int NodeCount, string ResolutionTier, string? RelativePosition = null);

/// <summary>
/// Condenses a real <see cref="DisplayGraph"/> into the prose/stats a Director prompt needs -- no such
/// type existed anywhere in this codebase before this phase (confirmed by prior research). Built once
/// per layout, reused across every song compiled against it.
/// </summary>
/// <param name="HasTreeOrMatrixModel">Whether any real model in this layout reads as Tree/Matrix-class by <see cref="TreeMatrixPropNameConvention"/> -- lets a <see cref="DirectorLesson"/> condition itself on this layout actually having a Tree/Matrix prop, rather than always showing Tree/Matrix-specific guidance regardless of layout.</param>
/// <param name="HasMultiModelGroupWithKnownPositions">Whether any real group has 3+ real individual model members with a known real <c>DisplayBounds</c> position -- the exact precondition a <c>Look.Travel</c> sweep (ordering members by real physical position) needs to be meaningful; a group whose members have no known position, or fewer than 3 of them, cannot support a genuine multi-member sweep.</param>
public sealed record LayoutSummary(
    int ModelCount,
    long TotalPixels,
    IReadOnlyList<LayoutSummaryEntry> Groups,
    IReadOnlyList<LayoutSummaryEntry> TopPixelHeavyModels,
    bool HasTreeOrMatrixModel = false,
    bool HasMultiModelGroupWithKnownPositions = false)
{
    private const int HighResolutionNodeCountThreshold = 300;
    private const int TopModelCount = 8;
    private const int MinMembersForMultiModelGroup = 3;

    /// <summary>Build-plan Phase 28a §4b: 5 real-position buckets, ordered left to right -- a designer thinks "stage left"/"center"/"stage right," never a raw X coordinate.</summary>
    public const string RelativePositionLeft = "left";
    public const string RelativePositionCenterLeft = "center-left";
    public const string RelativePositionCenter = "center";
    public const string RelativePositionCenterRight = "center-right";
    public const string RelativePositionRight = "right";

    private static readonly string[] RelativePositionBuckets =
    [
        RelativePositionLeft, RelativePositionCenterLeft, RelativePositionCenter, RelativePositionCenterRight, RelativePositionRight,
    ];

    public static LayoutSummary Build(DisplayGraph layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var modelsById = layout.Models.ToDictionary(m => m.Id);
        var totalPixels = layout.Models.Sum(m => (long)Math.Max(0, m.NodeCount));

        var knownXs = layout.Models.Where(m => m.Bounds is not null).Select(m => m.Bounds!.X).ToList();
        var minX = knownXs.Count > 0 ? knownXs.Min() : 0;
        var maxX = knownXs.Count > 0 ? knownXs.Max() : 0;

        var groups = layout.Groups
            .Select(g =>
            {
                var members = g.Members
                    .Where(m => m.Kind == DisplayGroupMemberKind.Model)
                    .Select(m => modelsById.Values.FirstOrDefault(dm => dm.XlightsName == m.RawName))
                    .Where(m => m is not null)
                    .Select(m => m!)
                    .ToList();
                var memberPixels = members.Sum(m => Math.Max(0, m.NodeCount));
                var boundedXs = members.Where(m => m.Bounds is not null).Select(m => m.Bounds!.X).ToList();
                var relativePosition = boundedXs.Count > 0 ? BucketRelativePosition(boundedXs.Average(), minX, maxX) : null;
                return new LayoutSummaryEntry($"group:{g.Name}", memberPixels, Tier(memberPixels), relativePosition);
            })
            .OrderByDescending(e => e.NodeCount)
            .ToList();

        var topModels = layout.Models
            .OrderByDescending(m => m.NodeCount)
            .Take(TopModelCount)
            .Select(m => new LayoutSummaryEntry(
                m.XlightsName, Math.Max(0, m.NodeCount), Tier(Math.Max(0, m.NodeCount)),
                m.Bounds is { } bounds ? BucketRelativePosition(bounds.X, minX, maxX) : null))
            .ToList();

        var hasTreeOrMatrixModel = layout.Models.Any(m => TreeMatrixPropNameConvention.IsTreeOrMatrixName(m.XlightsName));
        var hasMultiModelGroupWithKnownPositions = layout.Groups.Any(g =>
            g.Members.Count(m => m.Kind == DisplayGroupMemberKind.Model &&
                modelsById.Values.FirstOrDefault(dm => dm.XlightsName == m.RawName) is { Bounds: not null }) >= MinMembersForMultiModelGroup);

        return new LayoutSummary(layout.Models.Count, totalPixels, groups, topModels, hasTreeOrMatrixModel, hasMultiModelGroupWithKnownPositions);
    }

    private static string Tier(long nodeCount) => nodeCount >= HighResolutionNodeCountThreshold ? "high-resolution" : "low-resolution";

    /// <summary>
    /// Build-plan Phase 28a §4b: buckets <paramref name="x"/> into one of <see cref="RelativePositionBuckets"/>,
    /// relative to the layout's own real <paramref name="minX"/>/<paramref name="maxX"/> span -- a layout
    /// with no real X spread (<c>maxX &lt;= minX</c>, e.g. only one bounded model) always buckets to
    /// <see cref="RelativePositionCenter"/> rather than dividing by zero.
    /// </summary>
    private static string BucketRelativePosition(double x, double minX, double maxX)
    {
        if (maxX <= minX)
        {
            return RelativePositionCenter;
        }

        var fraction = Math.Clamp((x - minX) / (maxX - minX), 0.0, 1.0);
        var index = Math.Min(RelativePositionBuckets.Length - 1, (int)(fraction * RelativePositionBuckets.Length));
        return RelativePositionBuckets[index];
    }

    /// <summary>
    /// Renders as the compact prose block the Director prompt embeds -- never the full geometry, only
    /// what a layout decision needs.
    /// </summary>
    /// <param name="includeRelativePosition">
    /// Build-plan Phase 28a §4b: appends each entry's own real <see cref="LayoutSummaryEntry.RelativePosition"/>
    /// when known. Defaults to <see langword="false"/> -- every pre-existing caller (every Director/
    /// Choreographer prompt built before <c>ChoreographyGenerationSettings.EnableSectionTimelines</c>
    /// existed) keeps producing byte-identical prompt text; a caller opts into the new position clause
    /// explicitly, same disclosed-default-off convention as the flag itself.
    /// </param>
    public string ToPromptText(bool includeRelativePosition = false)
    {
        var lines = new List<string>
        {
            $"Layout: {ModelCount} models, {TotalPixels} total pixels.",
            "Groups (name: pixel weight, resolution tier):",
        };
        lines.AddRange(Groups.Select(e => FormatEntry(e, includeRelativePosition)));
        lines.Add("Top individually-addressable pixel-heavy models:");
        lines.AddRange(TopPixelHeavyModels.Select(e => FormatEntry(e, includeRelativePosition)));
        return string.Join('\n', lines);
    }

    private static string FormatEntry(LayoutSummaryEntry entry, bool includeRelativePosition) => includeRelativePosition && entry.RelativePosition is { Length: > 0 } position
        ? $"- {entry.Reference}: {entry.NodeCount}px, {entry.ResolutionTier}, {position}"
        : $"- {entry.Reference}: {entry.NodeCount}px, {entry.ResolutionTier}";
}
