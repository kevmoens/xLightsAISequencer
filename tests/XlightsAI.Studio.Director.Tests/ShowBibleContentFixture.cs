using System.Text.Json;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>Builds a structurally-valid <see cref="ShowBibleContent"/> (8-20 Looks, 3-5 palettes) against the real reference layout's own real group names, for fake-transport tests -- never a hand-typed JSON string (round-tripped through the real C# types instead, so it is always exactly what <see cref="StructuredJsonAiClient{TResponse}"/> would accept).</summary>
public static class ShowBibleContentFixture
{
    private static readonly string[] RealGroupRefs = ["group:MiniTrees", "group:Matrixes", "group:Outline", "group:Arches", "Tree"];

    public static ShowBibleContent BuildValid(int lookCount = 10)
    {
        var palettes = new List<ShowBiblePalette>
        {
            new("warm-gold", "Warm Gold", [new ShowBiblePaletteColor("#FFC880", PaletteRole.Base), new ShowBiblePaletteColor("#FFFFFF", PaletteRole.Key)]),
            new("high-energy", "High Energy", [new ShowBiblePaletteColor("#FF0030", PaletteRole.Base), new ShowBiblePaletteColor("#00AEEF", PaletteRole.Accent)]),
            new("cool-blue", "Cool Blue", [new ShowBiblePaletteColor("#0030FF", PaletteRole.Base)]),
        };

        var looks = new List<Look>();
        for (var i = 0; i < lookCount; i++)
        {
            var paletteRef = palettes[i % palettes.Count].Id;
            var subject = RealGroupRefs[i % RealGroupRefs.Length];
            looks.Add(new Look(
                Id: $"look-{i}",
                Name: $"Look {i}",
                Intent: $"Synthetic test look {i}.",
                EnergyCeiling: 0.1 + (0.08 * (i % 10)),
                Motion: i % 3 == 0 ? "hold" : "chase",
                PaletteRef: paletteRef,
                Subject: subject,
                Layers: [new LookLayer(1, subject, "On", LookLayer.RoleSubject, new Dictionary<string, string>(), "Normal", "Bars", 0.9)]));
        }

        var lookIds = looks.Select(l => l.Id).ToList();

        return new ShowBibleContent(
            Palettes: palettes,
            PropRoles: [new PropRoleAssignment("Tree", MusicalRole.Melody), new PropRoleAssignment("group:Matrixes", MusicalRole.Bass)],
            Looks: new LookLibrary(looks),
            EnergyArc: Enum.GetValues<SectionKind>().Select(k => new EnergyArcEntry(k, 0.5, 0.5, 2)).ToList(),
            PeakMoments: [new PeakMoment(lookIds[0], "The only Look reserved for this moment.")],
            SignatureGestures: [new SignatureGesture("Pulse", "A one-bar brightness pulse on the subject.")],
            Withholding: new WithholdingPlan([lookIds[^1]], "The final Look, held back until the last chorus."));
    }

    public static string ToModelResponseText(ShowBibleContent content, bool wrapInCodeFence = true)
    {
        var json = JsonSerializer.Serialize(content);
        return wrapInCodeFence ? $"Here is the Show Bible:\n```json\n{json}\n```" : json;
    }
}
