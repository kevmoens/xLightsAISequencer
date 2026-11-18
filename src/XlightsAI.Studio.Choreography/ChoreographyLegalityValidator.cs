using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 4 ("Compositional legality checks"). Architecture §7's own enumerated rules,
/// implemented as independent, composable checks over an already-compiled <see cref="ChoreographyIR"/> --
/// never thrown as an exception, always returned as a full list of every violation found in one pass
/// (architecture §9's own "guardrails, not a quality score" framing: a violation is a finding, not a
/// silent repair).
/// </summary>
/// <remarks>
/// <b>Disclosed, deliberately out-of-scope sub-rule:</b> architecture §7's "transition settings respect
/// the capability matrix" names a verified no-Adjust effect set and a Blur-eligible set, but its own
/// no-Reverse set is explicitly left unnamed ("a further set takes no Reverse") -- and <see cref="EffectTransition"/>
/// does not yet carry Adjust/Blur fields at all (both remain IR-only/unserialized per its own doc
/// comment). Checking this rule today would mean inventing data this build has never verified, which the
/// project's own discipline forbids ("do not invent xLights effect parameters") -- so it is not
/// implemented here, rather than faked.
/// </remarks>
public static class ChoreographyLegalityValidator
{
    private const string RuleParameterValidity = "parameter-validity";
    private const string RuleResolutionMatch = "resolution-aware-effect-matching";
    private const string RuleEasing = "easing-on-every-ramp";
    private const string RuleHardCutOrFade = "hard-cut-for-pops-fade-for-builds";
    private const string RulePeakUniqueness = "peak-reserved-look-binds-one-section";
    private const string RulePixelHeavyRequired = "never-schedule-without-a-pixel-heavy-prop";
    private const string RuleDeclaredDarkness = "declared-vs-accidental-darkness";
    private const string RuleSectionDensityContrast = "declared-vs-measured-change-density";
    private const string RuleEffectConcentration = "whole-song-effect-concentration";
    private const string RuleSilenceCrossValidation = "declared-darkness-vs-measured-audio-silence";

    /// <summary>
    /// Build-plan Phase 26: docs/MASTER-SYSTEM-ARCHITECTURE.md §9a's own disclosed gap -- the archived
    /// <c>XlightsAI.Studio.Composition.VisualBudget</c>'s density/concurrency-capping role was deliberately
    /// never ported, leaving nothing deterministic checking "how many props are simultaneously carrying an
    /// attention-grabbing effect at any one instant." Soft/advisory, same framing as every rule above except
    /// <see cref="RuleModelCoverage"/> -- deliberately NOT wired into <c>RevisionDirectiveDeriver</c>'s
    /// self-healing retry path. RuleModelCoverage escalated to self-healing because a prop silently getting
    /// zero effects for a whole show is an unambiguous content gap (user feedback 2026-10-31); "too many
    /// props fighting for attention at once" is a judgment call about busyness, the same soft-quality
    /// territory <see cref="RuleEffectConcentration"/> and <see cref="RuleSectionDensityContrast"/> already
    /// occupy -- a finding worth surfacing, never one deterministic enough to drive an automatic retry.
    /// </summary>
    private const string RuleConcurrentFocalEffectCap = "concurrent-focal-effect-cap";

    /// <summary>
    /// Build-plan Phase 26: the fraction of its own containing section's whole window an effect instance
    /// must span before <see cref="IsFocalEffect"/> calls it a steady-state base-layer "hold" rather than a
    /// momentary, attention-grabbing "focal" gesture -- the "hold is a steady state, never a dead one" rule
    /// already named by <c>SectionScriptPromptBuilder</c>'s own prompt guidance (a long color-wash/VU-reactive
    /// layer spanning most of a section, with shorter accents layered on top). Deliberately well above half
    /// so an ordinary multi-bar accent is never mistaken for a hold -- only a layer that genuinely spans
    /// most of the section counts as steady-state.
    /// </summary>
    private const double HoldCoverageThreshold = 0.6;

    /// <summary>
    /// Build-plan Phase 26: how many distinct models may simultaneously carry an active focal effect (see
    /// <see cref="IsFocalEffect"/>) before <see cref="RuleConcurrentFocalEffectCap"/> flags the instant as
    /// overcrowded, for an ordinary (non-<see cref="DenseSectionKinds"/>) section. No real corpus
    /// measurement of cross-model (as opposed to per-model) simultaneous-attention load exists --
    /// <c>LayerComplexityAnalyzer</c>/<c>LayerComplexityStats</c> (build-plan Phase 25 Part C/D.1) measure
    /// concurrent *layers stacked on one model*, never concurrent *focal models*. Rather than fabricate an
    /// unrelated number, this reuses that same real, corpus-calibrated complexity floor
    /// (<c>LayerComplexityAnalyzer.DefaultMinDistinctLayers</c> = 3, "the complexity bar... 3+, not 2+")
    /// one tier higher: a real 31-sequence training run (build-plan Phase 25 Part C) measured a sharp
    /// drop-off per additional concurrent-layer tier on a single model -- 551 windows at 3+, 290 at 4+,
    /// only 151 at 5+ -- so this rule's own cap sits one tier above that floor (allow up to 4 concurrent
    /// focal models, flag at 5+), the same "comfortably past the point real corpus practice itself got
    /// rare" framing <see cref="MaxSingleEffectShare"/> above already uses, applied cross-model instead of
    /// fabricating a second unrelated constant.
    /// </summary>
    private const int DefaultMaxConcurrentFocalModels = 4;

    /// <summary>
    /// One higher than <see cref="DefaultMaxConcurrentFocalModels"/> -- <c>PatternLibrary.DensityBySection</c>
    /// (build-plan Phase 2b §2b.8) measures Chorus/Bridge/Solo-kind sections as reliably denser
    /// (effect-starts/second) than Intro/Verse/PreChorus/Outro/Unknown ones, so a song's own busiest,
    /// most climactic moments earn one more simultaneously-focal prop before this rule flags them, rather
    /// than one flat cap applied to a show's loudest and quietest sections alike.
    /// </summary>
    private const int DensePeakSectionMaxConcurrentFocalModels = DefaultMaxConcurrentFocalModels + 1;

    /// <summary>Section kinds <see cref="DensePeakSectionMaxConcurrentFocalModels"/> applies to -- the peak-energy kinds this codebase already singles out for the loudest treatment (e.g. <c>StylePreset</c>'s own energy-ceiling guidance).</summary>
    private static readonly IReadOnlyCollection<SectionKind> DenseSectionKinds = new HashSet<SectionKind> { SectionKind.Chorus, SectionKind.Bridge, SectionKind.Solo };

