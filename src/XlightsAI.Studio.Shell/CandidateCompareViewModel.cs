using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Refinement;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-12 Step 084 ("Add candidate gallery, score explanations, preview and explicit selection") --
/// SCREEN 15 COMPARE's own bindable candidate gallery. Implementation guidance, verbatim: "Compare
/// best-vs-current with scores and synchronized preview; record deliberate human accept/reject and
/// optional feedback labels." Owns the UI-facing projection over <see cref="IRefinementRunStore"/>/
/// <see cref="IChoreographyQualityEvaluator"/>/<see cref="IRefinementCandidateCommitService"/> -- the
/// panel a real SCREEN 15 view would bind its candidate list and Accept/Reject buttons to. Contains no
/// Avalonia types, the same convention every other screen view model in this project already follows
/// (<see cref="AiEditProposalReviewViewModel"/>, this step's own closest PHASE-11 sibling).
///
/// <para>
/// Required acceptance, verbatim: "User can compare alternatives and commit chosen candidate." --
/// <see cref="LoadAsync"/> populates every recorded, inspectable attempt (not only
/// <see cref="LoopRun.BestCandidateId"/>) as its own <see cref="CandidateGalleryRow"/>, letting a caller
/// bind to and select any one of them before calling <see cref="AcceptAsync"/> with that row's own
/// <see cref="CandidateGalleryRow.CandidateId"/> -- never implicitly re-substituting the retained best.
/// </para>
/// </summary>
public sealed class CandidateCompareViewModel : ObservableObject
{
    private const long TicksPerSecond = 10_000_000L;

    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private LoopRun? _run;
    private IReadOnlyList<CandidateGalleryRow> _candidates = [];
    private Guid? _selectedCandidateId;
    private string? _errorMessage;
    private EditApplyResult? _lastAccepted;
    private LoopRun? _lastRejected;

    public ScreenLoadState LoadState
    {
        get => _loadState;
        private set
        {
            if (SetField(ref _loadState, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsError));
                OnPropertyChanged(nameof(IsLoaded));
            }
        }
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>The run this gallery currently shows, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public LoopRun? Run => _run;

    /// <summary>Every recorded attempt for <see cref="Run"/>, in attempt order -- "which song, time range, props, analysis source, version and approval state" (Section 5's own cross-cutting UI rule) is carried by <see cref="Run"/> itself, never duplicated per row.</summary>
    public IReadOnlyList<CandidateGalleryRow> Candidates => _candidates;

    /// <summary>The currently selected candidate's own <see cref="CandidateGalleryRow.CandidateId"/>, defaulted to <see cref="LoopRun.BestCandidateId"/> by <see cref="LoadAsync"/> when one exists, or <see langword="null"/> while <see cref="IsEmpty"/> or when the run retained no valid best.</summary>
    public Guid? SelectedCandidateId
    {
        get => _selectedCandidateId;
        private set => SetField(ref _selectedCandidateId, value);
    }

    /// <summary>The currently selected row, or <see langword="null"/> when <see cref="SelectedCandidateId"/> names no row currently in <see cref="Candidates"/>.</summary>
    public CandidateGalleryRow? SelectedCandidate => _candidates.FirstOrDefault(c => c.CandidateId == _selectedCandidateId);

    /// <summary>Set whenever <see cref="SelectCandidate"/>, <see cref="AcceptAsync"/>, or <see cref="RejectAsync"/> is rejected; cleared on the next successful call.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    /// <summary>The most recently committed transaction+document, or <see langword="null"/> before any successful <see cref="AcceptAsync"/>.</summary>
    public EditApplyResult? LastAccepted => _lastAccepted;

    /// <summary>The most recently rejected run's final state, or <see langword="null"/> before any successful <see cref="RejectAsync"/>.</summary>
    public LoopRun? LastRejected => _lastRejected;

