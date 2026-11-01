using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>Default <see cref="IPhraseSeamAssembler"/>. See the interface for the full contract.</summary>
public sealed class PhraseSeamAssembler : IPhraseSeamAssembler
{
    /// <summary>
    /// The highest effect layer this assembler will ever assign a cross-phrase effect to -- the same
    /// deliberate, disclosed bound <see cref="XlightsAI.Studio.Composition.ChoreographyCompiler.MaxLayers"/>
    /// already established: an effect that cannot find room among a reasonable number of coexisting
    /// layers exhausts the retry budget (<see cref="PhraseAssemblyExhaustedException"/>) rather than
    /// stacking onto an unbounded, practically unrenderable number of layers.
    /// </summary>
    public const int MaxLayers = 8;

    private readonly IEffectLockPolicy _lockPolicy;

    public PhraseSeamAssembler(IEffectLockPolicy lockPolicy)
    {
        ArgumentNullException.ThrowIfNull(lockPolicy);
        _lockPolicy = lockPolicy;
    }

    public PhraseAssemblyResult Assemble(
        SequenceDocument baseDocument,
        IReadOnlyList<PhraseAssemblyCandidate> phrasesInOrder,
        IReadOnlyList<PhraseSeamRequest> seamRequests,
        EditScope allowedScope)
    {
        ArgumentNullException.ThrowIfNull(baseDocument);
        ArgumentNullException.ThrowIfNull(phrasesInOrder);
        ArgumentNullException.ThrowIfNull(seamRequests);
        ArgumentNullException.ThrowIfNull(allowedScope);

        if (phrasesInOrder.Count == 0)
        {
            throw new PhraseAssemblyInvalidException(
                "At least one phrase assembly candidate is required to assemble -- a clean machine with " +
                "no accepted phrases yet has none, and must be reported clearly rather than assembled as a " +
                "silent no-op.");
        }

        var seenPhraseIds = new HashSet<Guid>();
        foreach (var phrase in phrasesInOrder)
        {
            ArgumentNullException.ThrowIfNull(phrase);
            phrase.Validate();

            if (!seenPhraseIds.Add(phrase.PhraseId))
            {
                throw new PhraseAssemblyInvalidException($"Duplicate phrase id '{phrase.PhraseId}' in phrasesInOrder.");
            }

            foreach (var effect in phrase.Effects)
            {
                if (!allowedScope.Models.Contains(effect.Target))
                {
                    throw new PhraseAssemblyInvalidException(
                        $"Phrase '{phrase.PhraseId}' effect '{effect.Id}' targets '{effect.Target.Value}', " +
                        "which is outside the permitted/verified targetable prop set for this assembly; " +
                        "no plan was assembled.");
                }

                if (effect.StartTicks < allowedScope.Time.StartTicks || effect.EndTicks > allowedScope.Time.EndTicks)
                {
                    throw new PhraseAssemblyInvalidException(
                        $"Phrase '{phrase.PhraseId}' effect '{effect.Id}' window [{effect.StartTicks}, {effect.EndTicks}) " +
                        $"falls outside the permitted edit scope's own time window [{allowedScope.Time.StartTicks}, " +
                        $"{allowedScope.Time.EndTicks}); no plan was assembled.");
                }
            }
        }

        // "Assemble canonical phrase plans" (implementation guidance, verbatim): the caller's own supplied
        // order is the canonical order; a malformed (out-of-order or phrase-window-overlapping) input is
        // rejected rather than silently re-sorted.
        for (var i = 1; i < phrasesInOrder.Count; i++)
        {
            var prev = phrasesInOrder[i - 1];
            var next = phrasesInOrder[i];
            if (next.Window.StartTicks < prev.Window.EndTicks)
            {
                throw new PhraseAssemblyInvalidException(
                    $"Phrase '{next.PhraseId}' window starts at {next.Window.StartTicks}, before phrase " +
                    $"'{prev.PhraseId}' window ends at {prev.Window.EndTicks}; phrasesInOrder must be " +
                    "strictly ascending and non-overlapping.");
            }
        }

        var expectedPairCount = phrasesInOrder.Count - 1;
        if (seamRequests.Count != expectedPairCount)
        {
            throw new PhraseAssemblyInvalidException(
                $"Expected exactly {expectedPairCount} seam request(s) for {phrasesInOrder.Count} phrase(s), " +
                $"but received {seamRequests.Count}.");
        }

        var seamByPair = new Dictionary<(Guid From, Guid To), SeamStrategy>();
        foreach (var request in seamRequests)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!seamByPair.TryAdd((request.FromPhraseId, request.ToPhraseId), request.Strategy))
            {
                throw new PhraseAssemblyInvalidException(
                    $"Duplicate seam request for phrase pair '{request.FromPhraseId}' -> '{request.ToPhraseId}'.");
            }
        }

        for (var i = 1; i < phrasesInOrder.Count; i++)
        {
            var key = (phrasesInOrder[i - 1].PhraseId, phrasesInOrder[i].PhraseId);
            if (!seamByPair.ContainsKey(key))
            {
                throw new PhraseAssemblyInvalidException(
                    $"Missing seam request for consecutive phrase pair '{key.Item1}' -> '{key.Item2}'.");
            }
        }

        // Never touch a locked effect: the whole assembly is rejected before a single phrase is resolved --
        // the identical "no partial mutation" rule ChoreographyCompiler already enforces for a whole batch
        // of candidates at once.
        _lockPolicy.AssertEditable(baseDocument, allowedScope);

        var working = phrasesInOrder.Select(p => p.Effects.ToList()).ToList();
        var seams = new List<PhraseSeamPlan>(expectedPairCount);
        var allWarnings = new List<string>();

        for (var i = 1; i < phrasesInOrder.Count; i++)
        {
            var prevPhrase = phrasesInOrder[i - 1];
            var nextPhrase = phrasesInOrder[i];
            var strategy = seamByPair[(prevPhrase.PhraseId, nextPhrase.PhraseId)];
            var seamWindow = new TimeWindow(prevPhrase.Window.EndTicks, nextPhrase.Window.StartTicks);
            var warnings = new List<string>();

            var prevEffects = working[i - 1];
            var nextEffects = working[i];

            var prevOverflowIdx = new List<int>();
            for (var e = 0; e < prevEffects.Count; e++)
            {
                if (prevEffects[e].EndTicks > prevPhrase.Window.EndTicks)
                {
                    prevOverflowIdx.Add(e);
                }
            }

            var nextUnderflowIdx = new List<int>();
            for (var e = 0; e < nextEffects.Count; e++)
            {
                if (nextEffects[e].StartTicks < nextPhrase.Window.StartTicks)
                {
                    nextUnderflowIdx.Add(e);
                }
            }

            var hasOverlap = prevOverflowIdx.Count > 0 || nextUnderflowIdx.Count > 0;

            switch (strategy)
            {
                case SeamStrategy.Trim:
                case SeamStrategy.Release:
                    {
                        var label = strategy == SeamStrategy.Trim ? "Trim" : "Release";
                        if (!hasOverlap)
                        {
                            warnings.Add(
                                $"{label} requested at seam '{prevPhrase.PhraseId}' -> '{nextPhrase.PhraseId}' " +
                                "but no overlapping effect was found; applied as a no-op.");
                            break;
                        }

                        ClipOverflow(prevEffects, prevOverflowIdx, nextPhrase.Window.StartTicks, clipEnd: true,
                            label, prevPhrase.PhraseId, nextPhrase.PhraseId, warnings);
                        ClipOverflow(nextEffects, nextUnderflowIdx, prevPhrase.Window.EndTicks, clipEnd: false,
                            label, prevPhrase.PhraseId, nextPhrase.PhraseId, warnings);
                        break;
                    }

                case SeamStrategy.Hold:
                    {
                        if (hasOverlap)
                        {
                            throw new PhraseAssemblyInvalidException(
                                $"Hold strategy requested at seam '{prevPhrase.PhraseId}' -> '{nextPhrase.PhraseId}' " +
                                "but an effect overlaps the phrase boundary; Hold requires a clean, non-overlapping gap.");
                        }

                        var gap = seamWindow.EndTicks - seamWindow.StartTicks;
                        if (gap > 0)
                        {
                            var extended = 0;
                            for (var e = 0; e < prevEffects.Count; e++)
                            {
                                if (prevEffects[e].EndTicks == prevPhrase.Window.EndTicks)
                                {
                                    prevEffects[e] = prevEffects[e] with { EndTicks = nextPhrase.Window.StartTicks };
                                    extended++;
                                }
                            }

                            if (extended == 0)
                            {
                                warnings.Add(
                                    $"Hold requested at seam '{prevPhrase.PhraseId}' -> '{nextPhrase.PhraseId}' but " +
                                    $"no effect reaches the phrase boundary to extend; a gap of {gap} tick(s) remains.");
                            }
                        }

                        break;
                    }

                case SeamStrategy.Bridge:
                    {
                        if (hasOverlap)
                        {
                            throw new PhraseAssemblyInvalidException(
                                $"Bridge strategy requested at seam '{prevPhrase.PhraseId}' -> '{nextPhrase.PhraseId}' " +
                                "but an effect overlaps the phrase boundary; Bridge requires a clean, non-overlapping gap.");
                        }

                        var gap = seamWindow.EndTicks - seamWindow.StartTicks;
                        if (gap > 0)
                        {
                            warnings.Add(
                                $"Bridge requested at seam '{prevPhrase.PhraseId}' -> '{nextPhrase.PhraseId}'; a gap " +
                                $"of {gap} tick(s) between phrases remains, pending a later transition-gesture step.");
                        }

                        break;
                    }

                default:
                    throw new PhraseAssemblyInvalidException($"Unknown seam strategy '{strategy}'.");
            }

            seams.Add(new PhraseSeamPlan(prevPhrase.PhraseId, nextPhrase.PhraseId, seamWindow, strategy, warnings));
            allWarnings.AddRange(warnings);
        }

        // Cross-phrase layer occupancy, seeded from every already-accepted effect in baseDocument -- the
        // same half-open overlap test and bounded layer-reassignment retry ChoreographyCompiler already
        // established. "Respect... layer boundaries" (this step's own implementation guidance, verbatim).
        var occupancy = new Dictionary<ModelId, List<(int Layer, long Start, long End)>>();
        foreach (var effect in baseDocument.Effects)
        {
            if (!occupancy.TryGetValue(effect.Target, out var existing))
            {
                existing = [];
                occupancy[effect.Target] = existing;
            }

            existing.Add((effect.Layer, effect.StartTicks, effect.EndTicks));
        }

        var finalEffects = new List<EffectInstance>();
        foreach (var effects in working)
        {
            foreach (var effect in effects)
            {
                if (!occupancy.TryGetValue(effect.Target, out var occupied))
                {
                    occupied = [];
                    occupancy[effect.Target] = occupied;
                }

                var chosenLayer = -1;
                for (var layer = 0; layer < MaxLayers; layer++)
                {
                    var collides = occupied.Any(o => o.Layer == layer && Overlaps(o.Start, o.End, effect.StartTicks, effect.EndTicks));
                    if (!collides)
                    {
                        chosenLayer = layer;
                        break;
                    }
                }

                if (chosenLayer < 0)
                {
                    // "...exhausted retry budget must not auto-commit" (required failure/edge case,
                    // verbatim): no partial document is ever returned from this method past this point.
                    throw new PhraseAssemblyExhaustedException(
                        effect.Target, new TimeWindow(effect.StartTicks, effect.EndTicks), MaxLayers);
                }

                var placed = chosenLayer == effect.Layer ? effect : effect with { Layer = chosenLayer };
                occupied.Add((chosenLayer, placed.StartTicks, placed.EndTicks));
                finalEffects.Add(placed);
            }
        }

        var document = finalEffects.Count == 0
            ? baseDocument
            : baseDocument with { Revision = baseDocument.Revision + 1, Effects = baseDocument.Effects.Concat(finalEffects).ToList() };

        return new PhraseAssemblyResult(document, seams, allWarnings);
    }

    /// <summary>
    /// Clips or drops every effect named by <paramref name="overflowIdx"/> (indices into
    /// <paramref name="effects"/>, ascending) against <paramref name="boundaryTicks"/>. Processes indices
    /// in descending order so earlier indices remain valid across an in-place <see cref="List{T}.RemoveAt"/>.
    /// </summary>
    private static void ClipOverflow(
        List<EffectInstance> effects, List<int> overflowIdx, long boundaryTicks, bool clipEnd,
        string label, Guid fromPhraseId, Guid toPhraseId, List<string> warnings)
    {
        foreach (var idx in overflowIdx.OrderByDescending(i => i))
        {
            var effect = effects[idx];
            if (clipEnd)
            {
                if (boundaryTicks <= effect.StartTicks)
                {
                    warnings.Add(
                        $"{label} at seam '{fromPhraseId}' -> '{toPhraseId}': effect '{effect.Id}' on " +
                        $"'{effect.Target.Value}' was entirely overlapped by the next phrase and was dropped.");
                    effects.RemoveAt(idx);
                    continue;
                }

                warnings.Add(
                    $"{label} at seam '{fromPhraseId}' -> '{toPhraseId}': effect '{effect.Id}' on " +
                    $"'{effect.Target.Value}' end clipped from {effect.EndTicks} to {boundaryTicks}.");
                effects[idx] = effect with { EndTicks = boundaryTicks };
            }
            else
            {
                if (boundaryTicks >= effect.EndTicks)
                {
                    warnings.Add(
                        $"{label} at seam '{fromPhraseId}' -> '{toPhraseId}': effect '{effect.Id}' on " +
                        $"'{effect.Target.Value}' was entirely overlapped by the previous phrase and was dropped.");
                    effects.RemoveAt(idx);
                    continue;
                }

                warnings.Add(
                    $"{label} at seam '{fromPhraseId}' -> '{toPhraseId}': effect '{effect.Id}' on " +
                    $"'{effect.Target.Value}' start clipped from {effect.StartTicks} to {boundaryTicks}.");
                effects[idx] = effect with { StartTicks = boundaryTicks };
            }
        }
    }

    private static bool Overlaps(long aStart, long aEnd, long bStart, long bEnd) => aStart < bEnd && bStart < aEnd;
}