    /// <summary>
    /// Build-plan Phase 22 §2c: public (same reason as <see cref="RuleModelCoverage"/>) so a caller can
    /// filter <see cref="LegalityViolation"/>s for diagnostics/reporting -- permanently advisory, confirmed
    /// by the user never to escalate into a blocking/retrying rule in this phase, unlike
    /// <see cref="RuleModelCoverage"/>'s own self-healing use.
    /// </summary>
    public const string RuleContrastAtEmphasis = "contrast-at-emphasis";

    /// <summary>
    /// Public (unlike every other rule id here) because <see cref="XlightsAI.Studio.Director.RevisionDirectiveDeriver"/>
    /// filters <see cref="ValidateShow"/>'s output by this id to derive a self-healing targeted
    /// regeneration directive for each uncovered model (see <see cref="LegalityViolation.AffectedModelId"/>)
    /// -- user feedback 2026-10-31 that Arch-category props were silently ending up with zero effects.
    /// </summary>
    public const string RuleModelCoverage = "every-model-used-at-least-once";

    /// <summary>
    /// Public (same reason as <see cref="RuleModelCoverage"/>) so <see cref="XlightsAI.Studio.Director.RevisionDirectiveDeriver"/>
    /// can derive a self-healing targeted regeneration directive for each affected model. User feedback
    /// (2026-11-15), confirmed against a real generation ("Carol of the Bells"): a 3-member group
    /// ("Matrixes", containing "Matrix"/"Matrix-2"/"Matrix3") received exactly one group-level effect from
    /// one section, which <see cref="RuleModelCoverage"/>'s own group-expansion correctly (and
    /// deliberately) counts as covering every member -- real pixels genuinely did light up for all three.
    /// But two of those three members ("Matrix"/"Matrix3") never received a single effect that named them
    /// directly, individually, anywhere else in the whole show -- the Show Bible itself had written
    /// dedicated Looks naming them individually, but none were ever chosen for any section. A viewer
    /// scrubbing that model's own row in xLights sees it completely empty for the entire song, even though
    /// <see cref="RuleModelCoverage"/> reports no violation. Escalated to self-healing (same tier as
    /// <see cref="RuleModelCoverage"/>) for the same reason that rule was escalated: a model with no moment
    /// of its own is an unambiguous content gap, not a busyness judgment call, regardless of whether some
    /// OTHER group it happens to belong to was used elsewhere. Only groups with more than one real member
    /// are checked -- for a single-member group, "the group's own effect" and "this model's own effect" are
    /// the same thing, so there is nothing distinct to flag.
    /// </summary>
    public const string RuleModelOnlyGroupCovered = "model-covered-only-via-group";

    /// <summary>
    /// Public (same reason as <see cref="RuleModelCoverage"/>) so <see cref="XlightsAI.Studio.Director.RevisionDirectiveDeriver"/>
    /// can derive a self-healing targeted regeneration directive naming the specific unused Look. User
    /// feedback (2026-11-15), confirmed against a real generation ("Carol of the Bells"): the Director's
    /// own Show Bible deliberately authors a small set of "hero" Looks it considers unmissable -- each
    /// <c>PeakMoment</c> ("the 2-4 bars that must be unmistakable") and each <c>Withholding.HeldBackLookIds</c>
    /// entry (the Look reserved for the final-chorus "ace") -- but nothing previously checked whether the
    /// independent, parallel per-section selection (13 sections, no shared memory of what the other 12
    /// picked) ever actually chose one. In a real run, the Director wrote dedicated Looks for both the
    /// whole-house ("group:Everything") and whole-matrix ("group:Matrixes") payoff moments and marked them
    /// as the song's peak/held-back ace -- and neither was ever selected by any of the 13 sections, so the
    /// show's own declared climax never happened. Checked only when the caller actually supplies
    /// <see cref="ValidateShow"/>'s own <c>heroLookIds</c> parameter (a caller that predates this knows
    /// nothing about Show Bible hero Looks, so is simply skipped, same discipline as <c>audioEnergy</c>).
    /// Escalated to self-healing (same tier as <see cref="RuleModelCoverage"/>) because a show's own
    /// declared, author-identified climax silently never firing is exactly the kind of unambiguous content
    /// gap that tier already exists for.
    /// </summary>
    public const string RuleHeroLookNeverUsed = "hero-look-never-used";

    /// <summary>
    /// User feedback (2026-11-14), confirmed against a real generation that "stayed on a single model":
    /// soft/advisory only (same framing as <see cref="RuleModelCoverage"/>/the effect-concentration and
    /// change-density checks below -- never blocks a compile), never a hard floor since no real
    /// calibrated "good" number exists yet for how often a group's own individual members should be
    /// named directly. See the real computation below for exactly what it measures.
    /// </summary>
    public const string RuleIndividualModelDiversity = "individual-model-diversity";

    /// <summary>A real group needs at least this many positioned members before "only one of them is ever named directly" is even a meaningful observation -- same threshold <c>XlightsAI.Studio.Director.LayoutSummary</c>'s own <c>HasMultiModelGroupWithKnownPositions</c> uses for the same real precondition a <see cref="Look.Travel"/> sweep needs.</summary>
    private const int MinPositionedMembersForDiversityCheck = 3;

    /// <summary>
    /// Build-plan Phase 27 (2026-11-14), confirmed against a real generation: a real named group
    /// (e.g. "Everything," "Roof," "Horizontals") can go completely untargeted as its OWN group-level
    /// effect for an entire show while <see cref="RuleModelCoverage"/> stays silent (its members
    /// happen to be covered by some other effect entirely) and <see cref="RuleIndividualModelDiversity"/>
    /// also stays silent (it only looks at per-MEMBER targeting, never at the group's own identity as
    /// a target). Soft/advisory only, same tier as the other two -- no calibrated "how often should a
    /// real group fire" number exists yet, and unlike <see cref="RuleIndividualModelDiversity"/> this
    /// applies to every real group regardless of member count or position data (a 1-member or
    /// unpositioned group can still genuinely never be used as its own group-level effect).
    /// </summary>
    public const string RuleGroupNeverTargetedDirectly = "group-never-targeted-directly";

    /// <summary>
    /// User feedback (2026-11-16), confirmed against a real generation ("much of the sequence... only a
    /// single model with an effect"): the 2026-11-13 fix that scoped <see cref="AlwaysMovingGapSplitter"/>'s
    /// own always-moving rule to a target's real "in play" windows removed the blanket-filler mask that
    /// used to hide this -- a section whose own Look only ever lights one real model for its entire
    /// window now reads exactly as sparse as it always compiled, with nothing left papering over it.
    /// User feedback (2026-11-16), after seeing this still happen across most of a real generated show:
    /// escalated from a purely soft/advisory finding to a real, self-healing targeted regeneration --
    /// same tier as <see cref="RuleModelCoverage"/> -- via <see cref="XlightsAI.Studio.Director.RevisionDirectiveDeriver.FromSectionModelBreadthViolations"/>.
    /// Still never blocks a compile outright; it drives one targeted re-prompt of the exact affected
    /// section (see <see cref="LegalityViolation.AffectedSectionId"/>), same single-repair-pass
    /// philosophy as every other self-healing rule.
    /// <para>
    /// No real calibrated number exists for "how many distinct models should a section light at
    /// minimum" -- the one real measurement this codebase has for section richness,
    /// <c>director-lessons/layer-count-minimum.md</c>'s own corpus comparison, is about concurrent
    /// LAYERS stacked on one subject prop, a different axis from distinct MODELS lit across the whole
    /// display. <see cref="MinDistinctModelsPerSection"/>/<see cref="MinDistinctModelsPerDenseSection"/>
    /// are therefore a disclosed, deliberately-raised floor (user feedback 2026-11-16: "multiple at a
    /// time on multiple models... more and more"), not a corpus-derived one.
    /// </para>
    /// </summary>
    public const string RuleSectionModelBreadth = "section-model-breadth";

