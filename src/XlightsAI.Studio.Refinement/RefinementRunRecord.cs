namespace XlightsAI.Studio.Refinement;

/// <summary>
/// The full persisted shape of one <see cref="LoopRun"/> file: the run itself plus every attempt
/// recorded against it so far, keyed by <see cref="CandidateAttempt.Attempt"/> ordinal so that
/// re-saving the same attempt number overwrites in place (idempotent resume) rather than appending a
/// duplicate entry -- this step's own required acceptance, verbatim: "Interrupted run resumes
/// idempotently."
/// </summary>
public sealed record RefinementRunRecord(LoopRun Run, IReadOnlyDictionary<int, CandidateAttempt> Attempts);
