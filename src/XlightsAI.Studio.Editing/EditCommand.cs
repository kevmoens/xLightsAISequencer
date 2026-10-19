using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Section 3 proposed contract: the base of every revision-aware mutation request against a
/// <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/>. <see cref="ExpectedRevision"/> is the
/// optimistic-concurrency check (Section 4: "Stale revision rejects rather than silently merging
/// overlapping edits") — <see cref="IEditTransactionService.ApplyAsync"/> never applies a command
/// whose <see cref="ExpectedRevision"/> does not match the document's current revision, no matter how
/// well-formed the rest of the command is.
/// </summary>
public abstract record EditCommand(DocumentRevision ExpectedRevision, EditScope Scope);
