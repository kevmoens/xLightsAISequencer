using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong.Tests;

/// <summary>
/// PHASE-14 Step 093 -- "Run whole-song rhythm/contrast/complexity checks; export and audition." Required
/// acceptance: "Export opens in xLights and passes continuity report." Implementation guidance: "Run
/// continuity evaluation across all phrase boundaries and sections; provide specific time/prop links and a
/// whole-song user gate." Failure/edge case: "Invalid model/format/version must fail safely without
/// rewriting source files." These tests exercise the real <see cref="SongContinuityEvaluator"/>/
/// <see cref="WholeSongExportAuditor"/>/<see cref="XsqExporter"/>/<see cref="XlightsLauncher"/> (no mocking)
/// against real <see cref="SequenceDocument"/>/<see cref="DisplayGraph"/>/<see cref="PhraseSeamPlan"/>/
/// <see cref="MotifVariationPlan"/> production types.
/// </summary>
public sealed class Step093AcceptanceTests : IDisposable
{
    private static readonly ModelId TreeModel = new("Tree");
    private static readonly ModelId ArchModel = new("Arch");
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-step093-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static DisplayGraph Layout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models:
        [
            new DisplayModel(TreeModel, "Tree", "Star", [], []),
            new DisplayModel(ArchModel, "Arch", "Arches", [], []),
        ],
        Groups: [],
        Warnings: []);

    /// <summary>
    /// Scales a compact "unit" tick value (as used by every other PHASE-14 WholeSong fixture, e.g.
    /// Step091AcceptanceTests' own <c>Effect(TreeModel, 0, 1000)</c>) up to a real <see cref="TimelineTick"/>
    /// (100 ns) value large enough to survive <see cref="XsqExporter"/>'s own 25ms frame-grid rounding
    /// (PHASE-02 Step 013/051) without collapsing to a non-positive duration -- this step's own tests are
    /// the first in this project to actually run fixtures through the real exporter, so this scale-up is
    /// this step's own, disclosed fixture convention.
    /// </summary>
    private const long TickScale = 10_000L;

    private static long Ticks(long units) => units * TickScale;

    private static EffectInstance Effect(ModelId target, long startUnits, long endUnits, int layer = 0) =>
        new EffectRecipeRegistry().CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, target, Ticks(startUnits), Ticks(endUnits)) with { Layer = layer };

    private static MotifOccurrenceVariation Occurrence(
        Guid templateId, Guid phraseId, bool isClimax, double multiplier, ModelId target, long start, long end,
        IReadOnlyList<string>? excludedReasons = null) =>
        new(templateId, phraseId, isClimax, multiplier, [Effect(target, start, end)], [Guid.NewGuid()], [], excludedReasons ?? []);

    private static MotifVariationPlan MotifPlan(IReadOnlyList<MotifOccurrenceVariation> occurrences, Guid? templateAnalysisVersionId = null) =>
        new(Guid.NewGuid(), new SongId(Guid.NewGuid()), templateAnalysisVersionId ?? Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, occurrences);

    /// <summary>
    /// The phase-gate's own required demo shape: Intro/Verse/Chorus/Verse/Chorus/Bridge/Final chorus,
    /// realized as a two-model candidate document with one resolved overlap seam, one unresolved-gap
    /// (Bridge) seam, and a chorus motif reused three times with the Final Chorus as its provable climax
    /// (mirrors Step092AcceptanceTests' own climax fixture). One motif occurrence additionally carries a
    /// disclosed exclusion -- "inspect continuity warnings" (phase gate text, verbatim).
    /// </summary>
    [Fact]
    public async Task Step093_Acceptance_Passes()
    {
        var templateId = Guid.NewGuid();
        var candidate = new SequenceDocument(Guid.NewGuid(), 1,
        [
            Effect(TreeModel, 0, 1000), // Intro
            Effect(ArchModel, 1000, 2000), // Verse
            Effect(TreeModel, 2000, 3000), // Chorus 1 (motif occurrence)
            Effect(ArchModel, 3000, 4000), // Verse
            Effect(TreeModel, 4000, 5000), // Chorus 2 (motif occurrence)
            Effect(ArchModel, 5200, 6000), // Bridge (200-tick gap before it -- left disclosed, not filled)
            Effect(TreeModel, 6000, 7000), // Final chorus (motif occurrence, climax)
        ]);

        var seams = new List<PhraseSeamPlan>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), new TimeWindow(Ticks(1000), Ticks(1000)), SeamStrategy.Trim,
                ["Trim at seam 'a' -> 'b': effect 'x' on 'Arch' end clipped from 2030 to 2000."]),
            new(Guid.NewGuid(), Guid.NewGuid(), new TimeWindow(Ticks(5000), Ticks(5200)), SeamStrategy.Bridge,
                ["Bridge requested at seam 'c' -> 'd'; a gap of 200 tick(s) between phrases remains, pending a later transition-gesture step."]),
        };

        var motifPlan = MotifPlan(
        [
            Occurrence(templateId, Guid.NewGuid(), isClimax: false, multiplier: 0.80, TreeModel, 2000, 3000),
            Occurrence(templateId, Guid.NewGuid(), isClimax: false, multiplier: 0.95, TreeModel, 4000, 5000,
                excludedReasons: ["Destination effect locked; occurrence excluded from this slot."]),
            Occurrence(templateId, Guid.NewGuid(), isClimax: true, multiplier: 1.25, TreeModel, 6000, 7000),
        ]);

        var evaluator = new SongContinuityEvaluator();
        var auditor = new WholeSongExportAuditor(evaluator, new XsqExporter(), new XlightsLauncher());
        var outputPath = Path.Combine(_root, "whole-song.xsq");

        var result = await auditor.ExportAndAuditAsync(
            Layout(), candidate, seams, motifPlan, motifPlan.SourceTemplateAnalysisVersionId,
            outputPath, "2026.03", XsqExportPolicy.RejectIfExists,
            xlightsExecutablePathForAudition: null, // no installed xLights in this environment -- disclosed, never faked.
            CancellationToken.None);

        // Required acceptance, verbatim: "...passes continuity report."
        Assert.True(result.ContinuityReport.Passed);
        Assert.DoesNotContain(result.ContinuityReport.Findings, f => f.Severity == ContinuitySeverity.Error);

        // "Inspect continuity warnings" (phase gate text, verbatim): both disclosed warnings are surfaced,
        // each linked to its own exact time/prop location -- Section 3's own "link anomalies directly to
        // timeline locations" architectural note.
        Assert.Contains(result.ContinuityReport.Findings,
            f => f.Code == ContinuityFindingCode.SeamOverlapResolved && f.Severity == ContinuitySeverity.Warning);
        Assert.Contains(result.ContinuityReport.Findings,
            f => f.Code == ContinuityFindingCode.UnresolvedGap && f.Severity == ContinuitySeverity.Warning
                 && f.Window.StartTicks == Ticks(5000) && f.Window.EndTicks == Ticks(5200));
        Assert.Contains(result.ContinuityReport.Findings,
            f => f.Code == ContinuityFindingCode.MotifInstanceExcluded && f.Severity == ContinuitySeverity.Warning);

        // "Export opens in xLights" (required acceptance, verbatim) -- the real file was actually written.
        Assert.True(File.Exists(result.Export.Path));
        Assert.False(string.IsNullOrWhiteSpace(result.Export.Sha256));
        Assert.Null(result.Audition); // no installed xLights executable was supplied -- disclosed NOT VERIFIED, never faked.

        // "Reopen and export approved full song without recomposition" (Section 7 item 7): a brand-new
        // exporter/auditor instance over the same already-decided plan reaches the same passed gate again.
        var secondPath = Path.Combine(_root, "whole-song-reopened.xsq");
        var reopened = await new WholeSongExportAuditor(new SongContinuityEvaluator(), new XsqExporter(), new XlightsLauncher())
            .ExportAndAuditAsync(
                Layout(), candidate, seams, motifPlan, motifPlan.SourceTemplateAnalysisVersionId,
                secondPath, "2026.03", XsqExportPolicy.RejectIfExists, null, CancellationToken.None);
        Assert.True(reopened.ContinuityReport.Passed);
        Assert.True(File.Exists(reopened.Export.Path));
    }

    [Fact]
    public void Step093_NegativePath_UnresolvedOverlap_IsFlagged_AsErrorSeverity()
    {
        // Phase test matrix row 4: "Cross-phrase overlapping effect is flagged for review."
        var candidate = new SequenceDocument(Guid.NewGuid(), 1,
        [
            Effect(TreeModel, 0, 1000),
            Effect(TreeModel, 900, 1500), // genuinely overlaps the previous effect on the same target+layer.
        ]);

        var findings = new SongContinuityEvaluator().Evaluate(candidate, [], null, null, CancellationToken.None);

        var finding = Assert.Single(findings);
        Assert.Equal(ContinuityFindingCode.UnresolvedOverlap, finding.Code);
        Assert.Equal(ContinuitySeverity.Error, finding.Severity);
        Assert.Contains(TreeModel, finding.Models);
    }

    [Fact]
    public async Task Step093_NegativePath_UnresolvedOverlap_BlocksExport_NoFileWritten()
    {
        var candidate = new SequenceDocument(Guid.NewGuid(), 1,
        [
            Effect(TreeModel, 0, 1000),
            Effect(TreeModel, 900, 1500),
        ]);
        var auditor = new WholeSongExportAuditor(new SongContinuityEvaluator(), new XsqExporter(), new XlightsLauncher());
        var outputPath = Path.Combine(_root, "blocked.xsq");

        var ex = await Assert.ThrowsAsync<SongContinuityBlockedException>(() => auditor.ExportAndAuditAsync(
            Layout(), candidate, [], null, null, outputPath, "2026.03", XsqExportPolicy.RejectIfExists, null, CancellationToken.None));

        Assert.Single(ex.BlockingFindings);
        Assert.Equal(ContinuityFindingCode.UnresolvedOverlap, ex.BlockingFindings[0].Code);
        Assert.False(File.Exists(outputPath)); // required failure path, verbatim: "...fail safely without rewriting source files."
    }

    [Fact]
    public void Step093_NegativePath_StaleMotifSource_IsFlagged_AsErrorSeverity()
    {
        // Phase test matrix row 5: "Motif source changes generate stale-instance warning."
        var templateId = Guid.NewGuid();
        var candidate = new SequenceDocument(Guid.NewGuid(), 1, [Effect(TreeModel, 0, 1000)]);
        var motifPlan = MotifPlan([Occurrence(templateId, Guid.NewGuid(), true, 1.0, TreeModel, 0, 1000)]);
        var currentVersion = Guid.NewGuid(); // deliberately different from motifPlan.SourceTemplateAnalysisVersionId.

        var findings = new SongContinuityEvaluator().Evaluate(candidate, [], motifPlan, currentVersion, CancellationToken.None);

        var finding = Assert.Single(findings);
        Assert.Equal(ContinuityFindingCode.MotifSourceStale, finding.Code);
        Assert.Equal(ContinuitySeverity.Error, finding.Severity);
    }

    [Fact]
    public async Task Step093_NegativePath_StaleMotifSource_BlocksExport_NoFileWritten()
    {
        var templateId = Guid.NewGuid();
        var candidate = new SequenceDocument(Guid.NewGuid(), 1, [Effect(TreeModel, 0, 1000)]);
        var motifPlan = MotifPlan([Occurrence(templateId, Guid.NewGuid(), true, 1.0, TreeModel, 0, 1000)]);
        var auditor = new WholeSongExportAuditor(new SongContinuityEvaluator(), new XsqExporter(), new XlightsLauncher());
        var outputPath = Path.Combine(_root, "stale.xsq");

        await Assert.ThrowsAsync<SongContinuityBlockedException>(() => auditor.ExportAndAuditAsync(
            Layout(), candidate, [], motifPlan, Guid.NewGuid(), outputPath, "2026.03",
            XsqExportPolicy.RejectIfExists, null, CancellationToken.None));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void Step093_MotifSource_NotStale_WhenVersionMatches_NoFindingEmitted()
    {
        var templateId = Guid.NewGuid();
        var candidate = new SequenceDocument(Guid.NewGuid(), 1, [Effect(TreeModel, 0, 1000)]);
        var motifPlan = MotifPlan([Occurrence(templateId, Guid.NewGuid(), true, 1.0, TreeModel, 0, 1000)]);

        var findings = new SongContinuityEvaluator().Evaluate(
            candidate, [], motifPlan, motifPlan.SourceTemplateAnalysisVersionId, CancellationToken.None);

        Assert.DoesNotContain(findings, f => f.Code == ContinuityFindingCode.MotifSourceStale);
    }

    [Fact]
    public void Step093_NegativePath_ClimaxLiftInconsistent_TwoOccurrencesBothClimax_IsFlagged()
    {
        // Phase test matrix row 6: "Section-to-section brightness arc is computed consistently."
        var templateId = Guid.NewGuid();
        var candidate = new SequenceDocument(Guid.NewGuid(), 1, [Effect(TreeModel, 0, 1000), Effect(ArchModel, 2000, 3000)]);
        var motifPlan = MotifPlan(
        [
            Occurrence(templateId, Guid.NewGuid(), isClimax: true, multiplier: 1.0, TreeModel, 0, 1000),
            Occurrence(templateId, Guid.NewGuid(), isClimax: true, multiplier: 1.1, ArchModel, 2000, 3000),
        ]);

        var findings = new SongContinuityEvaluator().Evaluate(candidate, [], motifPlan, null, CancellationToken.None);

        var finding = Assert.Single(findings);
        Assert.Equal(ContinuityFindingCode.ClimaxLiftInconsistent, finding.Code);
        Assert.Equal(ContinuitySeverity.Error, finding.Severity);
    }

    [Fact]
    public void Step093_NegativePath_ClimaxLiftInconsistent_ClimaxDoesNotCarryMaxMultiplier_IsFlagged()
    {
        var templateId = Guid.NewGuid();
        var candidate = new SequenceDocument(Guid.NewGuid(), 1, [Effect(TreeModel, 0, 1000), Effect(ArchModel, 2000, 3000)]);
        var motifPlan = MotifPlan(
        [
            Occurrence(templateId, Guid.NewGuid(), isClimax: true, multiplier: 0.9, TreeModel, 0, 1000),
            Occurrence(templateId, Guid.NewGuid(), isClimax: false, multiplier: 1.2, ArchModel, 2000, 3000),
        ]);

        var findings = new SongContinuityEvaluator().Evaluate(candidate, [], motifPlan, null, CancellationToken.None);

        Assert.Contains(findings, f => f.Code == ContinuityFindingCode.ClimaxLiftInconsistent);
    }

    [Fact]
    public void Step093_ClimaxLiftConsistent_RealSchedulerShape_NoFindingEmitted()
    {
        // The real MotifVariationScheduler's own provable guarantee (Step 092) never trips this check.
        var templateId = Guid.NewGuid();
        var candidate = new SequenceDocument(Guid.NewGuid(), 1,
        [
            Effect(TreeModel, 0, 1000), Effect(TreeModel, 2000, 3000), Effect(TreeModel, 4000, 5000),
        ]);
        var motifPlan = MotifPlan(
        [
            Occurrence(templateId, Guid.NewGuid(), isClimax: false, MotifVariationScheduler.ComputeMultiplier(0.80, false), TreeModel, 0, 1000),
            Occurrence(templateId, Guid.NewGuid(), isClimax: false, MotifVariationScheduler.ComputeMultiplier(0.90, false), TreeModel, 2000, 3000),
            Occurrence(templateId, Guid.NewGuid(), isClimax: true, MotifVariationScheduler.ComputeMultiplier(0.97, true), TreeModel, 4000, 5000),
        ]);

        var findings = new SongContinuityEvaluator().Evaluate(candidate, [], motifPlan, null, CancellationToken.None);

        Assert.DoesNotContain(findings, f => f.Code == ContinuityFindingCode.ClimaxLiftInconsistent);
    }

    [Fact]
    public async Task Step093_NegativePath_InvalidExportTarget_FailsSafely_NoFileWritten()
    {
        // Required failure/edge case, verbatim: "Invalid model/format/version must fail safely without
        // rewriting source files" -- exercised through the auditor, not only the underlying exporter directly.
        var registry = new EffectRecipeRegistry();
        var candidate = new SequenceDocument(Guid.NewGuid(), 1,
            [registry.CreateVerified(EffectRecipeRegistry.SimpleOnRecipeId, new ModelId("Ghost-Prop"), 0, 1000)]);
        var auditor = new WholeSongExportAuditor(new SongContinuityEvaluator(), new XsqExporter(), new XlightsLauncher());
        var outputPath = Path.Combine(_root, "invalid-target.xsq");

        var ex = await Assert.ThrowsAsync<XsqExportInvalidException>(() => auditor.ExportAndAuditAsync(
            Layout(), candidate, [], null, null, outputPath, "2026.03", XsqExportPolicy.RejectIfExists, null, CancellationToken.None));

        Assert.Contains("Ghost-Prop", ex.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task Step093_NegativePath_Cancellation_ThrowsBeforeAnyWork()
    {
        // Required failure/edge case, verbatim: "...cancellation... must not corrupt the project."
        var candidate = new SequenceDocument(Guid.NewGuid(), 1, [Effect(TreeModel, 0, 1000)]);
        var auditor = new WholeSongExportAuditor(new SongContinuityEvaluator(), new XsqExporter(), new XlightsLauncher());
        var outputPath = Path.Combine(_root, "cancelled.xsq");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => auditor.ExportAndAuditAsync(
            Layout(), candidate, [], null, null, outputPath, "2026.03", XsqExportPolicy.RejectIfExists, null, cts.Token));

        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void Step093_NegativePath_NullCandidateOrSeams_Throws()
    {
        var evaluator = new SongContinuityEvaluator();
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!, [], null, null, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() =>
            evaluator.Evaluate(new SequenceDocument(Guid.NewGuid(), 1, []), null!, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Step093_InstalledXlightsExecutableSupplied_LaunchesRealProcess_ForAudition()
    {
        // "...export and audition" (this step's own title, verbatim). No real xLights executable is
        // installed in this environment, so this test proves the real IXlightsLauncher is actually invoked
        // (and fails exactly the way it would for any missing executable) rather than silently skipped --
        // the auditor never substitutes a mock for the real launcher call.
        var candidate = new SequenceDocument(Guid.NewGuid(), 1, [Effect(TreeModel, 0, 1000)]);
        var auditor = new WholeSongExportAuditor(new SongContinuityEvaluator(), new XsqExporter(), new XlightsLauncher());
        var outputPath = Path.Combine(_root, "audition.xsq");

        var ex = await Assert.ThrowsAsync<XlightsLaunchInvalidException>(() => auditor.ExportAndAuditAsync(
            Layout(), candidate, [], null, null, outputPath, "2026.03", XsqExportPolicy.RejectIfExists,
            @"C:\NotInstalled\xLights.exe", CancellationToken.None));

        Assert.Contains("No xLights executable was found", ex.Message, StringComparison.Ordinal);
        // The export itself already succeeded and is never rolled back by an audition failure.
        Assert.True(File.Exists(outputPath));
    }
}
