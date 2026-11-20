using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>Build-plan Phase 22: which real, already-detected signal produced one <see cref="EmphasisInstant"/>.</summary>
public enum EmphasisSignalKind
{
    TransientHit,
    VocalNoteOnset,
    TimedCue,
}

/// <summary>
/// Build-plan Phase 22 (contrast-driven emphasis): one real, already-detected "this should feel like an
/// accent" moment -- a transient hit, a vocal-note onset, or a user timed cue -- reduced to the one real
/// tick it lands on. Never a synthesized/invented moment; every instant this planner ever sees is built
/// from data another real analyzer already produced (<see cref="Rhythm.TransientHit"/>,
/// <see cref="Melody.NoteEvent"/>, <c>XlightsAI.Studio.Director.TimedCue</c>).
/// </summary>
/// <param name="EndTimeTicks">
/// User follow-up (2026-11-10: "will any effects last as long as the note?"): the source signal's own
/// real end tick, when it genuinely has one -- a <see cref="Melody.NoteEvent"/>'s own real sung duration
/// for a <see cref="EmphasisSignalKind.VocalNoteOnset"/>. <see langword="null"/> (the default) for a
/// signal with no real duration of its own (a <see cref="Rhythm.TransientHit"/> is a single instant, not a
/// span) -- <see cref="TimingTrackEmitter.VocalNotes"/> uses this to size that track's own mark to the
/// real note's own length instead of falling back to the same short fixed snap <see cref="TimingTrackEmitter.Transients"/>
/// uses, but only when it is actually supplied; a signal source with no real end never has one invented.
/// </param>
public sealed record EmphasisInstant(long TimeTicks, EmphasisSignalKind Kind, long? EndTimeTicks = null);

/// <summary>
/// Build-plan Phase 22 §2e: one candidate "contrast event scope" -- a real xLights group, one
/// individually-addressable real model, or (one special entry) the whole real show -- <see cref="EmphasisContrastPlanner"/>
/// can report a patched target belongs to, for diagnostics/legibility. Built directly from the real
/// <see cref="DisplayGraph"/> (mirroring <c>XlightsAI.Studio.Director.LayoutSummary</c>'s own
/// groups/top-models condensation, but kept independent of it -- Choreography must never depend on
/// Director).
/// </summary>
public sealed record EmphasisSet(string Name, IReadOnlyList<ModelId> Models);

/// <summary>
/// Build-plan Phase 22 §2c: one real, measured (preceding effect, following effect) brightness-tier pair
/// at one real emphasis instant on one real compiled target -- the unit both the permanently-advisory
/// legality rule and the §2d deterministic fallback are built from. <see cref="HasContrast"/> is the
/// exact rule the user confirmed: the two tiers must differ by at least one step (Off/Dim/Bright is an
/// ordered scale, but any inequality counts, not just a specific direction) -- a same-tier transition
/// (including two different effects that both happen to measure the same tier) never counts as contrast.
/// </summary>
/// <param name="PrecedingTier">The real <see cref="BrightnessTier"/> of whatever compiled effect was already playing on <paramref name="Target"/> immediately before this instant, or <see langword="null"/> when it could not be resolved (an effect whose recipe id does not resolve to a known xLights effect name, or whose name has no catalog entry) -- never guessed.</param>
/// <param name="FollowingTier">The real <see cref="BrightnessTier"/> of the compiled effect that actually begins at/near this instant on <paramref name="Target"/>, same null-when-unresolved discipline.</param>
public sealed record EmphasisContrastFinding(long TimeTicks, EmphasisSignalKind Kind, ModelId Target, BrightnessTier? PrecedingTier, BrightnessTier? FollowingTier)
{
    public bool HasContrast => PrecedingTier is { } preceding && FollowingTier is { } following && preceding != following;
}

