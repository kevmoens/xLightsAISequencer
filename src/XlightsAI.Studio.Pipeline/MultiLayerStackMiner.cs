using System.Text.RegularExpressions;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 20 §2a/§2b: auto-extracts real, repeated multi-layer effect stacks from the corpus
/// (never hand-curated guesses) and promotes the ones seen in enough distinct real songs into a shippable,
/// additive <see cref="MultiLayerTemplateCatalog"/> -- the same "promote or honestly reject" discipline
/// <see cref="CraftRuleInducer"/> already established for craft rules, applied here to real multi-layer
/// stacks instead. Output is additive to (never a replacement for) the 3 hand-curated seed templates
/// embedded in <c>XlightsAI.Studio.Knowledge</c> -- <see cref="MultiLayerTemplateCatalogLoader"/> merges
/// the two at load time.
/// </summary>
public static class MultiLayerStackMiner
{
    /// <summary>Same default as <see cref="CraftRuleInducer.InduceAndVerify"/>'s own <c>minSongCount</c> -- a stack must be seen in at least this many distinct real songs before it ships, never forced into the catalog on the strength of one or two songs.</summary>
    public const int DefaultMinSongCount = 5;

    private static readonly HashSet<SectionKind> HighEnergyKinds = [SectionKind.Chorus, SectionKind.PreChorus, SectionKind.Solo];
    private static readonly HashSet<SectionKind> LowEnergyKinds = [SectionKind.Intro, SectionKind.Outro, SectionKind.Bridge];

    private static readonly Regex BlendModeSettingPattern = new(@"T_CHOICE_LayerMethod=([^,]+)", RegexOptions.Compiled);

