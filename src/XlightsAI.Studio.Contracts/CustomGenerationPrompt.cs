namespace XlightsAI.Studio.Contracts;

/// <summary>
/// Build-plan Phase 14 ("User-Directed Custom Generation Prompt"): a user's own free-text creative
/// direction for one generation run (colors, effects, layers, intensity, pacing, prop focus -- see
/// architecture docs/AI-CHOREOGRAPHY-BUILD-PLAN-PHASE-14-CUSTOM-PROMPT.md §2a). Declared here in
/// Contracts (not Director, where it is actually consumed) because <c>IWholeSongGenerator</c>
/// (Pipeline) also needs to name this type on its own public surface, and Pipeline cannot reference
/// Director (Director already references Pipeline -- see <c>AiChoreographyWholeSongGenerator</c>'s
/// own doc comment) without a cyclic project reference.
/// </summary>
/// <param name="Text">
/// The raw creative-direction text, capped at <see cref="MaxLength"/> characters -- the same
/// "bound everything that reaches an LLM call" discipline <c>RoleProviderConfig.MaxResponseBytes</c>
/// already established for responses, applied here to a user-authored request body. Enforced both
/// here (construction) and in the UI (live remaining-character count), so a persisted value can
/// never silently exceed the cap even if a future caller skips the UI.
/// </param>
/// <param name="StayCloseToExemplars">
/// Addresses user feedback (2026-10-31): regenerating the same song always retrieved the identical
/// nearest-match corpus exemplars (<see cref="XlightsAI.Studio.Pipeline.ExemplarIndex.FindSimilar"/> is
/// purely deterministic), so a "regenerate" only ever varied color, never which effects/motion got used.
/// <see langword="true"/> (the default, and every pre-existing caller's behavior) keeps exemplar
/// retrieval deterministic -- the closest real corpus match every time. <see langword="false"/> switches
/// to <see cref="XlightsAI.Studio.Pipeline.ExemplarIndex.FindVaried"/>, which weights candidates by
/// similarity but samples randomly rather than always taking the top match, deliberately reaching further
/// from the closest match than before. This necessarily makes the Director prompt's own "stable prefix"
/// non-reproducible across runs by design -- the whole point of unchecking it -- so a provider's prompt
/// cache does not help on a "varied" regeneration the way it does for a repeated "stay close" one.
/// </param>
/// <param name="Style">
/// User feedback (2026-11-01): previously the style was fixed at app-startup composition-root time
/// (<c>Program.cs</c> hardcoded "High-Energy / EDM" for every generation, every song), with no UI to
/// change it. <see langword="null"/> (the default, and every pre-existing caller/persisted file's
/// behavior) keeps that startup-configured default; a non-null value here -- a
/// <c>XlightsAI.Studio.Director.StylePresetCatalog</c> id or display name -- overrides it for this one
/// run. Declared as a plain string (not the <c>StylePreset</c> type itself) for the same cyclic-
/// reference reason <see cref="CustomGenerationPrompt"/>'s own class doc comment already explains:
/// Contracts cannot reference Director.
/// </param>
/// <param name="EnableQualityPass">
/// Build-plan "richer analysis tools" Phase 3 (2026-11-02): <see langword="false"/> (the default, and
/// every pre-existing caller's behavior) keeps the original single-pass "compile once, export" flow.
/// <see langword="true"/> opts this one run into the already-built render-&gt;measure-&gt;critique-&gt;revise
/// loop (<c>XlightsAI.Studio.Director.IterationLoop</c>) after the first compile -- genuinely slower
/// (it re-renders the actual xLights install and re-calls the Choreographer per revision) and only
/// available when an xLights executable/show directory is configured; see
/// <c>ChoreographyGenerationPipeline</c>'s own remarks for the exact unavailability message when it
/// is toggled on without that configured. Never requires a vision-capable Critic role to be configured
/// -- runs numeric-metric-only convergence when one is not, and adds visual critique on top when one is.
/// </param>
public sealed record CustomGenerationPrompt(string Text, bool StayCloseToExemplars = true, string? Style = null, bool EnableQualityPass = false)
{
    public const int MaxLength = 2000;

    public string Text { get; } = Text.Length <= MaxLength
        ? Text
        : throw new ArgumentException($"Custom generation prompt text exceeds the {MaxLength}-character cap (was {Text.Length}).", nameof(Text));

    /// <summary>The default, pre-existing behavior for every caller that never supplies a custom prompt -- see §4's non-regression criterion.</summary>
    public static readonly CustomGenerationPrompt Empty = new(string.Empty);

    public bool IsEmpty => Text.Length == 0;
}
