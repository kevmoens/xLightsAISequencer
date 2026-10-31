namespace XlightsAI.Studio.Director;

/// <summary>
/// Architecture §13: "<c>llm/</c> full call log with costs." One real chat-completion attempt's full,
/// inspectable record -- every <see cref="ShowBibleGenerator"/>/<see cref="SectionScriptGenerator"/>
/// attempt appends one of these via <see cref="JsonAiCallLogStore"/>, successful or not, so "when a show
/// comes out wrong you need to see what the Director actually said" (architecture §14) is answerable from
/// disk alone.
/// </summary>
/// <param name="SongId">Which song this call was made for.</param>
/// <param name="Role">Which of the three roles (architecture §14) made this call.</param>
/// <param name="Model">The provider's own model identifier that served this call.</param>
/// <param name="TimestampUtc">When this call completed (or failed).</param>
/// <param name="Usage">Whatever token usage the provider reported, or an all-null <see cref="AiUsage"/> for a call that never reached the provider.</param>
/// <param name="EstimatedCostUsd"><see cref="AiCostEstimator"/>'s own estimate, or <see langword="null"/> when the model is unrecognized -- never a fabricated number for an unknown model (same "null, never zero" discipline as <see cref="AiUsage.CacheReadInputTokens"/>).</param>
/// <param name="Note">A short free-text note (e.g. "attempt 2/3, validation retry", "transport failure") -- context a raw usage/cost number alone wouldn't carry.</param>
/// <param name="Diagnostics">
/// Build-plan Phase 11 §2c: the real request URI/body actually sent for this call (bounded like response
/// size already is), plus any endpoint sanity-check warning -- <see langword="null"/> only for a call whose
/// throwing/returning site never attached one (e.g. an older log entry read back, or a batch item with no
/// single per-call body of its own). This is the single field that would have made this phase's motivating
/// incident diagnosable from the call log alone, in seconds, instead of requiring source-level reverse-engineering.
/// </param>
public sealed record AiCallLogEntry(
    Guid SongId,
    AiRole Role,
    string Model,
    DateTimeOffset TimestampUtc,
    AiUsage Usage,
    decimal? EstimatedCostUsd,
    string? Note = null,
    RequestDiagnostics? Diagnostics = null);
