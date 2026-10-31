using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.10-11; architecture §8b): the Hypotheses-to-Theories induction+verification
/// loop. <see cref="TwoTwoFourPattern"/> is architecture §8b's own already-concrete rule, encoded
/// directly (no induction needed). <see cref="InduceAndVerify"/> additionally proposes a small number of
/// candidate rules from real corpus statistics computed here — not assumed — and mechanically checks
/// each one against every mineable song (the same <see cref="CorpusProvenanceTag.ExternalReference"/>
/// exclusion as <see cref="PatternLibraryBuilder"/>), promoting only the ones clearing
/// <paramref name="minConfidence"/>/<paramref name="minSongCount"/>.
/// </summary>
public static class CraftRuleInducer
{
    /// <summary>
    /// One candidate rule's checkable predicate: given one song's analysis, return every applicable
    /// instance's truth value (e.g. one bool per 8-bar phrase, or a single bool for the whole song) — an
    /// empty list means the rule had nothing applicable to check in this song (e.g. no 8-bar phrases, or
    /// no Mode A effects), which is excluded from both the denominator and the song-count tally, never
    /// counted as a false.
    /// </summary>
    private delegate IReadOnlyList<bool> Predicate(CorpusSongAnalysis song);

    public static CraftRules InduceAndVerify(
        IReadOnlyList<CorpusSongAnalysis> songs, double minConfidence = 0.70, int minSongCount = 5)
    {
        ArgumentNullException.ThrowIfNull(songs);

        var mineable = songs.Where(s => s.Entry.Provenance != CorpusProvenanceTag.ExternalReference).ToList();

        var candidates = new (string Id, string Statement, Predicate Predicate)[]
        {
            (
                "two-two-four-phrase-pattern",
                "Within an 8-bar phrase, a model is dominated by one effect family (>=70% of its own " +
                "instances in that phrase share one effect name) AND shows some detectable " +
                "direction/reverse-like parameter change between its first and second half (bars " +
                "[0,4) vs [4,8) of the phrase) -- the community '2/2/4' pattern (architecture §8b): one " +
                "effect family per 8-bar phrase, alternating direction/reverse roughly every 2 bars.",
                EvaluateTwoTwoFour),
            (
                "palette-rarely-exceeds-three-active-slots",
                "An effect instance rarely has more than 3 active palette checkbox slots ticked.",
                EvaluatePaletteSlotLimit),
            (
                "mode-a-effects-run-longer-than-mode-b",
                "Within a song, Mode A (timing-track-bound) effects have a longer mean duration than " +
                "Mode B (discrete, per-event) effects -- consistent with Mode A being used for " +
                "phrase/section-spanning washes and Mode B for short hits.",
                EvaluateModeADuration),
            (
                "blend-mode-left-at-default",
                "An effect instance's settings string omits an explicit T_CHOICE_LayerMethod (xLights' " +
                "default-omission serialization rule means the effect uses the default blend/layer " +
                "method rather than an explicitly chosen one).",
                EvaluateBlendModeDefault),
        };

        var evaluated = candidates
            .Select(c => Evaluate(c.Id, c.Statement, c.Predicate, mineable))
            .ToList();

        var promoted = evaluated.Where(r => r.Confidence >= minConfidence && r.SongCount >= minSongCount).ToList();
        var rejected = evaluated.Except(promoted).ToList();

        return new CraftRules(DateTimeOffset.UtcNow, minConfidence, minSongCount, promoted, rejected);
    }

    private static CraftRule Evaluate(string id, string statement, Predicate predicate, IReadOnlyList<CorpusSongAnalysis> songs)
    {
        var applicable = 0;
        var trueCount = 0;
        var songCount = 0;

        foreach (var song in songs)
        {
            var results = predicate(song);
            if (results.Count == 0)
            {
                continue;
            }

            songCount++;
            applicable += results.Count;
            trueCount += results.Count(r => r);
        }

        return new CraftRule(id, statement, applicable, trueCount, songCount, HasCodePredicate: true);
    }

    private static IReadOnlyList<bool> EvaluateTwoTwoFour(CorpusSongAnalysis song)
    {
        var results = new List<bool>();

        foreach (var phrase in song.Phrases.Phrases.Where(p => p.Bars == 8))
        {
            foreach (var modelGroup in song.AlignedEffects
                .Where(a => a.StartBarIndex >= phrase.StartBarIndex && a.StartBarIndex < phrase.EndBarIndexExclusive)
                .GroupBy(a => a.Effect.ModelName, StringComparer.Ordinal))
            {
                var instances = modelGroup.ToList();
                if (instances.Count == 0)
                {
                    continue;
                }

                var dominantFamily = instances
                    .GroupBy(a => a.Effect.EffectName, StringComparer.Ordinal)
                    .OrderByDescending(g => g.Count())
                    .First();
                var oneFamilyDominates = dominantFamily.Count() / (double)instances.Count >= 0.70;

                var midpoint = phrase.StartBarIndex + 4;
                var firstHalfDirections = DirectionTokens(instances.Where(a => a.StartBarIndex < midpoint));
                var secondHalfDirections = DirectionTokens(instances.Where(a => a.StartBarIndex >= midpoint));
                var directionChanges = firstHalfDirections.Count > 0 && secondHalfDirections.Count > 0
                    && !firstHalfDirections.SetEquals(secondHalfDirections);

                results.Add(oneFamilyDominates && directionChanges);
            }
        }

        return results;
    }

    private static HashSet<string> DirectionTokens(IEnumerable<AlignedEffectInstance> instances)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var instance in instances)
        {
            foreach (var pair in instance.Effect.SettingsString.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (pair.Contains("Direction", StringComparison.OrdinalIgnoreCase) ||
                    pair.Contains("Reverse", StringComparison.OrdinalIgnoreCase))
                {
                    tokens.Add(pair);
                }
            }
        }

        return tokens;
    }

    private static IReadOnlyList<bool> EvaluatePaletteSlotLimit(CorpusSongAnalysis song)
    {
        var results = new List<bool>();
        foreach (var aligned in song.AlignedEffects)
        {
            var activeSlots = aligned.Effect.PaletteString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Count(token => token.StartsWith("C_CHECKBOX_Palette", StringComparison.Ordinal) && token.EndsWith("=1", StringComparison.Ordinal));
            results.Add(activeSlots <= 3);
        }

        return results;
    }

    private static IReadOnlyList<bool> EvaluateModeADuration(CorpusSongAnalysis song)
    {
        var modeADurations = song.AlignedEffects
            .Where(a => PatternLibraryBuilder.IsModeA(a.Effect.SettingsString))
            .Select(a => (double)(a.Effect.EndTimeMs - a.Effect.StartTimeMs))
            .ToList();
        var modeBDurations = song.AlignedEffects
            .Where(a => !PatternLibraryBuilder.IsModeA(a.Effect.SettingsString))
            .Select(a => (double)(a.Effect.EndTimeMs - a.Effect.StartTimeMs))
            .ToList();

        if (modeADurations.Count == 0 || modeBDurations.Count == 0)
        {
            return [];
        }

        return [modeADurations.Average() > modeBDurations.Average()];
    }

    private static IReadOnlyList<bool> EvaluateBlendModeDefault(CorpusSongAnalysis song) =>
        song.AlignedEffects
            .Select(a => !a.Effect.SettingsString.Contains("T_CHOICE_LayerMethod=", StringComparison.Ordinal))
            .ToList();
}
