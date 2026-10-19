using System.Security.Cryptography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 057 -- real implementation of <see cref="IChoreographyTemplatePlanner"/>. Step 057's own
/// declared recipe, verbatim: "Create parent/instance pattern inheritance with adjustable mask and
/// transform; preview effect list and respect locks."
/// </summary>
/// <remarks>
/// <b>Palette parameters.</b> A <see cref="ChoreographyTemplateEffect.Parameters"/> key prefixed
/// <c>"Palette."</c> (ordinal, case-insensitive) is this recipe's own declared palette-aspect convention;
/// every other key is a motion-aspect parameter. A future recipe may declare a different convention under a
/// new, distinguishable <see cref="AnalyzerVersion"/> without redefining this one's meaning.
/// <para>
/// <b>Containment, not overlap.</b> Only effects fully contained within a source phrase's own half-open tick
/// window are captured into a template -- an effect straddling a phrase boundary belongs to neither phrase
/// unambiguously, so this recipe never captures it.
/// </para>
/// <para>
/// <b>Overrides always win.</b> An explicit <see cref="ChoreographyInstanceOverride"/> is included in a
/// preview regardless of <see cref="ChoreographyInheritanceMask"/> -- a human's own edit to one occurrence is
/// never silently suppressed by a mask meant for the template's own default inheritance.
/// </para>
/// </remarks>
public sealed class ChoreographyTemplatePlanner : IChoreographyTemplatePlanner
{
    /// <summary>Burned into every <see cref="ChoreographyTemplateAnalysis.AnalyzerVersion"/> this planner produces.</summary>
    public const string Version = "XlightsAI.Studio.Structure.ChoreographyTemplatePlanner/1.0.0";

    /// <summary>This recipe's own declared palette-parameter key convention (<see cref="ChoreographyInheritanceMask.Palette"/>).</summary>
    public const string PaletteParameterPrefix = "Palette.";

    private readonly IChoreographyTemplateStore _store;

