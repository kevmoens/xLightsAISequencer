namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Section 3's own proposed signature, kept exactly as specified. Runs a set of non-destructive
/// checks a first-run/launch flow uses to decide whether to proceed, degrade a feature, or show a clear
/// diagnostic -- never to crash. Realizes this step's own required failure/edge case verbatim: "Missing or
/// incompatible model, cancellation and offline mode must yield a recoverable state."
/// </summary>
public interface IStartupHealthChecks
{
    /// <summary>
    /// Never throws for a reportable health problem (missing/incompatible asset, unsupported OS/arch) --
    /// every such problem is returned as a human-readable entry in the result list. An empty list means every
    /// check passed. Only a programming error (null argument) or genuine cancellation throws.
    /// </summary>
    Task<IReadOnlyList<string>> CheckAsync(CancellationToken ct);
}
