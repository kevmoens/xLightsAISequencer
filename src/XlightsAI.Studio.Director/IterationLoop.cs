using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Refinement;
using XlightsAI.Studio.Refinement.Harness;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>One iteration's full outcome, kept forever under the project's own <c>renders/</c>/<c>critique/</c> artifact folders (architecture §13) -- iterations are never overwritten or discarded.</summary>
/// <param name="IterationNumber">0-based ordinal within the run.</param>
/// <param name="Scripts">Every section's <see cref="SectionScript"/> after this iteration's targeted regeneration (untouched sections carry forward their prior instance).</param>
/// <param name="CompiledResult">This iteration's recompiled IR.</param>
/// <param name="Export">This iteration's exported <c>.xsq</c>.</param>
/// <param name="Report">This iteration's full metric battery result.</param>
/// <param name="NumericFindings">This iteration's numeric critique.</param>
/// <param name="VisualCritiques">This iteration's visual critique, one per section a frame was judged for (empty if no vision-capable Critic client was supplied).</param>
/// <param name="DirectivesAppliedThisIteration">Which directives were acted on to PRODUCE this iteration (empty for iteration 0, which has nothing to revise yet).</param>
/// <param name="RegressionsVsPrevious">Architecture §9 convergence discipline: metrics that regressed versus the immediately previous iteration, even if a targeted metric improved. Empty for iteration 0 (nothing to diff against).</param>
/// <param name="Frames">
/// This iteration's own rendered frames, retained so the NEXT iteration's visual critique has a genuine
/// prior-iteration reference to compare against (architecture §9: "compare... against the previous")
/// instead of comparing this iteration's render against itself. <see langword="null"/> only when no
/// render ever ran for this iteration (there is no such case in <see cref="IterationLoop"/>'s own two
/// factory methods today; the default exists solely so older positional-constructor call sites in tests
/// that predate this field keep compiling).
/// </param>
public sealed record IterationResult(
    int IterationNumber,
    IReadOnlyDictionary<Guid, SectionScript> Scripts,
    ChoreographyEngine.Result CompiledResult,
    ExportResult Export,
    QualityReport Report,
    IReadOnlyList<CritiqueFinding> NumericFindings,
    IReadOnlyList<SectionVisualCritique> VisualCritiques,
    IReadOnlyList<RevisionDirective> DirectivesAppliedThisIteration,
    IReadOnlyList<MetricDelta> RegressionsVsPrevious,
    IReadOnlyList<PreviewFrame>? Frames = null);

/// <summary>
/// Build-plan Phase 7d: the full render -> measure -> critique -> revise loop, bounded and convergence-
/// checked per architecture §9. Deliberately does not own rendering itself -- <paramref name="renderAsync"/>
/// (supplied by the caller) is expected to wrap 7a's <c>RenderPipeline</c> against the real xLights
/// install; keeping that as an injected delegate rather than a direct dependency here means every other
/// piece of this orchestrator (directive derivation, targeted regeneration, convergence diff, best-of-N
/// selection) is unit-testable against fakes with no process/file-system real render required.
/// </summary>
public sealed class IterationLoop
{
    private readonly SectionScriptBatchGenerator _batchGenerator;
    private readonly string _stablePrefix;
    private readonly IReadOnlyDictionary<Guid, string> _volatileSuffixBySectionId;
    private readonly IReadOnlyDictionary<Guid, string> _sectionIntentById;
    private readonly LookLibrary _bibleLooks;
    private readonly DisplayGraph _layout;
    private readonly Func<IReadOnlyDictionary<Guid, SectionScript>, Task<(ChoreographyEngine.Result Compiled, ExportResult Export)>> _compileAndExportAsync;
    private readonly Func<ExportResult, CancellationToken, Task<IReadOnlyList<PreviewFrame>>> _renderAsync;
    private readonly VisualCritic? _visualCritic;
    private readonly IReadOnlyDictionary<Guid, (long Start, long End)> _sectionWindowsById;
    private readonly int _totalDisplayPointCount;
    private readonly int _maxOutputTokens;

    public IterationLoop(
        SectionScriptBatchGenerator batchGenerator,
        string stablePrefix,
        IReadOnlyDictionary<Guid, string> volatileSuffixBySectionId,
        IReadOnlyDictionary<Guid, string> sectionIntentById,
        LookLibrary bibleLooks,
        DisplayGraph layout,
        Func<IReadOnlyDictionary<Guid, SectionScript>, Task<(ChoreographyEngine.Result Compiled, ExportResult Export)>> compileAndExportAsync,
        Func<ExportResult, CancellationToken, Task<IReadOnlyList<PreviewFrame>>> renderAsync,
        IReadOnlyDictionary<Guid, (long Start, long End)> sectionWindowsById,
        int totalDisplayPointCount,
        VisualCritic? visualCritic = null,
        int maxOutputTokens = 4000)
    {
        _batchGenerator = batchGenerator ?? throw new ArgumentNullException(nameof(batchGenerator));
        ArgumentException.ThrowIfNullOrWhiteSpace(stablePrefix);
        _stablePrefix = stablePrefix;
        _volatileSuffixBySectionId = volatileSuffixBySectionId ?? throw new ArgumentNullException(nameof(volatileSuffixBySectionId));
        _sectionIntentById = sectionIntentById ?? throw new ArgumentNullException(nameof(sectionIntentById));
        _bibleLooks = bibleLooks ?? throw new ArgumentNullException(nameof(bibleLooks));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _compileAndExportAsync = compileAndExportAsync ?? throw new ArgumentNullException(nameof(compileAndExportAsync));
        _renderAsync = renderAsync ?? throw new ArgumentNullException(nameof(renderAsync));
        _sectionWindowsById = sectionWindowsById ?? throw new ArgumentNullException(nameof(sectionWindowsById));
        _totalDisplayPointCount = totalDisplayPointCount;
        _visualCritic = visualCritic;
        _maxOutputTokens = maxOutputTokens;
    }

