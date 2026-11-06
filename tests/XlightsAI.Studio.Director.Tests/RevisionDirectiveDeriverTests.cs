using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Refinement.Harness;

namespace XlightsAI.Studio.Director.Tests;

public sealed class RevisionDirectiveDeriverTests
{
    [Fact]
    public void FromMissedTimedCues_TargetsTheClosestOverlappingSection()
    {
        var earlySection = Guid.NewGuid();
        var lateSection = Guid.NewGuid();
        var windows = new Dictionary<Guid, (long Start, long End)>
        {
            [earlySection] = (TimeSpan.FromSeconds(0).Ticks, TimeSpan.FromSeconds(20).Ticks),
            [lateSection] = (TimeSpan.FromSeconds(20).Ticks, TimeSpan.FromSeconds(40).Ticks),
        };
        // Cue at 19.5s with 2s tolerance overlaps both sections ([17.5,21.5]); its own midpoint (19.5)
        // is closer to earlySection's window.
        var cue = new TimedCue(TimeSpan.FromSeconds(19.5), null, "hit it", TimeSpan.FromSeconds(2));

        var directives = RevisionDirectiveDeriver.FromMissedTimedCues([cue], windows);

        var directive = Assert.Single(directives);
        Assert.Equal(earlySection, directive.SectionId);
        Assert.Equal("timed-cue", directive.Kind);
        Assert.Contains("hit it", directive.Instruction);
    }

    [Fact]
    public void FromMissedTimedCues_CueOverlappingNoSection_ProducesNoDirective()
    {
        var windows = new Dictionary<Guid, (long Start, long End)> { [Guid.NewGuid()] = (0, TimeSpan.FromSeconds(10).Ticks) };
        var cue = new TimedCue(TimeSpan.FromSeconds(100), null, "hit it", TimeSpan.FromSeconds(1));

        Assert.Empty(RevisionDirectiveDeriver.FromMissedTimedCues([cue], windows));
    }

    [Fact]
    public void FromMissedTimedCues_EmptyList_ProducesNoDirective()
    {
        var windows = new Dictionary<Guid, (long Start, long End)> { [Guid.NewGuid()] = (0, TimeSpan.FromSeconds(10).Ticks) };

        Assert.Empty(RevisionDirectiveDeriver.FromMissedTimedCues([], windows));
    }

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
    public void FromSingleModelViolations_TargetsTheLongestSection()
    {
        var shortSection = Guid.NewGuid();
        var longSection = Guid.NewGuid();
        var windows = new Dictionary<Guid, (long Start, long End)>
        {
            [shortSection] = (0, 1_000),
            [longSection] = (0, 50_000),
        };

        var directives = RevisionDirectiveDeriver.FromSingleModelViolations([new ModelId("Arch 1")], windows);

        var directive = Assert.Single(directives);
        Assert.Equal(longSection, directive.SectionId);
        Assert.Equal("single-model-not-group", directive.Kind);
        Assert.Contains("Arch 1", directive.Instruction);
    }

    [Fact]
    public void FromSingleModelViolations_EmptyList_ProducesNoDirective()
    {
        var windows = new Dictionary<Guid, (long Start, long End)> { [Guid.NewGuid()] = (0, 10_000) };

        Assert.Empty(RevisionDirectiveDeriver.FromSingleModelViolations([], windows));
    }

    [Fact]
    public void FromSingleModelViolations_NoSectionWindows_ProducesNoDirective()
    {
        Assert.Empty(RevisionDirectiveDeriver.FromSingleModelViolations([new ModelId("Arch 1")], new Dictionary<Guid, (long Start, long End)>()));
    }

