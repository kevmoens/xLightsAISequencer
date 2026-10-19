using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// One song's own persisted <see cref="MotifVariationPlan"/>. Mirrors <see cref="SongDirectionPlanRecord"/>'s
/// own named-wrapper-plus-version shape, keyed by song id.
/// </summary>
/// <param name="SongId">The caller-defined song identity this plan was created for.</param>
/// <param name="Plan">The saved, already-validated plan.</param>
/// <param name="Version">Increments every time this exact <see cref="SongId"/> is saved again; <c>0</c> on first save.</param>
public sealed record MotifVariationPlanRecord(SongId SongId, MotifVariationPlan Plan, int Version);
