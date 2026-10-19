namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11.md Section 3's own proposed signature, implemented verbatim: the outcome of
/// <see cref="IEditProposalValidator.Validate"/> -- never thrown, always returned, so a caller (the
/// dry-run preview, or a future review UI) can show every reason a proposal would be rejected at once
/// ("Clean machine and unsupported runtime must produce user-readable diagnostics" -- Step 076's own
/// required failure/edge case) rather than surfacing only the first gate that happened to fail.
/// </summary>
/// <param name="Allowed"><see langword="true"/> only when <see cref="Errors"/> is empty.</param>
/// <param name="Errors">Every independent reason this proposal cannot be applied as-is; empty when <see cref="Allowed"/>.</param>
/// <param name="Warnings">Non-blocking observations (e.g. an operation whose range/targets currently overlap no effect) -- never prevents approval on their own.</param>
public sealed record ProposalValidation(
    bool Allowed,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
