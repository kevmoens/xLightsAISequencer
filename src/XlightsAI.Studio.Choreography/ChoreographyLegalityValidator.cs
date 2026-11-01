using XlightsAI.Studio.Knowledge;
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

    /// <summary>
    /// Public (unlike every other rule id here) because <see cref="XlightsAI.Studio.Director.RevisionDirectiveDeriver"/>
    /// filters <see cref="ValidateShow"/>'s output by this id to derive a self-healing targeted
    /// regeneration directive for each uncovered model (see <see cref="LegalityViolation.AffectedModelId"/>)
    /// -- user feedback 2026-10-31 that Arch-category props were silently ending up with zero effects.
    /// </summary>
    public const string RuleModelCoverage = "every-model-used-at-least-once";

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
    public sealed record SectionAssignment(
        string SectionId, Look Look, ChoreographyIR CompiledIR, IReadOnlyList<ModelId> TargetedModels,
        TimeWindow? Window = null, double? EnergyCeiling = null);

    /// <summary>Validates whole-song concerns that no single section's own compile can check in isolation: peak-Look uniqueness, pixel-heavy coverage, and declared-vs-accidental darkness.</summary>
    public static IReadOnlyList<LegalityViolation> ValidateShow(
        IReadOnlyList<SectionAssignment> sections, DisplayGraph layout, IReadOnlyList<PixelCoverageLadder.Rung> coverageLadder)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(coverageLadder);

        var violations = new List<LegalityViolation>();

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
        foreach (var section in sections)
        {
            var dutyCorrectedCoverage = EstimateDutyCorrectedCoverage(section.CompiledIR, layout);
            if (dutyCorrectedCoverage < 0.02 && !section.Look.DeclaredDarkness)
            {
                violations.Add(new LegalityViolation(RuleDeclaredDarkness,
                    $"Section '{section.SectionId}' (Look '{section.Look.Id}') compiles to near-zero duty-corrected pixel coverage ({dutyCorrectedCoverage:P1}) without DeclaredDarkness set -- undeclared darkness is a defect, declared darkness is negative space."));
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

        return violations;
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
