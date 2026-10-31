using System.Text.RegularExpressions;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>PHASE-2b (build plan §2b.8; architecture §8b): computes a <see cref="PatternLibrary"/> from a corpus of already-aligned song analyses.</summary>
public static partial class PatternLibraryBuilder
{
    private const long TicksPerSecond = 10_000_000L;

    /// <summary>
    /// The 7-effect timing-track table from architecture §6: a settings string containing any of these
    /// keys is reading a labeled timing track rather than emitting as a fixed-duration discrete
    /// effect — Mode A, architecture §8b's own "Mode A vs Mode B emission ratio."
    /// </summary>
    [GeneratedRegex("(_TimingTrack=|FIRETIMINGTRACK=|FireTiming)")]
    private static partial Regex ModeAKeyPattern();

    [GeneratedRegex(@"C_CHECKBOX_Palette\d+=1")]
    private static partial Regex ActivePaletteCheckboxPattern();

    [GeneratedRegex(@"T_CHOICE_LayerMethod=([^,]+)")]
    private static partial Regex BlendModePattern();

    public static PatternLibrary Build(IReadOnlyList<CorpusSongAnalysis> songs)
    {
        ArgumentNullException.ThrowIfNull(songs);

        var mineable = songs.Where(s => s.Entry.Provenance != CorpusProvenanceTag.ExternalReference).ToList();

        var effectFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        var effectFrequencyBySection = new Dictionary<SectionKind, Dictionary<string, int>>();
        var layerCountPerModel = new Dictionary<int, int>();
        var blendModeUsage = new Dictionary<string, int>(StringComparer.Ordinal);
        var paletteSlotDistribution = new Dictionary<int, int>();
        var sectionDensities = new Dictionary<SectionKind, List<double>>();
        var modeACount = 0;
        var modeBCount = 0;
        var totalEffects = 0;

        foreach (var song in mineable)
        {
            foreach (var aligned in song.AlignedEffects)
            {
                var effect = aligned.Effect;
                totalEffects++;

                Increment(effectFrequency, effect.EffectName);

                var sectionKind = aligned.SectionKind ?? SectionKind.Unknown;
                if (!effectFrequencyBySection.TryGetValue(sectionKind, out var bySection))
                {
                    bySection = new Dictionary<string, int>(StringComparer.Ordinal);
                    effectFrequencyBySection[sectionKind] = bySection;
                }

                Increment(bySection, effect.EffectName);

                if (IsModeA(effect.SettingsString))
                {
                    modeACount++;
                }
                else
                {
                    modeBCount++;
                }

                var paletteSlots = ActivePaletteCheckboxPattern().Count(effect.PaletteString);
                Increment(paletteSlotDistribution, paletteSlots);

                var blendMatch = BlendModePattern().Match(effect.SettingsString);
                if (blendMatch.Success)
                {
                    Increment(blendModeUsage, blendMatch.Groups[1].Value);
                }
            }

            foreach (var modelGroup in song.AlignedEffects.GroupBy(a => a.Effect.ModelName, StringComparer.Ordinal))
            {
                var layerCount = modelGroup.Select(a => a.Effect.LayerIndex).Distinct().Count();
                Increment(layerCountPerModel, layerCount);
            }

            AccumulateSectionDensities(song, sectionDensities);
        }

        return new PatternLibrary(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            SongCount: mineable.Count,
            TotalEffectCount: totalEffects,
            EffectFrequency: effectFrequency,
            EffectFrequencyBySection: effectFrequencyBySection.ToDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<string, int>)kv.Value),
            DensityBySection: sectionDensities.ToDictionary(kv => kv.Key, kv => kv.Value.Count == 0 ? 0 : kv.Value.Average()),
            LayerCountPerModelDistribution: layerCountPerModel,
            BlendModeUsage: blendModeUsage,
            PaletteActiveSlotDistribution: paletteSlotDistribution,
            ModeACount: modeACount,
            ModeBCount: modeBCount);
    }

    /// <summary>Architecture §6's own disclosed rule: a settings string naming a timing track it fires against. See <see cref="ModeAKeyPattern"/>.</summary>
    public static bool IsModeA(string settingsString) => ModeAKeyPattern().IsMatch(settingsString);

    private static void AccumulateSectionDensities(CorpusSongAnalysis song, Dictionary<SectionKind, List<double>> sectionDensities)
    {
        var bars = song.FingerprintSet.Bars.OrderBy(b => b.BarIndex).ToList();
        if (bars.Count == 0)
        {
            return;
        }

        foreach (var label in song.Labels.Labels)
        {
            var startBarEntry = bars.FirstOrDefault(b => b.BarIndex == label.StartBarIndex);
            if (startBarEntry is null)
            {
                continue;
            }

            var endBarEntry = bars.FirstOrDefault(b => b.BarIndex == label.EndBarIndexExclusive);
            var endTicks = endBarEntry?.Window.StartTicks ?? bars[^1].Window.EndTicks;
            var durationSeconds = (endTicks - startBarEntry.Window.StartTicks) / (double)TicksPerSecond;
            if (durationSeconds <= 0)
            {
                continue;
            }

            var effectsStarted = song.AlignedEffects.Count(a =>
                a.StartBarIndex >= label.StartBarIndex && a.StartBarIndex < label.EndBarIndexExclusive);

            var density = effectsStarted / durationSeconds;

            if (!sectionDensities.TryGetValue(label.Kind, out var list))
            {
                list = [];
                sectionDensities[label.Kind] = list;
            }

            list.Add(density);
        }
    }

    private static void Increment<TKey>(Dictionary<TKey, int> dictionary, TKey key) where TKey : notnull
    {
        dictionary[key] = dictionary.GetValueOrDefault(key) + 1;
    }
}
