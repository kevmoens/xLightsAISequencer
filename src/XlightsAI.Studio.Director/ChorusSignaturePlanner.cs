using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Director;

public sealed record ChorusSignature(Guid FamilyId, string Target, string Effect, string Palette, string Intensity)
{
    internal string Brief => $"Chorus family {FamilyId:N}: keep subject {Target}, effect {Effect}, palette {Palette}, and section-start rhythm anchor. The signature is placed automatically at {Intensity} intensity this time; leave space for it and vary supporting layers independently.";

    public SectionTimeline Apply(SectionTimeline timeline, SectionTimingCatalog catalog)
    {
        var start = catalog.Marks["section-start"];
        var end = catalog.Marks.Where(m => m.Key.StartsWith("beat-", StringComparison.Ordinal) && m.Value > start)
            .OrderBy(m => m.Value).Select(m => m.Key).FirstOrDefault() ?? "section-end";
        var signature = new SectionCue("section-start", Target, Effect, Intensity, LookLayer.RoleSubject,
            end, Annotation: $"chorus-signature:{FamilyId:N}", Kind: CueKind.Accent, PaletteSlot: Palette);
        return timeline with { Cues = [signature, .. timeline.CuesOrEmpty.Where(c =>
            c.Annotation?.StartsWith("chorus-signature:", StringComparison.Ordinal) != true)] };
    }
}

public static class ChorusSignaturePlanner
{
    public static IReadOnlyDictionary<Guid, ChorusSignature> Build(
        MusicModel music, LookLibrary looks, IReadOnlyList<SectionMotifLink> links)
    {
        var choruses = music.Sections.Labels.Where(s => s.Kind == SectionKind.Chorus && s.Confidence >= .6)
            .OrderBy(s => s.StartBarIndex).ToArray();
        var linksById = links.Where(l => l.Confidence >= .7 && l.LinkedPredecessorKind == SectionKind.Chorus)
            .ToDictionary(l => l.SectionId);
        var roots = new Dictionary<Guid, Guid>();
        foreach (var chorus in choruses)
        {
            roots[chorus.Id] = linksById.TryGetValue(chorus.Id, out var link) &&
                roots.TryGetValue(link.LinkedPredecessorSectionId, out var root) ? root : chorus.Id;
        }

        var looksForChorus = looks.Looks.Where(l => !l.DeclaredDarkness && l.Uniqueness is null &&
                (l.AllowedSectionKindsOrEmpty.Count == 0 || l.AllowedSectionKindsOrEmpty.Contains(SectionKind.Chorus)))
            .Select(l => (Look: l, Subject: l.Layers.FirstOrDefault(x => x.Role == LookLayer.RoleSubject)))
            .Where(x => x.Subject is not null).ToArray();
        var result = new Dictionary<Guid, ChorusSignature>();
        var familyNumber = 0;
        foreach (var family in roots.GroupBy(x => x.Value).Where(g => g.Count() > 1))
        {
            if (looksForChorus.Length == 0)
            {
                break;
            }
            var (look, layer) = looksForChorus[familyNumber++ % looksForChorus.Length];
            var occurrence = 0;
            foreach (var member in family)
            {
                result[member.Key] = new ChorusSignature(family.Key, layer!.Props, layer.Effect, look.PaletteRef,
                    occurrence++ % 2 == 0 ? IntensityTier.Bright : IntensityTier.Full);
            }
        }
        return result;
    }
}
