namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// A caller's own explicit, unvalidated seam-policy choice for one consecutive phrase pair --
/// implementation guidance, verbatim: "explicit seam policies (trim, hold, release, bridge)." Exactly one
/// request must exist per consecutive pair in the phrase order <see cref="PhraseSeamAssembler.Assemble"/>
/// receives; <see cref="PhraseSeamAssembler"/> rejects (as a malformed proposal) a set that is missing a
/// pair, duplicates a pair, or names a pair out of the actual phrase order.
/// </summary>
/// <param name="FromPhraseId">The earlier phrase's <see cref="PhraseAssemblyCandidate.PhraseId"/>.</param>
/// <param name="ToPhraseId">The immediately-following phrase's <see cref="PhraseAssemblyCandidate.PhraseId"/>.</param>
/// <param name="Strategy">The seam policy to apply at this boundary.</param>
public sealed record PhraseSeamRequest(Guid FromPhraseId, Guid ToPhraseId, SeamStrategy Strategy);
