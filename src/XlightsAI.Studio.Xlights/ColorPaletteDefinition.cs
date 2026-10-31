namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1c (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md, "a palette table"): one entry of
/// <see cref="SequenceDocument.Palettes"/>, serialized verbatim into a real xLights
/// <c>&lt;ColorPalette&gt;</c> element. <see cref="RawSettings"/> is the already-correct
/// <c>C_BUTTON_PaletteN=#RRGGBB,C_CHECKBOX_PaletteN=1,...</c> string (architecture §5: "a palette is
/// always eight <c>C_BUTTON_PaletteN</c> slots plus <c>C_CHECKBOX_PaletteN=1</c> marking which are
/// active") — generating that string correctly from a higher-level palette description is Phase 2's
/// generated-serializer scope; this type only carries and round-trips an already-verified one.
/// </summary>
public sealed record ColorPaletteDefinition(string RawSettings);
