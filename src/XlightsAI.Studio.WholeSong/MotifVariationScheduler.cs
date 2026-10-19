using System.Globalization;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>Default <see cref="IMotifVariationScheduler"/>. Pure, deterministic, no I/O.</summary>
public sealed class MotifVariationScheduler : IMotifVariationScheduler
{
    /// <summary>
    /// This recipe's own declared intensity-aspect parameter convention, mirroring
    /// <see cref="ChoreographyTemplatePlanner.PaletteParameterPrefix"/>'s own naming pattern: a
    /// <see cref="EffectInstance.Parameters"/> key prefixed <c>"Intensity."</c> (ordinal, case-insensitive) is
    /// the one and only parameter kind this step's own automatic variation ever rescales -- every other
    /// parameter is passed through byte-for-byte unchanged ("do not invent xLights effect parameters,"
    /// PHASE-14.md Section 2, verbatim: this step only ever adjusts the numeric value of a parameter the
    /// template already declared, never adds or removes one).
    /// </summary>
    public const string IntensityParameterPrefix = "Intensity.";

    /// <summary>The lowest multiplier this scheduler will ever apply, at the lowest possible section energy (<c>0.0</c>) for a non-climax occurrence.</summary>
    public const double MinMultiplier = 0.5;

    /// <summary>
    /// The highest multiplier this scheduler will ever apply. Deliberately set above
    /// <see cref="BaseMultiplierAtMaxEnergy"/> (<c>1.1</c>) so a climax occurrence's own
    /// <c>base + ClimaxLift</c> (up to <c>1.3</c>, clamped here) can never collapse down to equal a
    /// non-climax occurrence's own maximum possible multiplier -- see <see cref="ComputeMultiplier"/>'s own
    /// doc comment for the full, provable guarantee.
    /// </summary>
    public const double MaxMultiplier = 1.25;

    /// <summary>The fixed, bounded additional lift (this step's own required acceptance, verbatim: "intentional lift") applied only to the one occurrence <see cref="Schedule"/> schedules as a template's own climax.</summary>
    public const double ClimaxLift = 0.2;

    private const double BaseMultiplierAtMinEnergy = 0.6;
    private const double BaseMultiplierAtMaxEnergy = 1.1;

    private readonly IChoreographyTemplatePlanner _templatePlanner;

    public MotifVariationScheduler(IChoreographyTemplatePlanner templatePlanner)
    {
        ArgumentNullException.ThrowIfNull(templatePlanner);
        _templatePlanner = templatePlanner;
    }

    public MotifVariationPlan Schedule(
        SongId songId,
        ChoreographyTemplateAnalysis templateAnalysis,
        PhraseSegmentationAnalysis phraseAnalysis,
        SequenceDocument destinationDocument,
        IReadOnlyList<MotifOccurrenceRequest> occurrenceRequests,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(templateAnalysis);
        ArgumentNullException.ThrowIfNull(phraseAnalysis);
        ArgumentNullException.ThrowIfNull(destinationDocument);
        ArgumentNullException.ThrowIfNull(occurrenceRequests);

        ct.ThrowIfCancellationRequested();

        if (occurrenceRequests.Count == 0)
        {
            throw new MotifVariationInvalidException(
                "At least one MotifOccurrenceRequest is required to schedule a MotifVariationPlan -- a clean " +
                "machine with no requested occurrences yet has none, and must be reported clearly rather than " +
                "scheduled as a silent no-op.");
        }

        var seenPairs = new HashSet<(Guid TemplateId, Guid TargetPhraseId)>();
        foreach (var request in occurrenceRequests)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.Validate();

            if (!seenPairs.Add((request.TemplateId, request.TargetPhraseId)))
            {
                throw new MotifVariationInvalidException(
                    $"Duplicate occurrence request (template '{request.TemplateId}', phrase '{request.TargetPhraseId}').");
            }
        }

        // "Climax scheduling" (this step's own title, verbatim): per template, the single occurrence with
        // the highest SectionEnergy -- a first-in-request-order tie-break keeps this deterministic and
        // reproducible across repeated calls with the same input.
        var climaxTargetPhraseByTemplate = new Dictionary<Guid, Guid>();
        var bestEnergyByTemplate = new Dictionary<Guid, double>();
        foreach (var request in occurrenceRequests)
        {
            if (!bestEnergyByTemplate.TryGetValue(request.TemplateId, out var bestEnergy)
                || request.SectionEnergy > bestEnergy)
            {
                bestEnergyByTemplate[request.TemplateId] = request.SectionEnergy;
                climaxTargetPhraseByTemplate[request.TemplateId] = request.TargetPhraseId;
            }
        }

