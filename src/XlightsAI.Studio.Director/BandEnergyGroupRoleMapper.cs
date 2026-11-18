using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 10 §2b/§3d: the physical/spatial "home" a frequency band gets for the whole song --
/// bass reads as a full-canvas impact on the biggest structural groups, mids carry the melodic subject,
/// highs live on small/detail accents.
///
/// Build-plan Phase 25 Part A: <see cref="WholeCanvas"/> is a different *kind* of role than the other
/// three -- it answers "when" (a song-wide synchronized moment), not "where" (one frequency band's
/// spatial home), so a consumer must never treat it as a fourth peer in the foundation/subject/accent
/// band-to-group mapping (it gets its own prompt section; see <c>DirectorPromptBuilder.AppendWholeCanvasGroupSection</c>).
/// </summary>
public enum BandRole
{
    Foundation,
    Subject,
    Accent,

    /// <summary>
    /// Build-plan Phase 25 Part A: a group/model whose own pixel coverage against the whole layout meets
    /// <see cref="BandEnergyGroupRoleMapper.WholeCanvasCoverageThreshold"/> -- a catch-all group (e.g.
    /// named "Everything", "All", "Master", "WholeHouse") that is not tied to any single frequency band's
    /// spatial home. Detected structurally (by coverage ratio), never by name, since no fixed keyword list
    /// can cover every way a user might name a whole-layout group.
    ///
    /// <b>Build-plan Phase 25 Part B.3 audit finding:</b> <see cref="BandRole"/> is not purely cosmetic --
    /// <c>ChoreographyGenerationPipeline.BuildFillerSchedule</c> resolves every filler target's own
    /// <c>BandRole</c> (via <c>Classify</c>) into a role-key string the compiled <c>SectionFillerSchedule</c>
    /// uses to pick a narrower per-role filler pool when a <c>SectionScript</c> declared one. In practice,
    /// though, this never changes compiled output for <see cref="WholeCanvas"/> specifically:
    /// <c>SectionScriptValidator.KnownRoleKeys</c> only recognizes <c>"Foundation"</c>/<c>"Subject"</c>/
    /// <c>"Accent"</c> -- a declared <c>FillerEffectsByRole["WholeCanvas"]</c> key would fail validation, and
    /// the Director is never prompted that <c>"WholeCanvas"</c> is a valid key to declare in the first
    /// place -- so a whole-canvas-classified target's resolved role key never matches any section's own
    /// <c>FillerEffectsByRole</c> dictionary and <see cref="Choreography.SectionFillerSchedule.Resolve"/>
    /// always falls back to that section's flat pool, exactly as it did before this group had any role at
    /// all (unclassified). Confirmed acceptable for v1: today, <see cref="WholeCanvas"/> only ever affects
    /// <see cref="Director.DirectorPromptBuilder"/>'s own prompt text (informational guidance), never which
    /// exemplar/effect gets compiled for a group. Expanding scope to let the Director actually declare a
    /// whole-canvas-specific filler pool would require adding <c>"WholeCanvas"</c> to
    /// <c>SectionScriptValidator.KnownRoleKeys</c> and prompting for it -- a real but separate, not-yet-
    /// requested change.
    /// </summary>
    WholeCanvas,
}

/// <summary>One real layout group/model (<see cref="LayoutSummary.Groups"/> or <see cref="LayoutSummary.TopPixelHeavyModels"/>) classified into a <see cref="BandRole"/> by name, so the Director prompt can name actual props rather than a generic abstraction.</summary>
public sealed record ClassifiedLayoutEntry(string Reference, BandRole Role);

