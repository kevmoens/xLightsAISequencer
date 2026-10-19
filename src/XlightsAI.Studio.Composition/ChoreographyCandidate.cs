using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 071 ("Compose and resolve overlapping effects with layer/priority strategies"): one
/// candidate concrete effect placement competing for layer space on <see cref="Target"/> over
/// <see cref="Window"/>. Deliberately wraps an already-accepted <see cref="MusicalRoleAssignment"/>
/// (Step 066's own contract) rather than duplicating its <c>SourceTrackId</c>/<c>Role</c>/
/// <c>PrecedenceTier</c>/<c>Priority</c> fields -- <see cref="ChoreographyCompiler"/> reuses the
/// already-accepted <see cref="IRolePrecedenceResolver"/> tie-break rule directly against
/// <see cref="Assignment"/> instead of re-implementing it, the same convention Step 068's own
/// <see cref="VisualBudgetCandidate"/> already established.
/// </summary>
/// <param name="Id">This candidate's own stable identity, used only for deterministic tie-breaking and result correlation -- never written to any exported effect.</param>
/// <param name="Assignment">The underlying role assignment this candidate realizes; also this candidate's own precedence identity for <see cref="IRolePrecedenceResolver"/>.</param>
/// <param name="Target">The concrete, already-imported model/group this candidate would place an effect on.</param>
/// <param name="Window">The time span this effect would occupy if admitted unmodified.</param>
/// <param name="RecipeId">The already-verified <see cref="XlightsAI.Studio.Xlights.IEffectRecipeRegistry"/> recipe id this candidate would compile to -- never an invented/free-form effect shape.</param>
public sealed record ChoreographyCandidate(
    Guid Id,
    MusicalRoleAssignment Assignment,
    ModelId Target,
    TimeWindow Window,
    string RecipeId)
{
    /// <summary>
    /// Validates this candidate's own shape, independent of any other candidate, the base document, or
    /// the permitted edit scope -- those cross-candidate/cross-document checks are
    /// <see cref="ChoreographyCompiler"/>'s own, later responsibility.
    /// </summary>
    /// <exception cref="CompositionInvalidException">
    /// <see cref="Id"/> is empty, <see cref="Target"/>'s value is blank, <see cref="Window"/> is not a
    /// valid, non-empty, non-negative <c>[start, end)</c> range, or <see cref="RecipeId"/> is blank.
    /// </exception>
    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new CompositionInvalidException("A choreography candidate's Id must not be empty.");
        }

        ArgumentNullException.ThrowIfNull(Assignment);

        if (string.IsNullOrWhiteSpace(Target.Value))
        {
            throw new CompositionInvalidException($"Choreography candidate '{Id}' target model/group id must not be blank.");
        }

        if (Window.StartTicks < 0)
        {
            throw new CompositionInvalidException($"Choreography candidate '{Id}' window start must not be negative (was {Window.StartTicks}).");
        }

        if (Window.EndTicks <= Window.StartTicks)
        {
            throw new CompositionInvalidException(
                $"Choreography candidate '{Id}' window must satisfy StartTicks < EndTicks (was {Window.StartTicks}..{Window.EndTicks}).");
        }

        if (string.IsNullOrWhiteSpace(RecipeId))
        {
            throw new CompositionInvalidException($"Choreography candidate '{Id}' recipe id must not be blank.");
        }
    }
}
