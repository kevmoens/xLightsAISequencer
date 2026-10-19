using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Composition;

/// <summary>Default <see cref="IChoreographyCompiler"/>. See the interface and <c>implementation-evidence/PHASE-10/STEP-071.md</c> for the full rationale.</summary>
public sealed class ChoreographyCompiler : IChoreographyCompiler
{
    /// <summary>
    /// The highest effect layer this compiler will ever assign a candidate to. A deliberate, disclosed
    /// bound -- "layer/priority strategies" (this step's own heading, verbatim) means a candidate that
    /// cannot find room among a reasonable number of coexisting layers is dropped with a clear reason,
    /// never silently stacked onto an unbounded, practically unrenderable number of layers.
    /// </summary>
    public const int MaxLayers = 8;

    private readonly IRolePrecedenceResolver _precedenceResolver;
    private readonly IEffectLockPolicy _lockPolicy;
    private readonly IEffectRecipeRegistry _recipeRegistry;

    public ChoreographyCompiler(IRolePrecedenceResolver precedenceResolver, IEffectLockPolicy lockPolicy, IEffectRecipeRegistry recipeRegistry)
    {
        ArgumentNullException.ThrowIfNull(precedenceResolver);
        ArgumentNullException.ThrowIfNull(lockPolicy);
        ArgumentNullException.ThrowIfNull(recipeRegistry);
        _precedenceResolver = precedenceResolver;
        _lockPolicy = lockPolicy;
        _recipeRegistry = recipeRegistry;
    }

