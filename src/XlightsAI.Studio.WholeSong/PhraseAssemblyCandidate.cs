using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 091's own input shape: one already-accepted phrase's own effects (e.g. from an earlier
/// phase's accepted <c>ChoreographyCompilationResult.Document</c>), scoped to <see cref="Window"/> -- the
/// phrase's own canonical bar/tick range (PHASE-08 <c>PhraseCandidate.StartTicks</c>/<c>EndTicks</c>).
/// <see cref="Effects"/> are deliberately *not* required to lie fully inside <see cref="Window"/> --
/// an effect bleeding past its own phrase's boundary into a neighboring phrase's territory is exactly the
/// "cross-phrase overlapping effect" case <see cref="PhraseSeamAssembler"/> must detect and resolve per
/// the caller's own chosen <see cref="SeamStrategy"/>.
/// </summary>
/// <param name="PhraseId">This phrase's own stable identity (e.g. <c>PhraseCandidate.Id</c>) -- used to match this candidate against the <see cref="PhraseSeamRequest"/> entries that reference it.</param>
/// <param name="Window">This phrase's own canonical half-open <c>[Start,End)</c> tick range.</param>
/// <param name="Effects">Every effect already accepted for this phrase, unmodified.</param>
public sealed record PhraseAssemblyCandidate(
    Guid PhraseId,
    TimeWindow Window,
    IReadOnlyList<EffectInstance> Effects)
{
    /// <summary>
    /// Validates this candidate's own shape, independent of any other candidate or seam request --
    /// those cross-candidate checks are <see cref="PhraseSeamAssembler"/>'s own, later responsibility.
    /// </summary>
    /// <exception cref="PhraseAssemblyInvalidException">
    /// <see cref="PhraseId"/> is empty, <see cref="Window"/> does not satisfy <c>StartTicks &lt; EndTicks</c>,
    /// <see cref="Effects"/> contains a duplicate <see cref="EffectInstance.Id"/>, or any effect does not
    /// itself satisfy <c>StartTicks &lt; EndTicks</c>.
    /// </exception>
    public void Validate()
    {
        if (PhraseId == Guid.Empty)
        {
            throw new PhraseAssemblyInvalidException("A phrase assembly candidate's PhraseId must not be empty.");
        }

        if (Window.EndTicks <= Window.StartTicks)
        {
            throw new PhraseAssemblyInvalidException(
                $"Phrase '{PhraseId}' window must satisfy StartTicks < EndTicks (was {Window.StartTicks}..{Window.EndTicks}).");
        }

        ArgumentNullException.ThrowIfNull(Effects);

        var seen = new HashSet<Guid>();
        foreach (var effect in Effects)
        {
            ArgumentNullException.ThrowIfNull(effect);

            if (!seen.Add(effect.Id))
            {
                throw new PhraseAssemblyInvalidException($"Phrase '{PhraseId}' lists effect '{effect.Id}' more than once.");
            }

            if (effect.EndTicks <= effect.StartTicks)
            {
                throw new PhraseAssemblyInvalidException(
                    $"Phrase '{PhraseId}' effect '{effect.Id}' must satisfy StartTicks < EndTicks (was {effect.StartTicks}..{effect.EndTicks}).");
            }
        }
    }
}
