namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 069: one song's own persisted <see cref="VisualStory"/> -- the data-contract rule's own
/// "section visual direction" persistence (PHASE-10.md §4: "Persist user role-to-path mappings, palette IDs,
/// visual budget presets, section visual direction, generated gesture provenance and <c>ChoreographyPlan</c>
/// inputs hash."). Mirrors <see cref="VisualBudgetPreset"/>'s own named-wrapper-plus-version shape, keyed by
/// a caller-defined song scope id instead of a preset name -- a <see cref="VisualStory"/> is inherently
/// whole-song (it normalizes every section's intensity relative to every other section in the same song), so
/// it cannot be keyed per-phrase the way <see cref="MusicalRoleAssignmentSet"/> is.
/// </summary>
/// <param name="SongId">The caller-defined song identity this story was planned for.</param>
/// <param name="Story">The saved, already-validated story.</param>
/// <param name="Version">Increments every time this exact <see cref="SongId"/> is saved again; <c>0</c> on first save.</param>
public sealed record VisualStoryRecord(Guid SongId, VisualStory Story, int Version);