/// <summary>
/// Build-plan Phase 22 §4 acceptance criteria: everything a caller needs to report "2b's own real
/// adoption rate... before 2d's deterministic fallback is ever invoked" and "how often 2d's fallback had
/// to fire" from one real compile, without re-deriving either number by hand.
/// </summary>
public sealed record EmphasisContrastReport(
    IReadOnlyList<EmphasisContrastFinding> FindingsBeforeFallback,
    int PatchesApplied,
    IReadOnlyList<ModelId> DistinctPatchedTargets)
{
    public int MeasuredPairCount => FindingsBeforeFallback.Count;

    public int ContrastingPairCountBeforeFallback => FindingsBeforeFallback.Count(f => f.HasContrast);

    /// <summary>
    /// Build-plan Phase 22 §2b/§4: the real fraction of measured (instant, target) pairs that already had
    /// genuine brightness-tier contrast BEFORE <see cref="EmphasisContrastPlanner.ApplyContrastFallback"/>
    /// ever touched anything -- this is the real, measured number confirming §2b's prompt guidance (not
    /// §2d's deterministic patch) is the one actually doing the work. <c>1.0</c> (vacuously) when nothing
    /// was measurable at all, never a divide-by-zero.
    /// </summary>
    public double AdoptionRateBeforeFallback => MeasuredPairCount == 0 ? 1.0 : ContrastingPairCountBeforeFallback / (double)MeasuredPairCount;
}

/// <summary>
/// Build-plan Phase 22 (contrast-driven emphasis): the one place this requirement is actually enforced,
/// in two parts --
/// <list type="bullet">
/// <item><see cref="Measure"/> (§2c): a pure, read-only measurement over an already-compiled
/// <see cref="ChoreographyIR"/>, reused both by <see cref="ChoreographyLegalityValidator"/>'s own
/// permanently-advisory rule and by <see cref="ApplyContrastFallback"/> to decide what still needs
/// repairing.</item>
/// <item><see cref="ApplyContrastFallback"/> (§2d): a bounded, deterministic patch -- never a
/// section-wide regenerate/retry -- that substitutes a brief, real Dim/Off-tier filler immediately before
/// each real emphasis instant <see cref="Measure"/> found still flat, strictly subdividing existing
/// compiled coverage (never removing any), so the always-moving rule can only ever improve, never
/// regress, as a side effect.</item>
/// </list>
/// The user's own confirmed design: §2b (strong Choreographer prompt guidance, not implemented in this
/// class) is the PRIMARY mechanism; this class exists to measure whether that worked and to deterministically
/// repair the specific real instants where it did not -- never to proactively re-plan contrast everywhere,
/// which would make §2b redundant instead of primary.
/// </summary>
public static class EmphasisContrastPlanner
{
    internal static ChoreographyIR ApplyTimelineContrast(
        ChoreographyIR ir, IReadOnlyList<SectionCueWindow> cues, SectionMusicContext section,
        DisplayGraph layout, LookCompilerSettings settings, EffectVisualCharacterCatalog catalog,
        IReadOnlyList<SectionCueWindow> program)
    {
        var dim = SelectDimOrOffEffectName(BrightnessTier.Dim, catalog, settings.RecipeCatalog);
        if (dim is null)
        {
            return ir;
        }

        var holds = program.Where(c => c.IsHeld).ToArray();
        var effects = ir.Effects.ToList();
        var accentGroups = cues.Where(c => c.Cue.Kind == CueKind.Accent &&
                !holds.Any(h => c.OverlapsHeld(h, program)))
            .GroupBy(c => c.Window.StartTicks);
        var previousBright = false;
        foreach (var group in accentGroups)
        {
            var bright = group.Where(c => IntensityTierMapping.IsBrightTier(c.Cue.Intensity)).ToArray();
            if (previousBright && bright.Length > 0)
            {
                var end = group.Key;
                var beatTimes = section.BarGrid.Beats.Select(b => b.TimeTicks).ToArray();
                var index = Array.FindLastIndex(beatTimes, t => t < end);
                var beat = index >= 0 && index + 1 < beatTimes.Length
                    ? Math.Max(1L, beatTimes[index + 1] - beatTimes[index])
                    : FallbackBeatTicks;
                var start = Math.Max(section.Window.StartTicks, end - (long)(beat * MinPreBeatBeats));
                if (end - start >= FrameClock.FrameIntervalTicks)
                {
                    var protectedModels = holds.Where(h => h.Window.StartTicks < end)
                        .SelectMany(h => h.Models.Where(m => h.HeldEnd(program, m) > start)).ToHashSet();
                    var targets = (bright.Any(c => c.Cue.Intensity == IntensityTier.Full)
                            ? layout.Models.Select(m => m.Id)
                            : bright.SelectMany(c => c.Models))
                        .Where(m => !protectedModels.Contains(m)).ToHashSet();
                    effects = TimelineEffectMask.Remove(effects, layout, new TimeWindow(start, end), targets);
                    var recipe = LookCompiler.RecipeIdFor(dim, settings.RecipeCatalog);
                    foreach (var target in targets)
                    {
                        effects.Add(settings.RecipeRegistry.CreateVerified(recipe, target, start, end));
                    }
                }
            }
            previousBright = bright.Length > 0;
        }
        return ir with { Effects = effects };
    }

