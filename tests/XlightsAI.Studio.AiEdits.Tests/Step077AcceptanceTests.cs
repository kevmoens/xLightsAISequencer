using System.Security.Cryptography;
using System.Text.Json;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits.Tests;

/// <summary>
/// PHASE-11 Step 077 ("Implement explicit motif propagation and edit-scope intersection validation").
/// Required acceptance (verbatim): "Locked/out-of-scope effects cannot change even with malicious
/// proposal." Required failure/edge case (verbatim): "Out-of-scope, locked or stale revision must be
/// rejected without partial mutation." These tests exercise the real
/// <see cref="MotifPropagationExpander"/>, <see cref="MotifPropagationValidator"/>,
/// <see cref="MotifPropagationDryRunService"/>, <see cref="MotifPropagationApprovalService"/> and
/// <see cref="EffectLockPolicy"/> (no mocking library) against a hand-built fixture with two linked motif
/// occurrences plus a third, independent (locked) occurrence standing in for "Roofline" -- the phase gate's
/// own "verify roofline hash unchanged" invariant, now checked across a propagated, multi-occurrence edit.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Step077AcceptanceTests
{
    private static readonly EffectRecipeRegistry Recipes = new();

    private static EffectInstance On(string target, long startTicks, long endTicks, bool locked = false) =>
        Recipes.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId(target), startTicks, endTicks) with { Locked = locked };

    /// <summary>
    /// Five-model gate-demo fixture: "Roofline" is permanently locked and out of every scope used below
    /// (the phase gate's own "outside the selected arches" prop); "Arch 1"/"Arch 2" is the originally
    /// selected occurrence; "Arch 3"/"Arch 4" is a second, linked motif occurrence with one locked effect
    /// ("Arch 4") standing in for a malicious/overreaching propagation target; "Arch 5"/"Arch 6" is a
    /// third, fully unlocked linked occurrence that propagation may legitimately reach.
    /// </summary>
    private static SequenceDocument BuildFixture() => new(
        Guid.NewGuid(),
        Revision: 1,
        Effects:
        [
            On("Roofline", 0, 500_000_000, locked: true),
            On("Arch 1", 0, 100_000_000),
            On("Arch 2", 0, 100_000_000),
            On("Arch 3", 200_000_000, 300_000_000),
            On("Arch 4", 200_000_000, 300_000_000, locked: true),
            On("Arch 5", 400_000_000, 500_000_000),
            On("Arch 6", 400_000_000, 500_000_000),
        ]);

    private static EditScope OccurrenceAScope(SequenceDocument doc) => new(
        new TimeWindow(0, 100_000_000),
        new HashSet<ModelId> { Arch(doc, 1), Arch(doc, 2) });

    private static EditScope OccurrenceBScope(SequenceDocument doc) => new(
        new TimeWindow(200_000_000, 300_000_000),
        new HashSet<ModelId> { Arch(doc, 3), Arch(doc, 4) });

    private static EditScope OccurrenceCScope(SequenceDocument doc) => new(
        new TimeWindow(400_000_000, 500_000_000),
        new HashSet<ModelId> { Arch(doc, 5), Arch(doc, 6) });

    private static ModelId Arch(SequenceDocument doc, int n) =>
        doc.Effects.Single(e => e.Target.Value == $"Arch {n}").Target;

    private static EditProposal BaseProposal(SequenceDocument doc) => new(
        Guid.NewGuid(),
        doc.Revision,
        [
            new StructuredEditOperation(
                "op-1",
                StructuredEditOperationKinds.SetBrightness,
                new TimeWindow(0, 100_000_000),
                [Arch(doc, 1), Arch(doc, 2)],
                new Dictionary<string, string> { ["Level"] = "80" }),
        ],
        "rising piano pops");

    private static string RooflineHash(SequenceDocument doc)
    {
        var roofline = doc.Effects.Single(e => e.Target.Value == "Roofline");
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(roofline)));
    }

    /// <summary>
    /// The phase gate's own named worked example, narrowed to this step's own scope: propagate the base
    /// proposal to the one fully-unlocked linked occurrence (C), preview every affected occurrence at
    /// once, then accept -- exactly one transaction touching Arch 1/2 (original) and Arch 5/6
    /// (propagated); Roofline and the untouched occurrence (Arch 3/4) are byte-identical afterward.
    /// </summary>
    [Fact]
    public async Task Step077_Acceptance_PropagateToUnlockedOccurrence_ChangesOnlyIncludedOccurrences()
    {
        var doc = BuildFixture();
        var rooflineHashBefore = RooflineHash(doc);
        var baseScope = OccurrenceAScope(doc);
        var baseProposal = BaseProposal(doc);
        var occurrenceC = new MotifOccurrenceTarget(Guid.NewGuid(), OccurrenceCScope(doc));

        var selection = new MotifPropagationSelection(new HashSet<Guid> { occurrenceC.OccurrenceId });
        var expanded = MotifPropagationExpander.Expand(baseProposal, baseScope, [occurrenceC], selection);

        Assert.Equal(2, expanded.Proposal.Operations.Count);
        Assert.Contains("propagated to 1 other occurrence(s)", expanded.Proposal.Explanation, StringComparison.Ordinal);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var dryRunService = new MotifPropagationDryRunService(validator);
        var approvalService = new MotifPropagationApprovalService(validator);

        var dryRun = dryRunService.Preview(expanded, doc);

        Assert.Equal(4, dryRun.Diff.ChangedEffectCount);
        Assert.Equal(
            new[] { "Arch 1", "Arch 2", "Arch 5", "Arch 6" },
            dryRun.Diff.AffectedModels.Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal));

        // Preview never mutates the original document.
        Assert.Equal(1, doc.Revision);
        Assert.Equal(rooflineHashBefore, RooflineHash(doc));

        var result = await approvalService.AcceptAsync(dryRun, expanded, doc, CancellationToken.None);

        Assert.Equal(doc.Revision, result.Transaction.BeforeRevision);
        Assert.Equal(doc.Revision + 1, result.Transaction.AfterRevision);
        Assert.Equal(result.Document.Revision, result.Transaction.AfterRevision);

        foreach (var name in new[] { "Arch 1", "Arch 2", "Arch 5", "Arch 6" })
        {
            var updated = result.Document.Effects.Single(e => e.Target.Value == name);
            Assert.Equal("80", updated.Parameters["AiEdit.SetBrightness.Level"]);
        }

        // The untouched second occurrence (Arch 3/4, never selected for propagation) and Roofline are
        // completely unaffected -- byte-identical to the original document's own effects.
        var arch3Before = doc.Effects.Single(e => e.Target.Value == "Arch 3");
        var arch4Before = doc.Effects.Single(e => e.Target.Value == "Arch 4");
        Assert.Equal(JsonSerializer.Serialize(arch3Before), JsonSerializer.Serialize(result.Document.Effects.Single(e => e.Id == arch3Before.Id)));
        Assert.Equal(JsonSerializer.Serialize(arch4Before), JsonSerializer.Serialize(result.Document.Effects.Single(e => e.Id == arch4Before.Id)));
        Assert.Equal(rooflineHashBefore, RooflineHash(result.Document));
    }

    /// <summary>"Support instance-only choice" (implementation guidance, verbatim): an empty selection widens nothing -- only the original occurrence changes.</summary>
    [Fact]
    public void InstanceOnlySelection_ExpandsToOriginalOperationsOnly()
    {
        var doc = BuildFixture();
        var baseScope = OccurrenceAScope(doc);
        var baseProposal = BaseProposal(doc);
        var occurrenceC = new MotifOccurrenceTarget(Guid.NewGuid(), OccurrenceCScope(doc));

        var expanded = MotifPropagationExpander.Expand(baseProposal, baseScope, [occurrenceC], MotifPropagationSelection.InstanceOnly);

        Assert.Single(expanded.Proposal.Operations);
        Assert.Empty(expanded.IncludedOccurrenceIds);
        Assert.Equal(baseProposal.Explanation, expanded.Proposal.Explanation);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var dryRun = new MotifPropagationDryRunService(validator).Preview(expanded, doc);

        Assert.Equal(2, dryRun.Diff.ChangedEffectCount);
        Assert.Equal(new[] { "Arch 1", "Arch 2" }, dryRun.Diff.AffectedModels.Select(m => m.Value));
    }

    /// <summary>
    /// Required acceptance, verbatim: "Locked/out-of-scope effects cannot change even with malicious
    /// proposal." Propagating to an occurrence that contains a locked effect ("Arch 4") is rejected
    /// outright -- no partial commit of the occurrence's unlocked sibling ("Arch 3") either.
    /// </summary>
    [Fact]
    public void Propagate_ToOccurrenceContainingALockedEffect_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var doc = BuildFixture();
        var baseScope = OccurrenceAScope(doc);
        var baseProposal = BaseProposal(doc);
        var occurrenceB = new MotifOccurrenceTarget(Guid.NewGuid(), OccurrenceBScope(doc));

        var selection = new MotifPropagationSelection(new HashSet<Guid> { occurrenceB.OccurrenceId });
        var expanded = MotifPropagationExpander.Expand(baseProposal, baseScope, [occurrenceB], selection);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var validation = validator.Validate(expanded, doc);

        Assert.False(validation.Allowed);
        Assert.Contains(validation.Errors, e => e.Contains("locked", StringComparison.OrdinalIgnoreCase));

        var dryRunService = new MotifPropagationDryRunService(validator);
        var ex = Assert.Throws<EditProposalRejectedException>(() => dryRunService.Preview(expanded, doc));
        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);

        // No partial mutation: the document is completely untouched, including the occurrence's own
        // unlocked sibling effect (Arch 3) that, considered alone, would have been a perfectly valid edit.
        Assert.Equal(1, doc.Revision);
        Assert.DoesNotContain(doc.Effects, e => e.Parameters.ContainsKey("AiEdit.SetBrightness.Level"));
    }

    /// <summary>
    /// Selecting both a valid occurrence (C) and an invalid, locked occurrence (B) together must reject
    /// the *entire* widened proposal -- never silently apply the valid half while dropping the invalid
    /// half. "Reject... without partial mutation" (required failure/edge case, verbatim) applies to the
    /// whole propagated batch, not per-occurrence.
    /// </summary>
    [Fact]
    public void Propagate_ToMixOfValidAndLockedOccurrences_RejectsWholeBatch_NegativePath()
    {
        var doc = BuildFixture();
        var baseScope = OccurrenceAScope(doc);
        var baseProposal = BaseProposal(doc);
        var occurrenceB = new MotifOccurrenceTarget(Guid.NewGuid(), OccurrenceBScope(doc));
        var occurrenceC = new MotifOccurrenceTarget(Guid.NewGuid(), OccurrenceCScope(doc));

        var selection = new MotifPropagationSelection(new HashSet<Guid> { occurrenceB.OccurrenceId, occurrenceC.OccurrenceId });
        var expanded = MotifPropagationExpander.Expand(baseProposal, baseScope, [occurrenceB, occurrenceC], selection);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var validation = validator.Validate(expanded, doc);

        Assert.False(validation.Allowed);

        var approvalService = new MotifPropagationApprovalService(validator);
        var dryRunService = new MotifPropagationDryRunService(validator);
        Assert.Throws<EditProposalRejectedException>(() => dryRunService.Preview(expanded, doc));

        // Even Arch 5/Arch 6 (occurrence C's own fully-valid effects) must not have changed -- the whole
        // batch is rejected together, proving this is not a per-occurrence filter.
        Assert.DoesNotContain(doc.Effects, e => e.Parameters.ContainsKey("AiEdit.SetBrightness.Level"));
    }

    /// <summary>
    /// "Edit-scope intersection validation" (this step's own heading, verbatim): a propagated operation
    /// that reaches outside its own recorded occurrence scope (here, a hand-crafted operation naming
    /// "Roofline", simulating a malicious/hallucinated proposal that widened its own target set after
    /// expansion) is rejected, and Roofline is never touched -- the same "LLM cannot target Roofline
    /// outside selected arches" phase-gate invariant, now proven at the propagated-operation layer.
    /// </summary>
    [Fact]
    public void Operation_TargetingOutsideItsOwnPermittedOccurrenceScope_IsRejected_NegativePath()
    {
        var doc = BuildFixture();
        var occurrenceCScope = OccurrenceCScope(doc);
        var maliciousOp = new StructuredEditOperation(
            "op-1::occurrence:malicious",
            StructuredEditOperationKinds.SetBrightness,
            occurrenceCScope.Time,
            [new ModelId("Roofline")],
            new Dictionary<string, string> { ["Level"] = "100" });

        var proposal = new EditProposal(Guid.NewGuid(), doc.Revision, [maliciousOp], "malicious propagation");
        var propagated = new MotifPropagatedProposal(
            proposal,
            new Dictionary<string, EditScope> { [maliciousOp.OperationId] = occurrenceCScope },
            [Guid.NewGuid()]);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var validation = validator.Validate(propagated, doc);

        Assert.False(validation.Allowed);
        Assert.Contains(validation.Errors, e => e.Contains("Roofline", StringComparison.Ordinal) && e.Contains("outside its own permitted occurrence scope", StringComparison.Ordinal));

        Assert.DoesNotContain(doc.Effects, e => e.Target.Value == "Roofline" && e.Parameters.ContainsKey("AiEdit.SetBrightness.Level"));
    }

    /// <summary>Required failure/edge case, verbatim: "...or stale revision must be rejected without partial mutation."</summary>
    [Fact]
    public void StaleRevision_IsRejected_WithoutPartialMutation_NegativePath()
    {
        var doc = BuildFixture();
        var baseScope = OccurrenceAScope(doc);
        var baseProposal = BaseProposal(doc) with { BaseRevision = doc.Revision + 1 };
        var occurrenceC = new MotifOccurrenceTarget(Guid.NewGuid(), OccurrenceCScope(doc));

        var selection = new MotifPropagationSelection(new HashSet<Guid> { occurrenceC.OccurrenceId });
        var expanded = MotifPropagationExpander.Expand(baseProposal, baseScope, [occurrenceC], selection);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var validation = validator.Validate(expanded, doc);

        Assert.False(validation.Allowed);
        Assert.Contains(validation.Errors, e => e.Contains("revision", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(doc.Effects, e => e.Parameters.ContainsKey("AiEdit.SetBrightness.Level"));
    }

    /// <summary>
    /// Accept re-validates against the live document -- a lock newly applied between preview and accept
    /// (the same "concurrent edit landed" scenario Step 076 already proved for the single-occurrence path)
    /// is caught here too, and the already-computed dry run is never blindly committed.
    /// </summary>
    [Fact]
    public async Task AcceptAsync_LockAppliedBetweenPreviewAndAccept_IsRejected_WithoutMutation_NegativePath()
    {
        var doc = BuildFixture();
        var baseScope = OccurrenceAScope(doc);
        var baseProposal = BaseProposal(doc);
        var occurrenceC = new MotifOccurrenceTarget(Guid.NewGuid(), OccurrenceCScope(doc));
        var selection = new MotifPropagationSelection(new HashSet<Guid> { occurrenceC.OccurrenceId });
        var expanded = MotifPropagationExpander.Expand(baseProposal, baseScope, [occurrenceC], selection);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var dryRunService = new MotifPropagationDryRunService(validator);
        var approvalService = new MotifPropagationApprovalService(validator);

        var dryRun = dryRunService.Preview(expanded, doc);

        var arch5 = doc.Effects.Single(e => e.Target.Value == "Arch 5");
        var lockedEffects = doc.Effects.Select(e => e.Id == arch5.Id ? e with { Locked = true } : e).ToList();
        var driftedDoc = doc with { Effects = lockedEffects };

        var ex = await Assert.ThrowsAsync<EditProposalRejectedException>(
            () => approvalService.AcceptAsync(dryRun, expanded, driftedDoc, CancellationToken.None));

        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(driftedDoc.Effects, e => e.Parameters.ContainsKey("AiEdit.SetBrightness.Level"));
    }

    /// <summary>An internally inconsistent candidate set (duplicate occurrence id) is rejected before any operation is cloned.</summary>
    [Fact]
    public void Expand_DuplicateOccurrenceIdInCandidateSet_Throws_NegativePath()
    {
        var doc = BuildFixture();
        var baseScope = OccurrenceAScope(doc);
        var baseProposal = BaseProposal(doc);
        var sharedId = Guid.NewGuid();
        var occurrenceC1 = new MotifOccurrenceTarget(sharedId, OccurrenceCScope(doc));
        var occurrenceC2 = new MotifOccurrenceTarget(sharedId, OccurrenceBScope(doc));

        Assert.Throws<ArgumentException>(() => MotifPropagationExpander.Expand(
            baseProposal, baseScope, [occurrenceC1, occurrenceC2], MotifPropagationSelection.InstanceOnly));
    }

    /// <summary>A selection naming an occurrence id absent from the candidate set is rejected -- an internally inconsistent caller, not an untrusted-input gate.</summary>
    [Fact]
    public void Expand_SelectionNamesUnknownOccurrenceId_Throws_NegativePath()
    {
        var doc = BuildFixture();
        var baseScope = OccurrenceAScope(doc);
        var baseProposal = BaseProposal(doc);
        var occurrenceC = new MotifOccurrenceTarget(Guid.NewGuid(), OccurrenceCScope(doc));
        var selection = new MotifPropagationSelection(new HashSet<Guid> { Guid.NewGuid() });

        Assert.Throws<ArgumentException>(() => MotifPropagationExpander.Expand(
            baseProposal, baseScope, [occurrenceC], selection));
    }

    /// <summary>An empty proposal's operations list (base proposal with zero operations) is rejected -- the same "nothing to apply" gate <see cref="EditProposalValidator"/> already enforces.</summary>
    [Fact]
    public void EmptyOperationsList_IsRejected_NegativePath()
    {
        var doc = BuildFixture();
        var emptyProposal = new EditProposal(Guid.NewGuid(), doc.Revision, [], "no-op");
        var propagated = new MotifPropagatedProposal(emptyProposal, new Dictionary<string, EditScope>(), []);

        var validator = new MotifPropagationValidator(new EffectLockPolicy());
        var validation = validator.Validate(propagated, doc);

        Assert.False(validation.Allowed);
        Assert.Contains(validation.Errors, e => e.Contains("no operations", StringComparison.OrdinalIgnoreCase));
    }
}