    /// <summary>Minimum distinct real (group-expanded) models a non-dense, non-DeclaredDarkness section's own compiled IR should light across its whole window before <see cref="RuleSectionModelBreadth"/> flags it.</summary>
    private const int MinDistinctModelsPerSection = 3;

    /// <summary>User feedback (2026-11-16): two higher, not one, for <see cref="DenseSectionKinds"/> -- a show's own loudest, most climactic moments should light noticeably more of the display, several models carrying something at once, not just barely clear the ordinary floor.</summary>
    private const int MinDistinctModelsPerDenseSection = MinDistinctModelsPerSection + 2;

    /// <summary>
    /// Build-plan Phase 28a: public (same reason as <see cref="RuleModelCoverage"/>'s own publicness) so
    /// <see cref="SectionTimelineValidator"/>'s pre-compile concurrency rule can reuse this rule's own
    /// real, named floor instead of redefining a second, drift-prone copy of the same numbers.
    /// </summary>
    public static int MinDistinctModelsForSectionKind(SectionKind? kind) =>
        kind is { } k && DenseSectionKinds.Contains(k) ? MinDistinctModelsPerDenseSection : MinDistinctModelsPerSection;

    /// <summary>100ns ticks/second -- same unit as every other tick field in this codebase; used only by <see cref="RuleSectionDensityContrast"/> to turn a section's own compiled effect count into a real starts-per-second rate.</summary>
    private const long TicksPerSecond = 10_000_000L;

    /// <summary>
    /// Build-plan Phase 12 §3c: how much quieter (by measured change-rate) the quietest declared-ceiling
    /// section must be than the loudest before the two are considered "measurably" different -- the
    /// quietest section's own rate must fall under this fraction of the loudest's, or the pair is flagged.
    /// Deliberately lenient (a real ~5x target spread, architecture §10's own declared-ceiling range, would
    /// clear this trivially) -- this is a guardrail against a flat show, not a tight quality bar.
    /// </summary>
    private const double MaxQuietToLoudRateRatio = 0.8;

    /// <summary>
    /// Build-plan Phase 13 §2c: the most any single xLights effect name is allowed to account for of every
    /// compiled effect in a whole song before <see cref="RuleEffectConcentration"/> flags it -- soft/advisory,
    /// same framing as every other <c>ValidateShow</c> rule. Calibrated against the 2026-10-30 real-run
    /// comparison's own measured ~37% combined share for the AI-generated version's top 2 effects (a single
    /// effect alone was not even reported separately because it never needed to be -- this threshold is
    /// comfortably below that already-flagged combined figure, so a genuinely concentrated single effect
    /// trips it without false-flagging a merely-popular one).
    /// </summary>
    private const double MaxSingleEffectShare = 0.35;

    /// <summary>A song needs at least this many real, name-resolved effects before <see cref="RuleEffectConcentration"/> bothers computing a share -- a 1-2 effect sample is too small for "concentration" to mean anything.</summary>
    private const int MinEffectsForConcentrationCheck = 10;

    /// <summary>Below this duty-corrected pixel coverage, a section's own compiled visuals count as "dark" -- shared by <see cref="RuleDeclaredDarkness"/> and <see cref="RuleSilenceCrossValidation"/> so both rules agree on what "dark" means.</summary>
    private const double VisualDarknessThreshold = 0.02;

    /// <summary>
    /// Build-plan "richer analysis tools" Phase 5 (2026-11-02): above this real, measured
    /// <see cref="EnergyFrame.NormalizedEnergy"/> average, a section's own audio counts as clearly
    /// audible ("not quiet") for <see cref="RuleSilenceCrossValidation"/>'s own purposes. Deliberately well
    /// above a true-silence floor so an only-mildly-quiet section is never mistaken for loud.
    /// </summary>
    private const double AudibleEnergyThreshold = 0.3;

    /// <summary>Effects needing many pixels to read as intended (fine textural/gradient/continuous material) -- architecture §7: "high-resolution props get continuous, gradient, textural material... low-resolution props get discrete, graphic material."</summary>
    public static readonly IReadOnlyCollection<string> TexturalHighResolutionEffects = new HashSet<string>(StringComparer.Ordinal)
    {
        "Fire", "Liquid", "Text", "Pictures", "Shader", "Kaleidoscope", "Galaxy", "Life", "Snowstorm",
    };

    /// <summary>A prop below this many pixels is "low-resolution" for the purpose of <see cref="RuleResolutionMatch"/> -- architecture's own named examples (a 20px MiniStar, a ~200px Arch) are well under it; a few-hundred-pixel tree/matrix is well over.</summary>
    public const int LowResolutionNodeCountThreshold = 60;