    /// <summary>
    /// Loads every recorded attempt for <paramref name="runId"/> and re-evaluates each one's own
    /// <see cref="QualityReport"/> via <paramref name="evaluator"/> (the same "recompute rather than
    /// persist a <c>Report</c> field" convention <see cref="GestureRefinementOrchestrator"/>'s own
    /// resume path already established, STEP-083.md Section 2.1). <see cref="SelectedCandidateId"/>
    /// defaults to <see cref="LoopRun.BestCandidateId"/> when the loaded run retained one.
    /// </summary>
    public async Task<bool> LoadAsync(
        IRefinementRunStore store,
        IChoreographyQualityEvaluator evaluator,
        ChoreographyQualityContext context,
        Guid runId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            ct.ThrowIfCancellationRequested();

            var run = await store.LoadRunAsync(runId, ct).ConfigureAwait(false);
            if (run is null)
            {
                ErrorMessage = $"No refinement run with Id '{runId}' exists.";
                return false;
            }

            var attempts = await store.LoadAttemptsAsync(runId, ct).ConfigureAwait(false);
            var rows = new List<CandidateGalleryRow>(attempts.Count);
            foreach (var attempt in attempts)
            {
                var report = await evaluator.EvaluateAsync(run, attempt, context, ct).ConfigureAwait(false);
                rows.Add(new CandidateGalleryRow(
                    attempt.Id,
                    attempt.Attempt,
                    attempt.Status,
                    BestCandidateSelector.AggregateScore(report),
                    attempt.Violations.Count > 0 || report.Violations.Count > 0,
                    report,
                    FormatPreviewSummary(attempt),
                    attempt.Id == run.BestCandidateId));
            }

            _run = run;
            _candidates = rows;
            SelectedCandidateId = run.BestCandidateId ?? rows.FirstOrDefault(r => !r.HasViolations)?.CandidateId;
            ErrorMessage = null;
            LoadState = ScreenLoadState.Loaded;
            OnPropertyChanged(nameof(Run));
            OnPropertyChanged(nameof(Candidates));
            OnPropertyChanged(nameof(SelectedCandidate));
            return true;
        }
        catch (RefinementRunCorruptException ex)
        {
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            return false;
        }
    }

    /// <summary>
    /// "User can compare alternatives" (required acceptance, verbatim): selects any row currently in
    /// <see cref="Candidates"/>, not only the retained best -- a no-op (reported via
    /// <see cref="ErrorMessage"/>) when <paramref name="candidateId"/> names no currently-loaded row.
    /// </summary>
    public void SelectCandidate(Guid candidateId)
    {
        if (_candidates.All(c => c.CandidateId != candidateId))
        {
            ErrorMessage = $"Candidate '{candidateId}' is not one of the currently loaded gallery rows.";
            return;
        }

        SelectedCandidateId = candidateId;
        ErrorMessage = null;
        OnPropertyChanged(nameof(SelectedCandidate));
    }

    /// <summary>
    /// Commits <see cref="SelectedCandidateId"/> via <paramref name="commitService"/>. Populates
    /// <see cref="LastAccepted"/> on success; clears gallery state (there is nothing left pending once a
    /// run reaches its terminal <see cref="RefinementStatus.Accepted"/>). Returns <see langword="false"/>
    /// (via <see cref="ErrorMessage"/>) on any rejected gate -- "Out-of-scope, locked or stale revision
    /// must be rejected without partial mutation" (this step's own required failure/edge case, verbatim)
    /// -- leaving <see cref="Candidates"/>/<see cref="SelectedCandidateId"/> untouched so the caller can
    /// re-load against the current document and retry.
    /// </summary>
    /// <exception cref="InvalidOperationException">No candidate is currently selected (call <see cref="LoadAsync"/>/<see cref="SelectCandidate"/> first).</exception>
    public async Task<bool> AcceptAsync(
        IRefinementCandidateCommitService commitService,
        IRefinementRunStore store,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(commitService);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(document);

        if (_run is null || _selectedCandidateId is not { } candidateId)
        {
            throw new InvalidOperationException("No candidate is currently selected; call LoadAsync/SelectCandidate before AcceptAsync.");
        }

        try
        {
            var result = await commitService.AcceptAsync(store, _run.Id, candidateId, document, ct).ConfigureAwait(false);

            _lastAccepted = result;
            _run = null;
            _candidates = [];
            SelectedCandidateId = null;
            ErrorMessage = null;
            LoadState = ScreenLoadState.Empty;
            OnPropertyChanged(nameof(Run));
            OnPropertyChanged(nameof(Candidates));
            OnPropertyChanged(nameof(SelectedCandidate));
            OnPropertyChanged(nameof(LastAccepted));
            return true;
        }
        catch (Exception ex) when (ex is RefinementRunInvalidException or EditCommandRejectedException)
        {
            ErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Declines the whole currently-loaded run via <paramref name="commitService"/>, with an optional
    /// human feedback label. "Reject leaves state byte-equivalent" (this step's own closest sibling,
    /// PHASE-11 Step 076's own required acceptance, applied here): never touches any
    /// <see cref="SequenceDocument"/> in any call.
    /// </summary>
    /// <exception cref="InvalidOperationException">No run is currently loaded (call <see cref="LoadAsync"/> first).</exception>
    public async Task<bool> RejectAsync(
        IRefinementCandidateCommitService commitService,
        IRefinementRunStore store,
        string? feedbackLabel,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(commitService);
        ArgumentNullException.ThrowIfNull(store);

        if (_run is null)
        {
            throw new InvalidOperationException("No run is currently loaded; call LoadAsync before RejectAsync.");
        }

        try
        {
            var rejected = await commitService.RejectAsync(store, _run.Id, feedbackLabel, ct).ConfigureAwait(false);

            _lastRejected = rejected;
            _run = null;
            _candidates = [];
            SelectedCandidateId = null;
            ErrorMessage = null;
            LoadState = ScreenLoadState.Empty;
            OnPropertyChanged(nameof(Run));
            OnPropertyChanged(nameof(Candidates));
            OnPropertyChanged(nameof(SelectedCandidate));
            OnPropertyChanged(nameof(LastRejected));
            return true;
        }
        catch (RefinementRunInvalidException ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
    }

    private static string FormatPreviewSummary(CandidateAttempt attempt)
    {
        var gestures = attempt.Gestures ?? [];
        if (gestures.Count == 0)
        {
            return "(no gestures -- silence/empty generation)";
        }

        var parts = gestures.Select(g =>
        {
            var startSeconds = g.Window.StartTicks / (double)TicksPerSecond;
            var endSeconds = g.Window.EndTicks / (double)TicksPerSecond;
            var props = string.Join(", ", g.TargetProps.Select(p => p.Value));
            return $"{g.Kind} on {props} @ [{startSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}s, {endSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}s) intensity {g.StartIntensity:0.00}->{g.EndIntensity:0.00}";
        });

        return string.Join("; ", parts);
    }
}
