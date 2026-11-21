namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// One xLights layer-blend mode (<c>T_CHOICE_LayerMethod</c>) whose exact serialized XML value has been
/// confirmed against real, author-created xLights show files -- never guessed. See
/// <see cref="ConfirmedLayerBlendModeCatalog"/> for the full confirmation trail.
/// </summary>
/// <param name="CandidateName">The build plan's own name for this candidate (e.g. <c>"Reveal 1-on-2"</c>).</param>
/// <param name="XmlValue">The exact literal string xLights serializes after <c>T_CHOICE_LayerMethod=</c> (e.g. <c>"1 reveals 2"</c>). This is the only value <see cref="XlightsAI.Studio.Choreography"/> code may emit for this mode.</param>
/// <param name="ConfirmedAtInstalledVersion">True if this exact value was independently observed in a <c>.xsq</c> file whose own <c>&lt;head&gt;&lt;version&gt;</c> matched the verification machine's installed xLights build (2026.17); false if it was only observed in an older-version file (the string has been identical across every version sampled, 2020.47-2026.17, but this flag records the stronger/weaker form of evidence honestly).</param>
public sealed record ConfirmedLayerBlendMode(string CandidateName, string XmlValue, bool ConfirmedAtInstalledVersion);

/// <summary>
/// Phase 30 step 30.0 gate: the xLights layer-blend modes ("Mask", "Unmask", "Reveal 1-on-2",
/// "Reveal 2-on-1", "Average", "Subtractive", "Shadow 1-on-2", "Shadow 2-on-1") a Look may ever request,
/// together with their exact confirmed <c>T_CHOICE_LayerMethod</c> XML value.
///
/// Every value here was confirmed by grepping real, author-created xLights <c>.xsq</c> show files for
/// literal <c>T_CHOICE_LayerMethod=&lt;value&gt;</c> occurrences (not xLights' own <c>effectmetadata</c>,
/// which does not enumerate this control's choice list -- it is a custom widget, confirmed by diffing the
/// installed <c>effectmetadata/shared/Blending.json</c> against this repo's checked-in copy, which are
/// byte-identical). See <c>implementation-evidence/PHASE-30-blend-mode-baseline.md</c> and
/// <c>tests/.../Fixtures/blend-modes/blend-mode-exemplars.xsq</c> for the full evidence trail.
///
/// "Mask" and "Unmask" each round-trip through two real XML spellings depending on which of the two
/// stacked layers is the mask/unmask layer (<c>"1 is Mask"</c>/<c>"2 is Mask"</c>,
/// <c>"1 is Unmask"</c>/<c>"2 is Unmask"</c>); this catalog carries the <c>"2 is ..."</c> spelling for
/// each (the layer-2-relative-to-layer-1 reading, consistent with "Reveal 1-on-2"/"Reveal 2-on-1" already
/// being directional) since a later planner only ever needs one canonical direction per candidate name --
/// both spellings are equally confirmed, this is a selection, not a gap.
/// </summary>
public static class ConfirmedLayerBlendModeCatalog
{
    public static readonly IReadOnlyList<ConfirmedLayerBlendMode> Modes =
    [
        new("Mask", "2 is Mask", ConfirmedAtInstalledVersion: true),
        new("Unmask", "2 is Unmask", ConfirmedAtInstalledVersion: false),
        new("Reveal 1-on-2", "1 reveals 2", ConfirmedAtInstalledVersion: true),
        new("Reveal 2-on-1", "2 reveals 1", ConfirmedAtInstalledVersion: true),
        new("Average", "Average", ConfirmedAtInstalledVersion: false),
        new("Subtractive", "Subtractive", ConfirmedAtInstalledVersion: false),
        new("Shadow 1-on-2", "Shadow 1 on 2", ConfirmedAtInstalledVersion: false),
        new("Shadow 2-on-1", "Shadow 2 on 1", ConfirmedAtInstalledVersion: false),
    ];

    /// <summary>The default/no-op blend mode the pipeline already uses everywhere today -- never itself "chosen" by a planner, but needed so diversity math (fraction of non-default pairs) has a baseline to compare against.</summary>
    public const string DefaultXmlValue = "Normal";

    public static ConfirmedLayerBlendMode? Find(string candidateName) =>
        Modes.FirstOrDefault(m => string.Equals(m.CandidateName, candidateName, StringComparison.OrdinalIgnoreCase));

    /// <summary>True only for an XML value this catalog has confirmed (or the unconfirmed-but-harmless default). Any other string -- including a plausible-looking one a model might hallucinate -- must never be emitted.</summary>
    public static bool IsConfirmedXmlValue(string xmlValue) =>
        string.Equals(xmlValue, DefaultXmlValue, StringComparison.Ordinal)
        || Modes.Any(m => string.Equals(m.XmlValue, xmlValue, StringComparison.Ordinal));
}