    /// <summary>Validates one compiled section's own IR against the Look it was compiled from -- parameter validity, resolution-aware matching, easing-on-ramps and hard-cut/fade discipline.</summary>
    public static IReadOnlyList<LegalityViolation> ValidateSection(
        Look look, ChoreographyIR ir, DisplayGraph layout, EffectVocabularyCatalog vocabulary)
    {
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(vocabulary);

        var violations = new List<LegalityViolation>();
        var validator = new EffectSettingsValidator(vocabulary);
        var modelsById = layout.Models.ToDictionary(m => m.Id);

        foreach (var effect in ir.Effects)
        {
            // Parameter validity (A).
            if (!modelsById.TryGetValue(effect.Target, out var model))
            {
                continue; // Not this validator's concern -- XsqExporter itself rejects an unresolved target.
            }

            var xlightsEffectName = ResolveXlightsEffectName(effect.RecipeId, look);
            if (xlightsEffectName is not null)
            {
                foreach (var error in validator.ValidateSettings(xlightsEffectName, effect.Parameters))
                {
                    violations.Add(new LegalityViolation(RuleParameterValidity,
                        $"'{effect.Target.Value}' effect '{xlightsEffectName}': {error.Message}"));
                }

                // Resolution-aware matching (C).
                if (TexturalHighResolutionEffects.Contains(xlightsEffectName) && model.NodeCount < LowResolutionNodeCountThreshold)
                {
                    violations.Add(new LegalityViolation(RuleResolutionMatch,
                        $"'{effect.Target.Value}' ({model.NodeCount}px) runs textural effect '{xlightsEffectName}', which needs many pixels to read as intended -- expensive prop doing a cheap prop's job."));
                }
            }

            // Easing on every ramp (D): a literal "Ramp" value curve with no eased alternative is the
            // "most reliable tell of machine sequencing" (architecture §7) -- flagged, never silently
            // allowed, for every curve this effect carries.
            foreach (var curve in effect.ValueCurvesOrEmpty)
            {
                if (string.Equals(curve.CurveType, "Ramp", StringComparison.Ordinal))
                {
                    violations.Add(new LegalityViolation(RuleEasing,
                        $"'{effect.Target.Value}' parameter '{curve.ParameterKey}' uses a linear 'Ramp' curve -- use an eased alternative (Sine/Decaying Sine/Logarithmic/Exponential/...) unless linear is deliberate."));
                }
            }
        }

        // Hard cuts for pops, fades for builds (E).
        var motion = look.Motion;
        if (string.Equals(motion, "pop", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var layer in look.Layers.Where(l => l.TransitionIn is not null))
            {
                violations.Add(new LegalityViolation(RuleHardCutOrFade,
                    $"Look '{look.Id}' is tagged motion='pop' but layer z={layer.Z} declares a TransitionIn -- pops must be hard cuts, not fades."));
            }
        }
        else if (string.Equals(motion, "build", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var layer in look.Layers.Where(l => l.TransitionIn is null))
            {
                violations.Add(new LegalityViolation(RuleHardCutOrFade,
                    $"Look '{look.Id}' is tagged motion='build' but layer z={layer.Z} declares no TransitionIn -- builds must fade, not hard-cut."));
            }
        }

        return violations;
    }

    /// <summary>
    /// One Look assignment in a whole-song compile -- the unit <see cref="ValidateShow"/>'s own
    /// whole-song checks (peak uniqueness, pixel-heavy coverage, declared darkness, section density
    /// contrast) operate over.
    /// </summary>
    /// <param name="Window">
    /// Build-plan Phase 12 §3c: this section's own compiled time window, or <see langword="null"/> for a
    /// caller that predates this phase (every existing direct construction in tests) -- <see cref="RuleSectionDensityContrast"/>
    /// skips any section missing this, rather than inventing a duration to measure a rate against.
    /// </param>
    /// <param name="EnergyCeiling">
    /// Build-plan Phase 12 §3c: this section's own declared energy ceiling (the same value <c>ChoreographyEngine</c>
    /// derived from <c>EnergyCeilingByKind</c>, before being minned against the Look's own ceiling) -- <see langword="null"/>
    /// for a caller that predates this phase. <see cref="RuleSectionDensityContrast"/> skips any section
    /// missing this the same way it skips a missing <see cref="Window"/>.
    /// </param>
    /// <param name="Kind">
    /// Build-plan Phase 26: this section's own classified <see cref="SectionKind"/>, or <see langword="null"/>
    /// for a caller that predates this phase. Only consulted by <see cref="RuleConcurrentFocalEffectCap"/>, to
    /// pick between <see cref="DefaultMaxConcurrentFocalModels"/> and <see cref="DensePeakSectionMaxConcurrentFocalModels"/>
    /// -- a section missing this simply gets the default (non-dense) cap, never a guessed kind.
    /// </param>
    public sealed record SectionAssignment(
        string SectionId, Look Look, ChoreographyIR CompiledIR, IReadOnlyList<ModelId> TargetedModels,
        TimeWindow? Window = null, double? EnergyCeiling = null, SectionKind? Kind = null);

    /// <summary>Validates whole-song concerns that no single section's own compile can check in isolation: peak-Look uniqueness, pixel-heavy coverage, and declared-vs-accidental darkness.</summary>
    /// <param name="audioEnergy">
    /// Build-plan "richer analysis tools" Phase 5 (2026-11-02): this song's own real, measured broadband
    /// <see cref="EnergyEnvelope"/>, or <see langword="null"/> (the default, and every pre-existing
    /// caller's behavior) when none is available. Only consulted by <see cref="RuleSilenceCrossValidation"/>
    /// -- every other rule here is unaffected by its presence or absence.
    /// </param>
    /// <param name="heroLookIds">
    /// User feedback (2026-11-15): the Show Bible's own declared "unmissable" Look ids -- every
    /// <c>PeakMoment.LookId</c> plus every <c>WithholdingPlan.HeldBackLookIds</c> entry, or
    /// <see langword="null"/> (the default, and every pre-existing caller's behavior) when the caller has
    /// no Show Bible to draw them from. Only consulted by <see cref="RuleHeroLookNeverUsed"/>.
    /// </param>
    public static IReadOnlyList<LegalityViolation> ValidateShow(
        IReadOnlyList<SectionAssignment> sections, DisplayGraph layout, IReadOnlyList<PixelCoverageLadder.Rung> coverageLadder,
        EnergyEnvelope? audioEnergy = null, IReadOnlyList<string>? heroLookIds = null)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(coverageLadder);

        var violations = new List<LegalityViolation>();

        // Hero Look (the Show Bible's own declared "unmissable" moments) never used by any section --
        // see RuleHeroLookNeverUsed's own doc comment. Skipped entirely (not even attempted) when the
        // caller supplies no heroLookIds at all, same discipline RuleSilenceCrossValidation already
        // applies to a missing audioEnergy.
        if (heroLookIds is { Count: > 0 })
        {
            var usedLookIds = sections.Select(s => s.Look.Id).ToHashSet();
            foreach (var heroLookId in heroLookIds.Distinct())
            {
                if (!usedLookIds.Contains(heroLookId))
                {
                    violations.Add(new LegalityViolation(RuleHeroLookNeverUsed,
                        $"Look '{heroLookId}' is one of the Show Bible's own declared peak/held-back moments, but no section ever selected it -- the show's own intended climax never happened.",
                        AffectedModelId: heroLookId));
                }
            }
        }

        // Peak Look uniqueness (F): "a Look reserved for a peak binds to exactly one section."
        var reservedGroups = sections
            .Where(s => s.Look.Uniqueness is { Length: > 0 } u && u.StartsWith("reserved:", StringComparison.Ordinal))
            .GroupBy(s => s.Look.Uniqueness!);
        foreach (var group in reservedGroups)
        {
            var distinctLookIds = group.Select(s => s.Look.Id).Distinct().ToList();
            foreach (var lookId in distinctLookIds)
            {
                var sectionsUsingIt = group.Where(s => s.Look.Id == lookId).Select(s => s.SectionId).Distinct().ToList();
                if (sectionsUsingIt.Count > 1)
                {
                    violations.Add(new LegalityViolation(RulePeakUniqueness,
                        $"Look '{lookId}' (uniqueness '{group.Key}') is bound to {sectionsUsingIt.Count} sections ({string.Join(", ", sectionsUsingIt)}); a reserved Look must bind to exactly one."));
                }
            }
        }

