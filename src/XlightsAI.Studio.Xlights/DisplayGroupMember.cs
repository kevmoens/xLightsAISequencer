namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Step 011: one raw, as-written entry of a <c>&lt;modelGroup&gt;</c>'s <c>models</c>
/// attribute, together with its resolved <see cref="DisplayGroupMemberKind"/>. <see cref="RawName"/>
/// is preserved verbatim — never rewritten/normalized — so a broken reference's exact original text
/// is still inspectable.
/// </summary>
public sealed record DisplayGroupMember(string RawName, DisplayGroupMemberKind Kind);
