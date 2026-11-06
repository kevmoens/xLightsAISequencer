using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4's own top-level orchestration: compiles every assigned <see cref="Look"/> against
/// its own section (<see cref="LookCompiler"/>), emits the whole-song timing tracks once
/// (<see cref="TimingTrackEmitter"/>), applies the always-moving gap-splitting pass globally per target
/// across the full song (<see cref="AlwaysMovingGapSplitter"/> -- never per-section, since a gap can span
/// a section boundary), and runs every compositional legality check (<see cref="ChoreographyLegalityValidator"/>).
/// This is the "deterministic constraint engine" (architecture §7/L4) a hand-authored Look library is
/// run through end to end to produce a real, checkable <see cref="ChoreographyIR"/> -- no LLM involved.
/// </summary>
public static class ChoreographyEngine
{
    /// <param name="Layout">The real show layout this song is compiled against.</param>
    /// <param name="BeatGrid">The song's own measured beat/bar grid (Phase 5).</param>
    /// <param name="Sections">Classified sections (Phase 8) -- each one assigned a Look via <see cref="LookIdBySectionId"/>.</param>
    /// <param name="Phrases">Measured phrase segmentation (Phase 3's statistical fit, Phase 8's segmenter).</param>
    /// <param name="BandEnergy">Per-band energy (Phase 3 task 1), used for the labelled <c>Percussion</c> track -- <see langword="null"/> omits that track entirely rather than fabricating one.</param>
    /// <param name="VocalActivity">The vocalActive envelope (Phase 3 task 2), used only to populate <see cref="SectionMusicContext.VocalActive"/> -- <see langword="null"/> means every section compiles as instrumental.</param>
    /// <param name="LookLibrary">The hand-authored (or Director-produced, in a later phase) Look library.</param>
    /// <param name="LookIdBySectionId">Which Look (by id) each section (by its own <see cref="SectionLabelCandidate.Id"/>) is assigned to. A section with no entry is left uncompiled -- never silently assigned an invented default.</param>
    /// <param name="EnergyCeilingByKind">Per-<see cref="SectionKind"/> energy ceiling (architecture §10's structural arc) -- a kind with no entry defaults to <c>1.0</c> (no additional ceiling beyond the Look's own).</param>
    /// <param name="SongEndTicks">The song's own total duration, in ticks.</param>
    /// <param name="CompilerSettings">Vocabulary/recipe/palette configuration shared across every section's compile.</param>
    /// <param name="FillerSchedule">
    /// Build-plan Phase 15 §3: resolves, for any tick position and target, which section's own declared
    /// filler plan applies -- replaces the old single whole-song <c>FillerPool</c> that used to live on
    /// <see cref="LookCompilerSettings"/> (never actually consumed by <see cref="LookCompiler"/> itself;
    /// confirmed zero references) now that filler is a per-section (and optionally per-role) Choreographer
    /// declaration. Required: the always-moving rule must always have a real pool to draw from. Use
    /// <see cref="SectionFillerSchedule.Flat"/> for a single whole-song pool (e.g. a hand-authored-Look
    /// test with no real <see cref="SectionScript"/> to derive a per-section plan from).
    /// </param>
    /// <param name="CoverageLadderRungCount">How many rungs <see cref="PixelCoverageLadder.Build"/> produces.</param>
    /// <param name="VisemeMarks">Build-plan Phase 8 task 3's heuristic viseme/phoneme approximation (<see cref="XlightsAI.Studio.Stems.VisemeApproximator"/>), used only to populate the <see cref="LyricTimingTrackBuilder"/> lyric tracks -- <see langword="null"/>/empty means no <c>Lyrics_Phonemes</c>/<c>Lyrics_Words</c> content (still produces <c>Lyrics_Phrases</c> whenever <paramref name="VocalActivity"/> has real activity).</param>
    /// <param name="PhraseLookAssignmentsBySectionId">
    /// Build-plan Phase 10 §3e: an optional, additive override of <paramref name="LookIdBySectionId"/> for a
    /// section that should compile as several phrase-scoped sub-windows (<see cref="SectionScriptCompiler"/>'s
    /// own <c>SectionPhraseLookAssignment</c> list, one entry per phrase) instead of one Look spanning its
    /// entire bar range. A section present here (with a non-empty list) is compiled entirely from these
    /// assignments -- its own <paramref name="LookIdBySectionId"/> entry, if any, is then unused. A section
    /// absent here (the default, and every existing caller's own behavior) compiles exactly as before.
    /// </param>
    public sealed record Input(
        DisplayGraph Layout,
        BeatGrid BeatGrid,
        SectionLabelAnalysis Sections,
        PhraseSegmentationAnalysis Phrases,
        BandEnergyEnvelope? BandEnergy,
        VocalActivityEnvelope? VocalActivity,
        LookLibrary LookLibrary,
        IReadOnlyDictionary<Guid, string> LookIdBySectionId,
        IReadOnlyDictionary<SectionKind, double> EnergyCeilingByKind,
        long SongEndTicks,
        LookCompilerSettings CompilerSettings,
        SectionFillerSchedule FillerSchedule,
        int CoverageLadderRungCount = 7,
        IReadOnlyList<VisemeMark>? VisemeMarks = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<SectionPhraseLookAssignment>>? PhraseLookAssignmentsBySectionId = null,
        EnergyEnvelope? AudioEnergy = null);