        // Never an outline-only/small-props-only section without at least one pixel-heavy prop (G).
        // Confirmed against a real group-targeted Look (architecture §10/Phase 8's own group-collapse
        // optimization, LookCompiler): section.TargetedModels can itself be a real group's own ModelId
        // (e.g. "Matrixes"), which is never a literal member of pixelHeavyModels (an individually-
        // addressable-only set, by this ladder's own deliberate design -- see PixelCoverageLadder's own
        // doc comment). ResolveRealPixelWeight below is the group-aware fallback: a group whose own
        // combined real pixel weight clears the same threshold counts as pixel-heavy too, even though no
        // single member alone might.
        var pixelHeavyThreshold = coverageLadder.Count > 0 ? coverageLadder[^1].TotalPixels / 10.0 : 0;
        var pixelHeavyModels = new HashSet<ModelId>(coverageLadder.Count > 0 ? coverageLadder[^1].Models.Where(
            m => layout.Models.First(dm => dm.Id == m).NodeCount >= pixelHeavyThreshold) : []);
        foreach (var section in sections)
        {
            if (section.TargetedModels.Count > 0 &&
                !section.TargetedModels.Any(t => pixelHeavyModels.Contains(t) || ResolveRealPixelWeight(t.Value, layout) >= pixelHeavyThreshold))
            {
                violations.Add(new LegalityViolation(RulePixelHeavyRequired,
                    $"Section '{section.SectionId}' (Look '{section.Look.Id}') targets only small/outline props -- never a fully-small-props-only section without at least one pixel-heavy prop."));
            }
        }

        // Declared vs. accidental darkness (H): a section compiling to near-zero duty-corrected coverage
        // whose own Look never declared that as intentional is a defect, not negative space.
        //
        // Build-plan "richer analysis tools" Phase 5 (2026-11-02): when a real audioEnergy envelope is
        // available, also cross-validates visual darkness against this song's own real, measured audio in
        // the same window (L) -- catching both an undeclared-dark section that is actually audible, and a
        // deliberately-declared-dark Look applied where the real audio is not actually quiet (a plausible
        // sign the Look/section pairing is a mismatch). Soft/advisory, same framing as every rule above;
        // skipped entirely (not even attempted) when audioEnergy or the section's own Window is unavailable.
        foreach (var section in sections)
        {
            var dutyCorrectedCoverage = EstimateDutyCorrectedCoverage(section.CompiledIR, layout);
            var isVisuallyDark = dutyCorrectedCoverage < VisualDarknessThreshold;
            if (isVisuallyDark && !section.Look.DeclaredDarkness)
            {
                violations.Add(new LegalityViolation(RuleDeclaredDarkness,
                    $"Section '{section.SectionId}' (Look '{section.Look.Id}') compiles to near-zero duty-corrected pixel coverage ({dutyCorrectedCoverage:P1}) without DeclaredDarkness set -- undeclared darkness is a defect, declared darkness is negative space."));
            }

            if (audioEnergy is not null && section.Window is { } window)
            {
                var measuredEnergy = AverageNormalizedEnergyInWindow(audioEnergy, window.StartTicks, window.EndTicks);
                if (measuredEnergy > AudibleEnergyThreshold)
                {
                    if (isVisuallyDark && !section.Look.DeclaredDarkness)
                    {
                        violations.Add(new LegalityViolation(RuleSilenceCrossValidation,
                            $"Section '{section.SectionId}' (Look '{section.Look.Id}') compiles visually dark ({dutyCorrectedCoverage:P1} coverage) without DeclaredDarkness, but the real audio in this window measures {measuredEnergy:0.00} normalized energy -- not actually quiet."));
                    }
                    else if (section.Look.DeclaredDarkness)
                    {
                        violations.Add(new LegalityViolation(RuleSilenceCrossValidation,
                            $"Section '{section.SectionId}' (Look '{section.Look.Id}') declares DeclaredDarkness, but the real audio in this window measures {measuredEnergy:0.00} normalized energy -- not actually quiet; a declared-dark Look applied where the music is audible."));
                    }
                }
            }
        }

        // Section model breadth -- see RuleSectionModelBreadth's own doc comment (the lower-bound twin
        // of RuleConcurrentFocalEffectCap, M, below). Grouped by the real SectionId (build-plan Phase 10
        // §3e's own phrase-scoped sub-windows compile to several separate SectionAssignment entries
        // sharing one SectionId -- a deliberate narrowing in a LATER phrase, e.g. "phrase 1 establishes
        // the Look, phrase 2 recedes to just the subject," must not be flagged on its own slice alone;
        // only the whole section's own union, across every phrase it was ever split into, is measured).
        // A section exempt only when EVERY one of its own phrase slices declares darkness -- same
        // exemption RuleDeclaredDarkness (H) above already honors; a section mixing a declared-dark
        // phrase with a normal one is still measured on its normal phrase(s).
        foreach (var sectionGroup in sections.GroupBy(s => s.SectionId))
        {
            if (sectionGroup.All(s => s.Look.DeclaredDarkness))
            {
                continue;
            }

            var distinctModelsInSection = sectionGroup
                .SelectMany(s => s.CompiledIR.Effects)
                .SelectMany(e => ResolveRealModelIds(e.Target.Value, layout))
                .Distinct()
                .Count();

            var representativeSection = sectionGroup.First();
            var modelBreadthFloor = representativeSection.Kind is { } sectionKind && DenseSectionKinds.Contains(sectionKind)
                ? MinDistinctModelsPerDenseSection
                : MinDistinctModelsPerSection;

            if (distinctModelsInSection < modelBreadthFloor)
            {
                violations.Add(new LegalityViolation(RuleSectionModelBreadth,
                    $"Section '{representativeSection.SectionId}' only lights {distinctModelsInSection} distinct real model(s) across its whole compiled window -- expected at least {modelBreadthFloor} for this section kind. A Look naming only one subject with no support/secondary layer leaves most of the display dark for this section's entire duration.",
                    AffectedSectionId: representativeSection.SectionId));
            }
        }

