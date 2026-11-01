using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace XlightsAI.Studio.Xlights;

/// <summary>Default <see cref="IXsqStructuralReader"/>. See the interface doc for its purpose.</summary>
public sealed class XsqStructuralReader : IXsqStructuralReader
{
    public XsqStructuralDocument Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new XsqStructuralReadException($"No file exists at '{path}'.");
        }

        XDocument document;
        try
        {
            document = XDocument.Load(path, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new XsqStructuralReadException($"'{path}' is not well-formed XML.", ex);
        }

        var root = document.Root;
        if (root is null || root.Name != "xsequence")
        {
            throw new XsqStructuralReadException($"'{path}' has no root <xsequence> element.");
        }

        // FixedPointTiming is a boolean-ish flag (verified always "1" in real xLights output, see
        // docs/PHASE0-FINDINGS.md) -- NOT the frame interval. Its presence is still required (a real
        // xLights-written .xsq always carries it), but the actual frame interval is parsed from
        // head/sequenceTiming below (e.g. "50 ms").
        _ = root.Attribute("FixedPointTiming")?.Value
            ?? throw new XsqStructuralReadException($"'{path}' is missing the <xsequence FixedPointTiming=\"...\"> attribute.");

        var head = root.Element("head") ?? throw new XsqStructuralReadException($"'{path}' is missing the required <head> section.");
        var version = head.Element("version")?.Value
            ?? throw new XsqStructuralReadException($"'{path}' is missing <head><version>.");
        var sequenceTimingText = head.Element("sequenceTiming")?.Value
            ?? throw new XsqStructuralReadException($"'{path}' is missing <head><sequenceTiming>.");
        var frameIntervalText = sequenceTimingText.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (frameIntervalText is null || !int.TryParse(frameIntervalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frameIntervalMs))
        {
            throw new XsqStructuralReadException($"'{path}' has a malformed <head><sequenceTiming> value '{sequenceTimingText}'.");
        }

        var durationText = head.Element("sequenceDuration")?.Value
            ?? throw new XsqStructuralReadException($"'{path}' is missing <head><sequenceDuration>.");
        if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSeconds))
        {
            throw new XsqStructuralReadException($"'{path}' has a non-numeric sequenceDuration value '{durationText}'.");
        }

        var colorPalettesEl = root.Element("ColorPalettes") ?? throw new XsqStructuralReadException($"'{path}' is missing the required <ColorPalettes> section.");
        var colorPalettes = colorPalettesEl.Elements("ColorPalette").Select(e => e.Value).ToList();

        var effectDbEl = root.Element("EffectDB") ?? throw new XsqStructuralReadException($"'{path}' is missing the required <EffectDB> section.");
        var effectDb = effectDbEl.Elements("Effect").Select(e => e.Value).ToList();

        var displayElementsEl = root.Element("DisplayElements") ?? throw new XsqStructuralReadException($"'{path}' is missing the required <DisplayElements> section.");
        var displayElements = displayElementsEl.Elements("Element")
            .Select(e => new XsqStructuralElement(
                RequiredAttribute(e, "type", path),
                RequiredAttribute(e, "name", path),
                Effects: []))
            .ToList();

        var elementEffectsEl = root.Element("ElementEffects") ?? throw new XsqStructuralReadException($"'{path}' is missing the required <ElementEffects> section.");
        var elementEffects = new List<XsqStructuralElement>();
        var timingTracks = new List<XsqStructuralTimingTrack>();
        foreach (var elementEl in elementEffectsEl.Elements("Element"))
        {
            var type = RequiredAttribute(elementEl, "type", path);
            var name = RequiredAttribute(elementEl, "name", path);

            // PHASE-1c: a timing track's <Effect> marks carry only label/startTime/endTime -- no
            // ref/palette/name -- so they are read into their own shape rather than forced through the
            // ref/palette dangling-reference checks below, which only make sense for model placements.
            if (type == "timing")
            {
                // Build-plan Phase 17 §1c/§2b real-corpus finding: a real, otherwise well-formed .xsq
                // can carry a single zero-width timing mark ([X, X), start == end) -- confirmed across
                // several independently-sequenced shows (Mashup.xsq's 'Beats' track, Nutcracker
                // Dubstep.xsq's 'Beat Count', etc.), always as a harmless trailing artifact rather than a
                // sign of a corrupt track otherwise. Previously this threw and rejected the whole file --
                // every one of this reader's other consumers (not just corpus mining) hit the same
                // failure on these real files. A zero-width mark carries no usable interval information
                // (nothing can legitimately start and end at the same instant), so it is skipped rather
                // than kept with a fabricated nonzero width; every other mark on the track still parses
                // and is kept. A mark with endMs < startMs (truly inverted, never observed in the real
                // corpus) is still treated the same way -- equally not a usable interval.
                var marks = new List<XsqStructuralTimingMark>();
                foreach (var effectEl in elementEl.Elements("EffectLayer").SelectMany(layerEl => layerEl.Elements("Effect")))
                {
                    var startMs = RequiredLongAttribute(effectEl, "startTime", path);
                    var endMs = RequiredLongAttribute(effectEl, "endTime", path);
                    if (endMs <= startMs)
                    {
                        continue;
                    }

                    marks.Add(new XsqStructuralTimingMark((string?)effectEl.Attribute("label") ?? string.Empty, startMs, endMs));
                }

                timingTracks.Add(new XsqStructuralTimingTrack(name, marks));
                continue;
            }

            var placements = new List<XsqStructuralEffectPlacement>();
            foreach (var layerEl in elementEl.Elements("EffectLayer"))
            {
                foreach (var effectEl in layerEl.Elements("Effect"))
                {
                    var refIndex = RequiredIntAttribute(effectEl, "ref", path);
                    var paletteIndex = RequiredIntAttribute(effectEl, "palette", path);

                    if (refIndex < 0 || refIndex >= effectDb.Count)
                    {
                        throw new XsqStructuralReadException(
                            $"'{path}': effect on '{name}' has a dangling EffectDB ref={refIndex} (EffectDB has {effectDb.Count} entries).");
                    }

                    if (paletteIndex < 0 || paletteIndex >= colorPalettes.Count)
                    {
                        throw new XsqStructuralReadException(
                            $"'{path}': effect on '{name}' has a dangling palette ref={paletteIndex} (ColorPalettes has {colorPalettes.Count} entries).");
                    }

                    var startMs = RequiredLongAttribute(effectEl, "startTime", path);
                    var endMs = RequiredLongAttribute(effectEl, "endTime", path);
                    if (endMs <= startMs)
                    {
                        throw new XsqStructuralReadException(
                            $"'{path}': effect on '{name}' has a non-positive interval [{startMs}, {endMs}).");
                    }

                    placements.Add(new XsqStructuralEffectPlacement(
                        refIndex, RequiredAttribute(effectEl, "name", path), startMs, endMs, paletteIndex));
                }
            }

            elementEffects.Add(new XsqStructuralElement(type, name, placements));
        }

        return new XsqStructuralDocument(
            version, frameIntervalMs, durationSeconds, colorPalettes, effectDb, displayElements, elementEffects, timingTracks);
    }

    private static string RequiredAttribute(XElement element, string attributeName, string path) =>
        element.Attribute(attributeName)?.Value
        ?? throw new XsqStructuralReadException($"'{path}': <{element.Name}> is missing required attribute '{attributeName}'.");

    private static int RequiredIntAttribute(XElement element, string attributeName, string path)
    {
        var text = RequiredAttribute(element, attributeName, path);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new XsqStructuralReadException($"'{path}': <{element.Name} {attributeName}=\"{text}\"> is not a valid integer.");
    }

    private static long RequiredLongAttribute(XElement element, string attributeName, string path)
    {
        var text = RequiredAttribute(element, attributeName, path);
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new XsqStructuralReadException($"'{path}': <{element.Name} {attributeName}=\"{text}\"> is not a valid integer.");
    }
}
