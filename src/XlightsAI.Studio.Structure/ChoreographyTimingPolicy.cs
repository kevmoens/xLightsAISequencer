namespace XlightsAI.Studio.Structure;

/// <summary>
/// How a <see cref="ChoreographyTemplateEffect"/>'s own relative timing is mapped onto a target phrase
/// whose duration may differ from the source phrase's own duration (Section 12: inheritance mask
/// <c>timing: retime</c>). Declared per <see cref="ChoreographyInheritanceMask"/>, not per effect -- every
/// inherited effect in one template uses the same policy.
/// </summary>
public enum ChoreographyTimingPolicy
{
    /// <summary>Reuse the exact same tick offsets from the target phrase's own start -- only valid when the resulting window still fits inside the target phrase.</summary>
    Copy,

    /// <summary>Scale the source-relative offsets by the ratio of target-phrase duration to source-phrase duration -- "re-time the destination phrase" (Section 3, verbatim).</summary>
    Retime,
}
