using System.Xml.Linq;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.7; architecture §8b): parses a corpus <c>.xsq</c>'s
/// <c>&lt;ColorPalettes&gt;</c>, <c>&lt;EffectDB&gt;</c> and every <c>&lt;Element type="model"|"group"&gt;</c>'s
/// <c>&lt;EffectLayer&gt;</c> children into flat <see cref="ParsedEffectInstance"/> records. Reuses
/// <see cref="XsqStructuralReader"/> for the validated shape (dangling-ref checks, well-formedness, the
/// already-resolved <c>EffectDB</c>/<c>ColorPalettes</c> text) rather than re-parsing the XML from
/// scratch — the one piece <see cref="XsqStructuralDocument"/> does not carry is which
/// <c>&lt;EffectLayer&gt;</c> index a placement came from, so this type recovers that with a second,
/// position-matched pass over the same file's raw <see cref="XDocument"/>. Both passes iterate
/// <c>&lt;Element&gt;</c> → <c>&lt;EffectLayer&gt;</c> → <c>&lt;Effect&gt;</c> in the same document order
/// with the same timing-track exclusion, so the two placement lists line up positionally by construction.
/// </summary>
public static class EffectAlignmentParser
{
    private static readonly IXsqStructuralReader Reader = new XsqStructuralReader();

    public static IReadOnlyList<ParsedEffectInstance> Parse(string xsqPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xsqPath);

        var doc = Reader.Read(xsqPath);
        var layerIndices = ReadLayerIndices(xsqPath);

        var instances = new List<ParsedEffectInstance>();
        var flatIndex = 0;
        foreach (var element in doc.ElementEffects)
        {
            foreach (var placement in element.Effects)
            {
                var layerIndex = flatIndex < layerIndices.Count ? layerIndices[flatIndex] : -1;
                flatIndex++;

                var settings = placement.Ref >= 0 && placement.Ref < doc.EffectDb.Count ? doc.EffectDb[placement.Ref] : string.Empty;
                var palette = placement.Palette >= 0 && placement.Palette < doc.ColorPalettes.Count ? doc.ColorPalettes[placement.Palette] : string.Empty;

                instances.Add(new ParsedEffectInstance(
                    ModelName: element.Name,
                    ModelType: element.Type,
                    EffectName: placement.Name,
                    StartTimeMs: placement.StartTimeMs,
                    EndTimeMs: placement.EndTimeMs,
                    LayerIndex: layerIndex,
                    PaletteIndex: placement.Palette,
                    SettingsString: settings,
                    PaletteString: palette));
            }
        }

        return instances;
    }

    /// <summary>
    /// Replicates <see cref="XsqStructuralReader"/>'s own <c>&lt;ElementEffects&gt;</c> traversal
    /// exactly (same element/layer/effect ordering, same <c>type == "timing"</c> exclusion) purely to
    /// recover each placement's <c>&lt;EffectLayer&gt;</c> position — the one field
    /// <see cref="XsqStructuralDocument"/> does not expose.
    /// </summary>
    private static List<int> ReadLayerIndices(string xsqPath)
    {
        var document = XDocument.Load(xsqPath);
        var root = document.Root!;
        var elementEffectsEl = root.Element("ElementEffects")!;

        var layerIndices = new List<int>();
        foreach (var elementEl in elementEffectsEl.Elements("Element"))
        {
            var type = (string?)elementEl.Attribute("type") ?? string.Empty;
            if (type == "timing")
            {
                continue;
            }

            var layerIndex = 0;
            foreach (var layerEl in elementEl.Elements("EffectLayer"))
            {
                foreach (var _ in layerEl.Elements("Effect"))
                {
                    layerIndices.Add(layerIndex);
                }

                layerIndex++;
            }
        }

        return layerIndices;
    }
}