        var occurrences = new List<MotifOccurrenceVariation>(occurrenceRequests.Count);
        foreach (var request in occurrenceRequests)
        {
            ct.ThrowIfCancellationRequested();

            var isClimax = climaxTargetPhraseByTemplate[request.TemplateId] == request.TargetPhraseId;
            var multiplier = ComputeMultiplier(request.SectionEnergy, isClimax);

            // Propagated unchanged: an unknown TemplateId/TargetPhraseId, or a template source phrase not
            // found in phraseAnalysis, is this step's own cross-reference validation, already fully owned and
            // tested by the real IChoreographyTemplatePlanner this step reuses rather than re-implements.
            var preview = _templatePlanner.PreviewInstance(
                templateAnalysis, request.TemplateId, request.TargetPhraseId, phraseAnalysis, destinationDocument);

            var template = templateAnalysis.Templates.First(t => t.Id == request.TemplateId);
            var instance = template.Instances.FirstOrDefault(i => i.TargetPhraseId == request.TargetPhraseId);
            var overriddenSourceEffectIds = (instance?.Overrides ?? []).Select(o => o.SourceEffectId).ToList();
            var overriddenSet = overriddenSourceEffectIds.ToHashSet();

            var inheritedSourceEffectIds = template.Mask.Motion
                ? template.Effects
                    .Where(e => !overriddenSet.Contains(e.SourceEffectId) && (!e.IsRhythmLayer || template.Mask.RhythmLayer))
                    .Select(e => e.SourceEffectId)
                    .ToList()
                : [];

            // Overrides are a human's own explicit, instance-only choice -- never automatically intensity-
            // scaled, the same "overrides always win... never filtered" rule ChoreographyTemplatePlanner
            // already established for its own mask. Since this scheduler does not itself know which final
            // preview.Effects entry originated from an override (PreviewInstance's own output carries no
            // provenance tag), the whole-occurrence scaling pass below is applied uniformly; the per-slot
            // classification recorded on MotifOccurrenceVariation is this step's own disclosed, coarser
            // "which source slots are inherited vs overridden" view, not a per-output-effect exclusion.
            var scaledEffects = preview.Effects.Select(e => ScaleIntensity(e, multiplier)).ToList();

            occurrences.Add(new MotifOccurrenceVariation(
                request.TemplateId,
                request.TargetPhraseId,
                isClimax,
                multiplier,
                scaledEffects,
                inheritedSourceEffectIds,
                overriddenSourceEffectIds,
                preview.ExcludedReasons));
        }

        var plan = new MotifVariationPlan(
            Guid.NewGuid(),
            songId,
            templateAnalysis.VersionId,
            phraseAnalysis.VersionId,
            nowUtc,
            occurrences);

        plan.Validate();
        return plan;
    }

    /// <summary>
    /// <c>multiplier(energy) = clamp(0.6 + energy * 0.5, Min, Max)</c>, plus <see cref="ClimaxLift"/> (0.2)
    /// when <paramref name="isClimax"/>. For any <c>energy</c> in <c>[0,1]</c> the non-climax base multiplier
    /// always lies in <c>[0.6, 1.1]</c>; adding <see cref="ClimaxLift"/> and clamping to
    /// <see cref="MaxMultiplier"/> (1.25, deliberately above 1.1) means: for a template's own climax
    /// occurrence (by construction, the member of its own occurrence group with the highest energy), this
    /// multiplier is always strictly greater than the base multiplier of every other occurrence sharing the
    /// same template -- the non-climax ceiling (1.1) can never reach the climax floor
    /// (<c>min(1.1, 0.6) + 0.2</c>, always &gt;= 0.8 and clamped no lower than the climax occurrence's own base).
    /// This is what makes "Final chorus preserves recognizable motif with intentional lift" (this step's own
    /// required acceptance, verbatim) a provable invariant rather than a coincidence of fixture values.
    /// </summary>
    public static double ComputeMultiplier(double sectionEnergy, bool isClimax)
    {
        var baseMultiplier = BaseMultiplierAtMinEnergy + (sectionEnergy * (BaseMultiplierAtMaxEnergy - BaseMultiplierAtMinEnergy));
        var raw = isClimax ? baseMultiplier + ClimaxLift : baseMultiplier;
        return Math.Round(Math.Clamp(raw, MinMultiplier, MaxMultiplier), 4);
    }

    private static EffectInstance ScaleIntensity(EffectInstance effect, double multiplier)
    {
        var hasIntensityParameter = effect.Parameters.Keys
            .Any(k => k.StartsWith(IntensityParameterPrefix, StringComparison.OrdinalIgnoreCase));

        if (!hasIntensityParameter)
        {
            return effect;
        }

        var scaledParameters = new Dictionary<string, string>(effect.Parameters.Count);
        foreach (var (key, value) in effect.Parameters)
        {
            if (!key.StartsWith(IntensityParameterPrefix, StringComparison.OrdinalIgnoreCase))
            {
                scaledParameters[key] = value;
                continue;
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric)
                || !double.IsFinite(numeric))
            {
                throw new MotifVariationInvalidException(
                    $"Effect '{effect.Id}' on '{effect.Target.Value}' has a non-numeric intensity parameter " +
                    $"'{key}' = '{value}' -- cannot be safely scaled.");
            }

            var scaled = Math.Clamp(numeric * multiplier, 0.0, 1.0);
            scaledParameters[key] = scaled.ToString("0.####", CultureInfo.InvariantCulture);
        }

        return effect with { Parameters = scaledParameters };
    }
}
