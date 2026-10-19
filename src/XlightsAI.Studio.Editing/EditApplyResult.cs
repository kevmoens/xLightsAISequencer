using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>The immutable updated document plus the audit record of the command that produced it. See <see cref="IEditTransactionService.ApplyAsync"/>.</summary>
public sealed record EditApplyResult(SequenceDocument Document, EditTransaction Transaction);
