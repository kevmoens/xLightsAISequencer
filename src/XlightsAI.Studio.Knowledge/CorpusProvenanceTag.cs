namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// PHASE-2b (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md Phase 2 §2b.6; architecture §8b licensing section):
/// where one corpus sequence likely came from, so a future decision to ship generic corpus-wide
/// statistics/rules to other customers can filter by license. Set once per <c>CorpusEntry</c> at
/// manifest build time from filename correlation against <c>orig_zip/</c> and known reference shows —
/// never re-derived or guessed at mining time.
/// </summary>
public enum CorpusProvenanceTag
{
    /// <summary>The user's own sequence: no matching vendor/community zip was found for it, and nothing in its <c>.xsq</c> &lt;head&gt; contradicts that. Safe to mine for both local use and, if the licensing story changes, abstracted statistics.</summary>
    UserAuthored,

    /// <summary>A root-level sequence that matches a paid or free vendor/community download preserved in <c>orig_zip/</c> (or an equivalent vendor-named project subfolder) by filename correlation. Local mining only — architecture §8b: "do not train on or ship them."</summary>
    LikelyVendorOrCommunityDownload,

    /// <summary>The Johnson Family "Dubstep Christmas Light Show" sequence discussed throughout the architecture doc and <c>C:\xLights\CLAUDE.md</c> — an explicit external comparison reference, not part of this user's own corpus. Excluded from every aggregate PatternLibrary/CraftRules computation; may still be inspected as a single named exemplar.</summary>
    ExternalReference,
}
