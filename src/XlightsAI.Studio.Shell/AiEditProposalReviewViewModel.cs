using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-11 Step 076 ("Implement dry-run proposals, before/after diff and Accept/Reject") -- SCREEN 14
/// AI EDIT's own bindable preview drawer. Implementation guidance, verbatim: "Diff proposals against
/// immutable document and show changed effect count/models/time, with preview and explicit reject."
/// Owns the UI-facing projection over <see cref="IEditProposalDryRunService"/>/<see cref="IEditApprovalService"/>
/// -- the panel a real SCREEN 14 view would bind "before/after" text and Accept/Reject buttons to. Contains
/// no Avalonia types, the same convention every other screen view model in this project already follows
/// (<see cref="AiEditRequestPanelViewModel"/>, <see cref="StructuredCommandQuickActionsViewModel"/>).
///
/// <para>
/// Required acceptance, verbatim: "Reject leaves state byte-equivalent; accept records transaction." --
/// <see cref="Reject"/> only clears this view model's own bindable preview state and never touches
/// <paramref name="document"/> in any call (there is nothing to undo because nothing was ever mutated);
/// <see cref="AcceptAsync"/> is the only member that can ever produce an updated document, and only when
/// called explicitly.
/// </para>
/// </summary>
public sealed class AiEditProposalReviewViewModel : ObservableObject
{
    private const long TicksPerSecond = 10_000_000L;

    private ScreenLoadState _loadState = ScreenLoadState.Empty;
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

    /// <summary>The currently previewed dry run, or <see langword="null"/> while <see cref="IsEmpty"/> or after <see cref="Reject"/>/a successful <see cref="AcceptAsync"/>.</summary>
    public EditProposalDryRun? DryRun => _dryRun;

    /// <summary>Human-readable "changed effect count/models/time" summary of <see cref="DryRun"/>'s own <see cref="EditProposalDryRun.Diff"/> ("show changed effect count/models/time," implementation guidance, verbatim). Empty while <see cref="IsEmpty"/>.</summary>
    public string DiffSummary
    {
        get => _diffSummary;
        private set => SetField(ref _diffSummary, value);
    }

    /// <summary>Set whenever <see cref="PreviewAsync"/> or <see cref="AcceptAsync"/> is rejected; cleared on the next successful call. "Clean machine and unsupported runtime must produce user-readable diagnostics" (Step 076's own required failure/edge case) -- always the real validator's own joined error list, never a generic message.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    /// <summary>The most recently committed transaction+document, or <see langword="null"/> before any successful <see cref="AcceptAsync"/>.</summary>
    public EditApplyResult? LastAccepted => _lastAccepted;

    /// <summary>
    /// Builds a sandbox preview of <paramref name="proposal"/> against <paramref name="document"/>,
    /// populating <see cref="DryRun"/>/<see cref="DiffSummary"/>. Never mutates <paramref name="document"/>.
    /// Returns <see langword="false"/> (via <see cref="ErrorMessage"/>) on rejection, leaving any
    /// previously previewed <see cref="DryRun"/> untouched.
    /// </summary>
    public Task<bool> PreviewAsync(
        IEditProposalDryRunService dryRunService,
        EditProposal proposal,
        SelectionContext context,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dryRunService);

        try
        {
            ct.ThrowIfCancellationRequested();
            var dryRun = dryRunService.Preview(proposal, context, document);

            _dryRun = dryRun;
            DiffSummary = FormatDiffSummary(dryRun.Diff);
            ErrorMessage = null;
            LoadState = ScreenLoadState.Loaded;
            OnPropertyChanged(nameof(DryRun));
            return Task.FromResult(true);
        }
        catch (EditProposalRejectedException ex)
        {
            ErrorMessage = ex.Message;
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// "Discard candidate" (Section 3's own flowchart). Clears <see cref="DryRun"/>/<see cref="DiffSummary"/>
    /// without ever touching any document -- "Reject leaves state byte-equivalent" (required acceptance,
    /// verbatim) holds trivially because this method's own body never references a <see cref="SequenceDocument"/>.
    /// A no-op when nothing is currently previewed.
    /// </summary>
    public void Reject()
    {
        _dryRun = null;
        DiffSummary = string.Empty;
        ErrorMessage = null;
        LoadState = ScreenLoadState.Empty;
        OnPropertyChanged(nameof(DryRun));
    }

    /// <summary>
    /// Commits the currently previewed <see cref="DryRun"/> via <paramref name="approvalService"/>.
    /// Clears <see cref="DryRun"/> on success (there is no longer anything pending to accept or reject) and
    /// populates <see cref="LastAccepted"/>. Returns <see langword="false"/> (via <see cref="ErrorMessage"/>)
    /// when the document changed since preview -- <see cref="DryRun"/> is left intact so the caller can
    /// re-preview against the current document.
    /// </summary>
    /// <exception cref="InvalidOperationException">No proposal is currently previewed (call <see cref="PreviewAsync"/> first).</exception>
    public async Task<bool> AcceptAsync(
        IEditApprovalService approvalService,
        SelectionContext context,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(approvalService);

        if (_dryRun is null)
        {
            throw new InvalidOperationException("No proposal is currently previewed; call PreviewAsync before AcceptAsync.");
        }

        try
        {
            var result = await approvalService.AcceptAsync(_dryRun, context, document, ct).ConfigureAwait(false);

            _lastAccepted = result;
            _dryRun = null;
            DiffSummary = string.Empty;
            ErrorMessage = null;
            LoadState = ScreenLoadState.Empty;
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

    private static string FormatDiffSummary(ProposalDiff diff)
    {
        var models = diff.AffectedModels.Count == 0
            ? "(no props affected)"
            : string.Join(", ", diff.AffectedModels.Select(m => m.Value));
        var startSeconds = diff.AffectedTime.StartTicks / (double)TicksPerSecond;
        var endSeconds = diff.AffectedTime.EndTicks / (double)TicksPerSecond;
        return $"{diff.ChangedEffectCount} effect(s) changed on {models} @ [{startSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}s, {endSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}s)";
    }
}
