using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 091's own realization of PHASE-14.md Section 3's proposed
/// <c>TransitionPlan(Guid FromPhraseId, Guid ToPhraseId, TimeWindow Window, string Strategy, IReadOnlyList&lt;string&gt; Warnings)</c>
/// record, deliberately renamed from <c>TransitionPlan</c> -- <c>XlightsAI.Studio.Composition.TransitionPlan</c>
/// (PHASE-10 Step 070) already exists with a completely different shape
/// (<c>Guid VersionId, IReadOnlyList&lt;TransitionGesture&gt; Gestures</c>); reusing the same name here
/// would collide under this project's own <c>using XlightsAI.Studio.Composition;</c>. <see cref="Strategy"/>
/// is the strongly-typed <see cref="WholeSong.SeamStrategy"/> enum rather than the spec's proposed free-form
/// <c>string</c>, consistent with this repository's own "never an invented free-form value when a closed
/// set exists" convention.
/// </summary>
/// <param name="FromPhraseId">The earlier phrase's own identity.</param>
/// <param name="ToPhraseId">The immediately-following phrase's own identity.</param>
/// <param name="Window">The seam's own half-open tick range: <c>[FromPhrase.Window.EndTicks, ToPhrase.Window.StartTicks)</c>. Zero-length (<c>StartTicks == EndTicks</c>) when the two phrases are already perfectly adjacent.</param>
/// <param name="Strategy">The seam policy that was actually applied.</param>
/// <param name="Warnings">Non-fatal, disclosed observations about this one seam -- e.g. a no-op policy request, a remaining unbridged gap, or an effect clipped/dropped to resolve an overlap.</param>
public sealed record PhraseSeamPlan(
    Guid FromPhraseId,
    Guid ToPhraseId,
    TimeWindow Window,
    SeamStrategy Strategy,
    IReadOnlyList<string> Warnings);
