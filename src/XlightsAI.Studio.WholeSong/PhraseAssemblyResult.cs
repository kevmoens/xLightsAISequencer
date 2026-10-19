using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 091's own full assembly outcome: the resulting <see cref="SequenceDocument"/> (the
/// caller's own base document plus every admitted phrase effect, with every pre-existing effect carried
/// forward byte-for-byte unchanged -- the same convention <c>ChoreographyCompilationResult</c> already
/// established), every resolved seam, and every non-fatal, disclosed observation across the whole
/// assembly.
/// </summary>
/// <param name="Document">A new document revision carrying every admitted phrase effect (with any strategy-driven clip applied), or the caller's own unchanged <paramref name="Document"/> instance if every phrase contributed zero effects after seam resolution.</param>
/// <param name="Seams">Every seam's own resolved outcome, in phrase order.</param>
/// <param name="Warnings">The union of every <see cref="PhraseSeamPlan.Warnings"/> entry across <see cref="Seams"/>, in the same order -- a single, inspectable flat list for a caller that does not need per-seam detail.</param>
public sealed record PhraseAssemblyResult(
    SequenceDocument Document,
    IReadOnlyList<PhraseSeamPlan> Seams,
    IReadOnlyList<string> Warnings);
