using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Melody;
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
        EnergyEnvelope? AudioEnergy = null,
        IReadOnlyList<EmphasisInstant>? EmphasisInstants = null,
        EffectVisualCharacterCatalog? BrightnessCatalog = null,
        Random? EmphasisTravelRandom = null,
        IReadOnlyList<string>? HeroLookIds = null,
        /* Build-plan Phase 28a: an optional, additive override of LookIdBySectionId's own per-section
         * compile -- a section present here whose own SectionTimeline.CuesOrEmpty is non-empty compiles
         * via SectionTimelineCompiler.Compile instead of LookCompiler.Compile, merging into the exact
         * same ChoreographyIR and flowing through the exact same downstream AlwaysMovingGapSplitter/
         * EmphasisContrastPlanner/ChoreographyLegalityValidator.ValidateShow passes every other section's
         * compiled output already does -- never a parallel/bypassed pipeline. A section with no entry
         * (the default, and every pre-existing caller's own behavior), an entry with empty Cues, or a
         * section already compiled via PhraseLookAssignmentsBySectionId (that branch takes precedence,
         * same as it already does over LookIdBySectionId) compiles exactly as before.
         * TransientHits/VocalNotes/LeadInstrumentWindows below are consulted only when resolving a
         * SectionCue.AnchorId for a section named here. */
        IReadOnlyDictionary<Guid, SectionTimeline>? SectionTimelinesBySectionId = null,
        IReadOnlyList<TransientHit>? TransientHits = null,
        IReadOnlyList<NoteEvent>? VocalNotes = null,
        IReadOnlyList<LeadInstrumentWindow>? LeadInstrumentWindows = null);

    /// <param name="EmphasisContrast">
    /// Build-plan Phase 22 §2a/§2d: non-null exactly when both <see cref="Input.EmphasisInstants"/> and
    /// <see cref="Input.BrightnessCatalog"/> were supplied -- <see langword="null"/> (the default, and
    /// every pre-existing caller's own behavior) otherwise, never a fabricated empty report.
    /// </param>
    /// <param name="SectionAssignments">
    /// User feedback (2026-11-13): every section's own compiled assignment (window, Look, targeted
    /// models) -- exposed so a caller can determine, for any target, the real windows it was actually
    /// "in play" for (see <see cref="AlwaysMovingGapSplitter.FillGaps"/>'s own <c>activeWindows</c>
    /// parameter, which this same list already feeds internally) without re-deriving it.
    /// </param>
    public sealed record Result(
        ChoreographyIR IR,
        IReadOnlyList<LegalityViolation> Violations,
        IReadOnlyList<PixelCoverageLadder.Rung> CoverageLadder,
        BarGrid BarGrid,
        EmphasisContrastReport? EmphasisContrast = null,
        IReadOnlyList<ChoreographyLegalityValidator.SectionAssignment>? SectionAssignments = null);

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

        // User follow-up (2026-11-10): expose the same real detected transient hits/vocal-note onsets
        // this phase already carries (for EmphasisContrastPlanner's own later pass below) as real, named
        // timing tracks too -- a Look layer can now declare "Timescale": "Transients"/"VocalNotes" and
        // get LookCompiler's existing, unmodified Mode B discrete-emission machinery to react to them
        // directly, the same way it already reacts to Beats/Bars. No new compiler mechanism; only new
        // input data these generic lookups were always capable of consuming.
        if (input.EmphasisInstants is { Count: > 0 } emphasisInstantsForTracks)
        {
            var transientTrack = TimingTrackEmitter.Transients(emphasisInstantsForTracks, input.BeatGrid, input.SongEndTicks);
            if (transientTrack.Marks.Count > 0)
            {
                tracks["Transients"] = transientTrack;
            }

            var vocalNoteTrack = TimingTrackEmitter.VocalNotes(emphasisInstantsForTracks, input.BeatGrid, input.SongEndTicks);
            if (vocalNoteTrack.Marks.Count > 0)
            {
                tracks["VocalNotes"] = vocalNoteTrack;
            }
        }

        foreach (var (name, track) in LyricTimingTrackBuilder.BuildAll(input.Phrases, input.VocalActivity, input.VisemeMarks))
        {
            tracks[name] = track;
        }

        var mergedIR = new ChoreographyIR(tracks.Values.ToList(), [], []);
        var sectionAssignments = new List<ChoreographyLegalityValidator.SectionAssignment>();
        var violations = new List<LegalityViolation>();

        // Build-plan Phase 28b bugfix: a SectionTimeline's own Cues are generated/validated against the
        // REAL anchors of the section's own WHOLE window (see BuildSectionPromptMaterial/
        // SectionTimelineValidator, both section-scoped, never phrase-scoped). A phrase-scoped section
        // (PhraseLookAssignmentsBySectionId below) instead compiles as several NARROWER sub-window calls
        // to this same local function, each for one real phrase -- applying the whole-section timeline's
        // cues uniformly against every one of those narrower windows resolves a real anchor (valid for
        // the WHOLE section) against a phrase window that may not contain it at all, throwing a hard
        // ChoreographyCompilationException at compile time for an anchor the model never did anything
        // wrong to name (confirmed against a real Carol of the Bells run: AnchorId 'beat-25' valid for a
        // section's own full real mark set, invalid for one of its own real phrase sub-windows). A
        // phrase-scoped section's own cues are therefore never applied here -- same precedence
        // PhraseLookAssignmentsBySectionId already has over LookIdBySectionId itself (that branch fully
        // replaces this function's plain, whole-section call) now also applies to SectionTimelinesBySectionId.
        void CompileOneWindow(SectionLabelCandidate section, string lookId, TimeWindow window, bool allowSectionTimelineOverride)
        {
            var look = input.LookLibrary.Find(lookId)
                ?? throw new ChoreographyCompilationException($"Look '{lookId}' assigned to section '{section.Id}' is not in the supplied library.");

            var ceiling = input.EnergyCeilingByKind.TryGetValue(section.Kind, out var c) ? c : 1.0;
            var vocalActive = SectionMusicContext.IsVocalActiveMajority(input.VocalActivity, window);
            var targetChangeRate = SectionDensityMapper.TargetChangeRatePerSecond(ceiling);

            var context = new SectionMusicContext(window, section.Kind, ceiling, barGrid, tracks, vocalActive, targetChangeRate);

            ChoreographyIR compiled;
            if (allowSectionTimelineOverride &&
                input.SectionTimelinesBySectionId is { } timelinesBySectionId &&
                timelinesBySectionId.TryGetValue(section.Id, out var timeline) &&
                timeline.CuesOrEmpty.Count > 0)
            {
                var sectionPhrases = input.Phrases.Phrases
                    .Where(p => p.SectionLabelId == section.Id)
                    .OrderBy(p => p.StartBarIndex)
                    .ToList();
                compiled = SectionTimelineCompiler.Compile(
                    look, timeline, context, input.Layout, input.CompilerSettings,
                    sectionPhrases, input.TransientHits, input.VocalNotes, input.LeadInstrumentWindows);
            }
            else
            {
                compiled = LookCompiler.Compile(look, context, input.Layout, input.CompilerSettings);
            }

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

                    CompileOneWindow(section, assignment.LookId, phraseWindow, allowSectionTimelineOverride: false);
                }

                continue;
            }

            if (!input.LookIdBySectionId.TryGetValue(section.Id, out var lookId))
            {
                continue; // No assignment for this section -- left uncompiled, never invented.
            }

            var window = barGrid.WindowForBarRange(new BarIndex(section.StartBarIndex), new BarIndex(section.EndBarIndexExclusive))
                ?? throw new ChoreographyCompilationException($"Section '{section.Id}' bar range [{section.StartBarIndex},{section.EndBarIndexExclusive}) does not resolve against the bar grid.");

            CompileOneWindow(section, lookId, window, allowSectionTimelineOverride: true);
        }

        // The always-moving rule applies globally, per target, across the whole song -- a gap can
        // straddle a section boundary, which no single section's own compile can see.
        var songWindow = new TimeWindow(0, input.SongEndTicks);
        var effectsByTarget = mergedIR.Effects.GroupBy(e => e.Target).ToDictionary(g => g.Key, g => (IReadOnlyList<EffectInstance>)g.ToList());

        // User feedback (2026-11-13): the real per-tick section ceiling, not a single flattened
        // whole-song average -- a quiet section now reliably gets the full low-ceiling filler fraction
        // ComputeFillerDurationTicks already allows it, rather than sometimes getting a tighter cap
        // because some OTHER section elsewhere in the song happened to be louder.
        double CeilingFor(ChoreographyLegalityValidator.SectionAssignment assignment) =>
            Math.Min(assignment.Look.EnergyCeiling, input.EnergyCeilingByKind.GetValueOrDefault(SectionKindOf(input.Sections, assignment.SectionId), 1.0));

        var fallbackCeiling = sectionAssignments.Count == 0 ? 0.5 : sectionAssignments.Average(CeilingFor);

        double CeilingAtTick(long tick)
        {
            var containing = sectionAssignments.FirstOrDefault(s => s.Window is { } w && tick >= w.StartTicks && tick < w.EndTicks);
            return containing is not null ? CeilingFor(containing) : fallbackCeiling;
        }

        var gapFilledEffects = new List<EffectInstance>();
        foreach (var (target, existing) in effectsByTarget)
        {
            // User feedback (2026-11-13), measured against a real reference show (generated: 0.0%
            // average idle time per prop; reference: 82.1% idle): scoping the always-moving guarantee to
            // only the exact windows a section's compiled Look happened to name `target` directly was
            // tried and reverted (user feedback 2026-11-16, seeing most of the display go dark for most
            // of the song once that shipped): "we still need other models or groups doing something
            // unless music really has a large change." A `DeclaredDarkness` section IS that large
            // change -- a Look's own deliberate negative-space moment -- so it alone opts a target out of
            // this guarantee for its window; every other section `target` has at least one real effect in
            // somewhere in the show keeps the always-moving guarantee for every one of its own sections,
            // not only the ones that happened to name it directly.
            var activeWindows = sectionAssignments
                .Where(s => s.Window is not null && !s.Look.DeclaredDarkness)
                .Select(s => s.Window!.Value)
                .OrderBy(w => w.StartTicks)
                .ToList();

            gapFilledEffects.AddRange(AlwaysMovingGapSplitter.FillGaps(
                target, existing, songWindow, barGrid, tick => input.FillerSchedule.Resolve(tick, target), activeWindows, CeilingAtTick, input.CompilerSettings.RecipeRegistry));
        }

        mergedIR = mergedIR with { Effects = gapFilledEffects };

        // Build-plan Phase 22 (contrast-driven emphasis): §2b (strong Choreographer prompt guidance) is
        // the primary mechanism and is never re-implemented here -- this is purely the §2c measurement +
        // §2d bounded, deterministic repair of whatever real emphasis instants still measure flat after
        // the section scripts have already compiled. Both are no-ops (and EmphasisContrast stays null)
        // when the caller supplies neither real instants nor a real brightness catalog, so every
        // pre-existing caller/test compiles and behaves identically.
        EmphasisContrastReport? emphasisContrastReport = null;
        if (input.EmphasisInstants is { Count: > 0 } instants && input.BrightnessCatalog is { } brightnessCatalog)
        {
            var sectionWindows = sectionAssignments.Where(s => s.Window is not null).Select(s => s.Window!.Value).ToList();
            var (patchedIr, report) = EmphasisContrastPlanner.ApplyContrastFallback(
                mergedIR, instants, sectionWindows, input.BeatGrid, input.Layout, input.CompilerSettings.RecipeCatalog,
                brightnessCatalog, input.CompilerSettings.RecipeRegistry, input.EmphasisTravelRandom ?? new Random());

            mergedIR = patchedIr;
            emphasisContrastReport = report;

            violations.AddRange(ChoreographyLegalityValidator.ValidateEmphasisContrast(
                mergedIR, instants, input.BeatGrid, input.CompilerSettings.RecipeCatalog, brightnessCatalog));
        }

        var coverageLadder = PixelCoverageLadder.Build(input.Layout, input.CoverageLadderRungCount);
        violations.AddRange(ChoreographyLegalityValidator.ValidateShow(sectionAssignments, input.Layout, coverageLadder, input.AudioEnergy, input.HeroLookIds));

        return new Result(mergedIR, violations, coverageLadder, barGrid, emphasisContrastReport, sectionAssignments);
    }

    private static SectionKind SectionKindOf(SectionLabelAnalysis sections, string sectionId) =>
        sections.Labels.FirstOrDefault(l => l.Id.ToString() == sectionId)?.Kind ?? SectionKind.Unknown;
}
