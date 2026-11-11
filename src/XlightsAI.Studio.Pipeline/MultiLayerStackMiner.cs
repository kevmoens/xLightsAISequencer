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

    public static MultiLayerTemplateCatalog Build(IReadOnlyList<CorpusSongAnalysis> songs, int minSongCount = DefaultMinSongCount)
    {
        ArgumentNullException.ThrowIfNull(songs);

        var mineable = songs.Where(s => s.Entry.Provenance != CorpusProvenanceTag.ExternalReference).ToList();
        var aggregatesBySignature = new Dictionary<string, StackAggregate>(StringComparer.Ordinal);

        foreach (var song in mineable)
        {
            var beatMs = song.BeatGrid.SelectedTempo is { Bpm: > 0 } tempo ? 60_000.0 / tempo.Bpm : (double?)null;

            foreach (var modelGroup in song.AlignedEffects
                .Where(a => TreeMatrixPropNameConvention.IsTreeOrMatrixName(a.Effect.ModelName))
                .GroupBy(a => a.Effect.ModelName, StringComparer.Ordinal))
            {
                foreach (var occurrence in FindStackOccurrences(modelGroup.ToList(), beatMs))
                {
                    var signature = occurrence.Tier + "|" + string.Join(">", occurrence.Layers.Select(l => l.EffectName));
                    if (!aggregatesBySignature.TryGetValue(signature, out var aggregate))
                    {
                        aggregate = new StackAggregate(occurrence.Tier, occurrence.Layers.Select(l => l.EffectName).ToList());
                        aggregatesBySignature[signature] = aggregate;
                    }

                    aggregate.SongTitles.Add(song.Entry.SongTitle);
                    aggregate.CadenceVotes.Add(occurrence.Cadence);
                    for (var i = 0; i < occurrence.Layers.Count; i++)
                    {
                        aggregate.DutyByPosition[i].Add(occurrence.Layers[i].DutyCycle);
                    }
                }
            }
        }

        var promoted = aggregatesBySignature.Values
            .Where(a => a.SongTitles.Count >= minSongCount)
            .OrderByDescending(a => a.SongTitles.Count)
            .ThenBy(a => a.Tier, StringComparer.Ordinal)
            .ThenBy(a => string.Join(">", a.EffectNames), StringComparer.Ordinal)
            .Select((a, index) => BuildTemplate(a, index, minSongCount))
            .ToList();

        return new MultiLayerTemplateCatalog(Version: 1, GeneratedAtUtc: DateTimeOffset.UtcNow, Templates: promoted);
    }

    /// <summary>
    /// Build-plan Phase 20 §2a: groups one model's own aligned effects into maximal time-connected runs
    /// (adjacent/overlapping placements merged, same interval-merge technique <see cref="PatternLibraryBuilder"/>'s
    /// section-density accumulation already trusts for a different purpose), keeps only the runs whose
    /// real peak concurrency (<see cref="CraftRuleInducer.MaxConcurrentOverlap"/>) reaches 2+ distinct
    /// <see cref="ParsedEffectInstance.LayerIndex"/> values, then extracts the real effect-name stack
    /// (z-ordered by <see cref="ParsedEffectInstance.LayerIndex"/>) active at that run's own single busiest
    /// instant (<see cref="CraftRuleInducer.FindPeakOverlapSegment{T}"/>) as one real occurrence.
    /// </summary>
    private static IEnumerable<StackOccurrence> FindStackOccurrences(IReadOnlyList<AlignedEffectInstance> modelEffects, double? beatMs)
    {
        var ordered = modelEffects.OrderBy(a => a.Effect.StartTimeMs).ToList();
        var clusters = new List<List<AlignedEffectInstance>>();
        List<AlignedEffectInstance>? current = null;
        var currentEnd = long.MinValue;

        foreach (var instance in ordered)
        {
            if (current is not null && instance.Effect.StartTimeMs < currentEnd)
            {
                current.Add(instance);
                currentEnd = Math.Max(currentEnd, instance.Effect.EndTimeMs);
                continue;
            }

            current = [instance];
            currentEnd = instance.Effect.EndTimeMs;
            clusters.Add(current);
        }

        foreach (var cluster in clusters)
        {
            var distinctLayers = cluster.Select(a => a.Effect.LayerIndex).Distinct().Count();
            if (distinctLayers < 2)
            {
                continue;
            }

            var maxOverlap = CraftRuleInducer.MaxConcurrentOverlap(cluster.Select(a => (a.Effect.StartTimeMs, a.Effect.EndTimeMs)));
            if (maxOverlap < 2)
            {
                continue;
            }

            var (segmentStart, segmentEnd, activeAtPeak) = CraftRuleInducer.FindPeakOverlapSegment(
                cluster, a => a.Effect.StartTimeMs, a => a.Effect.EndTimeMs);
            if (activeAtPeak.Count < 2)
            {
                continue;
            }

            var clusterStart = cluster.Min(a => a.Effect.StartTimeMs);
            var clusterEnd = cluster.Max(a => a.Effect.EndTimeMs);
            var clusterSpan = (double)(clusterEnd - clusterStart);
            if (clusterSpan <= 0)
            {
                continue;
            }

            // One real stack occurrence per distinct LayerIndex active at the peak instant (never the same
            // layer twice even if that layer itself has several placements inside this cluster).
            var stackLayers = activeAtPeak
                .GroupBy(a => a.Effect.LayerIndex)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var representative = g.OrderBy(a => a.Effect.StartTimeMs).First();
                    var presentMs = cluster.Where(a => a.Effect.LayerIndex == g.Key)
                        .Sum(a => Math.Max(0, Math.Min(a.Effect.EndTimeMs, clusterEnd) - Math.Max(a.Effect.StartTimeMs, clusterStart)));
                    var duty = Math.Clamp(presentMs / clusterSpan, 0.0, 1.0);
                    return (representative.Effect.EffectName, DutyCycle: duty);
                })
                .ToList();

            var tier = ClassifyTier(cluster
                .Select(a => a.SectionKind)
                .Where(k => k is not null)
                .GroupBy(k => k!.Value)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault());

            var segmentDurationMs = (double)(segmentEnd - segmentStart);
            var cadence = ClassifyCadence(segmentDurationMs, beatMs, cluster[0].PhraseBars);

            yield return new StackOccurrence(tier, cadence, stackLayers.Select(l => (l.EffectName, l.DutyCycle)).ToList());
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

    private static MultiLayerTemplate BuildTemplate(StackAggregate aggregate, int index, int minSongCount)
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
            return new MultiLayerTemplateLayer(
                Role: role,
                Effect: effectName,
                DutyMin: dutyMin,
                DutyMax: dutyMax,
                Cadence: cadence,
                Notes: $"Auto-mined layer role -- real measured duty cycle across {duties.Count} occurrence(s) in this stack's own promoted songs.");
        }).ToList();

        var exampleSongs = aggregate.SongTitles.OrderBy(s => s, StringComparer.Ordinal).Take(3).ToList();
        var slug = string.Join("-", aggregate.EffectNames.Select(n => n.ToLowerInvariant().Replace(" ", "-")));

        return new MultiLayerTemplate(
            Id: $"mined-{aggregate.Tier}-{slug}-{index}",
            Name: $"Mined {aggregate.Tier}-energy {string.Join(" + ", aggregate.EffectNames)}",
            EnergyBand: aggregate.Tier,
            Props: ["Tree", "Matrixes", "Mega Tree"],
            Motion: motion,
            Layers: layers,
            SourceNote: $"Build-plan Phase 20 §2a/§2b: auto-mined from the real sequence corpus -- this exact " +
                $"{aggregate.EffectNames.Count}-layer stack ({string.Join(" + ", aggregate.EffectNames)}) was observed " +
                $"genuinely time-overlapping on a real Tree/Matrix-class prop in {aggregate.SongTitles.Count} distinct real " +
                $"songs (>= the {minSongCount}-song 'never forced' promotion threshold Phase 19 established), e.g. " +
                $"{string.Join(", ", exampleSongs)}. Palette-free and song-free -- only effect names, measured duty-cycle " +
                "ranges, and a cadence bucket are retained, no literal color or exact timing copied.");
    }

    private sealed record StackOccurrence(string Tier, string Cadence, IReadOnlyList<(string EffectName, double DutyCycle)> Layers);

    private sealed class StackAggregate(string tier, List<string> effectNames)
    {
        public string Tier { get; } = tier;
        public List<string> EffectNames { get; } = effectNames;
        public HashSet<string> SongTitles { get; } = new(StringComparer.Ordinal);
        public List<string> CadenceVotes { get; } = [];
        public Dictionary<int, List<double>> DutyByPosition { get; } = Enumerable.Range(0, effectNames.Count).ToDictionary(i => i, _ => new List<double>());
    }
}
