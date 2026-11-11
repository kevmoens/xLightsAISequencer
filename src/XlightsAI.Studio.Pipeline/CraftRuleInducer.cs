using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Structure;

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
    /// Build-plan Phase 17 §2f default: a favorite song (<see cref="CorpusEntry.IsFavorite"/>) counts as
    /// if it were this many songs toward <see cref="Evaluate"/>'s own confidence/song-count statistics,
    /// rather than literally duplicating its <see cref="CorpusSongAnalysis"/> N times before induction —
    /// the two are statistically equivalent for this scoring (every instance from a weight-N song counts
    /// N times toward both the numerator and denominator, and its song-count contribution is N), but a
    /// weight avoids ever materializing N copies of the same analysis object elsewhere in the pipeline
    /// (PatternLibrary/ExemplarIndex are unaffected -- this weight is local to rule induction only, per
    /// the user's explicit instruction that this is a craft-rule-statistics effect, not a retrieval
    /// boost). <b>Disclosed overfitting risk:</b> a weight of N makes one real show's own idiosyncrasies
    /// count as N independent data points, so a rule that is true only because of how *this one show*
    /// happens to be built can clear <paramref name="minSongCount"/>/<paramref name="minConfidence"/> on
    /// the strength of a single favorited song alone, dressed up as broad corpus agreement. 3 was chosen
    /// as a modest default -- enough to tip a borderline rule the favorite song agrees with, without
    /// letting one song single-handedly satisfy a 5-song threshold on its own (a weight of 5 or more
    /// would do exactly that, which is why it was rejected).
    /// </summary>
    public const int DefaultFavoriteSongWeightMultiplier = 3;

    /// <summary>Build-plan Phase 19 §2c: the same "3 or more concurrent layers" figure Phase 13 §2a's own `DirectorPromptBuilder` guidance currently states as a researched-but-unverified minimum -- re-checked here as a real, mechanically-measured corpus statistic instead of trusted as a guess.</summary>
    private const int HighEnergyLayerFloor = 3;

    /// <summary>Build-plan Phase 19 §2c: how soon after a detected <see cref="KeyChange"/> a fresh effect start still counts as "marking" that change, not a coincidence from unrelated later activity.</summary>
    private const double KeyChangeEffectWindowMs = 2000;

    /// <summary>
    /// Build-plan Phase 23 §2a/§2b: how close (in milliseconds) an effect's own start must land to a real
    /// detected <see cref="Rhythm.TransientHit"/> to count as "reacting to" that hit rather than a
    /// coincidence from unrelated nearby activity -- the same 300ms figure
    /// <c>XlightsAI.Studio.Director.TimedCueTransientResolver.SnappedTolerance</c> already established
    /// corpus-wide as "close enough to a real transient to snap to it" (Pipeline cannot reference Director,
    /// so this is a deliberate, disclosed duplication of that same convention, not a fresh guess).
    /// </summary>
    private const double TransientAdjacencyWindowMs = 300;

    /// <summary>
    /// Build-plan Phase 23 §2b: the same "short/discrete" cutoff
    /// <c>XlightsAI.Studio.Director.TimedCueComplianceChecker.MaxQualifyingEffectDuration</c> already
    /// established (2 seconds) -- duplicated here, not referenced, because Pipeline cannot reference
    /// Director (Director already references Pipeline); same disclosed-duplication discipline as
    /// <see cref="TransientAdjacencyWindowMs"/>.
    /// </summary>
    private const double MaxQualifyingShortEffectDurationMs = 2000;

    /// <summary>
    /// One candidate rule's checkable predicate: given one song's analysis, return every applicable
    /// instance's truth value (e.g. one bool per 8-bar phrase, or a single bool for the whole song) — an
    /// empty list means the rule had nothing applicable to check in this song (e.g. no 8-bar phrases, or
    /// no Mode A effects), which is excluded from both the denominator and the song-count tally, never
    /// counted as a false.
    /// </summary>
    private delegate IReadOnlyList<bool> Predicate(CorpusSongAnalysis song);

    /// <param name="favoriteSongWeightMultiplier">See <see cref="DefaultFavoriteSongWeightMultiplier"/>'s own doc comment for the mechanism and its disclosed overfitting risk. Must be &gt;= 1 (1 means "no favorite weighting" -- every song counts once, the pre-§2f behavior).</param>
    public static CraftRules InduceAndVerify(
        IReadOnlyList<CorpusSongAnalysis> songs, double minConfidence = 0.70, int minSongCount = 5,
        int favoriteSongWeightMultiplier = DefaultFavoriteSongWeightMultiplier)
    {
        ArgumentNullException.ThrowIfNull(songs);
        if (favoriteSongWeightMultiplier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(favoriteSongWeightMultiplier), favoriteSongWeightMultiplier, "Must be >= 1.");
        }

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
            (
                "high-energy-tree-matrix-layer-floor",
                $"During a high-energy (Chorus/PreChorus/Solo) section, a real Tree/Matrix-class prop " +
                $"typically reaches at least {HighEnergyLayerFloor} time-overlapping effect layers at its " +
                $"busiest moment -- not just one effect at a time.",
                EvaluateHighEnergyTreeMatrixLayerFloor),
            (
                "concurrent-props-vary-effect-choice",
                "When two or more different props/models are each showing an effect at the same instant, " +
                "they usually run DIFFERENT effect names from one another, not the identical effect " +
                "everywhere at once.",
                EvaluateConcurrentPropsVaryEffectChoice),
            (
                "key-change-correlates-with-elevated-effect-activity",
                "In the 2 seconds following a detected musical key change, real shows start noticeably " +
                "more new effects than that same song's own average rate elsewhere -- a key change tends " +
                "to get marked with a burst of fresh effect activity, not just more of what was already " +
                "running.",
                EvaluateKeyChangeCorrelatesWithEffectActivity),
            (
                "pre-hit-brightness-contrast-exists",
                "At a real detected hard transient hit, a model's own immediately-preceding effect " +
                "measurably differs in BrightnessTier from the effect at/after that hit -- real shows " +
                "already contrast brightness around a hit on their own, not only when deliberately " +
                "engineered to.",
                EvaluatePreHitBrightnessContrast),
            (
                "hard-hit-adjacent-moments-skew-short",
                "In the moments immediately around a real detected hard transient hit, the effects " +
                "placed there skew toward short/discrete effects (TimedCueComplianceChecker's own " +
                "qualifying-short-effect convention) more than that same song's own baseline rate of " +
                "short effects elsewhere.",
                EvaluateHardHitAdjacentMomentsSkewShort),
        };

        var evaluated = candidates
            .Select(c => Evaluate(c.Id, c.Statement, c.Predicate, mineable, favoriteSongWeightMultiplier))
            .ToList();

        var promoted = evaluated.Where(r => r.Confidence >= minConfidence && r.SongCount >= minSongCount).ToList();
        var rejected = evaluated.Except(promoted).ToList();

        return new CraftRules(DateTimeOffset.UtcNow, minConfidence, minSongCount, promoted, rejected);
    }

    private static CraftRule Evaluate(
        string id, string statement, Predicate predicate, IReadOnlyList<CorpusSongAnalysis> songs, int favoriteSongWeightMultiplier)
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

            // Build-plan Phase 17 §2f: a favorited song's instances/song-count count
            // favoriteSongWeightMultiplier times instead of once -- see DefaultFavoriteSongWeightMultiplier's
            // doc comment for the mechanism and its disclosed overfitting risk.
            var weight = song.Entry.IsFavorite ? favoriteSongWeightMultiplier : 1;

            songCount += weight;
            applicable += results.Count * weight;
            trueCount += results.Count(r => r) * weight;
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

    /// <summary>
    /// Build-plan Phase 19 §2c (user feedback: "not enough layers on Tree/Matrixes" -- same real-run
    /// complaint Phase 13 already measured by hand once). One instance per real Tree/Matrix-class model
    /// that has at least one effect placement during a Chorus/PreChorus/Solo section -- true when that
    /// model's own time-overlapping effect instances (across every <c>EffectLayer</c> it carries, real
    /// absolute timestamps, so two different occurrences of the same <see cref="SectionKind"/> never
    /// spuriously overlap each other) reach <see cref="HighEnergyLayerFloor"/> or more at their busiest
    /// moment.
    /// </summary>
    private static IReadOnlyList<bool> EvaluateHighEnergyTreeMatrixLayerFloor(CorpusSongAnalysis song)
    {
        var highEnergyKinds = new HashSet<SectionKind> { SectionKind.Chorus, SectionKind.PreChorus, SectionKind.Solo };

        return song.AlignedEffects
            .Where(a => a.SectionKind is { } kind && highEnergyKinds.Contains(kind) && TreeMatrixPropNameConvention.IsTreeOrMatrixName(a.Effect.ModelName))
            .GroupBy(a => a.Effect.ModelName, StringComparer.Ordinal)
            .Select(g => MaxConcurrentOverlap(g.Select(a => (a.Effect.StartTimeMs, a.Effect.EndTimeMs))) >= HighEnergyLayerFloor)
            .ToList();
    }

    /// <summary>The largest number of half-open <c>[Start,End)</c> intervals ever simultaneously active -- the real measure of "how many layers deep" a model's own effect placements run at their busiest instant. Build-plan Phase 20 §2a: made <see langword="internal"/> (was <see langword="private"/>) so <see cref="MultiLayerStackMiner"/> can reuse this exact sweep rather than re-deriving it.</summary>
    internal static int MaxConcurrentOverlap(IEnumerable<(long Start, long End)> intervals)
    {
        var events = new List<(long Time, int Delta)>();
        foreach (var (start, end) in intervals)
        {
            if (end <= start)
            {
                continue; // Zero/negative-width placement -- not a real interval, never contributes.
            }

            events.Add((start, 1));
            events.Add((end, -1));
        }

        if (events.Count == 0)
        {
            return 0;
        }

        // A -1 (end, exclusive) at the same instant as a +1 (start) must apply first, or two back-to-back
        // (never truly overlapping) placements would misread as one instant of double occupancy.
        var current = 0;
        var max = 0;
        foreach (var e in events.OrderBy(e => e.Time).ThenBy(e => e.Delta))
        {
            current += e.Delta;
            max = Math.Max(max, current);
        }

        return max;
    }

    /// <summary>
    /// Build-plan Phase 20 §2a: like <see cref="MaxConcurrentOverlap"/>, but also returns which of the
    /// input items are active during the single maximal-concurrency time segment (the real "what was
    /// actually stacked together, right now" set a multi-layer stack extraction needs, not just the
    /// count) -- the first maximal segment encountered in time order when there is more than one.
    /// </summary>
    internal static (long SegmentStart, long SegmentEnd, IReadOnlyList<T> ActiveDuringPeak) FindPeakOverlapSegment<T>(
        IReadOnlyList<T> items, Func<T, long> start, Func<T, long> end)
    {
        var events = new List<(long Time, int Delta, T Item)>();
        foreach (var item in items)
        {
            var s = start(item);
            var e = end(item);
            if (e <= s)
            {
                continue;
            }

            events.Add((s, 1, item));
            events.Add((e, -1, item));
        }

        if (events.Count == 0)
        {
            return (0, 0, []);
        }

        var ordered = events.OrderBy(e => e.Time).ThenBy(e => e.Delta).ToList();
        var active = new List<T>();
        var bestCount = 0;
        long bestStart = 0;
        long bestEnd = 0;
        IReadOnlyList<T> bestActive = [];

        var i = 0;
        while (i < ordered.Count)
        {
            var time = ordered[i].Time;
            while (i < ordered.Count && ordered[i].Time == time)
            {
                if (ordered[i].Delta == 1)
                {
                    active.Add(ordered[i].Item);
                }
                else
                {
                    active.Remove(ordered[i].Item);
                }

                i++;
            }

            var segmentEnd = i < ordered.Count ? ordered[i].Time : time;
            if (segmentEnd > time && active.Count > bestCount)
            {
                bestCount = active.Count;
                bestStart = time;
                bestEnd = segmentEnd;
                bestActive = active.ToList();
            }
        }

        return (bestStart, bestEnd, bestActive);
    }

    /// <summary>
    /// Build-plan Phase 19 §2c (the same "a lot of the same combined effects" complaint): one instance
    /// per effect placement that has at least one OTHER, different model's effect already running at its
    /// own start instant (never compared against another layer on its own same model -- that is the
    /// layer-floor rule's own question, not this one) -- true when at least one of those concurrently
    /// active other-model effects uses a different <see cref="ParsedEffectInstance.EffectName"/>, false
    /// when every concurrently active other-model effect happens to share this one's exact name (real
    /// shows running the literal same effect everywhere at once).
    /// </summary>
    private static IReadOnlyList<bool> EvaluateConcurrentPropsVaryEffectChoice(CorpusSongAnalysis song)
    {
        var results = new List<bool>();
        var active = new List<(string ModelName, string EffectName, long EndTimeMs)>();

        foreach (var aligned in song.AlignedEffects.OrderBy(a => a.Effect.StartTimeMs))
        {
            var start = aligned.Effect.StartTimeMs;
            active.RemoveAll(a => a.EndTimeMs <= start);

            var crossModelActive = active.Where(a => !string.Equals(a.ModelName, aligned.Effect.ModelName, StringComparison.Ordinal)).ToList();
            if (crossModelActive.Count > 0)
            {
                results.Add(crossModelActive.Any(a => !string.Equals(a.EffectName, aligned.Effect.EffectName, StringComparison.Ordinal)));
            }

            active.Add((aligned.Effect.ModelName, aligned.Effect.EffectName, aligned.Effect.EndTimeMs));
        }

        return results;
    }

    /// <summary>
    /// Build-plan Phase 19 §2c (the "what keys did they use" ask): one instance per real, detected
    /// <see cref="KeyChange"/> -- true when the count of effect starts in the
    /// <see cref="KeyChangeEffectWindowMs"/> window right after it exceeds what this SAME song's own
    /// whole-song average effect-start rate would predict for a window that size. Deliberately compared
    /// against each song's own baseline rate (never a fixed absolute count) so a densely-sequenced song is
    /// never unfairly flagged as "always true" just for being busy everywhere.
    /// </summary>
    private static IReadOnlyList<bool> EvaluateKeyChangeCorrelatesWithEffectActivity(CorpusSongAnalysis song)
    {
        if (song.KeyChanges is not { Count: > 0 } keyChanges || song.AlignedEffects.Count == 0)
        {
            return [];
        }

        var totalDurationMs = song.AlignedEffects.Max(a => a.Effect.EndTimeMs);
        if (totalDurationMs <= 0)
        {
            return [];
        }

        var expectedInWindow = song.AlignedEffects.Count / (double)totalDurationMs * KeyChangeEffectWindowMs;

        var results = new List<bool>();
        foreach (var keyChange in keyChanges)
        {
            var keyChangeMs = keyChange.TimeTicks / (double)TimeSpan.TicksPerMillisecond;
            var actualInWindow = song.AlignedEffects.Count(a => a.Effect.StartTimeMs >= keyChangeMs && a.Effect.StartTimeMs < keyChangeMs + KeyChangeEffectWindowMs);
            results.Add(actualInWindow > expectedInWindow);
        }

        return results;
    }

    /// <summary>
    /// Build-plan Phase 23 §2a: one instance per real (model, hard hit) pair where that model actually has
    /// an effect genuinely BEGINNING at/near the hit (within <see cref="TransientAdjacencyWindowMs"/> --
    /// the same "this is the effect reacting to the hit, not a long effect merely still playing through
    /// it" discipline <c>EmphasisContrastPlanner.Measure</c> already uses for real generation) AND a real
    /// preceding effect on that same model to contrast it against (the very first effect on a model has no
    /// "before" to measure, so it is skipped, never fabricated) AND both effects' <see cref="BrightnessTier"/>
    /// resolve via the shipped <see cref="EffectVisualCharacterCatalogLoader"/> catalog (an effect whose
    /// name has no catalog entry is skipped, same null-when-unresolved discipline as Phase 22). True when
    /// the two tiers differ by at least one step.
    /// </summary>
    private static IReadOnlyList<bool> EvaluatePreHitBrightnessContrast(CorpusSongAnalysis song)
    {
        if (song.TransientHits is not { Count: > 0 } hits)
        {
            return [];
        }

        var catalog = EffectVisualCharacterCatalogLoader.Load();
        var results = new List<bool>();

        foreach (var modelGroup in song.AlignedEffects.GroupBy(a => a.Effect.ModelName, StringComparer.Ordinal))
        {
            var ordered = modelGroup.OrderBy(a => a.Effect.StartTimeMs).ToList();

            foreach (var hit in hits)
            {
                var hitMs = hit.TimeTicks / (double)TimeSpan.TicksPerMillisecond;
                var idx = ordered.FindIndex(a => a.Effect.StartTimeMs <= hitMs && hitMs < a.Effect.EndTimeMs);
                if (idx <= 0)
                {
                    continue;
                }

                var following = ordered[idx];
                if (Math.Abs(following.Effect.StartTimeMs - hitMs) > TransientAdjacencyWindowMs)
                {
                    continue;
                }

                var preceding = ordered[idx - 1];
                var precedingTier = catalog.Find(preceding.Effect.EffectName)?.BrightnessTier;
                var followingTier = catalog.Find(following.Effect.EffectName)?.BrightnessTier;
                if (precedingTier is null || followingTier is null)
                {
                    continue;
                }

                results.Add(precedingTier != followingTier);
            }
        }

        return results;
    }

    /// <summary>
    /// Build-plan Phase 23 §2b: one instance per real detected hard hit that has at least one real effect
    /// placement (any model) starting within <see cref="TransientAdjacencyWindowMs"/> of it -- true when the
    /// fraction of those hit-adjacent effects that qualify as "short/discrete"
    /// (<see cref="MaxQualifyingShortEffectDurationMs"/>, <c>TimedCueComplianceChecker</c>'s own convention)
    /// exceeds that SAME song's own baseline short-effect fraction across every effect in the song --
    /// compared against each song's own baseline (never a fixed absolute fraction) so a song that already
    /// favors short effects everywhere is never unfairly credited just for being generally choppy, same
    /// discipline as <see cref="EvaluateKeyChangeCorrelatesWithEffectActivity"/>.
    /// </summary>
    private static IReadOnlyList<bool> EvaluateHardHitAdjacentMomentsSkewShort(CorpusSongAnalysis song)
    {
        if (song.TransientHits is not { Count: > 0 } hits || song.AlignedEffects.Count == 0)
        {
            return [];
        }

        var baselineShortFraction = song.AlignedEffects.Count(a => a.Effect.EndTimeMs - a.Effect.StartTimeMs <= MaxQualifyingShortEffectDurationMs)
            / (double)song.AlignedEffects.Count;

        var results = new List<bool>();
        foreach (var hit in hits)
        {
            var hitMs = hit.TimeTicks / (double)TimeSpan.TicksPerMillisecond;
            var nearby = song.AlignedEffects
                .Where(a => Math.Abs(a.Effect.StartTimeMs - hitMs) <= TransientAdjacencyWindowMs)
                .ToList();
            if (nearby.Count == 0)
            {
                continue;
            }

            var observedShortFraction = nearby.Count(a => a.Effect.EndTimeMs - a.Effect.StartTimeMs <= MaxQualifyingShortEffectDurationMs) / (double)nearby.Count;
            results.Add(observedShortFraction > baselineShortFraction);
        }

        return results;
    }
}
