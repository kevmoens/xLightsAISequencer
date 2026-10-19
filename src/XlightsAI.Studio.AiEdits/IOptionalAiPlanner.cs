using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11.md Section 3's own proposed boundary, implemented against the already-accepted
/// <see cref="SelectionContext"/> (Step 072's own ADR) rather than the spec's proposed <c>EditContext</c>
/// -- the same narrowing <see cref="IStructuredEditInterpreter"/> already made, kept consistent here.
/// Section 3, verbatim: "Optional local/cloud LLM receives a redacted, least-privilege context
/// containing just selected props/time and available op schema." Every implementation (see
/// <see cref="NullOptionalAiPlanner"/>, <see cref="HttpStructuredOutputAiPlanner"/>) must either return a
/// fully-validated <see cref="EditProposal"/> or throw -- never a partial proposal, and never a document
/// mutation (this interface, like <see cref="IStructuredEditInterpreter"/>, never touches the document's
/// own persisted state; only a later, explicit approval step does).
/// </summary>
public interface IOptionalAiPlanner
{
    /// <exception cref="ArgumentNullException"><paramref name="instruction"/>, <paramref name="context"/>, or <paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    /// <exception cref="AiProviderDisabledException">No provider is enabled (<see cref="NullOptionalAiPlanner"/> only) -- Step 075's own required acceptance: "Provider off means no outbound calls."</exception>
    /// <exception cref="AiProposalRejectedException">The provider's response failed an untrusted-input gate -- Step 075's own required acceptance: "malformed JSON rejected."</exception>
    Task<EditProposal> ProposeAsync(
        string instruction,
        SelectionContext context,
        SequenceDocument document,
        CancellationToken ct = default);
}
