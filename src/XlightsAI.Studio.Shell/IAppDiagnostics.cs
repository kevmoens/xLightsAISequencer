namespace XlightsAI.Studio.Shell;

/// <summary>
/// Structured, correlation-id'd diagnostics sink. The <c>RecordFailure(string, Exception)</c>
/// signature matches PHASE-01's proposed <c>IAppDiagnostics</c> contract (Section 3); the rest
/// of this interface is this step's own extension to make the diagnostics page and crash
/// handling observable and testable.
/// </summary>
public interface IAppDiagnostics
{
    /// <summary>Most recent events (bounded, newest last), including any recovered from a prior run's persisted log.</summary>
    IReadOnlyList<DiagnosticEvent> RecentEvents { get; }

    /// <summary>Raised whenever a new event is recorded.</summary>
    event EventHandler? Changed;

    /// <summary>Records a structured event. Never throws, even if the persisted sink is unavailable.</summary>
    void RecordEvent(DiagnosticEventLevel level, string code, string message);

    /// <summary>Records a failure at <see cref="DiagnosticEventLevel.Error"/>. Never throws.</summary>
    void RecordFailure(string code, Exception error);
}
