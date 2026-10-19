using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Shared "apply a validated <see cref="EditProposal"/> to a document's effect list" logic, used by both
/// <see cref="EditProposalDryRunService.Preview"/> (sandbox only) and
/// <see cref="EditProposalApprovalService.Accept"/> (real commit) so the exact same mutation the user
/// previewed is the exact same mutation that gets committed -- never a second, independently-written code
/// path that could silently diverge from what the preview showed.
///
/// <para>
/// <b>Disclosed architecture decision.</b> This does not route through the already-accepted
/// <c>IEditTransactionService.ApplyAsync(SetEffectParameters)</c> pipeline. That pipeline validates every
/// parameter key/value against the effect's own <see cref="IEffectRecipeRegistry"/> recipe (PHASE-03 Step
/// 020: "do not invent xLights effect parameters") -- but the only recipe registered anywhere in this
/// repository today (<see cref="EffectRecipeRegistry.SimpleOnRecipeId"/>) recognizes exactly one parameter,
/// <c>EffectFamily=On</c>. None of Step 073's five quick-action kinds (brightness/direction/speed/snap/
/// role) are themselves verified xLights effect-settings parameters; forcing them through that recipe gate
/// would make every dry-run proposal reject unconditionally, which cannot be this step's intent. Instead,
/// every operation's own <see cref="StructuredEditOperation.Parameters"/> are recorded on the affected
/// effect's own <see cref="EffectInstance.Parameters"/> bag under a disjoint <c>"AiEdit.&lt;Kind&gt;.&lt;Key&gt;"</c>
/// namespace (or a bare <c>"AiEdit.&lt;Kind&gt;"</c> marker for a parameterless kind like
/// <see cref="StructuredEditOperationKinds.SnapToGrid"/>) -- explicitly disclosed proposal metadata, never
/// mixed with or validated as a real xLights recipe parameter. Translating this metadata into a concrete,
/// recipe-verified xLights effect is a later step's/phase's own scope once more than one recipe exists.
/// </para>
/// </summary>
internal static class ProposalApplier
{
    public static readonly IReadOnlySet<string> KnownOperationKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        StructuredEditOperationKinds.SetBrightness,
        StructuredEditOperationKinds.SetDirection,
        StructuredEditOperationKinds.SetSpeed,
        StructuredEditOperationKinds.SnapToGrid,
        StructuredEditOperationKinds.ReplaceRole,
    };

    public readonly record struct ApplyResult(
        IReadOnlyList<EffectInstance> AfterEffects,
        IReadOnlyList<EffectInstance> BeforeAffected,
        IReadOnlyList<EffectInstance> AfterAffected,
        ProposalDiff Diff);

    /// <summary>
    /// Applies every operation in <paramref name="proposal"/> to a fresh copy of
    /// <paramref name="document"/>'s own effect list. An effect is "reached" by an operation using the
    /// same half-open overlap rule <see cref="XlightsAI.Studio.Editing.EffectLockPolicy"/> already uses:
    /// on an in-scope target and <c>effect.Start &lt; op.Range.End &amp;&amp; effect.End &gt; op.Range.Start</c>.
    /// Never mutates <paramref name="document"/> itself.
    /// </summary>
    public static ApplyResult Apply(SequenceDocument document, EditProposal proposal)
    {
        var before = document.Effects;
        var after = before.ToList();
        var changedIds = new HashSet<Guid>();

        foreach (var operation in proposal.Operations)
        {
            var targetSet = operation.Targets.ToHashSet();
            for (var i = 0; i < after.Count; i++)
            {
                var effect = after[i];
                if (!targetSet.Contains(effect.Target))
                {
                    continue;
                }

                if (!(effect.StartTicks < operation.Range.EndTicks && effect.EndTicks > operation.Range.StartTicks))
                {
                    continue;
                }

                var updatedParameters = new Dictionary<string, string>(effect.Parameters, StringComparer.Ordinal);
                if (operation.Parameters.Count == 0)
                {
                    updatedParameters[$"AiEdit.{operation.Kind}"] = "true";
                }
                else
                {
                    foreach (var (key, value) in operation.Parameters)
                    {
                        updatedParameters[$"AiEdit.{operation.Kind}.{key}"] = value;
                    }
                }

                after[i] = effect with { Parameters = updatedParameters };
                changedIds.Add(effect.Id);
            }
        }

        var beforeAffected = before.Where(e => changedIds.Contains(e.Id)).ToList();
        var afterAffected = after.Where(e => changedIds.Contains(e.Id)).ToList();

        var affectedModels = afterAffected
            .Select(e => e.Target)
            .Distinct()
            .OrderBy(m => m.Value, StringComparer.Ordinal)
            .ToList();

        var affectedTime = afterAffected.Count > 0
            ? new TimeWindow(afterAffected.Min(e => e.StartTicks), afterAffected.Max(e => e.EndTicks))
            : UnionOfRanges(proposal.Operations);

        var diff = new ProposalDiff(changedIds.Count, affectedModels, affectedTime);
        return new ApplyResult(after, beforeAffected, afterAffected, diff);
    }

    private static TimeWindow UnionOfRanges(IReadOnlyList<StructuredEditOperation> operations)
    {
        var start = operations.Min(o => o.Range.StartTicks);
        var end = operations.Max(o => o.Range.EndTicks);
        return new TimeWindow(start, end);
    }
}
