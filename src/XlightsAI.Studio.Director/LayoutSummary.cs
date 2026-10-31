using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>One group/model entry condensed for the Director prompt -- a prop reference plus its own pixel weight and resolution tier, not the full geometry record.</summary>
public sealed record LayoutSummaryEntry(string Reference, int NodeCount, string ResolutionTier);

/// <summary>
/// Condenses a real <see cref="DisplayGraph"/> into the prose/stats a Director prompt needs -- no such
/// type existed anywhere in this codebase before this phase (confirmed by prior research). Built once
/// per layout, reused across every song compiled against it.
/// </summary>
public sealed record LayoutSummary(
    int ModelCount,
    long TotalPixels,
    IReadOnlyList<LayoutSummaryEntry> Groups,
    IReadOnlyList<LayoutSummaryEntry> TopPixelHeavyModels)
{
    private const int HighResolutionNodeCountThreshold = 300;
    private const int TopModelCount = 8;

    public static LayoutSummary Build(DisplayGraph layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var modelsById = layout.Models.ToDictionary(m => m.Id);
        var totalPixels = layout.Models.Sum(m => (long)Math.Max(0, m.NodeCount));

        var groups = layout.Groups
            .Select(g =>
            {
                var memberPixels = g.Members
                    .Where(m => m.Kind == DisplayGroupMemberKind.Model)
                    .Sum(m => modelsById.Values.FirstOrDefault(dm => dm.XlightsName == m.RawName) is { } model ? Math.Max(0, model.NodeCount) : 0);
                return new LayoutSummaryEntry($"group:{g.Name}", memberPixels, Tier(memberPixels));
            })
            .OrderByDescending(e => e.NodeCount)
            .ToList();

        var topModels = layout.Models
            .OrderByDescending(m => m.NodeCount)
            .Take(TopModelCount)
            .Select(m => new LayoutSummaryEntry(m.XlightsName, Math.Max(0, m.NodeCount), Tier(Math.Max(0, m.NodeCount))))
            .ToList();

        return new LayoutSummary(layout.Models.Count, totalPixels, groups, topModels);
    }

    private static string Tier(long nodeCount) => nodeCount >= HighResolutionNodeCountThreshold ? "high-resolution" : "low-resolution";

    /// <summary>Renders as the compact prose block the Director prompt embeds -- never the full geometry, only what a layout decision needs.</summary>
    public string ToPromptText()
    {
        var lines = new List<string>
        {
            $"Layout: {ModelCount} models, {TotalPixels} total pixels.",
            "Groups (name: pixel weight, resolution tier):",
        };
        lines.AddRange(Groups.Select(g => $"- {g.Reference}: {g.NodeCount}px, {g.ResolutionTier}"));
        lines.Add("Top individually-addressable pixel-heavy models:");
        lines.AddRange(TopPixelHeavyModels.Select(m => $"- {m.Reference}: {m.NodeCount}px, {m.ResolutionTier}"));
        return string.Join('\n', lines);
    }
}
