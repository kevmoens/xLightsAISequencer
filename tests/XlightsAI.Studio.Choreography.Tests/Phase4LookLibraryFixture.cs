using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>
/// Build-plan Phase 4 acceptance criterion: "Feed the compiler a hand-authored Look library (3-5 Looks)
/// against a real song + layout." Four hand-authored Looks, each targeting real group/model names present
/// in <c>C:\xLights\xlights_rgbeffects.xml</c> (Outline, MiniTrees, Matrixes, Tree, Arches -- the exact
/// names <c>EXPORT_AUDIT.md</c>'s own fix list already uses), each exercising a different part of
/// architecture §4-§7: Mode A timing-track binding, Mode A value-curve binding, declared transitions
/// (hard-cut vs. fade), propDelay stagger, and declared negative space.
/// </summary>
public static class Phase4LookLibraryFixture
{
    public static LookLibrary Build()
    {
        var goldenHold = new Look(
            Id: "golden-hold",
            Name: "Golden Hold",
            Intent: "Warm full-house steady state for vocal verses. Many props, one colour.",
            EnergyCeiling: 0.45,
            Motion: "hold",
            PaletteRef: "warm-gold",
            Subject: "group:Outline",
            Layers:
            [
                new LookLayer(1, "group:MiniTrees", "Twinkle", LookLayer.RoleSecondary, new Dictionary<string, string>(), "Normal", "Bars", 0.25),
                new LookLayer(2, "group:Matrixes", "Plasma", LookLayer.RoleSupport, new Dictionary<string, string>(), "Normal", "Bars", 0.85),
                new LookLayer(
                    3, "group:Outline", "Color Wash", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", null, 0.9,
                    Curves: new Dictionary<string, LookCurveBinding> { ["C_SLIDER_Brightness"] = new("Timing Track Fade Proportional", "Beats") }),
            ],
            PropDelays: new Dictionary<string, string> { ["mass"] = "+0ms", ["mid"] = "+60ms", ["accent"] = "+120ms" },
            AllowedSectionKinds: [SectionKind.Verse, SectionKind.Unknown]);

        var chorusChase = new Look(
            Id: "chorus-chase",
            Name: "Chorus Chase",
            Intent: "Full-canvas high-energy reveal. The matrices carry the beat.",
            EnergyCeiling: 0.9,
            Motion: "chase",
            PaletteRef: "high-energy",
            Subject: "group:Matrixes",
            Layers:
            [
                new LookLayer(1, "group:MiniTrees", "Twinkle", LookLayer.RoleSecondary, new Dictionary<string, string>(), "Normal", "Bars", 0.3),
                new LookLayer(2, "group:Matrixes", "Butterfly", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.8),
                new LookLayer(3, "group:Outline", "SingleStrand", LookLayer.RoleSupport, new Dictionary<string, string>(), "Normal", "Beats", 0.35),
            ],
            AllowedSectionKinds: [SectionKind.Chorus, SectionKind.PreChorus]);

        var preChorusBuild = new Look(
            Id: "pre-chorus-build",
            Name: "Pre-Chorus Build",
            Intent: "Subdivision acceleration and prop accumulation ahead of the chorus reveal.",
            EnergyCeiling: 0.7,
            Motion: "build",
            PaletteRef: "warm-gold",
            Subject: "Tree",
            Layers:
            [
                new LookLayer(1, "Tree", "Plasma", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.85, TransitionIn: new LookTransition("Wipe", NoteValue.HalfBar)),
                new LookLayer(2, "group:Arches", "Color Wash", LookLayer.RoleSupport, new Dictionary<string, string>(), "Normal", "Bars", 0.9, TransitionIn: new LookTransition("Fade", NoteValue.QuarterBar)),
            ],
            AllowedSectionKinds: [SectionKind.PreChorus, SectionKind.Bridge]);

        var breakdownDip = new Look(
            Id: "breakdown-dip",
            Name: "Breakdown Dip",
            Intent: "Deliberate near-black dip -- the highest-impact move in the vocabulary, used sparingly and declared.",
            EnergyCeiling: 0.12,
            Motion: "hold",
            PaletteRef: "warm-gold",
            Subject: "group:Outline",
            Layers:
            [
                new LookLayer(1, "group:Outline", "Color Wash", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.1),
                new LookLayer(2, "Tree", "Color Wash", LookLayer.RoleSupport, new Dictionary<string, string>(), "Normal", "Bars", 0.06),
            ],
            DeclaredDarkness: true,
            AllowedSectionKinds: [SectionKind.Bridge, SectionKind.Outro, SectionKind.Intro]);

        return new LookLibrary([goldenHold, chorusChase, preChorusBuild, breakdownDip]);
    }

    /// <summary>Picks a Look id for a classified section -- a simple, declared, deterministic rule (never randomized), not a creative judgment (that is Phase 5/6's own scope).</summary>
    public static string LookIdFor(SectionKind kind) => kind switch
    {
        SectionKind.Chorus or SectionKind.Solo => "chorus-chase",
        SectionKind.PreChorus => "pre-chorus-build",
        SectionKind.Bridge or SectionKind.Outro => "breakdown-dip",
        _ => "golden-hold",
    };
}