    /// <summary>
    /// Build-plan Phase 30 step 30.1: maps a real, raw <c>T_CHOICE_LayerMethod</c> XML value (as actually
    /// written by xLights in a real corpus file -- see <c>implementation-evidence/PHASE-30-blend-mode-baseline.md</c>'s
    /// own occurrence tally) to its <see cref="ConfirmedLayerBlendMode.CandidateName"/>, for every real
    /// spelling 30.0's corpus grep found (both the "1 is ..."/"2 is ..." directional spellings for
    /// Mask/Unmask alike -- <see cref="ConfirmedLayerBlendModeCatalog"/> only ever EMITS the single
    /// canonical "2 is ..." spelling per candidate, but mining must recognize both real spellings as
    /// equally real evidence of that same candidate, per the catalog's own doc comment). A raw value with
    /// no entry here (a real, confirmed-to-exist-in-the-corpus value that is nonetheless not one of this
    /// phase's 8 build-plan candidates -- <c>"Layered"</c>, <c>"Effect 1"</c>/<c>"Effect 2"</c>,
    /// <c>"Brightness"</c>, <c>"Additive"</c>, <c>"Bottom-Top"</c>, <c>"Min"</c>, <c>"Left-Right"</c>, the
    /// <c>"True Unmask"</c> variants, or the implicit default <c>"Normal"</c>) maps to <see langword="null"/>
    /// -- real signal, but out of this phase's candidate scope, never guessed into one.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RawBlendModeToCandidateName = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["2 is Mask"] = "Mask",
        ["1 is Mask"] = "Mask",
        ["2 is Unmask"] = "Unmask",
        ["1 is Unmask"] = "Unmask",
        ["1 reveals 2"] = "Reveal 1-on-2",
        ["2 reveals 1"] = "Reveal 2-on-1",
        ["Average"] = "Average",
        ["Subtractive"] = "Subtractive",
        ["Shadow 1 on 2"] = "Shadow 1-on-2",
        ["Shadow 2 on 1"] = "Shadow 2-on-1",
    };

    /// <summary>Returns the confirmed <see cref="ConfirmedLayerBlendMode.CandidateName"/> this real effect's own settings string carries evidence of (between it and whatever sits immediately below it in z-order), or <see langword="null"/> when the settings string has no <c>T_CHOICE_LayerMethod</c> key at all, or carries one this phase does not recognize as a candidate.</summary>
    public static string? TryMapRawBlendModeToCandidate(string settingsString)
    {
        ArgumentNullException.ThrowIfNull(settingsString);
        var match = BlendModeSettingPattern.Match(settingsString);
        return match.Success && RawBlendModeToCandidateName.TryGetValue(match.Groups[1].Value, out var candidate) ? candidate : null;
    }

    /// <summary>
    /// Build-plan Phase 25 Part D.1: <paramref name="minDistinctLayers"/> defaults to
    /// <see cref="LayerComplexityAnalyzer.DefaultMinDistinctLayers"/> (3, raised from the original 2+), and
    /// <paramref name="restrictToTreeMatrixModels"/> defaults to <see langword="false"/> -- "any model/group
    /// with genuine layering is discoverable," not just Tree/Matrix-class props. Pass
    /// <paramref name="restrictToTreeMatrixModels"/>: <see langword="true"/> to recover the original Phase 20
    /// scope.
    /// </summary>
    public static MultiLayerTemplateCatalog Build(
        IReadOnlyList<CorpusSongAnalysis> songs,
        int minSongCount = DefaultMinSongCount,
        int minDistinctLayers = LayerComplexityAnalyzer.DefaultMinDistinctLayers,
        bool restrictToTreeMatrixModels = false)
    {
        var promoted = BuildAggregates(songs, minDistinctLayers, restrictToTreeMatrixModels)
            .Where(a => a.SongTitles.Count >= minSongCount)
            .OrderByDescending(a => a.SongTitles.Count)
            .ThenBy(a => a.Tier, StringComparer.Ordinal)
            .ThenBy(a => string.Join(">", a.EffectNames), StringComparer.Ordinal)
            .Select((a, index) => BuildTemplate(a, index, minSongCount))
            .ToList();

        return new MultiLayerTemplateCatalog(Version: 1, GeneratedAtUtc: DateTimeOffset.UtcNow, Templates: promoted);
    }

    /// <summary>
    /// Build-plan Phase 27 (2026-11-14): a real, genuine 3+-layer stack observed in exactly one real
    /// song today has no path into the shippable catalog at all -- <see cref="Build"/> only ever ships
    /// what clears <paramref name="minSongCount"/>. This surfaces every candidate that falls BELOW that
    /// bar (never one that already promoted via <see cref="Build"/> -- no double-listing), tagged
    /// <c>ConfidenceTier: "single-song-showcase"</c> so the Director prompt (and any human reader) can
    /// tell "real shows reliably do this" apart from "one real show did this once, it's good
    /// inspiration." Never replaces <see cref="Build"/>'s own output -- always additive.
    /// </summary>
    public static MultiLayerTemplateCatalog BuildShowcase(
        IReadOnlyList<CorpusSongAnalysis> songs,
        int minSongCount = DefaultMinSongCount,
        int minDistinctLayers = LayerComplexityAnalyzer.DefaultMinDistinctLayers,
        bool restrictToTreeMatrixModels = false)
    {
        var showcased = BuildAggregates(songs, minDistinctLayers, restrictToTreeMatrixModels)
            .Where(a => a.SongTitles.Count < minSongCount)
            .OrderByDescending(a => a.SongTitles.Count)
            .ThenBy(a => a.Tier, StringComparer.Ordinal)
            .ThenBy(a => string.Join(">", a.EffectNames), StringComparer.Ordinal)
            .Select((a, index) => BuildTemplate(a, index, minSongCount, idPrefix: "showcase", confidenceTier: "single-song-showcase"))
            .ToList();

        return new MultiLayerTemplateCatalog(Version: 1, GeneratedAtUtc: DateTimeOffset.UtcNow, Templates: showcased);
    }

    /// <summary>
    /// User feedback (2026-11-12): <see cref="Build"/> only ever surfaces the stacks that actually
    /// cleared <paramref name="minSongCount"/> -- a near-miss stack seen in, say, 4 of a required 5
    /// distinct songs is silently discarded with no visibility at all, the same blind spot
    /// <c>CraftRuleInducer.InduceAndVerify</c>'s own promoted-vs-rejected split already avoids for craft
    /// rules. This is every real candidate stack this run found, promoted or not, for a one-off tool
    /// (<c>tools/CorpusMiner</c>) to log in full -- never used by the production <see cref="Build"/> path
    /// itself, which still only ships what actually promotes.
    /// </summary>
    public static IReadOnlyList<MultiLayerStackCandidate> BuildCandidates(
        IReadOnlyList<CorpusSongAnalysis> songs,
        int minSongCount = DefaultMinSongCount,
        int minDistinctLayers = LayerComplexityAnalyzer.DefaultMinDistinctLayers,
        bool restrictToTreeMatrixModels = false) =>
        BuildAggregates(songs, minDistinctLayers, restrictToTreeMatrixModels)
            .OrderByDescending(a => a.SongTitles.Count)
            .ThenBy(a => a.Tier, StringComparer.Ordinal)
            .ThenBy(a => string.Join(">", a.EffectNames), StringComparer.Ordinal)
            .Select(a => new MultiLayerStackCandidate(
                a.Tier, a.EffectNames, a.SongTitles.Count, a.SongTitles.Count >= minSongCount,
                a.SongTitles.OrderBy(s => s, StringComparer.Ordinal).Take(5).ToList()))
            .ToList();

    private static IReadOnlyList<StackAggregate> BuildAggregates(
        IReadOnlyList<CorpusSongAnalysis> songs, int minDistinctLayers, bool restrictToTreeMatrixModels)
    {
        ArgumentNullException.ThrowIfNull(songs);

        var mineable = songs.Where(s => s.Entry.Provenance != CorpusProvenanceTag.ExternalReference).ToList();
        var aggregatesBySignature = new Dictionary<string, StackAggregate>(StringComparer.Ordinal);

        foreach (var song in mineable)
        {
            var beatMs = song.BeatGrid.SelectedTempo is { Bpm: > 0 } tempo ? 60_000.0 / tempo.Bpm : (double?)null;

            foreach (var modelGroup in song.AlignedEffects
                .Where(a => !restrictToTreeMatrixModels || TreeMatrixPropNameConvention.IsTreeOrMatrixName(a.Effect.ModelName))
                .GroupBy(a => a.Effect.ModelName, StringComparer.Ordinal))
            {
                var isTreeOrMatrix = TreeMatrixPropNameConvention.IsTreeOrMatrixName(modelGroup.Key);
                foreach (var occurrence in FindStackOccurrences(modelGroup.ToList(), beatMs, minDistinctLayers))
                {
                    var signature = occurrence.Tier + "|" + string.Join(">", occurrence.Layers.Select(l => l.EffectName));
                    if (!aggregatesBySignature.TryGetValue(signature, out var aggregate))
                    {
                        aggregate = new StackAggregate(occurrence.Tier, occurrence.Layers.Select(l => l.EffectName).ToList());
                        aggregatesBySignature[signature] = aggregate;
                    }

                    aggregate.SongTitles.Add(song.Entry.SongTitle);
                    aggregate.CadenceVotes.Add(occurrence.Cadence);
                    aggregate.IsTreeOrMatrixVotes.Add(isTreeOrMatrix);
                    for (var i = 0; i < occurrence.Layers.Count; i++)
                    {
                        aggregate.DutyByPosition[i].Add(occurrence.Layers[i].DutyCycle);
                        aggregate.BlendModeVotesByPosition[i].Add(occurrence.Layers[i].BlendModeToPrevious);
                    }
                }
            }
        }

        return aggregatesBySignature.Values.ToList();
    }

    /// <summary>
    /// Build-plan Phase 20 §2a / Phase 25 Part D.1: delegates clustering and peak-instant extraction to
    /// the shared <see cref="LayerComplexityAnalyzer.FindLayerWindows"/> sweep (keeps only runs whose real
    /// peak distinct-layer count reaches <paramref name="minDistinctLayers"/>), then extracts the real
    /// effect-name stack (z-ordered by <see cref="ParsedEffectInstance.LayerIndex"/>) active at that run's
    /// own single busiest instant as one real occurrence.
    /// </summary>
    private static IEnumerable<StackOccurrence> FindStackOccurrences(IReadOnlyList<AlignedEffectInstance> modelEffects, double? beatMs, int minDistinctLayers)
    {
        foreach (var window in LayerComplexityAnalyzer.FindLayerWindows(modelEffects, minDistinctLayers))
        {
            var clusterSpan = (double)(window.ClusterEnd - window.ClusterStart);
            if (clusterSpan <= 0)
            {
                continue;
            }

            // One real stack occurrence per distinct LayerIndex active at the peak instant (never the same
            // layer twice even if that layer itself has several placements inside this cluster).
            var stackLayers = window.ActiveAtPeak
                .GroupBy(a => a.Effect.LayerIndex)
                .OrderBy(g => g.Key)
                .Select((g, position) =>
                {
                    var representative = g.OrderBy(a => a.Effect.StartTimeMs).First();
                    var presentMs = window.ClusterEffects.Where(a => a.Effect.LayerIndex == g.Key)
                        .Sum(a => Math.Max(0, Math.Min(a.Effect.EndTimeMs, window.ClusterEnd) - Math.Max(a.Effect.StartTimeMs, window.ClusterStart)));
                    var duty = Math.Clamp(presentMs / clusterSpan, 0.0, 1.0);
                    // Build-plan Phase 30 step 30.1: the first/base position (0) has nothing below it in
                    // this stack's own declared z-order to carry a real "blend against the previous layer"
                    // reading for -- only position 1+ ever gets a real mined candidate here, same
                    // "nothing invented" discipline as every duty-cycle/cadence field beside it.
                    var blendModeToPrevious = position == 0 ? null : TryMapRawBlendModeToCandidate(representative.Effect.SettingsString);
                    return (representative.Effect.EffectName, DutyCycle: duty, BlendModeToPrevious: blendModeToPrevious);
                })
                .ToList();

            var tier = ClassifyTier(window.DominantSectionKind);
            var segmentDurationMs = (double)(window.PeakSegmentEnd - window.PeakSegmentStart);
            var cadence = ClassifyCadence(segmentDurationMs, beatMs, window.ClusterEffects[0].PhraseBars);

            yield return new StackOccurrence(tier, cadence, stackLayers.Select(l => (l.EffectName, l.DutyCycle, l.BlendModeToPrevious)).ToList());
        }
    }

    private static string ClassifyTier(SectionKind? dominantKind)
    {
        if (dominantKind is { } kind)
        {
            if (HighEnergyKinds.Contains(kind))
            {
                return "high";
            }

            if (LowEnergyKinds.Contains(kind))
            {
                return "low";
            }
        }

        return "mid";
    }

    private static string ClassifyCadence(double segmentDurationMs, double? beatMs, int? phraseBars)
    {
        if (beatMs is not { } beat || beat <= 0)
        {
            return "phrase";
        }

        if (segmentDurationMs <= beat * 2)
        {
            return "beat";
        }

        var assumedBeatsPerBar = 4;
        var phraseMs = beat * assumedBeatsPerBar * (phraseBars ?? 8);
        return segmentDurationMs <= phraseMs ? "phrase" : "section";
    }

    /// <summary>
    /// Build-plan Phase 30 step 30.1: the plurality vote across every real occurrence's own reading at
    /// this layer position -- same "most common wins" discipline <see cref="BuildTemplate"/> already uses
    /// for <see cref="ClassifyCadence"/>'s own votes. <see langword="null"/> ("no mined evidence")
    /// competes as its own candidate, so a stack whose real corpus occurrences mostly left the setting at
    /// its implicit default correctly yields <see langword="null"/> rather than whatever confirmed mode
    /// happened to show up in a minority of occurrences.
    /// </summary>
    private static string? MajorityBlendModeVote(IReadOnlyList<string?> votes)
    {
        if (votes.Count == 0)
        {
            return null;
        }

        return votes
            .GroupBy(v => v, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .First().Key;
    }

    private static MultiLayerTemplate BuildTemplate(
        StackAggregate aggregate, int index, int minSongCount, string idPrefix = "mined", string confidenceTier = "corpus-validated")
    {
        var cadence = aggregate.CadenceVotes
            .GroupBy(c => c, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .First().Key;

        var motion = cadence switch
        {
            "beat" => "chase",
            "section" => "hold",
            _ => "build",
        };

        var layers = aggregate.EffectNames.Select((effectName, i) =>
        {
            var duties = aggregate.DutyByPosition[i];
            var dutyMin = duties.Count > 0 ? Math.Round(duties.Min(), 2) : 0.1;
            var dutyMax = duties.Count > 0 ? Math.Round(duties.Max(), 2) : 0.9;
            if (dutyMax <= dutyMin)
            {
                dutyMax = Math.Min(1.0, dutyMin + 0.05);
            }

            var role = i == 0 ? "subject" : i == aggregate.EffectNames.Count - 1 ? "secondary" : "support";
            var blendModeToPrevious = MajorityBlendModeVote(aggregate.BlendModeVotesByPosition[i]);
            return new MultiLayerTemplateLayer(
                Role: role,
                Effect: effectName,
                DutyMin: dutyMin,
                DutyMax: dutyMax,
                Cadence: cadence,
                Notes: $"Auto-mined layer role -- real measured duty cycle across {duties.Count} occurrence(s) in this stack's own promoted songs.",
                BlendModeToPrevious: blendModeToPrevious);
        }).ToList();

        var exampleSongs = aggregate.SongTitles.OrderBy(s => s, StringComparer.Ordinal).Take(3).ToList();
        var slug = string.Join("-", aggregate.EffectNames.Select(n => n.ToLowerInvariant().Replace(" ", "-")));

        // Build-plan Phase 25 Part D.1: now that the tree/matrix-only restriction is optional, a majority
        // of this signature's own real occurrences may come from a non-Tree/Matrix model -- claiming
        // "Tree/Matrixes/Mega Tree" regardless would misdescribe where this stack was actually observed.
        var isTreeOrMatrix = aggregate.IsTreeOrMatrixVotes.Count(v => v) * 2 >= aggregate.IsTreeOrMatrixVotes.Count;
        var props = isTreeOrMatrix ? new[] { "Tree", "Matrixes", "Mega Tree" } : new[] { "Any" };

        var sourceNote = confidenceTier == "single-song-showcase"
            ? $"Build-plan Phase 27 (2026-11-14): auto-mined from the real sequence corpus -- this exact " +
                $"{aggregate.EffectNames.Count}-layer stack ({string.Join(" + ", aggregate.EffectNames)}) was observed " +
                $"genuinely time-overlapping on a real prop in only {aggregate.SongTitles.Count} distinct real song(s) " +
                $"(below the {minSongCount}-song 'never forced' promotion threshold Phase 19 established) -- e.g. " +
                $"{string.Join(", ", exampleSongs)}. Not corpus-validated: treat as a real, inspirational example of " +
                "what one real show did, not a reliably-repeated technique. Palette-free and song-free -- only effect " +
                "names, measured duty-cycle ranges, and a cadence bucket are retained, no literal color or exact timing copied."
            : $"Build-plan Phase 20 §2a/§2b: auto-mined from the real sequence corpus -- this exact " +
                $"{aggregate.EffectNames.Count}-layer stack ({string.Join(" + ", aggregate.EffectNames)}) was observed " +
                $"genuinely time-overlapping on a real Tree/Matrix-class prop in {aggregate.SongTitles.Count} distinct real " +
                $"songs (>= the {minSongCount}-song 'never forced' promotion threshold Phase 19 established), e.g. " +
                $"{string.Join(", ", exampleSongs)}. Palette-free and song-free -- only effect names, measured duty-cycle " +
                "ranges, and a cadence bucket are retained, no literal color or exact timing copied.";

        return new MultiLayerTemplate(
            Id: $"{idPrefix}-{aggregate.Tier}-{slug}-{index}",
            Name: $"Mined {aggregate.Tier}-energy {string.Join(" + ", aggregate.EffectNames)}",
            EnergyBand: aggregate.Tier,
            Props: props,
            Motion: motion,
            Layers: layers,
            SourceNote: sourceNote,
            ConfidenceTier: confidenceTier);
    }

    /// <summary>One real candidate multi-layer stack signature and how many distinct real songs it was seen in, whether or not it actually promoted -- see <see cref="BuildCandidates"/>.</summary>
    public sealed record MultiLayerStackCandidate(string Tier, IReadOnlyList<string> EffectNames, int SongCount, bool Promoted, IReadOnlyList<string> ExampleSongs);

    private sealed record StackOccurrence(string Tier, string Cadence, IReadOnlyList<(string EffectName, double DutyCycle, string? BlendModeToPrevious)> Layers);

    private sealed class StackAggregate(string tier, List<string> effectNames)
    {
        public string Tier { get; } = tier;
        public List<string> EffectNames { get; } = effectNames;
        public HashSet<string> SongTitles { get; } = new(StringComparer.Ordinal);
        public List<string> CadenceVotes { get; } = [];
        public List<bool> IsTreeOrMatrixVotes { get; } = [];
        public Dictionary<int, List<double>> DutyByPosition { get; } = Enumerable.Range(0, effectNames.Count).ToDictionary(i => i, _ => new List<double>());

        /// <summary>Build-plan Phase 30 step 30.1: one vote (a confirmed candidate name, or <see langword="null"/> for "no mined blend-mode evidence this occurrence") per position, per real occurrence -- <see cref="BuildTemplate"/> takes the plurality vote, same discipline as <see cref="CadenceVotes"/>.</summary>
        public Dictionary<int, List<string?>> BlendModeVotesByPosition { get; } = Enumerable.Range(0, effectNames.Count).ToDictionary(i => i, _ => new List<string?>());
    }
}