    /// <summary>
    /// Runs the very first iteration: no directives (nothing has been critiqued yet), render/measure/
    /// critique the already-compiled starting point as-is.
    /// </summary>
    public async Task<IterationResult> RunFirstAsync(
        Guid songId, IReadOnlyDictionary<Guid, SectionScript> initialScripts, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(initialScripts);

        var (compiled, export) = await _compileAndExportAsync(initialScripts).ConfigureAwait(false);
        // previousFrames: null -- iteration 0 has no prior iteration to compare against yet, so visual
        // critique is skipped entirely rather than comparing this render against itself (see
        // MeasureAndCritiqueAsync remarks).
        var (report, numericFindings, visualCritiques, frames) = await MeasureAndCritiqueAsync(export, previousFrames: null, ct).ConfigureAwait(false);

        return new IterationResult(0, initialScripts, compiled, export, report, numericFindings, visualCritiques, [], [], frames);
    }

    /// <summary>
    /// Runs one more iteration on top of <paramref name="previous"/>: derives directives from its own
    /// critique, regenerates ONLY the sections those directives target (architecture §9: "the expensive
    /// Show Bible call is usually untouched" -- so is every undirected SectionScript), recompiles/
    /// re-exports, re-measures/re-critiques, and diffs the new report against <paramref name="previous"/>'s
    /// (convergence discipline). Returns <see langword="null"/> when no directive was produced and the
    /// sequence has nothing left this loop can act on -- EXCEPT immediately after iteration 0: iteration 0
    /// structurally cannot have produced a visual-critique directive (there was no prior iteration for it
    /// to compare against), so a zero-directive result there is not yet real convergence evidence. In that
    /// one case this method instead runs a directive-free re-measurement pass (same scripts, genuinely
    /// re-rendered) so iteration 1 gets the first real previous-vs-candidate visual comparison architecture
    /// §9 calls for, rather than the caller stopping before any real comparison was ever attempted. From
    /// iteration 1 onward a zero-directive result is real convergence and this method returns
    /// <see langword="null"/> as before.
    /// </summary>
    public async Task<IterationResult?> RunNextAsync(Guid songId, IterationResult previous, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(previous);

        var directives = DeriveDirectives(previous);

        if (directives.Count == 0)
        {
            if (previous.IterationNumber > 0)
            {
                return null;
            }

            // Forced first comparison pass (see remarks above): same scripts, no regeneration call, but a
            // genuine re-render/re-measure/re-critique so this iteration's visual critique finally has
            // iteration 0's real frames as its reference instead of nothing.
            var (compiledSame, exportSame) = await _compileAndExportAsync(previous.Scripts).ConfigureAwait(false);
            var (reportSame, numericFindingsSame, visualCritiquesSame, framesSame) =
                await MeasureAndCritiqueAsync(exportSame, previous.Frames, ct).ConfigureAwait(false);
            var diffSame = QualityReportDiff.Compare(previous.Report, reportSame);

            return new IterationResult(
                previous.IterationNumber + 1, previous.Scripts, compiledSame, exportSame, reportSame,
                numericFindingsSame, visualCritiquesSame, [], diffSame.Regressions, framesSame);
        }

        var scripts = await TargetedSectionRegenerator.RegenerateAsync(
            _batchGenerator, _stablePrefix, previous.Scripts, directives, _volatileSuffixBySectionId,
            songId, _bibleLooks, _layout, _maxOutputTokens, ct).ConfigureAwait(false);

        var (compiled, export) = await _compileAndExportAsync(scripts).ConfigureAwait(false);
        var (report, numericFindings, visualCritiques, frames) = await MeasureAndCritiqueAsync(export, previous.Frames, ct).ConfigureAwait(false);

        var diff = QualityReportDiff.Compare(previous.Report, report);

        return new IterationResult(
            previous.IterationNumber + 1, scripts, compiled, export, report, numericFindings, visualCritiques, directives, diff.Regressions, frames);
    }

