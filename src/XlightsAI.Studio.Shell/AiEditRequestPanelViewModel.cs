using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-11 Step 074 ("Add edit request panel, suggestions and change explanation") -- SCREEN 14 AI
/// EDIT's own bindable selection-summary drawer plus free-text instruction request. Implementation
/// guidance, verbatim: "Create selection-summary drawer listing affected props and exact time range and
/// AI text input; do not send until explicit action." Contains no Avalonia types, the same convention
/// every other screen view model in this project already follows
/// (<see cref="SelectionContextEditorViewModel"/>, <see cref="StructuredCommandQuickActionsViewModel"/>).
///
/// <para>
/// This step adds no new domain contract: <see cref="AiEdits.SelectionContext"/>,
/// <see cref="IStructuredEditInterpreter"/> and <see cref="EditProposal"/> (Steps 072/073) already carry
/// every invariant this panel needs (revision, scope, locks, explanation). Step 074's own narrow scope is
/// the UI-facing composition over those already-accepted contracts: an always-visible target-context
/// summary, a fixed suggestion list drawn from the real interpreter's own closed grammar, and an explicit
/// two-phase request flow (type, then submit) so typing alone can never reach the interpreter.
/// </para>
///
/// <para>
/// Required acceptance, verbatim: "Request always displays target context" --
/// <see cref="AttachContext"/> populates <see cref="TargetSummary"/> immediately and independently of
/// whether any instruction has ever been typed or submitted; <see cref="TargetSummary"/> is never cleared
/// by a failed <see cref="SubmitAsync"/> call (the same "leave bindable state completely unchanged on
/// failure" convention <see cref="StructuredCommandQuickActionsViewModel"/> already establishes for
/// <c>LastProposal</c>, applied here to the target-context display itself).
/// </para>
/// </summary>
public sealed class AiEditRequestPanelViewModel : ObservableObject
{
    private const long TicksPerSecond = 10_000_000L;

    /// <summary>
    /// The fixed, closed suggestion list this panel offers -- drawn directly from
    /// <see cref="StructuredCommandInterpreter"/>'s own known, offline grammar (Step 073), never a
    /// free-form or AI-generated list. Shown alongside the free-text input, not in place of it.
    /// </summary>
    public static IReadOnlyList<string> Suggestions { get; } =
    [
        "brighten",
        "dim",
        "reverse",
        "direction left-to-right",
        "faster",
        "slower",
        "snap to grid",
        "follow lead",
    ];

    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private SelectionContext? _context;
    private string _targetSummary = string.Empty;
    private string _instructionText = string.Empty;
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

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    /// <summary>The selection this panel is currently drawn for, or <see langword="null"/> while <see cref="IsEmpty"/>.</summary>
    public SelectionContext? Context => _context;

    /// <summary>
    /// The affected props and exact half-open time range for <see cref="Context"/>, always populated as
    /// soon as a context is attached -- "Request always displays target context" (required acceptance,
    /// verbatim), independent of <see cref="InstructionText"/> or whether a request was ever submitted.
    /// Empty only while <see cref="IsEmpty"/>.
    /// </summary>
    public string TargetSummary
    {
        get => _targetSummary;
        private set => SetField(ref _targetSummary, value);
    }

    /// <summary>
    /// The free-text instruction the user is composing. Setting this property never invokes the
    /// interpreter and never sends anything -- "do not send until explicit action" (implementation
    /// guidance, verbatim); only an explicit <see cref="SubmitAsync"/> call does.
    /// </summary>
    public string InstructionText
    {
        get => _instructionText;
        set => SetField(ref _instructionText, value);
    }

