namespace XlightsAI.Studio.Choreography;

/// <summary>
/// One violation of a build-plan Phase 4 task 4 ("compositional legality") rule -- never thrown, always
/// collected, so a caller sees every problem in one pass rather than fixing them one exception at a time.
/// </summary>
/// <param name="AffectedModelId">
/// User feedback (2026-10-31): the one real model this violation is about, when the rule is about a
/// specific model rather than the show as a whole (e.g. <c>"every-model-used-at-least-once"</c>) -- lets a
/// downstream consumer (<see cref="XlightsAI.Studio.Director.RevisionDirectiveDeriver"/>) act on exactly
/// which model to target without parsing <see cref="Message"/>'s free text. <see langword="null"/> for
/// every rule that is not about one specific model, and for every violation produced before this field
/// existed.
/// </param>
public sealed record LegalityViolation(string Rule, string Message, string? AffectedModelId = null);