        // Section-energy-driven change density (I), build-plan Phase 12 §3c: the quietest declared-ceiling
        // section should be measurably quieter, by its own compiled change-rate, than the loudest -- a
        // soft, inspectable finding, not a hard compile failure, same framing as RuleDeclaredDarkness (H)
        // immediately above. A section missing either EnergyCeiling or Window (any caller that predates
        // this phase) is simply excluded, never substituted with an invented value.
        var measurable = sections.Where(s => s.EnergyCeiling is not null && s.Window is not null).ToList();
        if (measurable.Count >= 2)
        {
            var quietest = measurable.MinBy(s => s.EnergyCeiling)!;
            var loudest = measurable.MaxBy(s => s.EnergyCeiling)!;
            if (quietest.SectionId != loudest.SectionId && loudest.EnergyCeiling > quietest.EnergyCeiling)
            {
                var quietRate = MeasuredChangeRate(quietest);
                var loudRate = MeasuredChangeRate(loudest);
                if (loudRate > 0 && quietRate > loudRate * MaxQuietToLoudRateRatio)
                {
                    violations.Add(new LegalityViolation(RuleSectionDensityContrast,
                        $"Quietest declared section '{quietest.SectionId}' (ceiling {quietest.EnergyCeiling:0.00}) measures {quietRate:0.00} effect starts/sec, not measurably quieter than loudest declared section '{loudest.SectionId}' (ceiling {loudest.EnergyCeiling:0.00}) at {loudRate:0.00}/sec -- declared energy ceilings should translate into a measurable change-density spread, not a flat show."));
                }
            }
        }

