using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Refinement.Harness;

namespace XlightsAI.Studio.Director.Tests;

public sealed class RevisionDirectiveDeriverTests
{
    [Fact]
    public void FromModelCoverageViolations_TargetsTheLongestSection()
    {
        // User feedback (2026-10-31): an uncovered model (e.g. an Arch) should be directed at the section
        // with the most real screen-time to plausibly fit it into.
        var shortSection = Guid.NewGuid();
        var longSection = Guid.NewGuid();
        var windows = new Dictionary<Guid, (long Start, long End)>
        {
            [shortSection] = (0, 1_000),
            [longSection] = (0, 50_000),
        };
        var violations = new[]
        {
            new LegalityViolation(ChoreographyLegalityValidator.RuleModelCoverage, "Model 'Arch 1' never received any effect.", AffectedModelId: "Arch 1"),
        };

        var directives = RevisionDirectiveDeriver.FromModelCoverageViolations(violations, windows);

        var directive = Assert.Single(directives);
        Assert.Equal(longSection, directive.SectionId);
        Assert.Equal("model-coverage", directive.Kind);
        Assert.Contains("Arch 1", directive.Instruction);
    }

    [Fact]
    public void FromModelCoverageViolations_MultipleUncoveredModels_OneDirectiveEachOnTheSameLongestSection()
    {
        var onlySection = Guid.NewGuid();
        var windows = new Dictionary<Guid, (long Start, long End)> { [onlySection] = (0, 10_000) };
        var violations = new[]
        {
            new LegalityViolation(ChoreographyLegalityValidator.RuleModelCoverage, "Model 'Arch 1' never received any effect.", AffectedModelId: "Arch 1"),
            new LegalityViolation(ChoreographyLegalityValidator.RuleModelCoverage, "Model 'Arch 2' never received any effect.", AffectedModelId: "Arch 2"),
        };

        var directives = RevisionDirectiveDeriver.FromModelCoverageViolations(violations, windows);

        Assert.Equal(2, directives.Count);
        Assert.All(directives, d => Assert.Equal(onlySection, d.SectionId));
    }

    [Fact]
    public void FromModelCoverageViolations_IgnoresOtherRules_AndMissingAffectedModelId()
    {
        var windows = new Dictionary<Guid, (long Start, long End)> { [Guid.NewGuid()] = (0, 10_000) };
        var violations = new[]
        {
            new LegalityViolation("some-other-rule", "Unrelated finding.", AffectedModelId: "Arch 1"),
            new LegalityViolation(ChoreographyLegalityValidator.RuleModelCoverage, "No AffectedModelId set."),
        };

        Assert.Empty(RevisionDirectiveDeriver.FromModelCoverageViolations(violations, windows));
    }

    [Fact]
    public void FromModelCoverageViolations_NoSectionWindows_ProducesNoDirective()
    {
        var violations = new[]
        {
            new LegalityViolation(ChoreographyLegalityValidator.RuleModelCoverage, "Model 'Arch 1' never received any effect.", AffectedModelId: "Arch 1"),
        };

        Assert.Empty(RevisionDirectiveDeriver.FromModelCoverageViolations(violations, new Dictionary<Guid, (long Start, long End)>()));
    }

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
