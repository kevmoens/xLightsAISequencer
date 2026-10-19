using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>Default <see cref="IEffectLockPolicy"/>. See interface doc for scope/ownership.</summary>
public sealed class EffectLockPolicy : IEffectLockPolicy
{
    public void AssertEditable(SequenceDocument document, EditScope scope)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scope);

        if (!scope.RespectLocks)
        {
            return;
        }

        foreach (var effect in document.Effects)
        {
            if (!effect.Locked)
            {
                continue;
            }

            if (!IsReachedByScope(effect, scope))
            {
                continue;
            }

            throw new EditCommandRejectedException(
                EditRejectionReason.Locked,
                $"Effect '{effect.Id}' on '{effect.Target.Value}' is locked and cannot be edited.");
        }
    }

    /// <summary>
    /// An effect is "reached" by <paramref name="scope"/> either by exact identity
    /// (<see cref="EditScope.EffectIds"/>, when supplied) or by being on an in-scope model and
    /// overlapping the scope's half-open time window (PHASE-01 Section 2: <c>[Start, End)</c>
    /// overlap is <c>effect.Start &lt; scope.End &amp;&amp; effect.End &gt; scope.Start</c>).
    /// </summary>
    private static bool IsReachedByScope(EffectInstance effect, EditScope scope)
    {
        if (scope.EffectIds is not null)
        {
            return scope.EffectIds.Contains(effect.Id);
        }

        return scope.Models.Contains(effect.Target)
            && effect.StartTicks < scope.Time.EndTicks
            && effect.EndTicks > scope.Time.StartTicks;
    }
}
