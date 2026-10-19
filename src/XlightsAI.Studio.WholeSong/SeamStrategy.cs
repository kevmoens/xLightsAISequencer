namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 091's explicit seam policy between two consecutive, already-accepted phrases --
/// implementation guidance, verbatim: "explicit seam policies (trim, hold, release, bridge)." A caller
/// chooses one strategy per adjacent phrase pair via <see cref="PhraseSeamRequest"/>; nothing here is
/// auto-derived from the seam shape, matching the repository's standing "AI/system proposes, human
/// decides" convention (PHASE-14.md Section 2: "<c>ChoreographyPlan</c> is an intent proposal, not a
/// destructive replacement").
/// </summary>
public enum SeamStrategy
{
    /// <summary>An effect bleeding past its own phrase's boundary into the next phrase's territory is clipped exactly at the boundary.</summary>
    Trim,

    /// <summary>A silent gap between two phrases is closed by extending the trailing phrase's own boundary-touching effect(s) forward to meet the next phrase.</summary>
    Hold,

    /// <summary>Same clipping mechanics as <see cref="Trim"/> -- recorded under a distinct, disclosed label for provenance/audit only; this never invents a new xLights effect parameter (e.g. a synthesized fade), per PHASE-14.md Section 2's "do not invent xLights effect parameters" rule.</summary>
    Release,

    /// <summary>A silent gap between two phrases is left deliberately unfilled and disclosed via a warning, pending a later transition-gesture step (PHASE-10's own <c>ITransitionRecipePlanner</c>/PHASE-14 Step 093) rather than inventing a bridge effect here.</summary>
    Bridge,
}
