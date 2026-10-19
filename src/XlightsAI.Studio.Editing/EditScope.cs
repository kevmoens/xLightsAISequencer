using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Section 3 proposed contract, first consumed by Step 019 ("Drag effect starts..."): the
/// exact time window and target model set a single <see cref="EditCommand"/> is permitted to touch.
/// Half-open <see cref="Time"/> (PHASE-01 Section 2's canonical rule), never a row index. An empty
/// <see cref="Models"/> set means "no model is in scope" (reject everything), never "every model" —
/// callers construct the exact permitted set explicitly; this type never defaults to unrestricted.
/// <see cref="EffectIds"/>, when supplied, additionally restricts the scope to exactly those effect
/// identities (Section 3: never mixing selected row indices with identity). <see cref="RespectLocks"/>
/// defaults to <see langword="true"/> — a caller must opt in, explicitly, to bypass lock enforcement
/// (no production call site does this yet; it exists for the same reason
/// <c>XsqExportPolicy.OverwriteWithBackup</c> exists — an explicit, auditable exception, never a
/// silent default).
/// </summary>
public sealed record EditScope(
    TimeWindow Time,
    IReadOnlySet<ModelId> Models,
    IReadOnlySet<Guid>? EffectIds = null,
    bool RespectLocks = true);