    public ChoreographyTemplatePlanner(IChoreographyTemplateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<ChoreographyTemplateAnalysis> DefineTemplateAsync(
        ChoreographyTemplateAnalysis? current,
        ChoreographyTemplateContext context,
        Guid motifGroupId,
        Guid sourcePhraseId,
        ChoreographyInheritanceMask mask,
        IReadOnlySet<int> rhythmLayers,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(rhythmLayers);

        ValidateContext(context);

        var group = context.MotifClustering.Groups.FirstOrDefault(g => g.Id == motifGroupId)
            ?? throw new ChoreographyTemplateInvalidException(context.SongId, $"Motif group '{motifGroupId}' was not found in the supplied motif clustering analysis.");

        if (!group.PhraseIds.Contains(sourcePhraseId))
        {
            throw new ChoreographyTemplateInvalidException(context.SongId,
                $"Source phrase '{sourcePhraseId}' is not a member of motif group '{motifGroupId}'.");
        }

        var sourcePhrase = context.PhraseAnalysis.Phrases.FirstOrDefault(p => p.Id == sourcePhraseId)
            ?? throw new ChoreographyTemplateInvalidException(context.SongId,
                $"Source phrase '{sourcePhraseId}' was not found in the supplied phrase segmentation analysis.");

        if (!Enum.IsDefined(mask.Timing))
        {
            throw new ChoreographyTemplateInvalidException(context.SongId, $"ChoreographyInheritanceMask.Timing ({mask.Timing}) is not a recognized value.");
        }

        ct.ThrowIfCancellationRequested();

        var capturedEffects = context.SourceDocument.Effects
            .Where(e => e.StartTicks >= sourcePhrase.StartTicks && e.EndTicks <= sourcePhrase.EndTicks)
            .OrderBy(e => e.StartTicks)
            .Select(e => new ChoreographyTemplateEffect(
                SourceEffectId: e.Id,
                Target: e.Target,
                Layer: e.Layer,
                RelativeStartTicks: e.StartTicks - sourcePhrase.StartTicks,
                RelativeEndTicks: e.EndTicks - sourcePhrase.StartTicks,
                RecipeId: e.RecipeId,
                RecipeVersion: e.RecipeVersion,
                Parameters: e.Parameters,
                IsRhythmLayer: rhythmLayers.Contains(e.Layer)))
            .ToList();

        var existingTemplate = current?.Templates.FirstOrDefault(t => t.MotifGroupId == motifGroupId && t.SourcePhraseId == sourcePhraseId);

        var template = new ChoreographyTemplate(
            Id: existingTemplate?.Id ?? Guid.NewGuid(),
            MotifGroupId: motifGroupId,
            SourcePhraseId: sourcePhraseId,
            Mask: mask,
            Effects: capturedEffects,
            Instances: existingTemplate?.Instances ?? []);

        var templates = (current?.Templates ?? [])
            .Where(t => t.Id != template.Id)
            .Append(template)
            .ToList();

        var analysis = new ChoreographyTemplateAnalysis(
            VersionId: Guid.NewGuid(),
            SongId: context.SongId,
            SourceHash: context.SourceHash,
            SourceMotifClusteringVersionId: context.MotifClustering.VersionId,
            SourceDocumentRevision: context.SourceDocument.Revision,
            AnalyzerVersion: Version,
            Templates: templates);

        await SaveGuardedAsync(current, analysis, ct).ConfigureAwait(false);
        return analysis;
    }

    public async Task<ChoreographyTemplateAnalysis> SetInstanceOverrideAsync(
        ChoreographyTemplateAnalysis current,
        ChoreographyTemplateContext context,
        Guid templateId,
        Guid targetPhraseId,
        ChoreographyInstanceOverride @override,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(@override);

        ValidateContext(context);

        var template = current.Templates.FirstOrDefault(t => t.Id == templateId)
            ?? throw new ChoreographyTemplateInvalidException(context.SongId, $"Template '{templateId}' was not found in the supplied analysis.");

        var group = context.MotifClustering.Groups.FirstOrDefault(g => g.Id == template.MotifGroupId)
            ?? throw new ChoreographyTemplateInvalidException(context.SongId, $"Motif group '{template.MotifGroupId}' was not found in the supplied motif clustering analysis.");

        if (!group.PhraseIds.Contains(targetPhraseId))
        {
            throw new ChoreographyTemplateInvalidException(context.SongId,
                $"Target phrase '{targetPhraseId}' is not a member of motif group '{template.MotifGroupId}'.");
        }

        if (@override.EndTicks <= @override.StartTicks)
        {
            throw new ChoreographyTemplateInvalidException(context.SongId,
                $"Override EndTicks ({@override.EndTicks}) must be greater than StartTicks ({@override.StartTicks}).");
        }

        if (string.IsNullOrWhiteSpace(@override.RecipeId))
        {
            throw new ChoreographyTemplateInvalidException(context.SongId, "Override RecipeId must not be empty.");
        }

        ct.ThrowIfCancellationRequested();

        var existingInstance = template.Instances.FirstOrDefault(i => i.TargetPhraseId == targetPhraseId);

        var updatedOverrides = (existingInstance?.Overrides ?? [])
            .Where(o => o.SourceEffectId != @override.SourceEffectId)
            .Append(@override)
            .OrderBy(o => o.StartTicks)
            .ToList();

        var updatedInstance = new ChoreographyInstance(
            Id: existingInstance?.Id ?? Guid.NewGuid(),
            TemplateId: templateId,
            TargetPhraseId: targetPhraseId,
            Overrides: updatedOverrides);

        var updatedInstances = template.Instances
            .Where(i => i.TargetPhraseId != targetPhraseId)
            .Append(updatedInstance)
            .ToList();

        var updatedTemplate = template with { Instances = updatedInstances };

        var templates = current.Templates
            .Where(t => t.Id != templateId)
            .Append(updatedTemplate)
            .ToList();

        var analysis = current with
        {
            VersionId = Guid.NewGuid(),
            Templates = templates,
        };

        await SaveGuardedAsync(current, analysis, ct).ConfigureAwait(false);
        return analysis;
    }

    public ChoreographyPreviewResult PreviewInstance(
        ChoreographyTemplateAnalysis analysis,
        Guid templateId,
        Guid targetPhraseId,
        PhraseSegmentationAnalysis phraseAnalysis,
        SequenceDocument destinationDocument)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(phraseAnalysis);
        ArgumentNullException.ThrowIfNull(destinationDocument);

        var template = analysis.Templates.FirstOrDefault(t => t.Id == templateId)
            ?? throw new ChoreographyTemplateInvalidException(analysis.SongId, $"Template '{templateId}' was not found in the supplied analysis.");

        var targetPhrase = phraseAnalysis.Phrases.FirstOrDefault(p => p.Id == targetPhraseId)
            ?? throw new ChoreographyTemplateInvalidException(analysis.SongId, $"Target phrase '{targetPhraseId}' was not found in the supplied phrase segmentation analysis.");

        var sourcePhrase = phraseAnalysis.Phrases.FirstOrDefault(p => p.Id == template.SourcePhraseId)
            ?? throw new ChoreographyTemplateInvalidException(analysis.SongId, $"Template source phrase '{template.SourcePhraseId}' was not found in the supplied phrase segmentation analysis.");

        var instance = template.Instances.FirstOrDefault(i => i.TargetPhraseId == targetPhraseId);
        var remainingOverrides = (instance?.Overrides ?? []).ToDictionary(o => o.SourceEffectId);

        var effects = new List<EffectInstance>();
        var exclusions = new List<string>();

        var sourceDuration = sourcePhrase.EndTicks - sourcePhrase.StartTicks;
        var targetDuration = targetPhrase.EndTicks - targetPhrase.StartTicks;
        var retimeRatio = sourceDuration > 0 ? targetDuration / (double)sourceDuration : 1.0;

        foreach (var templateEffect in template.Effects)
        {
            if (remainingOverrides.Remove(templateEffect.SourceEffectId, out var overrideForSlot))
            {
                AddIfUnlocked(ToEffectInstance(overrideForSlot, targetPhraseId, templateEffect.SourceEffectId), effects, exclusions, destinationDocument);
                continue;
            }

            if (!template.Mask.Motion)
            {
                continue;
            }

            if (templateEffect.IsRhythmLayer && !template.Mask.RhythmLayer)
            {
                continue;
            }

            long newStart;
            long newEnd;
            if (template.Mask.Timing == ChoreographyTimingPolicy.Copy)
            {
                newStart = targetPhrase.StartTicks + templateEffect.RelativeStartTicks;
                newEnd = targetPhrase.StartTicks + templateEffect.RelativeEndTicks;

                if (newEnd > targetPhrase.EndTicks || newStart < targetPhrase.StartTicks)
                {
                    exclusions.Add($"Source effect '{templateEffect.SourceEffectId}' copied window [{newStart},{newEnd}) does not fit inside target phrase '{targetPhraseId}' [{targetPhrase.StartTicks},{targetPhrase.EndTicks}).");
                    continue;
                }
            }
            else
            {
                newStart = targetPhrase.StartTicks + (long)Math.Round(templateEffect.RelativeStartTicks * retimeRatio);
                newEnd = targetPhrase.StartTicks + (long)Math.Round(templateEffect.RelativeEndTicks * retimeRatio);
                if (newEnd > targetPhrase.EndTicks)
                {
                    newEnd = targetPhrase.EndTicks;
                }
            }

            var parameters = template.Mask.Palette
                ? templateEffect.Parameters
                : templateEffect.Parameters
                    .Where(kv => !kv.Key.StartsWith(PaletteParameterPrefix, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

            var candidate = new EffectInstance(
                Id: DeterministicGuid(targetPhraseId, templateEffect.SourceEffectId),
                Target: templateEffect.Target,
                Layer: templateEffect.Layer,
                StartTicks: newStart,
                EndTicks: newEnd,
                RecipeId: templateEffect.RecipeId,
                RecipeVersion: templateEffect.RecipeVersion,
                Parameters: parameters);

            AddIfUnlocked(candidate, effects, exclusions, destinationDocument);
        }

        // Orphaned overrides: the user's own explicit choice for a source effect the template no longer
        // captures. Still honored as a standalone, instance-only addition -- never silently dropped.
        foreach (var orphan in remainingOverrides.Values)
        {
            AddIfUnlocked(ToEffectInstance(orphan, targetPhraseId, orphan.SourceEffectId), effects, exclusions, destinationDocument);
        }

        return new ChoreographyPreviewResult(effects, exclusions);
    }

    private static EffectInstance ToEffectInstance(ChoreographyInstanceOverride @override, Guid targetPhraseId, Guid sourceEffectId) =>
        new(
            Id: DeterministicGuid(targetPhraseId, sourceEffectId),
            Target: @override.Target,
            Layer: @override.Layer,
            StartTicks: @override.StartTicks,
            EndTicks: @override.EndTicks,
            RecipeId: @override.RecipeId,
            RecipeVersion: @override.RecipeVersion,
            Parameters: @override.Parameters);

    /// <summary>"Respect locks" (Step 057's own implementation guidance, verbatim): never propose a candidate that overlaps an existing locked destination effect on the same target.</summary>
    private static void AddIfUnlocked(EffectInstance candidate, List<EffectInstance> effects, List<string> exclusions, SequenceDocument destinationDocument)
    {
        var conflict = destinationDocument.Effects.FirstOrDefault(existing =>
            existing.Locked
            && existing.Target == candidate.Target
            && existing.StartTicks < candidate.EndTicks
            && existing.EndTicks > candidate.StartTicks);

        if (conflict is not null)
        {
            exclusions.Add($"Would overwrite locked effect '{conflict.Id}' on '{conflict.Target.Value}' -- excluded from preview.");
            return;
        }

        effects.Add(candidate);
    }

    /// <summary>Deterministic, reproducible <see cref="EffectInstance.Id"/> for a preview effect, derived from the target phrase and originating source effect -- the same inputs always produce the same preview identity.</summary>
    private static Guid DeterministicGuid(Guid a, Guid b)
    {
        Span<byte> buffer = stackalloc byte[32];
        a.TryWriteBytes(buffer[..16]);
        b.TryWriteBytes(buffer[16..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(buffer, hash);
        return new Guid(hash[..16]);
    }

    private static void ValidateContext(ChoreographyTemplateContext context)
    {
        if (string.IsNullOrWhiteSpace(context.SourceHash))
        {
            throw new ChoreographyTemplateInvalidException(context.SongId, "SourceHash is empty -- missing media.");
        }

        if (context.MotifClustering.SongId != context.SongId)
        {
            throw new ChoreographyTemplateInvalidException(context.SongId,
                $"MotifClusteringAnalysis.SongId ({context.MotifClustering.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.MotifClustering.SourceHash != context.SourceHash)
        {
            throw new ChoreographyTemplateInvalidException(context.SongId,
                "MotifClusteringAnalysis.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (context.PhraseAnalysis.SongId != context.SongId)
        {
            throw new ChoreographyTemplateInvalidException(context.SongId,
                $"PhraseSegmentationAnalysis.SongId ({context.PhraseAnalysis.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.PhraseAnalysis.SourceHash != context.SourceHash)
        {
            throw new ChoreographyTemplateInvalidException(context.SongId,
                "PhraseSegmentationAnalysis.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (context.MotifClustering.SourcePhraseSegmentationVersionId != context.PhraseAnalysis.VersionId)
        {
            throw new ChoreographyTemplateInvalidException(context.SongId,
                "MotifClusteringAnalysis.SourcePhraseSegmentationVersionId does not match the supplied PhraseSegmentationAnalysis.VersionId -- stale or mismatched analysis.");
        }
    }

    /// <summary>
    /// Optimistic-concurrency guard: if anything is already persisted for this song, it must match
    /// <paramref name="current"/> exactly before <paramref name="next"/> is written -- "stale persisted
    /// state must not corrupt the project" (Step 057's own failure/edge case, verbatim).
    /// </summary>
    private async Task SaveGuardedAsync(ChoreographyTemplateAnalysis? current, ChoreographyTemplateAnalysis next, CancellationToken ct)
    {
        var path = _store.GetPath(next.SongId);

        if (File.Exists(path))
        {
            ChoreographyTemplateAnalysis onDisk;
            try
            {
                onDisk = await _store.LoadAsync(path, ct).ConfigureAwait(false);
            }
            catch (ChoreographyTemplateCorruptException)
            {
                onDisk = null!;
            }

            if (onDisk is not null && onDisk.VersionId != current?.VersionId)
            {
                throw new ChoreographyTemplateInvalidException(next.SongId,
                    "The persisted choreography template analysis has changed since it was last loaded -- reload before saving again.");
            }
        }
        else if (current is not null)
        {
            throw new ChoreographyTemplateInvalidException(next.SongId,
                "A 'current' analysis was supplied but none is persisted yet for this song -- reload (pass null) before defining the first template.");
        }

        ct.ThrowIfCancellationRequested();

        await _store.SaveAsync(next, ct).ConfigureAwait(false);
    }
}
