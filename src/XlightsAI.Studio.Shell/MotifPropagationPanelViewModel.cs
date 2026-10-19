using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-11 Step 077 ("Implement explicit motif propagation and edit-scope intersection validation") --
/// SCREEN 14 AI EDIT's own bindable motif-propagation drawer, layered on top of the already-accepted Step
/// 076 <see cref="AiEditProposalReviewViewModel"/> preview/accept/reject conventions. Implementation
/// guidance, verbatim: "Apply motif propagation only after preview of all affected occurrences and each
/// target locks/scope permissions; support instance-only choice." Contains no Avalonia types, the same
/// convention every other screen view model in this project already follows.
///
/// <para>
/// <see cref="AttachCandidates"/> lists every other occurrence a caller (a future motif-group inspector,
/// per <c>XlightsAI.Studio.Structure.MotifGroupCandidate</c>) offers as a propagation target; the user then
/// toggles individual occurrences in or out via <see cref="IncludeOccurrence"/>/<see cref="ExcludeOccurrence"/>
/// before <see cref="PreviewAsync"/> is ever called -- an occurrence is never included by default.
/// "Support instance-only choice" is simply never toggling any occurrence on: <see cref="SelectedOccurrenceIds"/>
/// starts empty and <see cref="MotifPropagationExpander.Expand"/> treats an empty selection as "widen
/// nothing."
/// </para>
/// </summary>
public sealed class MotifPropagationPanelViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private EditProposal? _baseProposal;
    private EditScope? _baseScope;
    private IReadOnlyList<MotifOccurrenceTarget> _candidates = [];
    private readonly HashSet<Guid> _selectedOccurrenceIds = [];
    private EditProposalDryRun? _dryRun;
    private string _diffSummary = string.Empty;
    private string? _errorMessage;
    private EditApplyResult? _lastAccepted;

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

    /// <summary>Every other occurrence of this motif offered as a propagation candidate, as attached by <see cref="AttachCandidates"/>.</summary>
    public IReadOnlyList<MotifOccurrenceTarget> Candidates => _candidates;

    /// <summary>
    /// Exactly the occurrence ids the user has explicitly toggled on. Empty is "instance-only" (this
    /// step's own required support, verbatim) -- never populated automatically.
    /// </summary>
    public IReadOnlySet<Guid> SelectedOccurrenceIds => _selectedOccurrenceIds;

    /// <summary>The currently previewed widened dry run, or <see langword="null"/> while <see cref="IsEmpty"/> or after <see cref="Reject"/>/a successful <see cref="AcceptAsync"/>.</summary>
    public EditProposalDryRun? DryRun => _dryRun;

    /// <summary>Human-readable "changed effect count/models/time" summary of <see cref="DryRun"/>'s own diff, covering every included occurrence at once. Empty while <see cref="IsEmpty"/>.</summary>
    public string DiffSummary
    {
        get => _diffSummary;
        private set => SetField(ref _diffSummary, value);
    }

    /// <summary>Set whenever <see cref="PreviewAsync"/> or <see cref="AcceptAsync"/> is rejected; cleared on the next successful call.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    /// <summary>The most recently committed transaction+document, or <see langword="null"/> before any successful <see cref="AcceptAsync"/>.</summary>
    public EditApplyResult? LastAccepted => _lastAccepted;

    /// <summary>
    /// Attaches the base proposal/scope (the single-occurrence edit already approved by the Step 076
    /// review drawer, not yet committed) and the full propagation candidate set. Resets any
    /// previously selected occurrences, previewed dry run and error state.
    /// </summary>
    public void AttachCandidates(EditProposal baseProposal, EditScope baseScope, IReadOnlyList<MotifOccurrenceTarget> candidates)
    {
        ArgumentNullException.ThrowIfNull(baseProposal);
        ArgumentNullException.ThrowIfNull(baseScope);
        ArgumentNullException.ThrowIfNull(candidates);

        _baseProposal = baseProposal;
        _baseScope = baseScope;
        _candidates = candidates;
        _selectedOccurrenceIds.Clear();
        _dryRun = null;
        DiffSummary = string.Empty;
        ErrorMessage = null;
        LoadState = ScreenLoadState.Loaded;
        OnPropertyChanged(nameof(Candidates));
        OnPropertyChanged(nameof(SelectedOccurrenceIds));
        OnPropertyChanged(nameof(DryRun));
    }

    /// <summary>Toggles one candidate occurrence on. A no-op if it is not among <see cref="Candidates"/> or already included.</summary>
    public void IncludeOccurrence(Guid occurrenceId)
    {
        if (_candidates.Any(c => c.OccurrenceId == occurrenceId) && _selectedOccurrenceIds.Add(occurrenceId))
        {
            OnPropertyChanged(nameof(SelectedOccurrenceIds));
        }
    }

    /// <summary>Toggles one candidate occurrence off (back to "instance-only" if it was the last one selected). A no-op if it was not included.</summary>
    public void ExcludeOccurrence(Guid occurrenceId)
    {
        if (_selectedOccurrenceIds.Remove(occurrenceId))
        {
            OnPropertyChanged(nameof(SelectedOccurrenceIds));
        }
    }

    /// <summary>
    /// Widens the attached base proposal for exactly <see cref="SelectedOccurrenceIds"/>, validates every
    /// resulting operation's own edit-scope intersection and locks, and builds a sandbox preview -- "Apply
    /// motif propagation only after preview of all affected occurrences and each target locks/scope
    /// permissions" (implementation guidance, verbatim). Never mutates <paramref name="document"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No candidates have been attached via <see cref="AttachCandidates"/> yet.</exception>
    public Task<bool> PreviewAsync(
        IMotifPropagationDryRunService dryRunService,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dryRunService);
        ArgumentNullException.ThrowIfNull(document);

        if (_baseProposal is null || _baseScope is null)
        {
            throw new InvalidOperationException("No propagation candidates have been attached; call AttachCandidates before PreviewAsync.");
        }

        try
        {
            ct.ThrowIfCancellationRequested();
            var selection = new MotifPropagationSelection(_selectedOccurrenceIds.ToHashSet());
            var expanded = MotifPropagationExpander.Expand(_baseProposal, _baseScope, _candidates, selection);
            var dryRun = dryRunService.Preview(expanded, document);

            _dryRun = dryRun;
            DiffSummary = FormatDiffSummary(dryRun.Diff, expanded.IncludedOccurrenceIds.Count);
            ErrorMessage = null;
            OnPropertyChanged(nameof(DryRun));
            return Task.FromResult(true);
        }
        catch (EditProposalRejectedException ex)
        {
            ErrorMessage = ex.Message;
            return Task.FromResult(false);
        }
    }

    /// <summary>"Discard candidate" -- clears the previewed dry run without ever touching any document.</summary>
    public void Reject()
    {
        _dryRun = null;
        DiffSummary = string.Empty;
        ErrorMessage = null;
        OnPropertyChanged(nameof(DryRun));
    }

    /// <summary>
    /// Commits the currently previewed widened proposal. Re-validates against the live document, so a
    /// lock or revision change landing between preview and accept is caught here, not silently committed.
    /// </summary>
    /// <exception cref="InvalidOperationException">No proposal is currently previewed (call <see cref="PreviewAsync"/> first).</exception>
    public async Task<bool> AcceptAsync(
        IMotifPropagationApprovalService approvalService,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(approvalService);
        ArgumentNullException.ThrowIfNull(document);

        if (_dryRun is null || _baseProposal is null || _baseScope is null)
        {
            throw new InvalidOperationException("No proposal is currently previewed; call PreviewAsync before AcceptAsync.");
        }

        try
        {
            var selection = new MotifPropagationSelection(_selectedOccurrenceIds.ToHashSet());
            var expanded = MotifPropagationExpander.Expand(_baseProposal, _baseScope, _candidates, selection);
            var result = await approvalService.AcceptAsync(_dryRun, expanded, document, ct).ConfigureAwait(false);

            _lastAccepted = result;
            _dryRun = null;
            DiffSummary = string.Empty;
            ErrorMessage = null;
            OnPropertyChanged(nameof(DryRun));
            OnPropertyChanged(nameof(LastAccepted));
            return true;
        }
        catch (EditProposalRejectedException ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
    }

    private static string FormatDiffSummary(ProposalDiff diff, int includedOccurrenceCount)
    {
        var models = diff.AffectedModels.Count == 0
            ? "(no props affected)"
            : string.Join(", ", diff.AffectedModels.Select(m => m.Value));
        return $"{diff.ChangedEffectCount} effect(s) changed on {models}, across {includedOccurrenceCount} propagated occurrence(s) plus the original instance";
    }
}
