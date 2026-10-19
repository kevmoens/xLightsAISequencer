using XlightsAI.Studio.Editing;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 077 ("Implement explicit motif propagation and edit-scope intersection validation"):
/// one other occurrence of the same motif/gesture a base <see cref="EditProposal"/> was built for,
/// expressed purely in this project's own already-accepted vocabulary (<see cref="EditScope"/>) -- the
/// same "disclosed narrowing" convention <see cref="SelectionContext"/>'s own remarks already established
/// for this project: a caller (a future SCREEN 14 propagation inspector) resolves which other phrase/time
/// window/model set shares this motif using <c>XlightsAI.Studio.Structure.MotifGroupCandidate</c>, but
/// this project never references that aggregate directly -- it only ever receives the already-resolved
/// time/model boundary that one other occurrence is permitted to reach.
/// </summary>
/// <param name="OccurrenceId">Stable identity for this one other occurrence, distinct from <see cref="EditProposal.Id"/> and from every other <see cref="MotifOccurrenceTarget.OccurrenceId"/> in the same candidate set.</param>
/// <param name="Scope">The exact time window and model set this one other occurrence is permitted to reach -- never widened by <see cref="MotifPropagationExpander"/>, only ever intersected with/checked against by <see cref="MotifPropagationValidator"/>.</param>
public sealed record MotifOccurrenceTarget(Guid OccurrenceId, EditScope Scope);
