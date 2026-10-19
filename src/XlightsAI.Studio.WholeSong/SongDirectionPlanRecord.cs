using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// One song's own persisted <see cref="SongDirectionPlan"/>. Mirrors
/// <see cref="XlightsAI.Studio.Composition.VisualStoryRecord"/>'s own named-wrapper-plus-version shape,
/// keyed by song id -- a song-level direction plan is inherently whole-song, the same reasoning
/// <c>VisualStoryRecord</c>'s own doc comment already gives.
/// </summary>
/// <param name="SongId">The caller-defined song identity this plan was created for.</param>
/// <param name="Plan">The saved, already-validated plan.</param>
/// <param name="Version">Increments every time this exact <see cref="SongId"/> is saved again; <c>0</c> on first save.</param>
public sealed record SongDirectionPlanRecord(SongId SongId, SongDirectionPlan Plan, int Version);
