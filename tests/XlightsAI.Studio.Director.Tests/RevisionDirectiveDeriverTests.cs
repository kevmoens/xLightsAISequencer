using XlightsAI.Studio.Refinement.Harness;

namespace XlightsAI.Studio.Director.Tests;

public sealed class RevisionDirectiveDeriverTests
{
    [Fact]
    public void FromNumericFindings_MappedMetric_ProducesOneDirectivePerSection()
    {
        var sectionA = Guid.NewGuid();
        var sectionB = Guid.NewGuid();
        var findings = new[] { new CritiqueFinding("PixelHeavyDutyCycle", CritiqueSeverity.Warning, "Matrix duty cycle below target.") };
        var mapping = new Dictionary<string, IReadOnlyList<Guid>> { ["PixelHeavyDutyCycle"] = [sectionA, sectionB] };

        var directives = RevisionDirectiveDeriver.FromNumericFindings(findings, mapping);

        Assert.Equal(2, directives.Count);
        Assert.Contains(directives, d => d.SectionId == sectionA && d.Kind == "PixelHeavyDutyCycle");
        Assert.Contains(directives, d => d.SectionId == sectionB && d.Kind == "PixelHeavyDutyCycle");
    }

    [Fact]
    public void FromNumericFindings_UnmappedMetric_ProducesNoDirective()
    {
        var findings = new[] { new CritiqueFinding("BeatHitRate", CritiqueSeverity.Warning, "Below band.") };

        var directives = RevisionDirectiveDeriver.FromNumericFindings(findings, new Dictionary<string, IReadOnlyList<Guid>>());

        Assert.Empty(directives);
    }

    [Fact]
    public void FromVisualCritiques_ConfidentLoss_ProducesDirective()
    {
        var sectionId = Guid.NewGuid();
        var critique = new SectionVisualCritique(
            sectionId,
            new PairwiseVerdict(PairwiseWinner.Reference, 0.9),
            new PairwiseVerdict(PairwiseWinner.Reference, 0.85),
            FlippedBetweenOrderings: false,
            AgreedWinner: PairwiseWinner.Reference);

        var directives = RevisionDirectiveDeriver.FromVisualCritiques([critique], new Dictionary<Guid, string> { [sectionId] = "bold red snap" });

        var directive = Assert.Single(directives);
        Assert.Equal(sectionId, directive.SectionId);
        Assert.Equal("visual", directive.Kind);
        Assert.Contains("bold red snap", directive.Instruction);
    }

    [Fact]
    public void FromVisualCritiques_FlippedVerdict_ProducesNoDirective()
    {
        var critique = new SectionVisualCritique(
            Guid.NewGuid(),
            new PairwiseVerdict(PairwiseWinner.Candidate, 0.6),
            new PairwiseVerdict(PairwiseWinner.Reference, 0.55),
            FlippedBetweenOrderings: true,
            AgreedWinner: null);

        Assert.Empty(RevisionDirectiveDeriver.FromVisualCritiques([critique], new Dictionary<Guid, string>()));
    }

    [Fact]
    public void FromVisualCritiques_CandidateWon_ProducesNoDirective()
    {
        var critique = new SectionVisualCritique(
            Guid.NewGuid(),
            new PairwiseVerdict(PairwiseWinner.Candidate, 0.9),
            new PairwiseVerdict(PairwiseWinner.Candidate, 0.9),
            FlippedBetweenOrderings: false,
            AgreedWinner: PairwiseWinner.Candidate);

        Assert.Empty(RevisionDirectiveDeriver.FromVisualCritiques([critique], new Dictionary<Guid, string>()));
    }
}
