namespace XlightsAI.Studio.Shell;

/// <summary>
/// One structured diagnostic record. <see cref="CorrelationId"/> lets a single failure be traced
/// across the in-memory log, the persisted log file and (in later phases) a bug report, without
/// relying on timestamp matching. <see cref="Message"/> is expected to already be redacted (see
/// <see cref="AppDiagnostics"/>) before it reaches this record.
/// </summary>
public sealed record DiagnosticEvent(
    DateTimeOffset TimestampUtc,
    string CorrelationId,
    DiagnosticEventLevel Level,
    string Code,
    string Message);