    public sealed record Result(
        ChoreographyIR IR,
        IReadOnlyList<LegalityViolation> Violations,
        IReadOnlyList<PixelCoverageLadder.Rung> CoverageLadder,
        BarGrid BarGrid);

    public static Result Compile(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var barGrid = BarGrid.FromBeatGrid(input.BeatGrid, input.SongEndTicks);

        var tracks = new Dictionary<string, TimingTrack>(StringComparer.Ordinal)
        {
            ["Beats"] = TimingTrackEmitter.Beats(input.BeatGrid, input.SongEndTicks),
            ["Bars"] = TimingTrackEmitter.Bars(barGrid),
            ["Phrases"] = TimingTrackEmitter.Phrases(input.Phrases),
            ["Sections"] = TimingTrackEmitter.Sections(input.Sections, barGrid),
        };
        if (input.BandEnergy is not null)
        {
            tracks["Percussion"] = TimingTrackEmitter.Percussion(input.BandEnergy);
        }

        foreach (var (name, track) in LyricTimingTrackBuilder.BuildAll(input.Phrases, input.VocalActivity, input.VisemeMarks))
        {
            tracks[name] = track;
        }

        var mergedIR = new ChoreographyIR(tracks.Values.ToList(), [], []);
        var sectionAssignments = new List<ChoreographyLegalityValidator.SectionAssignment>();
        var violations = new List<LegalityViolation>();

        void CompileOneWindow(SectionLabelCandidate section, string lookId, TimeWindow window)
        {
            var look = input.LookLibrary.Find(lookId)
                ?? throw new ChoreographyCompilationException($"Look '{lookId}' assigned to section '{section.Id}' is not in the supplied library.");

            var ceiling = input.EnergyCeilingByKind.TryGetValue(section.Kind, out var c) ? c : 1.0;
            var vocalActive = SectionMusicContext.IsVocalActiveMajority(input.VocalActivity, window);
            var targetChangeRate = SectionDensityMapper.TargetChangeRatePerSecond(ceiling);

            var context = new SectionMusicContext(window, section.Kind, ceiling, barGrid, tracks, vocalActive, targetChangeRate);
            var compiled = LookCompiler.Compile(look, context, input.Layout, input.CompilerSettings);

            mergedIR = mergedIR.Merge(compiled);

            var targetedModels = compiled.Effects.Select(e => e.Target).Distinct().ToList();
            sectionAssignments.Add(new ChoreographyLegalityValidator.SectionAssignment(section.Id.ToString(), look, compiled, targetedModels, window, ceiling));
            violations.AddRange(ChoreographyLegalityValidator.ValidateSection(look, compiled, input.Layout, input.CompilerSettings.Vocabulary));
        }

        foreach (var section in input.Sections.Labels.OrderBy(l => l.StartBarIndex))
        {
            // Build-plan Phase 10 §3e: a section with one or more declared phrase-scoped assignments
            // compiles entirely from them (its own sub-windows, each its own Look) instead of one Look
            // spanning the whole section -- every section not named here (the default) compiles exactly
            // as before.
            if (input.PhraseLookAssignmentsBySectionId is { } phraseAssignmentsBySectionId &&
                phraseAssignmentsBySectionId.TryGetValue(section.Id, out var phraseAssignments) &&
                phraseAssignments.Count > 0)
            {
                foreach (var assignment in phraseAssignments)
                {
                    var phraseWindow = barGrid.WindowForBarRange(assignment.StartBar, assignment.EndBarExclusive)
                        ?? throw new ChoreographyCompilationException($"Phrase-scoped assignment for section '{section.Id}' bar range [{assignment.StartBar},{assignment.EndBarExclusive}) does not resolve against the bar grid.");

                    CompileOneWindow(section, assignment.LookId, phraseWindow);
                }

                continue;
            }

            if (!input.LookIdBySectionId.TryGetValue(section.Id, out var lookId))
            {
                continue; // No assignment for this section -- left uncompiled, never invented.
            }

            var window = barGrid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive))
                ?? throw new ChoreographyCompilationException($"Section '{section.Id}' bar range [{section.StartBarIndex},{section.EndBarIndexExclusive}) does not resolve against the bar grid.");