    /// <summary>Set whenever a submitted request fails (unrecognized instruction, out-of-scope, locked target or stale revision); cleared on the next successful submit. <see cref="LastProposal"/> and <see cref="TargetSummary"/> are left completely unchanged on failure.</summary>
    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set => SetField(ref _actionErrorMessage, value);
    }

    /// <summary>The most recently accepted proposal, or <see langword="null"/> before any successful submit. Never applied to a document by this panel -- Step 076's own later "Accept/Reject" owns commit.</summary>
    public EditProposal? LastProposal => _lastProposal;

    /// <summary>The change explanation for <see cref="LastProposal"/> ("change explanation," step heading, verbatim), or <see langword="null"/> before any successful submit.</summary>
    public string? Explanation => _lastProposal?.Explanation;

    /// <summary>
    /// Attaches <paramref name="context"/> to this panel, immediately populating
    /// <see cref="TargetSummary"/>. Resets <see cref="InstructionText"/>/<see cref="LastProposal"/>/
    /// <see cref="ActionErrorMessage"/>, so a stale request from a previous selection can never be
    /// confused with the newly attached one.
    /// </summary>
    public void AttachContext(SelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        TargetSummary = FormatTargetSummary(context);
        _instructionText = string.Empty;
        OnPropertyChanged(nameof(InstructionText));
        _lastProposal = null;
        OnPropertyChanged(nameof(LastProposal));
        OnPropertyChanged(nameof(Explanation));
        ActionErrorMessage = null;
        LoadState = ScreenLoadState.Loaded;
        OnPropertyChanged(nameof(Context));
    }

    /// <summary>
    /// The explicit action that actually sends <see cref="InstructionText"/> to
    /// <paramref name="interpreter"/>, scoped to the already-attached <see cref="Context"/>. Returns
    /// <see langword="false"/> (via <see cref="ActionErrorMessage"/>) on any rejection -- never a partial
    /// mutation of <see cref="LastProposal"/>, <see cref="Explanation"/> or <see cref="TargetSummary"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="interpreter"/> or <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="SelectionContext"/> has been attached via <see cref="AttachContext"/> yet.</exception>
    public Task<bool> SubmitAsync(
        IStructuredEditInterpreter interpreter,
        SequenceDocument document,
        CancellationToken ct = default) => SubmitAsync(interpreter, planner: null, document, ct);

    /// <summary>
    /// PHASE-11 Step 075's own extension of <see cref="SubmitAsync(IStructuredEditInterpreter, SequenceDocument, CancellationToken)"/>:
    /// Section 3's own flowchart, verbatim -- "Known deterministic command? Yes -> Local structured
    /// interpreter... No -> Optional model configured + consent? No -> Explain manual alternatives; Yes
    /// -> Request structured AI proposal." <paramref name="interpreter"/> is always tried first and
    /// exclusively owns every recognized instruction; <paramref name="planner"/> is only ever consulted
    /// when <paramref name="interpreter"/> rejects with <see cref="EditRejectionReason.UnrecognizedInstruction"/>
    /// -- a stale-revision or locked rejection is never silently retried against the optional planner.
    /// Passing <see langword="null"/> for <paramref name="planner"/> reproduces the original offline-only
    /// behavior exactly (the flowchart's own "No -> Explain manual alternatives" branch).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="interpreter"/> or <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No <see cref="SelectionContext"/> has been attached via <see cref="AttachContext"/> yet.</exception>
    public async Task<bool> SubmitAsync(
        IStructuredEditInterpreter interpreter,
        IOptionalAiPlanner? planner,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(document);

        if (_context is null)
        {
            throw new InvalidOperationException("No selection context has been attached; call AttachContext before SubmitAsync.");
        }

        try
        {
            ct.ThrowIfCancellationRequested();
            var proposal = interpreter.ParseKnownCommand(InstructionText, _context, document, ct);
            AcceptProposal(proposal);
            return true;
        }
        catch (EditCommandRejectedException ex) when (ex.Reason == EditRejectionReason.UnrecognizedInstruction && planner is not null)
        {
            try
            {
                var proposal = await planner.ProposeAsync(InstructionText, _context, document, ct).ConfigureAwait(false);
                AcceptProposal(proposal);
                return true;
            }
            catch (AiProviderDisabledException)
            {
                ActionErrorMessage = "No optional AI provider is enabled; try a known offline command (see Suggestions), or enable a provider first.";
                return false;
            }
            catch (AiProposalRejectedException aiEx)
            {
                ActionErrorMessage = aiEx.Message;
                return false;
            }
        }
        catch (EditCommandRejectedException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    private void AcceptProposal(EditProposal proposal)
    {
        _lastProposal = proposal;
        OnPropertyChanged(nameof(LastProposal));
        OnPropertyChanged(nameof(Explanation));
        ActionErrorMessage = null;
    }

    private static string FormatTargetSummary(SelectionContext context)
    {
        var props = context.Scope.Models.Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToArray();
        var propsText = props.Length == 0 ? "(no props selected)" : string.Join(", ", props);
        var startSeconds = context.Scope.Time.StartTicks / (double)TicksPerSecond;
        var endSeconds = context.Scope.Time.EndTicks / (double)TicksPerSecond;
        return $"{propsText} @ [{startSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}s, {endSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}s)";
    }
}
