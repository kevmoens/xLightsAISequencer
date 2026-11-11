namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Build-plan Phase 18 §6 (Phase E): one effect/resolution-tier's own shippable, reusable visual-character
/// entry -- the committed, curated result of the offline Phase A-D characterization sweep, never something
/// computed or regenerated during ordinary generation. Lives in <c>Knowledge</c> (not <c>Director</c>,
/// where the raw <c>EffectVisualCharacterizationManifest</c> this is derived from lives) so it can be
/// loaded here the same embedded-resource way as <see cref="MultiLayerTemplateCatalog"/>, without this
/// project needing a reference to <c>Choreography</c>/<c>Director</c> -- <see cref="ResolutionTier"/> is
/// therefore a plain string (e.g. <c>"Low"</c>/<c>"High"</c>), not the <c>Choreography</c>-owned
/// resolution-tier enum.
/// </summary>
/// <param name="EffectName">A real xLights effect name.</param>
/// <param name="ResolutionTier"><c>"Low"</c> or <c>"High"</c> -- which representative fixture this entry was characterized against.</param>
/// <param name="Description">The AI-observed description from the real Phase D sweep (a disclosed heuristic, not ground truth) -- always present, even when <see cref="DocumentedBehavior"/> also is.</param>
/// <param name="GeneratedAt">When the originating Phase D characterization run produced this entry.</param>
/// <param name="DocumentedBehavior">
/// Optional: the effect's real, verbatim manual.xlights.org description, for effects whose isolated
/// synthetic-fixture render came back dark/uninformative for a known, disclosed reason (needs external
/// input this pipeline's fixture never supplies, is audio/behavior-driven in a way the synthetic track
/// didn't trigger, or is a genuinely unresolved rendering gap) -- never fabricated, only ever a real quote
/// with <see cref="DocumentationSource"/> citing where it came from.
/// </param>
/// <param name="DocumentationSource">The manual.xlights.org URL <see cref="DocumentedBehavior"/> was quoted from. Null exactly when <see cref="DocumentedBehavior"/> is null.</param>
/// <param name="MeanBrightness">
/// Build-plan Phase 21 §2a: this effect/tier's own real, measured mean whole-frame luminance (0-1), averaged
/// over every pixel in its fixture's canvas -- lit or not -- across its entire real Phase B render. Never
/// estimated/guessed; computed directly from the same <c>PreviewFrame</c>s <see cref="Description"/> was
/// written from. Appended after the existing, optional <see cref="DocumentedBehavior"/>/
/// <see cref="DocumentationSource"/> fields (rather than inserted alongside <see cref="Description"/>) so
/// every pre-Phase-21 positional construction of this record keeps compiling unchanged -- additive, not a
/// breaking rename, per the build plan's own explicit requirement.
/// </param>
/// <param name="PeakBrightness">This effect/tier's single brightest frame's own whole-frame average (0-1) -- the ceiling <see cref="MeanBrightness"/> ever reaches across the render, not a per-pixel maximum.</param>
/// <param name="BrightnessTier">The coarse <see cref="Knowledge.BrightnessTier"/> bucket <see cref="MeanBrightness"/> falls into, using two real cut points derived from this catalog's own real <see cref="MeanBrightness"/> distribution (build-plan Phase 21 §2b, computed by <c>EffectVisualCharacterCatalogBuilder</c> in the <c>Director</c> project) -- never an arbitrary absolute threshold.</param>
public sealed record EffectVisualCharacter(
    string EffectName,
    string ResolutionTier,
    string Description,
    DateTimeOffset GeneratedAt,
    string? DocumentedBehavior = null,
    string? DocumentationSource = null,
    double MeanBrightness = 0.0,
    double PeakBrightness = 0.0,
    BrightnessTier BrightnessTier = Knowledge.BrightnessTier.Off);

/// <summary>
/// Build-plan Phase 21 §2b: the coarse, real tier <see cref="EffectVisualCharacter.MeanBrightness"/> is
/// bucketed into. Three tiers, never more -- "Off" (effectively dark), "Dim" (sparse/low whole-canvas
/// coverage -- including a scattered effect whose individual lit pixels are intense but whose own canvas is
/// mostly dark at any instant, e.g. Fireworks/Snowflakes/Snowstorm/Meteors), and "Bright" (dense/high
/// coverage).
/// </summary>
public enum BrightnessTier
{
    Off,
    Dim,
    Bright,
}

/// <summary>
/// The full shippable catalog -- <see cref="EffectVisualCharacterCatalogLoader"/>'s own return shape, and
/// what <c>DirectorKnowledgeContext</c> carries into the Director's stable prompt prefix. Built offline by
/// <c>tools/EffectCatalogBuilder</c> from a real Phase D <c>EffectVisualCharacterizationManifest</c> sweep,
/// then committed to the repo as this project's own embedded seed data -- never generated or loaded from
/// <c>%AppData%</c> at runtime, the same "core, ships with the app, changes rarely" discipline as
/// <see cref="MultiLayerTemplateCatalog"/>.
/// </summary>
public sealed record EffectVisualCharacterCatalog(DateTimeOffset GeneratedAtUtc, IReadOnlyList<EffectVisualCharacter> Characters)
{
    /// <summary>The best available entry for <paramref name="effectName"/> -- prefers the High-resolution-tier entry (richer canvas, generally more representative) and falls back to Low when only that tier was characterized. Null when this effect has no catalog entry at all (excluded hardware effect, or never characterized).</summary>
    public EffectVisualCharacter? Find(string effectName) =>
        Characters.FirstOrDefault(c => string.Equals(c.EffectName, effectName, StringComparison.Ordinal) && c.ResolutionTier == "High")
        ?? Characters.FirstOrDefault(c => string.Equals(c.EffectName, effectName, StringComparison.Ordinal));
}