    public ChoreographyCompilationResult Compile(
        SequenceDocument baseDocument,
        IReadOnlyList<ChoreographyCandidate> candidates,
        EditScope allowedScope)
    {
        ArgumentNullException.ThrowIfNull(baseDocument);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(allowedScope);

        if (candidates.Count == 0)
        {
            throw new CompositionInvalidException(
                "At least one choreography candidate is required to compose -- a clean machine with no " +
                "planned role gestures yet has none, and must be reported clearly rather than compiled " +
                "as a silent no-op.");
        }

        var seenIds = new HashSet<Guid>();
        foreach (var candidate in candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            candidate.Validate();

            if (!seenIds.Add(candidate.Id))
            {
                throw new CompositionInvalidException($"Duplicate choreography candidate id '{candidate.Id}'.");
            }

            if (!allowedScope.Models.Contains(candidate.Target))
            {
                throw new CompositionInvalidException(
                    $"Choreography candidate '{candidate.Id}' targets '{candidate.Target.Value}', which is " +
                    "outside the permitted/verified targetable prop set for this compile; no plan was composed.");
            }

            if (candidate.Window.StartTicks < allowedScope.Time.StartTicks || candidate.Window.EndTicks > allowedScope.Time.EndTicks)
            {
                throw new CompositionInvalidException(
                    $"Choreography candidate '{candidate.Id}' window [{candidate.Window.StartTicks}, {candidate.Window.EndTicks}) " +
                    $"falls outside the permitted edit scope's own time window [{allowedScope.Time.StartTicks}, {allowedScope.Time.EndTicks}); " +
                    "no plan was composed.");
            }
        }

        // Never touch a locked effect: if the scope this compile is permitted to touch overlaps any
        // already-locked effect, the entire compile is rejected before a single candidate is resolved --
        // the identical "no partial mutation" rule every Editing-project command already enforces for a
        // single EditCommand, applied here to a whole batch of candidates at once.
        _lockPolicy.AssertEditable(baseDocument, allowedScope);

        var ordered = OrderByPrecedence(candidates);

        // Seed per-target layer occupancy from every already-accepted effect in baseDocument (locked or
        // not) -- a new candidate must never silently share a layer with ANY existing effect. "No
        // unintended model conflicts" (required acceptance, verbatim) is a property of the whole
        // document, not only of its locked effects.
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

        var newEffects = new List<EffectInstance>();
        var resultsById = new Dictionary<Guid, ChoreographyCandidateResult>(candidates.Count);
        var warnings = new List<string>();

        foreach (var candidate in ordered)
        {
            if (!occupancy.TryGetValue(candidate.Target, out var occupied))
            {
                occupied = [];
                occupancy[candidate.Target] = occupied;
            }

            var chosenLayer = -1;
            for (var layer = 0; layer < MaxLayers; layer++)
            {
                var collides = occupied.Any(o => o.Layer == layer && Overlaps(o.Start, o.End, candidate.Window.StartTicks, candidate.Window.EndTicks));
                if (!collides)
                {
                    chosenLayer = layer;
                    break;
                }
            }

            if (chosenLayer < 0)
            {
                var reason =
                    $"'{candidate.Assignment.Role}' role (track '{candidate.Assignment.SourceTrackId}') on " +
                    $"'{candidate.Target.Value}' at [{candidate.Window.StartTicks}, {candidate.Window.EndTicks}) " +
                    $"collides with a higher-precedence effect on every layer up to {MaxLayers}; dropped to " +
                    "avoid an unintended model conflict.";
                warnings.Add(reason);
                resultsById[candidate.Id] = new ChoreographyCandidateResult(candidate, ChoreographyResolutionDecision.Dropped, null, reason);
                continue;
            }

            var effect = _recipeRegistry.CreateVerified(candidate.RecipeId, candidate.Target, candidate.Window.StartTicks, candidate.Window.EndTicks)
                with
            { Layer = chosenLayer };

            occupied.Add((chosenLayer, candidate.Window.StartTicks, candidate.Window.EndTicks));
            newEffects.Add(effect);

            if (chosenLayer == 0)
            {
                resultsById[candidate.Id] = new ChoreographyCandidateResult(candidate, ChoreographyResolutionDecision.Accepted, chosenLayer, null);
            }
            else
            {
                var reason =
                    $"'{candidate.Assignment.Role}' role (track '{candidate.Assignment.SourceTrackId}') on " +
                    $"'{candidate.Target.Value}' was moved to layer {chosenLayer} to coexist with a " +
                    "higher-precedence effect already occupying a lower layer over an overlapping window -- " +
                    "purposeful layer coordination, not a dropped effect.";
                warnings.Add(reason);
                resultsById[candidate.Id] = new ChoreographyCandidateResult(candidate, ChoreographyResolutionDecision.Reassigned, chosenLayer, reason);
            }
        }

        var document = newEffects.Count == 0
            ? baseDocument
            : baseDocument with { Revision = baseDocument.Revision + 1, Effects = baseDocument.Effects.Concat(newEffects).ToList() };

        // Restore the caller's own original candidate order for Results -- precedence order only governs
        // resolution, not the shape of the returned report (the same convention VisualBudgetResolver
        // already established).
        var resultsByOriginalOrder = candidates.Select(c => resultsById[c.Id]).ToList();

        return new ChoreographyCompilationResult(document, resultsByOriginalOrder, warnings);
    }

    /// <summary>
    /// Orders <paramref name="candidates"/> highest-precedence-first, reusing the already-accepted
    /// <see cref="IRolePrecedenceResolver"/> against the distinct set of underlying assignments (so two
    /// candidates sharing an equal-valued <see cref="MusicalRoleAssignment"/> -- e.g. two gestures for the
    /// same lead track within one phrase -- unambiguously share the same rank), then breaking ties
    /// deterministically by window start, target, and finally the candidate's own id.
    /// </summary>
    private IReadOnlyList<ChoreographyCandidate> OrderByPrecedence(IReadOnlyList<ChoreographyCandidate> candidates)
    {
        var distinctAssignments = candidates.Select(c => c.Assignment).Distinct().ToList();
        var rankedAssignments = _precedenceResolver.ResolveOrder(distinctAssignments);

        var rank = new Dictionary<MusicalRoleAssignment, int>();
        for (var i = 0; i < rankedAssignments.Count; i++)
        {
            rank[rankedAssignments[i]] = i;
        }

        return candidates
            .OrderBy(c => rank[c.Assignment])
            .ThenBy(c => c.Window.StartTicks)
            .ThenBy(c => c.Target.Value, StringComparer.Ordinal)
            .ThenBy(c => c.Id)
            .ToList();
    }

    private static bool Overlaps(long aStart, long aEnd, long bStart, long bEnd) => aStart < bEnd && bStart < aEnd;
}
