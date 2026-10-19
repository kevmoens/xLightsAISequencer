namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only projection of the currently-selected <see cref="XlightsAI.Studio.Xlights.EffectInstance"/>
/// — PHASE-03 Step 018's own "inspector" half. Built fresh from the real document on every selection
/// change (never a cached/stale copy); <see langword="null"/> exactly when nothing is selected or the
/// selected id no longer exists in the current document (e.g. after an undo/reload) — the same
/// explicit "missing" handling every other screen in this project uses, never a dangling/blank
/// inspector silently pointing at nothing.
/// </summary>
public sealed record EffectInspectorView(
    Guid EffectId,
    string TargetModelName,
    int Layer,
    long StartTicks,
    long EndTicks,
    string RecipeId,
    string RecipeVersion,
    bool IsLocked,
    IReadOnlyList<KeyValuePair<string, string>> Parameters);