            CompileOneWindow(section, lookId, window);
        }

        // The always-moving rule applies globally, per target, across the whole song -- a gap can
        // straddle a section boundary, which no single section's own compile can see.
        var songWindow = new TimeWindow(0, input.SongEndTicks);
        var effectsByTarget = mergedIR.Effects.GroupBy(e => e.Target).ToDictionary(g => g.Key, g => (IReadOnlyList<EffectInstance>)g.ToList());

        var gapFilledEffects = new List<EffectInstance>();
        foreach (var (target, existing) in effectsByTarget)
        {
            // A whole-song-average energy ceiling for filler cadence -- a per-tick-aware cadence would
            // need this pass to re-enter each section's own declared ceiling, a disclosed simplification
            // since the always-moving rule's own hard requirement (max gap <= 1 bar) does not depend on it.
            var averageCeiling = sectionAssignments.Count == 0
                ? 0.5
                : sectionAssignments.Average(s => Math.Min(s.Look.EnergyCeiling, input.EnergyCeilingByKind.GetValueOrDefault(SectionKindOf(input.Sections, s.SectionId), 1.0)));

            gapFilledEffects.AddRange(AlwaysMovingGapSplitter.FillGaps(
                target, existing, songWindow, barGrid, tick => input.FillerSchedule.Resolve(tick, target), averageCeiling, input.CompilerSettings.RecipeRegistry));
        }

        mergedIR = mergedIR with { Effects = gapFilledEffects };

        var coverageLadder = PixelCoverageLadder.Build(input.Layout, input.CoverageLadderRungCount);
        violations.AddRange(ChoreographyLegalityValidator.ValidateShow(sectionAssignments, input.Layout, coverageLadder, input.AudioEnergy));

        return new Result(mergedIR, violations, coverageLadder, barGrid);
    }

    private static SectionKind SectionKindOf(SectionLabelAnalysis sections, string sectionId) =>
        sections.Labels.FirstOrDefault(l => l.Id.ToString() == sectionId)?.Kind ?? SectionKind.Unknown;
}
