using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-11 Step 073 ("Implement deterministic command AST for brightness/motion/direction/speed/
/// replace") -- SCREEN 14 AI EDIT's own bindable offline quick-actions panel. Owns the UI-facing
/// projection over <see cref="IStructuredEditInterpreter"/>: every quick-action button a real SCREEN 14
/// view would bind to (Brighten, Dim, Reverse, Faster, Slower, Snap to grid, Follow &lt;role&gt;) is
/// just a fixed instruction string passed to <see cref="ParseAsync"/> -- no network call, no optional AI
/// planner is ever consulted by this view model (Step 073's own required acceptance, verbatim: "UI quick
/// actions work offline and no token usage"). Contains no Avalonia types, the same convention every
/// other screen view model in this project already follows (<see cref="SelectionContextEditorViewModel"/>).
///
/// <para>
/// Mirrors <see cref="SelectionContextEditorViewModel"/>'s own explicit loading/empty/error/loaded state
/// machine (no proposal parsed yet is <see cref="ScreenLoadState.Empty"/>) and its own "catch the
/// domain's typed invalid-input exception, set <see cref="ActionErrorMessage"/>, leave bindable state
/// completely unchanged on failure" convention -- <see cref="LastProposal"/> is never partially updated
/// by a rejected instruction.
/// </para>
/// </summary>
public sealed class StructuredCommandQuickActionsViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _actionErrorMessage;
    private EditProposal? _lastProposal;

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

    /// <summary>Set whenever a parse attempt fails (unrecognized instruction, locked target or stale revision); cleared on the next successful parse. <see cref="LastProposal"/> is left completely unchanged on failure.</summary>
    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set => SetField(ref _actionErrorMessage, value);
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>The most recently parsed proposal, or <see langword="null"/> while <see cref="IsEmpty"/>. Never applied to <paramref name="document"/> by this view model -- Step 076's own later "Accept/Reject" owns commit.</summary>
    public EditProposal? LastProposal => _lastProposal;

    /// <summary>
    /// Parses <paramref name="instruction"/> against <paramref name="context"/>/<paramref name="document"/>
    /// via <paramref name="interpreter"/>. Returns <see langword="false"/> (via
    /// <see cref="ActionErrorMessage"/>) on any rejection (unrecognized instruction, out-of-range
    /// parameter, locked target or stale revision) -- never a partial mutation of <see cref="LastProposal"/>.
    /// </summary>
    public Task<bool> ParseAsync(
        IStructuredEditInterpreter interpreter,
        string instruction,
        SelectionContext context,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(interpreter);

        try
        {
            ct.ThrowIfCancellationRequested();
            var proposal = interpreter.ParseKnownCommand(instruction, context, document, ct);

            _lastProposal = proposal;
            ActionErrorMessage = null;
            LoadState = ScreenLoadState.Loaded;
            OnPropertyChanged(nameof(LastProposal));
            return Task.FromResult(true);
        }
        catch (EditCommandRejectedException ex)
        {
            ActionErrorMessage = ex.Message;
            return Task.FromResult(false);
        }
    }
}
