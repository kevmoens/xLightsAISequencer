namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 068: one named, persisted <see cref="VisualBudget"/> -- the data-contract rule's own
/// "visual budget presets" (PHASE-10.md §4: "Persist user role-to-path mappings, palette IDs, visual budget
/// presets, section visual direction, generated gesture provenance and <c>ChoreographyPlan</c> inputs
/// hash.").
/// </summary>
/// <param name="Name">The caller-chosen, case-sensitive preset name; unique per <see cref="IVisualBudgetPresetStore"/> directory.</param>
/// <param name="Budget">The saved budget values.</param>
/// <param name="Version">Increments every time this exact <see cref="Name"/> is saved again; <c>0</c> on first save.</param>
public sealed record VisualBudgetPreset(string Name, VisualBudget Budget, int Version);
