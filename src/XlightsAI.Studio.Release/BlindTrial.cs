namespace XlightsAI.Studio.Release;

/// <summary>
/// Phase-local contract (not in the Section 3 proposed signature list; disclosed, mirrors Step 098's
/// own <c>ICompatibilityFixtureCatalog</c> precedent of isolating one genuinely new concern). One blind
/// A/B review session: <see cref="PresentationOrder"/> carries the same candidate ids
/// <see cref="IBlindTrialOrderingService.CreateTrial"/> was given, permuted into the real randomized
/// order a human reviewer should actually be shown them in — Step 099's own implementation guidance,
/// verbatim: "Randomize candidate ordering for human A/B reviews."
/// </summary>
/// <param name="AnonymousTrialId">Fresh, unique identity for this one review session — links every <see cref="AestheticRating"/> recorded within it.</param>
/// <param name="PresentationOrder">The candidate ids in the order a reviewer should actually see them; always a permutation of the ids the trial was created from (same set, same count, no candidate added or dropped).</param>
public sealed record BlindTrial(string AnonymousTrialId, IReadOnlyList<string> PresentationOrder);
