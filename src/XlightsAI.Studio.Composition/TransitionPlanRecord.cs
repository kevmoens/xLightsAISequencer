namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 070: one phrase's own persisted <see cref="TransitionPlan"/>. Mirrors
/// <see cref="VisualStoryRecord"/>'s own named-wrapper-plus-version shape, keyed by a caller-defined phrase
/// scope id (a plan of builds/blackouts/transitions/releases/phrase-endings is phrase-scoped, the same scope
/// <see cref="MusicalRoleAssignmentSet"/> already uses -- not whole-song like <see cref="VisualStory"/>).
/// </summary>
/// <param name="PhraseId">The caller-defined phrase/scope identity this plan was planned for.</param>
/// <param name="Plan">The saved, already-validated plan.</param>
/// <param name="Version">Increments every time this exact <see cref="PhraseId"/> is saved again; <c>0</c> on first save.</param>
public sealed record TransitionPlanRecord(Guid PhraseId, TransitionPlan Plan, int Version);
