namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 058's own title, verbatim: "Build phrase relationship/propagation inspector" -- the
/// persisted record of one explicit user link/unlink decision between two phrases, independent of whatever
/// <see cref="MotifClusterer"/> would otherwise automatically propose. A phrase-local, concrete narrowing of
/// PHASE-08 Section 3's proposed <c>MotifLink.UserApproved</c> bit -- here expressed as a full decision
/// (linked or explicitly not-linked) over an unordered phrase pair, since <see cref="MotifGroupOverrideApplier"/>
/// consumes the whole replace-all decision set together with the automatic <see cref="MotifLinkCandidate"/>
/// list to recompute effective group membership, rather than approving/rejecting one already-proposed link
/// in isolation.
/// </summary>
/// <param name="PhraseAId">One of the two phrases this decision concerns. Order is not significant -- a decision for (A, B) and (B, A) are the same decision.</param>
/// <param name="PhraseBId">The other phrase this decision concerns. Always different from <see cref="PhraseAId"/>.</param>
/// <param name="ForceLinked">
/// <see langword="true"/>: the user explicitly declares these two phrases share a motif, regardless of
/// (or in the absence of) any automatic <see cref="MotifLinkCandidate"/> between them -- "link."
/// <see langword="false"/>: the user explicitly declares these two phrases do not share a motif, even if
/// <see cref="MotifClusterer"/> would otherwise propose a link between them -- "unlink."
/// </param>
public sealed record MotifLinkOverride(Guid PhraseAId, Guid PhraseBId, bool ForceLinked);
