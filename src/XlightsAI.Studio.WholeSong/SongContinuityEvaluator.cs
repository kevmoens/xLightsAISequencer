using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>Default <see cref="ISongContinuityEvaluator"/>. Pure, deterministic, no I/O.</summary>
public sealed class SongContinuityEvaluator : ISongContinuityEvaluator
{
    public IReadOnlyList<ContinuityFinding> Evaluate(
        SequenceDocument candidate,
        IReadOnlyList<PhraseSeamPlan> seams,
        MotifVariationPlan? motifPlan,
        Guid? currentTemplateAnalysisVersionId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(seams);

        ct.ThrowIfCancellationRequested();

        var findings = new List<ContinuityFinding>();

        AddUnresolvedOverlapFindings(candidate, findings);

        foreach (var seam in seams)
        {
            ArgumentNullException.ThrowIfNull(seam);
            AddSeamFindings(candidate, seam, findings);
        }

        if (motifPlan is not null)
        {
            ct.ThrowIfCancellationRequested();
            AddMotifStaleFinding(motifPlan, currentTemplateAnalysisVersionId, findings);
            AddMotifExcludedFindings(motifPlan, findings);
            AddClimaxLiftFindings(motifPlan, findings);
        }

        return findings;
    }

    /// <summary>
    /// Phase test matrix row 4, "Cross-phrase overlapping effect is flagged for review", applied directly
    /// to <paramref name="candidate"/> -- a genuine same-target/same-layer overlap, which never survives a
    /// real <see cref="PhraseSeamAssembler.Assemble"/> call but is not guaranteed for an arbitrary caller-
    /// supplied document (this evaluator's own, disclosed, narrower input contract -- see
    /// <see cref="ISongContinuityEvaluator"/>).
    /// </summary>
    private static void AddUnresolvedOverlapFindings(SequenceDocument candidate, List<ContinuityFinding> findings)
    {
        foreach (var group in candidate.Effects.GroupBy(e => (e.Target, e.Layer)))
        {
            var ordered = group.OrderBy(e => e.StartTicks).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previous = ordered[i - 1];
                var current = ordered[i];
                if (current.StartTicks < previous.EndTicks)
                {
                    findings.Add(new ContinuityFinding(
                        ContinuityFindingCode.UnresolvedOverlap,
                        ContinuitySeverity.Error,
                        new TimeWindow(current.StartTicks, previous.EndTicks),
                        [group.Key.Target],
                        $"Effects '{previous.Id}' and '{current.Id}' on '{group.Key.Target.Value}' layer " +
                        $"{group.Key.Layer} overlap ({previous.StartTicks}-{previous.EndTicks} vs " +
                        $"{current.StartTicks}-{current.EndTicks}); this must be resolved before the whole " +
                        "song is exported."));
                }
            }
        }
    }

    /// <summary>
    /// Re-surfaces each already-disclosed <see cref="PhraseSeamPlan.Warnings"/> entry as its own
    /// <see cref="ContinuityFinding"/> at the whole-song review stage (phase test matrix rows 3 and 4).
    /// <see cref="ContinuityFinding.Models"/> is derived from every effect in <paramref name="candidate"/>
    /// whose window touches <paramref name="seam"/>'s own window -- a seam itself carries no model
    /// reference, so this is a best-effort, disclosed association, not an exact provenance link.
    /// </summary>
    private static void AddSeamFindings(SequenceDocument candidate, PhraseSeamPlan seam, List<ContinuityFinding> findings)
    {
        if (seam.Warnings.Count == 0)
        {
            return;
        }

        var touchingModels = candidate.Effects
            .Where(e => e.StartTicks <= seam.Window.EndTicks && e.EndTicks >= seam.Window.StartTicks)
            .Select(e => e.Target)
            .Distinct()
            .ToList();

        foreach (var warning in seam.Warnings)
        {
            var code = warning.Contains("clipped", StringComparison.Ordinal) || warning.Contains("dropped", StringComparison.Ordinal)
                ? ContinuityFindingCode.SeamOverlapResolved
                : warning.Contains("gap of", StringComparison.Ordinal)
                    ? ContinuityFindingCode.UnresolvedGap
                    : null;

            if (code is null)
            {
                // A no-op/disclosed-but-non-actionable seam warning (e.g. "applied as a no-op") carries no
                // reviewable continuity risk of its own; it stays in Step 091's own PhraseAssemblyResult.Warnings
                // and is deliberately not duplicated into the whole-song continuity report.
                continue;
            }

            findings.Add(new ContinuityFinding(
                code,
                ContinuitySeverity.Warning,
                seam.Window,
                touchingModels,
                warning));
        }
    }

    /// <summary>Phase test matrix row 5, "Motif source changes generate stale-instance warning."</summary>
    private static void AddMotifStaleFinding(
        MotifVariationPlan motifPlan, Guid? currentTemplateAnalysisVersionId, List<ContinuityFinding> findings)
    {
        if (currentTemplateAnalysisVersionId is not { } current || current == motifPlan.SourceTemplateAnalysisVersionId)
        {
            return;
        }

        var models = motifPlan.Occurrences.SelectMany(o => o.Effects.Select(e => e.Target)).Distinct().ToList();
        var window = OccurrenceWindow(motifPlan.Occurrences);

        findings.Add(new ContinuityFinding(
            ContinuityFindingCode.MotifSourceStale,
            ContinuitySeverity.Error,
            window,
            models,
            $"MotifVariationPlan '{motifPlan.Id}' was scheduled against template analysis version " +
            $"'{motifPlan.SourceTemplateAnalysisVersionId}', but the current template analysis version is " +
            $"'{current}'; re-schedule this plan before exporting the whole song."));
    }

    private static void AddMotifExcludedFindings(MotifVariationPlan motifPlan, List<ContinuityFinding> findings)
    {
        foreach (var occurrence in motifPlan.Occurrences)
        {
            if (occurrence.ExcludedReasons.Count == 0)
            {
                continue;
            }

            var models = occurrence.Effects.Select(e => e.Target).Distinct().ToList();
            var window = occurrence.Effects.Count == 0
                ? new TimeWindow(0, 0)
                : new TimeWindow(occurrence.Effects.Min(e => e.StartTicks), occurrence.Effects.Max(e => e.EndTicks));

            findings.Add(new ContinuityFinding(
                ContinuityFindingCode.MotifInstanceExcluded,
                ContinuitySeverity.Warning,
                window,
                models,
                $"Motif occurrence (template '{occurrence.TemplateId}', phrase '{occurrence.TargetPhraseId}') " +
                $"excluded: {string.Join("; ", occurrence.ExcludedReasons)}"));
        }
    }

    /// <summary>Phase test matrix row 6, "Section-to-section brightness arc is computed consistently."</summary>
    private static void AddClimaxLiftFindings(MotifVariationPlan motifPlan, List<ContinuityFinding> findings)
    {
        foreach (var group in motifPlan.Occurrences.GroupBy(o => o.TemplateId))
        {
            var members = group.ToList();
            if (members.Count < 2)
            {
                continue;
            }

            var climaxCount = members.Count(o => o.IsClimax);
            var maxMultiplier = members.Max(o => o.IntensityMultiplier);
            var climaxHasMax = members.Where(o => o.IsClimax).All(o => o.IntensityMultiplier >= maxMultiplier);

            if (climaxCount == 1 && climaxHasMax)
            {
                continue;
            }

            var models = members.SelectMany(o => o.Effects.Select(e => e.Target)).Distinct().ToList();
            var window = OccurrenceWindow(members);

            findings.Add(new ContinuityFinding(
                ContinuityFindingCode.ClimaxLiftInconsistent,
                ContinuitySeverity.Error,
                window,
                models,
                $"Motif template '{group.Key}' has {climaxCount} occurrence(s) marked as climax across " +
                $"{members.Count} total occurrence(s), and/or its climax does not carry the strictly " +
                "highest intensity multiplier; the brightness arc for this motif is not consistently computed."));
        }
    }

    private static TimeWindow OccurrenceWindow(IReadOnlyList<MotifOccurrenceVariation> occurrences)
    {
        var allEffects = occurrences.SelectMany(o => o.Effects).ToList();
        return allEffects.Count == 0
            ? new TimeWindow(0, 0)
            : new TimeWindow(allEffects.Min(e => e.StartTicks), allEffects.Max(e => e.EndTicks));
    }
}