        // Whole-song effect-name concentration (J), build-plan Phase 13 §2c: a show whose compiled effects
        // lean heavily on one single xLights effect name reads as monotone regardless of how varied its
        // colors/timing are -- same soft/advisory framing as every rule above, never a hard rejection. An
        // effect this validator cannot resolve a real name for (ResolveXlightsEffectName returning null --
        // a non-generated recipe id) is simply excluded from the tally, never guessed.
        var effectNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections)
        {
            foreach (var effect in section.CompiledIR.Effects)
            {
                var name = ResolveXlightsEffectName(effect.RecipeId, section.Look);
                if (name is not null)
                {
                    effectNameCounts[name] = effectNameCounts.GetValueOrDefault(name) + 1;
                }
            }
        }

        var totalResolvedEffects = effectNameCounts.Values.Sum();
        if (totalResolvedEffects >= MinEffectsForConcentrationCheck)
        {
            var (topEffect, topCount) = effectNameCounts.OrderByDescending(kv => kv.Value).First();
            var share = topCount / (double)totalResolvedEffects;
            if (share > MaxSingleEffectShare)
            {
                violations.Add(new LegalityViolation(RuleEffectConcentration,
                    $"Effect '{topEffect}' accounts for {share:P1} of all {totalResolvedEffects} name-resolved effects in this show ({effectNameCounts.Count} distinct effect name(s) used total) -- no single effect should dominate a whole song's own effect vocabulary this heavily."));
            }
        }

        // Every real model used at least once across the whole show (K). User feedback (2026-10-31): the
        // Director prompt only ever asked in plain text for small/outline props (e.g. an Arch) to get
        // "their own moment" -- nothing checked whether it actually complied, and real generated shows
        // left Arch models with zero effects for the entire song. Soft/advisory, same framing as every
        // other ValidateShow rule: this never blocks a compile, it surfaces an uncovered model so a
        // caller (e.g. IterationLoop, via a derived RevisionDirective naming the model) can self-heal with
        // a targeted regeneration instead of the gap going unnoticed. Only models with real pixels
        // (NodeCount > 0) are checked -- a 0-pixel/bookkeeping-only model has nothing to ever light.
        var coveredModelIds = new HashSet<ModelId>(sections
            .SelectMany(s => s.CompiledIR.Effects)
            .SelectMany(e => ResolveRealModelIds(e.Target.Value, layout)));
        foreach (var model in layout.Models.Where(m => m.NodeCount > 0 && !coveredModelIds.Contains(m.Id)))
        {
            violations.Add(new LegalityViolation(RuleModelCoverage,
                $"Model '{model.Id.Value}' never received any effect anywhere in the whole show -- every real model should appear in at least one Look across the song.",
                AffectedModelId: model.Id.Value));
        }

        // Model covered only incidentally through some OTHER group's own group-level effect (see
        // RuleModelOnlyGroupCovered's own doc comment): a real model whose ModelId was never literally a
        // compiled effect's own Target anywhere in the whole show, even though RuleModelCoverage's
        // group-expansion above correctly counts it as "covered" because it belongs to a group that got
        // used elsewhere. Only checked for groups with more than one real member -- a single-member
        // group's own effect already IS that model's own direct moment.
        var directEffectTargets = sections.SelectMany(s => s.CompiledIR.Effects).Select(e => e.Target).ToHashSet();
        var multiMemberGroupModelNames = layout.Groups
            .Where(g => g.Members.Count(m => m.Kind == DisplayGroupMemberKind.Model) > 1)
            .SelectMany(g => g.Members.Where(m => m.Kind == DisplayGroupMemberKind.Model).Select(m => m.RawName))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var model in layout.Models.Where(m =>
                     m.NodeCount > 0 &&
                     coveredModelIds.Contains(m.Id) &&
                     !directEffectTargets.Contains(m.Id) &&
                     multiMemberGroupModelNames.Contains(m.XlightsName)))
        {
            violations.Add(new LegalityViolation(RuleModelOnlyGroupCovered,
                $"Model '{model.Id.Value}' only ever received an effect as an incidental member of some other group-level effect -- it never received its own direct, individually-targeted effect anywhere in the whole show. Give it a real moment of its own, not just group membership.",
                AffectedModelId: model.Id.Value));
        }

        // Individual-model diversity within a multi-model group (soft/advisory -- see RuleIndividualModelDiversity's
        // own doc comment). Measured directly on the real compiled IR, not the raw Look JSON: a real
        // per-member Target in the compiled effects means SOME mechanism (an individual Look naming that
        // member directly, or a Travel/PropDelays expansion) genuinely gave that specific member its own
        // moment -- whichever mechanism produced it, real per-member diversity is the real signal, not
        // which declared field caused it. Flags only the specific pattern a real generation exhibited:
        // exactly ONE of a qualifying group's own members ever receives a real per-member effect, and it
        // does so more than once (a real, measured repetition of the same single model), while the
        // group's other members never get their own per-member moment at all.
        foreach (var group in layout.Groups)
        {
            var memberIds = group.Members
                .Where(m => m.Kind == DisplayGroupMemberKind.Model)
                .Select(m => layout.Models.FirstOrDefault(dm => dm.XlightsName == m.RawName))
                .Where(m => m is { Bounds: not null })
                .Select(m => m!.Id)
                .ToHashSet();

            if (memberIds.Count < MinPositionedMembersForDiversityCheck)
            {
                continue;
            }

            var perMemberUsage = sections.SelectMany(s => s.CompiledIR.Effects)
                .Where(e => memberIds.Contains(e.Target))
                .GroupBy(e => e.Target)
                .ToList();

            if (perMemberUsage.Count == 1 && perMemberUsage[0].Count() >= 2)
            {
                violations.Add(new LegalityViolation(RuleIndividualModelDiversity,
                    $"Advisory: group '{group.Name}' has {memberIds.Count} real positioned members, but only '{perMemberUsage[0].Key.Value}' ever received its own per-member effect (used {perMemberUsage[0].Count()} time(s) across the show) -- none of the group's other members ever did. Consider spreading individual-model moments across the group's own members, or a Travel sweep, instead of repeating the same one.",
                    AffectedModelId: perMemberUsage[0].Key.Value));
            }
        }

        // Group never targeted directly (soft/advisory -- see RuleGroupNeverTargetedDirectly's own
        // doc comment). Independent of member-level coverage: a real group whose own ModelId (the
        // group-level target a Look's "group:Name" reference compiles to -- see PropGroupResolver's
        // own doc comment on how a group-targeted effect round-trips) never appears as any compiled
        // effect's Target at all, even once, across the whole show.
        foreach (var group in layout.Groups)
        {
            var groupId = new ModelId(group.Name);
            if (!directEffectTargets.Contains(groupId))
            {
                violations.Add(new LegalityViolation(RuleGroupNeverTargetedDirectly,
                    $"Advisory: group '{group.Name}' was never targeted directly as its own group-level effect anywhere in the whole show (its real Layout listing offered it as a real group reference). Its members may still have received individual effects elsewhere, but the group itself -- a real, editable whole-group moment -- never fired.",
                    AffectedModelId: group.Name));
            }
        }

        // Concurrent focal-effect density cap (M), build-plan Phase 26: docs/MASTER-SYSTEM-ARCHITECTURE.md
        // §9a's own disclosed gap -- nothing today checks how many distinct props are simultaneously
        // carrying an attention-grabbing ("focal," see IsFocalEffect) effect. A plain sweep-line over every
        // section's own focal effects (grouped by real, group-expanded model id -- ResolveRealModelIds,
        // same group-collapse awareness RuleModelCoverage above already needs) finds the real peak
        // concurrent-focal-model count at every instant; a section missing its own Window is skipped
        // entirely (never inventing one to measure a hold against), the same discipline RuleSectionDensityContrast
        // and RuleDeclaredDarkness already apply to a missing Window.
        var focalEvents = new List<(long Time, int Delta, ModelId Model)>();
        foreach (var section in sections)
        {
            if (section.Window is not { } sectionWindow)
            {
                continue;
            }

            foreach (var effect in section.CompiledIR.Effects)
            {
                if (effect.EndTicks <= effect.StartTicks || !IsFocalEffect(effect, sectionWindow))
                {
                    continue;
                }

                foreach (var modelId in ResolveRealModelIds(effect.Target.Value, layout))
                {
                    focalEvents.Add((effect.StartTicks, 1, modelId));
                    focalEvents.Add((effect.EndTicks, -1, modelId));
                }
            }
        }

        if (focalEvents.Count > 0)
        {
            // End events (-1) apply before start events (+1) at the same instant -- same tie-break
            // LayerComplexityAnalyzer's own sweep line uses, so two back-to-back (never truly overlapping)
            // focal effects on the same model never misread as one instant of double occupancy.
            var activeCounts = new Dictionary<ModelId, int>();
            var wasOvercrowded = false;
            foreach (var e in focalEvents.OrderBy(e => e.Time).ThenBy(e => e.Delta))
            {
                activeCounts.TryGetValue(e.Model, out var count);
                count += e.Delta;
                if (count <= 0)
                {
                    activeCounts.Remove(e.Model);
                }
                else
                {
                    activeCounts[e.Model] = count;
                }

                var owningSection = sections.FirstOrDefault(s => s.Window is { } w && e.Time >= w.StartTicks && e.Time < w.EndTicks);
                var cap = owningSection?.Kind is { } kind && DenseSectionKinds.Contains(kind)
                    ? DensePeakSectionMaxConcurrentFocalModels
                    : DefaultMaxConcurrentFocalModels;

                var isOvercrowded = activeCounts.Count > cap;
                if (isOvercrowded && !wasOvercrowded)
                {
                    violations.Add(new LegalityViolation(RuleConcurrentFocalEffectCap,
                        $"{activeCounts.Count} distinct models simultaneously carry an active focal effect at {TimeSpan.FromTicks(e.Time):mm\\:ss} (section '{owningSection?.SectionId ?? "unknown"}') -- exceeds the cap of {cap} concurrent focal models for this section kind."));
                }

                wasOvercrowded = isOvercrowded;
            }
        }

        return violations;
    }

    /// <summary>
    /// Build-plan Phase 26: whether <paramref name="effect"/> is a "focal" (active, attention-grabbing)
    /// effect rather than a steady-state base-layer "hold" -- an effect spanning at least
    /// <see cref="HoldCoverageThreshold"/> of its own containing section's whole window reads as that
    /// steady base layer, not a momentary focal gesture, and is excluded so <see cref="RuleConcurrentFocalEffectCap"/>
    /// never trips on ordinary base+accent layering, only on genuinely simultaneous accents. A section
    /// whose own window has non-positive duration has nothing real to measure a hold's coverage against,
    /// so every effect in it counts as focal rather than being silently excluded.
    /// </summary>
    private static bool IsFocalEffect(EffectInstance effect, TimeWindow sectionWindow)
    {
        var sectionDuration = sectionWindow.EndTicks - sectionWindow.StartTicks;
        if (sectionDuration <= 0)
        {
            return true;
        }

        var effectDuration = effect.EndTicks - effect.StartTicks;
        return effectDuration < sectionDuration * HoldCoverageThreshold;
    }

    /// <summary>
    /// Every concrete <see cref="ModelId"/> a <paramref name="targetName"/> resolves to -- itself directly,
    /// when it names an individual model, or every real, distinct, recursively-expanded member when it
    /// names a group (reuses <see cref="PropGroupResolver"/>'s own already-verified expansion, same as
    /// <see cref="ResolveRealPixelWeight"/>). Empty for a name that resolves to neither, never guessed.
    /// </summary>
    private static IReadOnlyList<ModelId> ResolveRealModelIds(string targetName, DisplayGraph layout)
    {
        var directId = new ModelId(targetName);
        if (layout.Models.Any(m => m.Id == directId))
        {
            return [directId];
        }

        try
        {
            return PropGroupResolver.Resolve(targetName, layout);
        }
        catch (ArgumentException)
        {
            return [];
        }
    }

    /// <summary>This section's own compiled effect-start rate (count of effects in its own compiled IR, divided by its own window duration in seconds) -- a coarse, per-section approximation (the always-moving gap-filler's own additions, applied globally after every section compiles, are not reflected here), sufficient only for <see cref="RuleSectionDensityContrast"/>'s own quietest-vs-loudest comparison.</summary>
    private static double MeasuredChangeRate(SectionAssignment section)
    {
        var window = section.Window!.Value;
        var durationSeconds = (window.EndTicks - window.StartTicks) / (double)TicksPerSecond;
        return durationSeconds > 0 ? section.CompiledIR.Effects.Count / durationSeconds : 0.0;
    }

    /// <summary>
    /// Build-plan "richer analysis tools" Phase 5 (2026-11-02): averages <see cref="EnergyFrame.NormalizedEnergy"/>
    /// across every frame overlapping the half-open <c>[startTicks, endTicks)</c> window -- the same
    /// windowed-query shape as <see cref="BandEnergyEnvelope.AverageInWindow"/>, just over broadband
    /// <see cref="EnergyEnvelope"/> instead (no equivalent query method exists on that type yet). Returns
    /// <c>0</c> for a window with no overlapping frames, the same "silence is a valid, inspectable result"
    /// convention <see cref="BandEnergyEnvelope.AverageInWindow"/> already uses.
    /// </summary>
    private static double AverageNormalizedEnergyInWindow(EnergyEnvelope energy, long startTicks, long endTicks)
    {
        var sampleRate = energy.Settings.AnalysisSampleRate;

        double sum = 0;
        var count = 0;
        foreach (var frame in energy.Frames)
        {
            var frameStartTicks = sampleRate > 0 ? frame.StartSample * TicksPerSecond / sampleRate : 0;
            var frameEndTicks = sampleRate > 0 ? frame.EndSample * TicksPerSecond / sampleRate : 0;
            if (frameEndTicks <= startTicks || frameStartTicks >= endTicks)
            {
                continue;
            }

            sum += frame.NormalizedEnergy;
            count++;
        }

        return count == 0 ? 0.0 : sum / count;
    }

    /// <summary>Rough duty-corrected coverage estimate: sum of (target pixel weight x that effect's own duty-cycle correction) across every effect in the IR, normalized by the layout's own total pixels. A coarse, section-wide approximation (not a true per-frame render), sufficient only to flag a clearly-empty section.</summary>
    private static double EstimateDutyCorrectedCoverage(ChoreographyIR ir, DisplayGraph layout)
    {
        var totalPixels = layout.Models.Sum(m => Math.Max(0, m.NodeCount));
        if (totalPixels <= 0 || ir.Effects.Count == 0)
        {
            return 0.0;
        }

        var litByModel = new Dictionary<ModelId, double>();
        foreach (var effect in ir.Effects)
        {
            // Duty correction is keyed by xLights effect name; without the recipe id -> effect name
            // mapping at this call site, a neutral 1.0 (no correction) is used rather than guessed --
            // same "never invent" discipline as everywhere else in this validator. ResolveRealPixelWeight
            // (not a plain modelsById lookup) is required here: effect.Target can itself be a real
            // group's own ModelId (LookCompiler's group-collapse optimization), which never appears in
            // layout.Models directly -- a plain lookup silently treated every group-targeted effect as
            // lighting zero pixels, reading as "near-zero coverage" regardless of how pixel-heavy the
            // group actually is.
            var pixels = ResolveRealPixelWeight(effect.Target.Value, layout);
            if (pixels > 0)
            {
                litByModel[effect.Target] = Math.Max(litByModel.GetValueOrDefault(effect.Target), pixels);
            }
        }

        return litByModel.Values.Sum() / totalPixels;
    }

    /// <summary>
    /// Resolves <paramref name="targetName"/>'s own real pixel weight, whether it names an individual
    /// model or a real xLights group (summed over its real, distinct, recursively-expanded member models
    /// -- reuses <see cref="PropGroupResolver"/>'s own already-verified expansion rather than
    /// re-deriving nested-group/cycle handling a second time). 0 for a name that resolves to neither.
    /// </summary>
    private static long ResolveRealPixelWeight(string targetName, DisplayGraph layout)
    {
        var modelsById = layout.Models.ToDictionary(m => m.Id);
        if (modelsById.TryGetValue(new ModelId(targetName), out var directModel))
        {
            return Math.Max(0, directModel.NodeCount);
        }

        try
        {
            return PropGroupResolver.Resolve(targetName, layout)
                .Sum(id => modelsById.TryGetValue(id, out var member) ? Math.Max(0, member.NodeCount) : 0);
        }
        catch (ArgumentException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Build-plan Phase 22 §2c: permanently-advisory report of every real emphasis instant (transient hit,
    /// vocal-note onset, timed cue) whose own real compiled brightness contrast (<see cref="EmphasisContrastPlanner.Measure"/>)
    /// still does not clear a real tier step -- even AFTER <see cref="EmphasisContrastPlanner.ApplyContrastFallback"/>
    /// has already run its own deterministic repair pass (the caller, <see cref="ChoreographyEngine.Compile"/>,
    /// runs this against the FINAL compiled IR). Confirmed by the user: this never blocks a generation or
    /// triggers a retry -- a non-empty result here is purely a visibility/measurement finding, forever.
    /// </summary>
    public static IReadOnlyList<LegalityViolation> ValidateEmphasisContrast(
        ChoreographyIR ir, IReadOnlyList<EmphasisInstant> instants, Rhythm.BeatGrid beatGrid,
        EffectRecipeCatalog recipeCatalog, EffectVisualCharacterCatalog brightnessCatalog)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(instants);
        ArgumentNullException.ThrowIfNull(beatGrid);
        ArgumentNullException.ThrowIfNull(recipeCatalog);
        ArgumentNullException.ThrowIfNull(brightnessCatalog);

        var findings = EmphasisContrastPlanner.Measure(ir, instants, beatGrid, recipeCatalog, brightnessCatalog);

        return findings
            .Where(f => !f.HasContrast)
            .Select(f => new LegalityViolation(RuleContrastAtEmphasis,
                $"Emphasis instant ({f.Kind}) at {TimeSpan.FromTicks(f.TimeTicks):mm\\:ss} on '{f.Target.Value}' does not show a real brightness-tier step -- preceding={f.PrecedingTier?.ToString() ?? "unresolved"}, following={f.FollowingTier?.ToString() ?? "unresolved"}.",
                f.Target.Value))
            .ToList();
    }

    /// <summary>
    /// Recovers the real xLights effect name an already-compiled <see cref="EffectInstance.RecipeId"/>
    /// came from, without needing the full recipe catalog threaded through every validator call site: a
    /// generated recipe's own id is deterministic (<c>effect.generated.&lt;lowercase-effect-name&gt;.v&lt;version&gt;</c>,
    /// see <c>GeneratedEffectRecipe</c>), so matching its lowercased middle segment case-insensitively
    /// against <paramref name="look"/>'s own declared layer effect names recovers the original casing.
    /// </summary>
    private static string? ResolveXlightsEffectName(string recipeId, Look look)
    {
        const string prefix = "effect.generated.";
        if (!recipeId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = recipeId[prefix.Length..];
        var versionIndex = rest.LastIndexOf(".v", StringComparison.Ordinal);
        var lowerName = versionIndex > 0 ? rest[..versionIndex] : rest;

        return look.Layers.FirstOrDefault(l => string.Equals(l.Effect, lowerName, StringComparison.OrdinalIgnoreCase))?.Effect;
    }
}