    /// <summary>Build-plan Phase 22 §2a: the pre-beat window's own real bounds, in beats -- confirmed by the user as "the ~0.5-1 beat immediately before."</summary>
    public const double MinPreBeatBeats = 0.5;

    public const double MaxPreBeatBeats = 1.0;

    /// <summary>A plausible single-beat duration (120 BPM) used only when a song's own beat grid has fewer than two beats to measure a real interval from -- never fabricated as a "detected" tempo.</summary>
    private static readonly long FallbackBeatTicks = TimeSpan.TicksPerSecond / 2;

    /// <summary>
    /// Build-plan Phase 22 §2e: candidate "contrast event scopes" built directly from <paramref name="layout"/> --
    /// every real xLights group with at least one real (non-zero-pixel) member, every individually-addressable
    /// real model, and one final "whole show" entry spanning every real model. Mirrors
    /// <c>LayoutSummary.Build</c>'s own group/model condensation, kept independent so <c>Choreography</c>
    /// never needs a reference to <c>Director</c>.
    /// </summary>
    public static IReadOnlyList<EmphasisSet> BuildEmphasisSets(DisplayGraph layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var modelsByXlightsName = layout.Models
            .Where(m => m.NodeCount > 0)
            .GroupBy(m => m.XlightsName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var sets = new List<EmphasisSet>();

        foreach (var group in layout.Groups)
        {
            var members = group.Members
                .Where(m => m.Kind == DisplayGroupMemberKind.Model && modelsByXlightsName.ContainsKey(m.RawName))
                .Select(m => modelsByXlightsName[m.RawName])
                .Distinct()
                .ToList();

            if (members.Count > 0)
            {
                sets.Add(new EmphasisSet($"group:{group.Name}", members));
            }
        }

        foreach (var (xlightsName, modelId) in modelsByXlightsName)
        {
            sets.Add(new EmphasisSet(xlightsName, [modelId]));
        }

        var allModelIds = modelsByXlightsName.Values.Distinct().ToList();
        if (allModelIds.Count > 0)
        {
            sets.Add(new EmphasisSet("*whole-show*", allModelIds));
        }

        return sets;
    }

    /// <summary>
    /// Build-plan Phase 22 §2c: measures, for every real <paramref name="instants"/> entry, every real
    /// compiled target whose own effect genuinely BEGINS at/near that instant (within about one real beat
    /// of it -- a long-running effect that merely happens to still be playing through the moment is not
    /// "the effect the emphasis uses," only a fresh transition counts) the real brightness-tier pair
    /// (preceding, following). A target with no preceding effect at all (the very first compiled effect on
    /// that target in the whole song) is skipped -- there is no real "before" to measure a contrast
    /// against, never fabricated. Pure/read-only: never mutates <paramref name="ir"/>.
    /// </summary>
    public static IReadOnlyList<EmphasisContrastFinding> Measure(
        ChoreographyIR ir, IReadOnlyList<EmphasisInstant> instants, BeatGrid beatGrid,
        EffectRecipeCatalog recipeCatalog, EffectVisualCharacterCatalog brightnessCatalog)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(instants);
        ArgumentNullException.ThrowIfNull(beatGrid);
        ArgumentNullException.ThrowIfNull(recipeCatalog);
        ArgumentNullException.ThrowIfNull(brightnessCatalog);

        var byTarget = BuildOrderedByTarget(ir);
        var findings = new List<EmphasisContrastFinding>();

        foreach (var instant in instants.OrderBy(i => i.TimeTicks))
        {
            var tolerance = BeatDurationTicksNear(instant.TimeTicks, beatGrid);

            foreach (var (target, list) in byTarget)
            {
                var idx = list.FindIndex(e => e.StartTicks <= instant.TimeTicks && instant.TimeTicks < e.EndTicks);
                if (idx <= 0)
                {
                    continue;
                }

                var following = list[idx];
                if (Math.Abs(following.StartTicks - instant.TimeTicks) > tolerance)
                {
                    continue;
                }

                var preceding = list[idx - 1];
                findings.Add(new EmphasisContrastFinding(
                    instant.TimeTicks, instant.Kind, target,
                    ResolveTier(preceding, recipeCatalog, brightnessCatalog),
                    ResolveTier(following, recipeCatalog, brightnessCatalog)));
            }
        }

        return findings;
    }