    private IReadOnlyList<RevisionDirective> DeriveDirectives(IterationResult previous)
    {
        var numericSectionMap = BuildNumericFindingSectionMap(previous.NumericFindings);
        return RevisionDirectiveDeriver.FromNumericFindings(previous.NumericFindings, numericSectionMap)
            .Concat(RevisionDirectiveDeriver.FromVisualCritiques(previous.VisualCritiques, _sectionIntentById))
            .ToList();
    }

    /// <summary>
    /// Build-plan Phase 7d task 10: bounded best-of-N (4-8). Runs <paramref name="candidateCount"/>
    /// independent <see cref="RunNextAsync"/> attempts from the SAME <paramref name="previous"/> iteration
    /// (each its own regeneration, since the Choreographer's own output is not deterministic across calls)
    /// and returns the one with the highest <see cref="Refinement.BestCandidateSelector.AggregateScore"/>
    /// among those with zero <see cref="QualityReport.Violations"/> -- never the last one tried.
    /// </summary>
    public async Task<IterationResult?> RunBestOfNAsync(Guid songId, IterationResult previous, int candidateCount, CancellationToken ct)
    {
        if (candidateCount is < 4 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateCount), candidateCount, "Architecture §9: best-of-N search must stay small (4-8) -- unimodal in N, reward hacking sets in past that range.");
        }

        IterationResult? best = null;
        for (var i = 0; i < candidateCount; i++)
        {
            var candidate = await RunNextAsync(songId, previous, ct).ConfigureAwait(false);
            if (candidate is null)
            {
                continue; // No directive this attempt -- nothing new to compare.
            }

            if (candidate.Report.Violations.Count > 0)
            {
                continue; // A violating candidate can never become best (mirrors BestCandidateSelector's own rule).
            }

            if (best is null || Refinement.BestCandidateSelector.AggregateScore(candidate.Report) > Refinement.BestCandidateSelector.AggregateScore(best.Report))
            {
                best = candidate;
            }
        }

        return best;
    }

    /// <param name="previousFrames">
    /// The previous iteration's own rendered frames, or <see langword="null"/> when there is no previous
    /// iteration yet (iteration 0). Architecture §9's judging discipline is to compare each iteration
    /// "against the previous" -- with no previous frames available there is nothing genuine to compare
    /// against, so visual critique is skipped entirely rather than comparing <paramref name="export"/>'s
    /// own render against itself (start vs. end of the same section), which would produce a structurally
    /// meaningless "candidate always wins" verdict and never a real comparison.
    /// </param>
    private async Task<(QualityReport Report, IReadOnlyList<CritiqueFinding> NumericFindings, IReadOnlyList<SectionVisualCritique> VisualCritiques, IReadOnlyList<PreviewFrame> Frames)> MeasureAndCritiqueAsync(
        ExportResult export, IReadOnlyList<PreviewFrame>? previousFrames, CancellationToken ct)
    {
        var frames = await _renderAsync(export, ct).ConfigureAwait(false);
        var context = new HarnessSequenceContext(frames, _totalDisplayPointCount);
        var report = SequenceMetricBattery.Evaluate(Guid.NewGuid(), context);
        var numericFindings = NumericCritic.Critique(report);

        var visualCritiques = new List<SectionVisualCritique>();
        if (_visualCritic is not null && frames.Count > 0 && previousFrames is { Count: > 0 })
        {
            foreach (var (sectionId, window) in _sectionWindowsById)
            {
                var candidateFrame = FrameAtOrLast(frames, window.End);
                var referenceFrame = FrameAtOrLast(previousFrames, window.End);
                if (!_sectionIntentById.TryGetValue(sectionId, out var intent))
                {
                    continue;
                }

                // A real vision model (especially a small local one) does not always return parseable JSON
                // for a given call -- that is a property of this one section's critique attempt, not a
                // reason to abandon the whole iteration's measurement. Skip just this section's critique
                // rather than throwing; every other section's critique, and the numeric battery above,
                // still completes normally.
                try
                {
                    visualCritiques.Add(await _visualCritic.CompareAsync(sectionId, candidateFrame, referenceFrame, intent, ct).ConfigureAwait(false));
                }
                catch (AiCallRejectedException)
                {
                }
            }
        }

        return (report, numericFindings, visualCritiques, frames);
    }

    private static PreviewFrame FrameAtOrLast(IReadOnlyList<PreviewFrame> frames, long ticks) =>
        frames.FirstOrDefault(f => f.TimeTicks >= ticks) ?? frames[^1];

    // Numeric findings are whole-sequence, not section-scoped -- the only two metrics this loop can
    // currently translate to specific sections are the ones architecture §9's own battery computes
    // per-section inputs for (PixelHeavyDutyCycle/per-section energy); every other metric is logged as a
    // finding but produces no directive (nowhere specific to send it), consistent with
    // RevisionDirectiveDeriver's own "a metric with no mapped sections produces no directive" rule.
    private static IReadOnlyDictionary<string, IReadOnlyList<Guid>> BuildNumericFindingSectionMap(IReadOnlyList<CritiqueFinding> findings) =>
        new Dictionary<string, IReadOnlyList<Guid>>(StringComparer.Ordinal);
}
