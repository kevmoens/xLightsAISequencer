using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

/// <summary>One named color's functional role within a <see cref="ShowBiblePalette"/> (architecture §5: base/key/accent).</summary>
public static class PaletteRole
{
    public const string Base = "base";
    public const string Key = "key";
    public const string Accent = "accent";
}

/// <summary>One color within a palette: its own hex value plus the functional role it plays.</summary>
public sealed record ShowBiblePaletteColor(string HexColor, string Role);

/// <summary>
/// Architecture §5: "3-5 named palettes, each a real xLights ColorPalette string... assign functional
/// roles -- base (environment), key/contrast (keeps the subject readable), accent (transitions, rhythm,
/// focal moments) -- because converting 'pick colours' into 'assign functions' is far more tractable
/// than open-ended colour choice." <see cref="Id"/> is what <see cref="Look.PaletteRef"/> resolves
/// against.
/// </summary>
public sealed record ShowBiblePalette(string Id, string Name, IReadOnlyList<ShowBiblePaletteColor> Colors);

/// <summary>Known musical-role targets a prop can be assigned (architecture §5: "bass -> large/low/wide props; melody -> the tall or focal prop; highs -> small/high/fast props").</summary>
public static class MusicalRole
{
    public const string Bass = "bass";
    public const string Melody = "melody";
    public const string Highs = "highs";
}

/// <summary>One prop/group's assigned musical role -- architecture §5's own research-backed correspondence, never a pitch-class-to-hue mapping (explicitly rejected in the same section).</summary>
public sealed record PropRoleAssignment(string Target, string MusicalRole);

/// <summary>Architecture §10's structural arc, per section kind: an energy ceiling never exceeded, plus a target rate of change and hue-family count.</summary>
public sealed record EnergyArcEntry(SectionKind Kind, double EnergyCeiling, double TargetChangeRatePerSecond, int TargetHueFamilyCount);

/// <summary>Architecture §5: "the 2-4 bars that must be unmistakable, each with a reserved Look and an explicit statement of what distinguishes it from every other peak."</summary>
public sealed record PeakMoment(string LookId, string Distinction);

/// <summary>Architecture §5: "2-4 recognizable 1-2 bar movements, established early, varied through, restated at full scale at the end."</summary>
public sealed record SignatureGesture(string Name, string Description);

/// <summary>Architecture §5: "which builds hold flat and explode only in their final bars, and what the reserved 'ace' is for the final chorus."</summary>
public sealed record WithholdingPlan(IReadOnlyList<string> HeldBackLookIds, string FinalChorusAce);

/// <summary>
/// Build-plan Phase 5 task 2: the flat, shallow, reviewable Director output (architecture §5, verbatim
/// section list) -- "a human can read the whole thing." <see cref="Looks"/> reuses Phase 4's own
/// <see cref="LookLibrary"/> directly (never a parallel type) since a Look's own shape was already
/// designed with exactly this document in mind (<c>Look.Intent</c>'s own doc comment: "what a human
/// reviewing the Show Bible reads to judge it"). This is exactly the shape the model's own JSON output
/// deserializes into -- deliberately carries no document identity (no <c>Id</c>/<c>SongId</c>/generation
/// timestamp): "least privilege," the same discipline <c>AiEdits.AiPlannerRequestPayload</c> already
/// applies to outbound requests, applied here to the inbound shape instead -- a model has no business
/// inventing a GUID or a timestamp, so it is never asked to.
/// </summary>
public sealed record ShowBibleContent(
    IReadOnlyList<ShowBiblePalette> Palettes,
    IReadOnlyList<PropRoleAssignment> PropRoles,
    LookLibrary Looks,
    IReadOnlyList<EnergyArcEntry> EnergyArc,
    IReadOnlyList<PeakMoment> PeakMoments,
    IReadOnlyList<SignatureGesture> SignatureGestures,
    WithholdingPlan Withholding);

/// <summary>The persisted, identity-bearing wrapper around one generated <see cref="ShowBibleContent"/> -- <see cref="JsonShowBibleStore"/>'s own unit of storage.</summary>
public sealed record ShowBible(Guid Id, Guid SongId, ShowBibleContent Content, DateTimeOffset GeneratedAtUtc);
