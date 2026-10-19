namespace XlightsAI.Studio.Contracts;

/// <summary>
/// Shared canonical identifiers, declared once per PHASE-01 Section 2 ("Shared canonical C#
/// identifiers... Define these once in Studio.Contracts... Do not redefine copies with different
/// namespaces in code."). Only <see cref="ProjectId"/> is consumed starting this step (Step 006);
/// the remaining types are declared now, per the spec's single-declaration rule, so later phases
/// that introduce <c>SongAsset</c>, <c>BeatGrid</c>, <c>SequenceDocument</c>, etc. reference one
/// pinned definition instead of each phase inventing its own.
/// </summary>
public readonly record struct ProjectId(Guid Value);

public readonly record struct SongId(Guid Value);

public readonly record struct ModelId(string Value);

public readonly record struct DocumentRevision(long Value);

/// <summary>100 ns ticks from song origin.</summary>
public readonly record struct TimelineTick(long Value);

/// <summary>A half-open <c>[Start, End)</c> time range expressed in <see cref="TimelineTick"/> ticks.</summary>
public readonly record struct TimeWindow(long StartTicks, long EndTicks);
