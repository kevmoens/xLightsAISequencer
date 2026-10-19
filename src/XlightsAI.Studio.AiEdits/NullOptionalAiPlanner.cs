using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// The <see cref="IOptionalAiPlanner"/> every disabled provider configuration resolves to (see
/// <see cref="OptionalAiPlannerFactory.Create"/>). Required acceptance, verbatim: "Provider off means no
/// outbound calls" -- this type owns no <see cref="HttpClient"/>, no endpoint and no credential; it is
/// architecturally incapable of making a network request, not merely configured not to.
/// </summary>
public sealed class NullOptionalAiPlanner : IOptionalAiPlanner
{
    public static NullOptionalAiPlanner Instance { get; } = new();

    private NullOptionalAiPlanner()
    {
    }

    public Task<EditProposal> ProposeAsync(
        string instruction,
        SelectionContext context,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);

        ct.ThrowIfCancellationRequested();

        throw new AiProviderDisabledException();
    }
}