    /// <summary>
    /// Build-plan Phase 22 §2d: patches every real (instant, target) pair <see cref="Measure"/> finds
    /// still missing contrast -- never the whole-song proactive pass (§2b is primary; see this class's own
    /// doc comment). When more than one real target misses contrast at the exact same instant, §2e's
    /// travel rule picks exactly one to patch (randomly, excluding whichever target the immediately
    /// preceding patched instant in the same real section used) -- a single real candidate is always used
    /// as-is (the "one fixed prop" minority case). Each patch strictly subdivides existing compiled
    /// coverage (trims/replaces the tail of the immediately preceding effect on that target, or drops it
    /// entirely when fully consumed) -- it can only ever shorten an existing gap-free span into two
    /// gap-free spans, never introduce a new gap, so the always-moving rule can only improve, never
    /// regress, as a side effect. A patch too small to represent (the available room is this song's own
    /// real sub-frame rounding limit) or with no real, registered Off/Dim effect available is simply
    /// skipped -- left to the permanently-advisory report, never fabricated.
    /// </summary>
    public static (ChoreographyIR Ir, EmphasisContrastReport Report) ApplyContrastFallback(
        ChoreographyIR ir, IReadOnlyList<EmphasisInstant> instants, IReadOnlyList<TimeWindow> sectionWindows,
        BeatGrid beatGrid, DisplayGraph layout, EffectRecipeCatalog recipeCatalog, EffectVisualCharacterCatalog brightnessCatalog,
        IEffectRecipeRegistry recipeRegistry, Random random)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(instants);
        ArgumentNullException.ThrowIfNull(sectionWindows);
        ArgumentNullException.ThrowIfNull(beatGrid);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(recipeCatalog);
        ArgumentNullException.ThrowIfNull(brightnessCatalog);
        ArgumentNullException.ThrowIfNull(recipeRegistry);
        ArgumentNullException.ThrowIfNull(random);

        var findingsBefore = Measure(ir, instants, beatGrid, recipeCatalog, brightnessCatalog);
        if (findingsBefore.Count == 0)
        {
            return (ir, new EmphasisContrastReport(findingsBefore, 0, []));
        }

        var byTarget = BuildOrderedByTarget(ir);

        var missedByInstant = findingsBefore
            .Where(f => !f.HasContrast)
            .GroupBy(f => f.TimeTicks)
            .OrderBy(g => g.Key)
            .ToList();

        var patchedTargets = new HashSet<ModelId>();
        var patchesApplied = 0;
        string? previousPatchedTargetValue = null;
        long? previousSectionStartTicks = null;

