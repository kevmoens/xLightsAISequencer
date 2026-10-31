using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>Build-plan Phase 10 §2b/§3d: the physical/spatial "home" a frequency band gets for the whole song -- bass reads as a full-canvas impact on the biggest structural groups, mids carry the melodic subject, highs live on small/detail accents.</summary>
public enum BandRole
{
    Foundation,
    Subject,
    Accent,
}

/// <summary>One real layout group/model (<see cref="LayoutSummary.Groups"/> or <see cref="LayoutSummary.TopPixelHeavyModels"/>) classified into a <see cref="BandRole"/> by name, so the Director prompt can name actual props rather than a generic abstraction.</summary>
public sealed record ClassifiedLayoutEntry(string Reference, BandRole Role);

/// <summary>One section's measured bass/mid/high share of its own bar-range window, from the real <see cref="BandEnergyEnvelope"/> -- the "real measured basis" §3d asks the static per-style <see cref="StylePreset.PropRoleBias"/> weights to be extended with.</summary>
public sealed record SectionBandShare(SectionKind Kind, int StartBarIndex, int EndBarIndexExclusive, double BassShare, double MidShare, double HighShare)
{
    /// <summary>"bass"/"mid"/"high" (matching <see cref="MusicalRole"/>'s own naming, "highs" aside), or <see langword="null"/> when the window carried no measurable energy (e.g. outside the analyzed song, or silence).</summary>
    public string? DominantBand => (BassShare, MidShare, HighShare) switch
    {
        (0, 0, 0) => null,
        var (bass, mid, high) when bass >= mid && bass >= high => MusicalRole.Bass,
        var (_, mid, high) when mid >= high => MusicalRole.Melody,
        _ => MusicalRole.Highs,
    };
}

/// <summary>
/// Build-plan Phase 10 §3d: once real <see cref="BandEnergyEnvelope"/> data is wired through (§3a, already
/// done), (1) compute each section's own dominant-band profile from that real data, and (2) classify the
/// real imported layout's own groups/models into the §2b foundation/subject/accent roles by name, so the
/// Director prompt can state a single, named, consistent bass-to-group mapping for the whole song instead
/// of leaving group-role assignment to be re-decided per Look. Classification is keyword-based (not a
/// hardcoded list of one user's own group names) so it generalizes to any imported layout; a group/model
/// whose name matches nothing recognizable (e.g. "Everything") is simply omitted rather than forced into a
/// role it does not describe.
/// </summary>
public static class BandEnergyGroupRoleMapper
{
    private static readonly string[] AccentKeywords = ["minitree", "mini tree", "icicle", "strand", "star", "sparkle", "spike", "snowflake"];
    private static readonly string[] SubjectKeywords = ["megatree", "mega tree", "tree", "arch", "matrix"];
    private static readonly string[] FoundationKeywords = ["outline", "gutter", "roof", "horizontal", "eave", "ridge", "fence"];

    /// <summary>
    /// Classifies every real group (<see cref="LayoutSummary.Groups"/>) and individually-addressable
    /// pixel-heavy model (<see cref="LayoutSummary.TopPixelHeavyModels"/>) in <paramref name="layout"/> by
    /// name -- accent keywords checked first so e.g. "MiniTrees" classifies as an accent group, not a
    /// subject one, despite containing "tree". A broad catch-all group like "Everything" matches no
    /// keyword and is omitted, not misclassified.
    /// </summary>
    public static IReadOnlyList<ClassifiedLayoutEntry> Classify(LayoutSummary layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ClassifiedLayoutEntry>();

        foreach (var entry in layout.Groups.Concat(layout.TopPixelHeavyModels))
        {
            if (!seen.Add(entry.Reference))
            {
                continue;
            }

            var role = ClassifyName(entry.Reference);
            if (role is { } r)
            {
                result.Add(new ClassifiedLayoutEntry(entry.Reference, r));
            }
        }

        return result;
    }

    private static BandRole? ClassifyName(string reference)
    {
        var name = reference.StartsWith("group:", StringComparison.Ordinal) ? reference["group:".Length..] : reference;

        if (AccentKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return BandRole.Accent;
        }

        if (SubjectKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return BandRole.Subject;
        }

        if (FoundationKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return BandRole.Foundation;
        }

        return null;
    }

    /// <summary>
    /// Computes each real section's own bass/mid/high share of total energy across its own bar-range
    /// window, from <see cref="MusicModel.BandEnergy"/>'s real per-frame data -- never a whole-song average
    /// standing in for every section. Returns one entry per <see cref="MusicModel.Sections"/> label,
    /// ordered by <see cref="SectionLabelCandidate.StartBarIndex"/>; every share is <c>(0,0,0)</c> (per
    /// <see cref="BandEnergyEnvelope.AverageInWindow"/>'s own "silence is a valid, inspectable result"
    /// convention) when <see cref="MusicModel.BandEnergy"/> is <see langword="null"/> or a section's bar
    /// range falls outside the song's own beat grid.
    /// </summary>
    public static IReadOnlyList<SectionBandShare> ComputeSectionProfiles(MusicModel music)
    {
        ArgumentNullException.ThrowIfNull(music);

        var sections = music.Sections.Labels.OrderBy(l => l.StartBarIndex).ToList();
        if (music.BandEnergy is null || sections.Count == 0)
        {
            return sections.Select(s => new SectionBandShare(s.Kind, s.StartBarIndex, s.EndBarIndexExclusive, 0, 0, 0)).ToList();
        }

        var barGrid = BarGrid.FromBeatGrid(music.BeatGrid, music.SongDurationTicks);
        var bandEnergy = music.BandEnergy;

        return sections.Select(section =>
        {
            var window = barGrid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive));
            if (window is null)
            {
                return new SectionBandShare(section.Kind, section.StartBarIndex, section.EndBarIndexExclusive, 0, 0, 0);
            }

            var (bass, mid, high) = bandEnergy.AverageInWindow(window.Value.StartTicks, window.Value.EndTicks);
            var total = bass + mid + high;
            return total <= 0
                ? new SectionBandShare(section.Kind, section.StartBarIndex, section.EndBarIndexExclusive, 0, 0, 0)
                : new SectionBandShare(section.Kind, section.StartBarIndex, section.EndBarIndexExclusive, bass / total, mid / total, high / total);
        }).ToList();
    }

    /// <summary>Whole-song average band share across every section's own profile, weighted by bar count -- the single "real measured basis" figure the §3d mapping rule is grounded in.</summary>
    public static (double BassShare, double MidShare, double HighShare) ComputeSongWideShare(IReadOnlyList<SectionBandShare> sectionProfiles)
    {
        ArgumentNullException.ThrowIfNull(sectionProfiles);

        var weighted = sectionProfiles
            .Select(s => (Weight: Math.Max(1, s.EndBarIndexExclusive - s.StartBarIndex), s.BassShare, s.MidShare, s.HighShare))
            .ToList();

        var totalWeight = weighted.Sum(w => (double)w.Weight);
        if (totalWeight <= 0)
        {
            return (0, 0, 0);
        }

        return (
            weighted.Sum(w => w.Weight * w.BassShare) / totalWeight,
            weighted.Sum(w => w.Weight * w.MidShare) / totalWeight,
            weighted.Sum(w => w.Weight * w.HighShare) / totalWeight);
    }
}