/// <summary>
/// Build-plan Phase 28a §4c: one real, measured sub-window sample of a section's own band-energy curve
/// -- unlike <see cref="SectionBandShare"/>'s own single whole-section share, <see cref="BandEnergyGroupRoleMapper.ComputeSectionCurve"/>
/// returns a short ordered list of these so the prompt can show energy movement WITHIN a section (e.g.
/// "mids dominate early, bass takes over by the back half"), anchored to real ticks a cue can reason
/// against via <see cref="SectionTimingMarks"/>.
/// </summary>
/// <param name="StartTicks">Start of this sub-window (inclusive), real ticks within the section's own window.</param>
/// <param name="EndTicks">End of this sub-window (exclusive).</param>
/// <param name="DominantBand">"bass"/"mid"/"high" (<see cref="MusicalRole"/>'s own naming), or <see langword="null"/> when this sub-window carried no measurable energy.</param>
public sealed record SectionBandCurveSample(long StartTicks, long EndTicks, string? DominantBand);

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
    /// <summary>
    /// Build-plan Phase 25 Part C calibration: measured, not guessed, against a real
    /// <c>CorpusTrainingRunner</c> run over 31 external <c>C:\xLights</c> sequences (2026-11-11) --
    /// <c>CatchAllCoverageRatioDistribution</c> (360 group samples across 30 songs with a layout): Median
    /// 8.0%, P90 55.7%, Max 100%. The real histogram is not a smooth curve -- it has two distinct clusters
    /// above the bulk of ordinary groups: 30 samples (one per song) land in the 0.5-0.6 bucket, and a
    /// separate 30 samples (also one per song, <c>Max</c> confirms several hit exactly 100%) land in the
    /// 0.9-1.0+ bucket, with every bucket in between (0.6-0.9) carrying zero samples -- a clean, real gap.
    /// The 0.5-0.6 cluster is a real "large" group (e.g. a whole roofline or yard section) that still
    /// deliberately excludes other real estate (a Tree, an Arch) -- exactly the false-positive this
    /// threshold exists to avoid; the 0.9-1.0+ cluster is the genuine catch-all ("ALL_Display"/"Everything"
    /// -style groups that really do cover the whole layout). Moving this threshold down toward the overall
    /// P90 (55.7%) would merge those two clusters and misclassify the first group's worth of real "just a
    /// big prop" groups as whole-canvas. 0.90 sits safely inside the real empty 0.6-0.9 gap between the two
    /// clusters, so it is kept as the calibrated value, not lowered.
    /// </summary>
    public const double WholeCanvasCoverageThreshold = 0.90;

    private static readonly string[] AccentKeywords = ["minitree", "mini tree", "icicle", "strand", "star", "sparkle", "spike", "snowflake"];
    private static readonly string[] SubjectKeywords = ["megatree", "mega tree", "tree", "arch", "matrix"];
    private static readonly string[] FoundationKeywords = ["outline", "gutter", "roof", "horizontal", "eave", "ridge", "fence"];

    /// <summary>
    /// Classifies every real group (<see cref="LayoutSummary.Groups"/>) and individually-addressable
    /// pixel-heavy model (<see cref="LayoutSummary.TopPixelHeavyModels"/>) in <paramref name="layout"/>.
    /// Coverage is checked first, before any name-based keyword match (build-plan Phase 25 Part A):
    /// an entry whose own pixel count covers <see cref="WholeCanvasCoverageThreshold"/> or more of the
    /// layout's own <see cref="LayoutSummary.TotalPixels"/> is <see cref="BandRole.WholeCanvas"/>
    /// regardless of its name, since no fixed keyword list can cover every way a user might name a
    /// whole-layout catch-all group (e.g. "Everything", "All", "Master", "WholeHouse"). Everything under
    /// that threshold falls through to the unchanged name-based keyword match -- accent keywords checked
    /// first so e.g. "MiniTrees" classifies as an accent group, not a subject one, despite containing
    /// "tree". An entry matching neither signal is omitted, not misclassified.
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

            var role = ClassifyEntry(entry, layout.TotalPixels);
            if (role is { } r)
            {
                result.Add(new ClassifiedLayoutEntry(entry.Reference, r));
            }
        }

        return result;
    }

    private static BandRole? ClassifyEntry(LayoutSummaryEntry entry, long totalPixels)
    {
        if (totalPixels > 0 && entry.NodeCount / (double)totalPixels >= WholeCanvasCoverageThreshold)
        {
            return BandRole.WholeCanvas;
        }

        return ClassifyName(entry.Reference);
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

    /// <summary>
    /// Build-plan Phase 28a §4c: additive alongside <see cref="ComputeSectionProfiles"/> (which stays
    /// unchanged) -- splits one real section's own window into <paramref name="sampleCount"/> (2-4,
    /// default 3) equal real-tick sub-windows and reuses the exact same per-frame
    /// <see cref="BandEnergyEnvelope.AverageInWindow"/> data the single-label version already reads, so
    /// the prompt can show energy MOVEMENT within a section instead of one static dominant-band label.
    /// Returns an empty list (never invented samples) when <see cref="MusicModel.BandEnergy"/> is
    /// <see langword="null"/>, <paramref name="sectionId"/> does not match any real section, or its bar
    /// range falls outside the song's own beat grid.
    /// </summary>
    public static IReadOnlyList<SectionBandCurveSample> ComputeSectionCurve(MusicModel music, Guid sectionId, int sampleCount = 3)
    {
        ArgumentNullException.ThrowIfNull(music);
        if (sampleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCount), sampleCount, "Must be positive.");
        }

        var section = music.Sections.Labels.FirstOrDefault(s => s.Id == sectionId);
        if (section is null || music.BandEnergy is null)
        {
            return [];
        }

        var barGrid = BarGrid.FromBeatGrid(music.BeatGrid, music.SongDurationTicks);
        var window = barGrid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive));
        if (window is null)
        {
            return [];
        }

        var totalTicks = window.Value.EndTicks - window.Value.StartTicks;
        if (totalTicks <= 0)
        {
            return [];
        }

        var bandEnergy = music.BandEnergy;
        var samples = new List<SectionBandCurveSample>(sampleCount);
        for (var i = 0; i < sampleCount; i++)
        {
            var subStart = window.Value.StartTicks + totalTicks * i / sampleCount;
            var subEnd = window.Value.StartTicks + totalTicks * (i + 1) / sampleCount;
            var (bass, mid, high) = bandEnergy.AverageInWindow(subStart, subEnd);
            var total = bass + mid + high;
            string? dominant = total <= 0
                ? null
                : bass >= mid && bass >= high ? MusicalRole.Bass : mid >= high ? MusicalRole.Melody : MusicalRole.Highs;
            samples.Add(new SectionBandCurveSample(subStart, subEnd, dominant));
        }

        return samples;
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