        foreach (var group in missedByInstant)
        {
            TimeWindow? containingWindow = null;
            foreach (var window in sectionWindows)
            {
                if (group.Key >= window.StartTicks && group.Key < window.EndTicks)
                {
                    containingWindow = window;
                    break;
                }
            }

            if (containingWindow is not { } sectionWindow)
            {
                continue; // A real instant outside every known section window has nothing to anchor a patch against -- skipped, never guessed.
            }

            if (previousSectionStartTicks != sectionWindow.StartTicks)
            {
                previousPatchedTargetValue = null; // A new section's own run of emphasis moments starts its own travel exclusion fresh.
            }

            previousSectionStartTicks = sectionWindow.StartTicks;

            var candidates = group.ToList();
            var chosen = candidates.Count == 1 ? candidates[0] : ChooseTravelCandidate(candidates, previousPatchedTargetValue, random);

            if (!byTarget.TryGetValue(chosen.Target, out var list))
            {
                continue;
            }

            var idx = list.FindIndex(e => e.StartTicks <= chosen.TimeTicks && chosen.TimeTicks < e.EndTicks);
            if (idx <= 0)
            {
                continue;
            }

            var following = list[idx];
            var preceding = list[idx - 1];

            var beatDuration = BeatDurationTicksNear(chosen.TimeTicks, beatGrid);
            var fraction = MinPreBeatBeats + (random.NextDouble() * (MaxPreBeatBeats - MinPreBeatBeats));
            var desiredWindowTicks = (long)Math.Round(beatDuration * fraction);

            var windowEnd = following.StartTicks;
            var windowStart = Math.Max(preceding.StartTicks, windowEnd - desiredWindowTicks);

            if (windowEnd - windowStart < FrameClock.FrameIntervalTicks)
            {
                continue; // No real room to carve a representable sub-frame patch out of -- never fabricated.
            }

            var effectName = SelectDimOrOffEffectName(chosen.FollowingTier, brightnessCatalog, recipeCatalog);
            if (effectName is null)
            {
                continue; // No real, registered Off/Dim effect is available -- left for the permanently-advisory report rather than guessed.
            }

            EffectInstance patch;
            try
            {
                var recipeId = LookCompiler.RecipeIdFor(effectName, recipeCatalog);
                patch = recipeRegistry.CreateVerified(recipeId, chosen.Target, windowStart, windowEnd) with { Layer = following.Layer };
            }
            catch (EffectPlanInvalidException)
            {
                continue; // Never let a patch attempt throw out of this deterministic pass -- skip, advisory report still surfaces the miss.
            }

            var updatedList = new List<EffectInstance>(list);
            var precedingIndex = updatedList.IndexOf(preceding);

            // Real-run finding (2026-11-09, full-length track): trimming preceding down to exactly
            // windowStart can leave a REMAINDER shorter than one real exported frame (the same
            // "unrepresentable sliver" AlwaysMovingGapSplitter's own FillGap already guards against) --
            // confirmed as a real XsqExporter rejection ("rounds to a non-positive duration") on a real
            // run, not a hypothetical. Absorbed into the patch itself (grown backward to preceding's own
            // start) rather than left as a too-short trimmed effect, same "absorb, never emit
            // unrepresentable" discipline used everywhere else in this codebase.
            if (windowStart - preceding.StartTicks < FrameClock.FrameIntervalTicks)
            {
                windowStart = preceding.StartTicks;
                updatedList.RemoveAt(precedingIndex); // Fully consumed by the (possibly grown) patch -- dropped, never left as a sub-frame effect.
            }
            else
            {
                updatedList[precedingIndex] = preceding with { EndTicks = windowStart };
            }

            if (patch.StartTicks != windowStart)
            {
                patch = patch with { StartTicks = windowStart };
            }

            updatedList.Add(patch);
            byTarget[chosen.Target] = updatedList.OrderBy(e => e.StartTicks).ToList();

            patchedTargets.Add(chosen.Target);
            patchesApplied++;
            previousPatchedTargetValue = chosen.Target.Value;
        }