    [Fact]
    public void FromPostProcessingCues_RangeSpanningTwoSections_ProducesOneDirectivePerOverlappingSection()
    {
        // Unlike FromModelCoverageViolations/FromMissedTimedCues (pick the single closest section for an
        // automatically-detected miss), a direct, user-authored touch-up naming its own exact range should
        // touch EVERY section it overlaps.
        var earlySection = Guid.NewGuid();
        var lateSection = Guid.NewGuid();
        var windows = new Dictionary<Guid, (long Start, long End)>
        {
            [earlySection] = (TimeSpan.FromSeconds(0).Ticks, TimeSpan.FromSeconds(20).Ticks),
            [lateSection] = (TimeSpan.FromSeconds(20).Ticks, TimeSpan.FromSeconds(40).Ticks),
        };
        var cue = new TimedCue(TimeSpan.FromSeconds(19), TimeSpan.FromSeconds(22), "make it pop", TimeSpan.FromSeconds(0.5));

        var directives = RevisionDirectiveDeriver.FromPostProcessingCues([cue], windows);

        Assert.Equal(2, directives.Count);
        Assert.Contains(directives, d => d.SectionId == earlySection);
        Assert.Contains(directives, d => d.SectionId == lateSection);
        Assert.All(directives, d => Assert.Equal("post-processing-edit", d.Kind));
        Assert.All(directives, d => Assert.Contains("make it pop", d.Instruction));
    }

    [Fact]
    public void FromPostProcessingCues_SingleInstantWithinOneSection_ProducesOneDirective()
    {
        var onlySection = Guid.NewGuid();
        var windows = new Dictionary<Guid, (long Start, long End)> { [onlySection] = (0, TimeSpan.FromSeconds(30).Ticks) };
        var cue = new TimedCue(TimeSpan.FromSeconds(10), null, "add a sparkle here", TimeSpan.FromSeconds(1));

        var directives = RevisionDirectiveDeriver.FromPostProcessingCues([cue], windows);

        var directive = Assert.Single(directives);
        Assert.Equal(onlySection, directive.SectionId);
        Assert.Contains("add a sparkle here", directive.Instruction);
    }

    [Fact]
    public void FromPostProcessingCues_CueOverlappingNoSection_ProducesNoDirective()
    {
        var windows = new Dictionary<Guid, (long Start, long End)> { [Guid.NewGuid()] = (0, TimeSpan.FromSeconds(10).Ticks) };
        var cue = new TimedCue(TimeSpan.FromSeconds(100), null, "too late", TimeSpan.FromSeconds(0.5));

        Assert.Empty(RevisionDirectiveDeriver.FromPostProcessingCues([cue], windows));
    }

    [Fact]
    public void FromPostProcessingCues_MultipleCues_EachProducesItsOwnDirectives()
    {
        var sectionA = Guid.NewGuid();
        var sectionB = Guid.NewGuid();
        var windows = new Dictionary<Guid, (long Start, long End)>
        {
            [sectionA] = (0, TimeSpan.FromSeconds(10).Ticks),
            [sectionB] = (TimeSpan.FromSeconds(10).Ticks, TimeSpan.FromSeconds(20).Ticks),
        };
        var cues = new[]
        {
            new TimedCue(TimeSpan.FromSeconds(5), null, "first fix", TimeSpan.FromSeconds(0.5)),
            new TimedCue(TimeSpan.FromSeconds(15), null, "second fix", TimeSpan.FromSeconds(0.5)),
        };

        var directives = RevisionDirectiveDeriver.FromPostProcessingCues(cues, windows);

        Assert.Equal(2, directives.Count);
        Assert.Contains(directives, d => d.SectionId == sectionA && d.Instruction.Contains("first fix"));
        Assert.Contains(directives, d => d.SectionId == sectionB && d.Instruction.Contains("second fix"));
    }

    [Fact]
    public void FromPostProcessingCues_EmptyCueList_ProducesNoDirective()
    {
        var windows = new Dictionary<Guid, (long Start, long End)> { [Guid.NewGuid()] = (0, 10_000) };

        Assert.Empty(RevisionDirectiveDeriver.FromPostProcessingCues([], windows));
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