        var updatedIr = ir with { Effects = byTarget.Values.SelectMany(l => l).ToList() };
        return (updatedIr, new EmphasisContrastReport(findingsBefore, patchesApplied, patchedTargets.ToList()));
    }

    /// <summary>Build-plan Phase 22 §2e: among several real targets missing contrast at the exact same instant, picks one at random, excluding whichever target the immediately preceding patched instant (in the same real section) used -- "two consecutive moments don't land on the same set by chance," confirmed as deliberately unspecified/random beyond that one exclusion.</summary>
    private static EmphasisContrastFinding ChooseTravelCandidate(IReadOnlyList<EmphasisContrastFinding> candidates, string? previousTargetValue, Random random)
    {
        var pool = previousTargetValue is null
            ? candidates
            : candidates.Where(c => !string.Equals(c.Target.Value, previousTargetValue, StringComparison.Ordinal)).ToList();

        if (pool.Count == 0)
        {
            pool = candidates; // Every real candidate collides with the previous pick -- no real alternative exists; use what is real rather than fabricate one.
        }

        return pool[random.Next(pool.Count)];
    }

    private static Dictionary<ModelId, List<EffectInstance>> BuildOrderedByTarget(ChoreographyIR ir) =>
        ir.Effects.GroupBy(e => e.Target).ToDictionary(g => g.Key, g => g.OrderBy(e => e.StartTicks).ToList());

    private static long BeatDurationTicksNear(long tick, BeatGrid beatGrid)
    {
        var beats = beatGrid.Beats;
        if (beats.Length < 2)
        {
            return FallbackBeatTicks;
        }

        var ordered = beats.OrderBy(b => b.TimeTicks).ToArray();
        for (var i = 0; i < ordered.Length - 1; i++)
        {
            if (ordered[i].TimeTicks <= tick && tick < ordered[i + 1].TimeTicks)
            {
                return Math.Max(1, ordered[i + 1].TimeTicks - ordered[i].TimeTicks);
            }
        }

        return tick < ordered[0].TimeTicks
            ? Math.Max(1, ordered[1].TimeTicks - ordered[0].TimeTicks)
            : Math.Max(1, ordered[^1].TimeTicks - ordered[^2].TimeTicks);
    }

    /// <summary>Recovers the real brightness tier of <paramref name="effect"/>, via its own recipe id's registered xLights effect name -- <see langword="null"/> when the recipe id does not resolve, or the resolved name has no catalog entry, never guessed.</summary>
    private static BrightnessTier? ResolveTier(EffectInstance effect, EffectRecipeCatalog recipeCatalog, EffectVisualCharacterCatalog brightnessCatalog)
    {
        var recipe = recipeCatalog.Recipes.FirstOrDefault(r => string.Equals(r.RecipeId, effect.RecipeId, StringComparison.Ordinal));
        return recipe is null ? null : brightnessCatalog.Find(recipe.XlightsEffectName)?.BrightnessTier;
    }

    /// <summary>
    /// Picks a real, registered (i.e. <paramref name="recipeCatalog"/>-resolvable, so <see cref="IEffectRecipeRegistry.CreateVerified"/>
    /// is guaranteed to succeed) Off/Dim-tier effect name from <paramref name="brightnessCatalog"/> --
    /// preferring the strongest available drop (Off) whenever <paramref name="followingTier"/> is
    /// <see cref="BrightnessTier.Bright"/> or unresolved, Dim otherwise (a Dim-to-Dim transition would
    /// never register as contrast, so stepping all the way to Off is still the safer choice when a real
    /// Dim-tier following effect is what this patch must contrast against). Deterministic (ordered by
    /// effect name) -- real variety here is the user's own confirmed, secondary concern (§2e's prop
    /// travel), not this specific effect-name choice. <see langword="null"/> when no real catalog entry at
    /// either tier is also a registered recipe -- never invented.
    /// </summary>
    private static string? SelectDimOrOffEffectName(BrightnessTier? followingTier, EffectVisualCharacterCatalog brightnessCatalog, EffectRecipeCatalog recipeCatalog)
    {
        var registeredNames = new HashSet<string>(recipeCatalog.Recipes.Select(r => r.XlightsEffectName), StringComparer.Ordinal);
        var preferOff = followingTier != BrightnessTier.Dim;

        var candidates = brightnessCatalog.Characters
            .Where(c => registeredNames.Contains(c.EffectName) && c.BrightnessTier is BrightnessTier.Off or BrightnessTier.Dim)
            .OrderBy(c => preferOff ? (c.BrightnessTier == BrightnessTier.Off ? 0 : 1) : (c.BrightnessTier == BrightnessTier.Dim ? 0 : 1))
            .ThenBy(c => c.EffectName, StringComparer.Ordinal)
            .ToList();

        return candidates.FirstOrDefault()?.EffectName;
    }
}
