using System.Globalization;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 5 task 5: "every Look in the returned Show Bible must validate against Phase 2's
/// generated [vocabulary]." Validates a freshly-deserialized <see cref="ShowBibleContent"/> against the
/// real layout and the real effect vocabulary/recipe catalog -- reuses <see cref="PropGroupResolver"/>
/// and <see cref="EffectSettingsValidator"/> directly (the same checks <c>Choreography.ChoreographyLegalityValidator</c>
/// already established for Phase 4) rather than re-deriving a parallel set of rules. Never throws; always
/// returns the full violation list so <see cref="ShowBibleGenerator"/> can decide whether to retry.
/// </summary>
public static class ShowBibleValidator
{
    public const int MinLooks = 8;
    public const int MaxLooks = 20;
    public const int MinPalettes = 3;
    public const int MaxPalettes = 5;

    private static readonly HashSet<string> ValidPaletteRoles = new(StringComparer.Ordinal) { PaletteRole.Base, PaletteRole.Key, PaletteRole.Accent };
    private static readonly HashSet<string> ValidMusicalRoles = new(StringComparer.Ordinal) { MusicalRole.Bass, MusicalRole.Melody, MusicalRole.Highs };
    private static readonly HashSet<string> ValidLookRoles = new(StringComparer.Ordinal) { LookLayer.RoleSubject, LookLayer.RoleSupport, LookLayer.RoleSecondary };

    public static IReadOnlyList<ShowBibleValidationError> Validate(
        ShowBibleContent content, DisplayGraph layout, EffectVocabularyCatalog vocabulary, EffectRecipeCatalog recipeCatalog,
        MultiLayerTemplateCatalog? templates = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(recipeCatalog);

        var errors = new List<ShowBibleValidationError>();

        ValidatePalettes(content.Palettes, errors);
        var paletteIds = new HashSet<string>(content.Palettes.Select(p => p.Id), StringComparer.Ordinal);

        ValidateLooks(content.Looks.Looks, layout, vocabulary, recipeCatalog, paletteIds, templates, errors);
        var lookIds = new HashSet<string>(content.Looks.Looks.Select(l => l.Id), StringComparer.Ordinal);

        ValidatePropRoles(content.PropRoles, layout, errors);
        ValidateEnergyArc(content.EnergyArc, errors);
        ValidatePeakMoments(content.PeakMoments, lookIds, errors);
        ValidateSignatureGestures(content.SignatureGestures, errors);
        ValidateWithholding(content.Withholding, lookIds, errors);

        return errors;
    }

    private static void ValidatePalettes(IReadOnlyList<ShowBiblePalette> palettes, List<ShowBibleValidationError> errors)
    {
        if (palettes.Count is < MinPalettes or > MaxPalettes)
        {
            errors.Add(new ShowBibleValidationError("palette-count", $"Expected {MinPalettes}-{MaxPalettes} palettes (architecture §5); got {palettes.Count}."));
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var palette in palettes)
        {
            if (string.IsNullOrWhiteSpace(palette.Id))
            {
                errors.Add(new ShowBibleValidationError("palette-id", "A palette has a blank id."));
            }
            else if (!seenIds.Add(palette.Id))
            {
                errors.Add(new ShowBibleValidationError("palette-id", $"Duplicate palette id '{palette.Id}'."));
            }

            if (palette.Colors.Count == 0)
            {
                errors.Add(new ShowBibleValidationError("palette-colors", $"Palette '{palette.Id}' has no colors."));
            }

            foreach (var color in palette.Colors)
            {
                if (!IsValidHex(color.HexColor))
                {
                    errors.Add(new ShowBibleValidationError("palette-color-hex", $"Palette '{palette.Id}' has an invalid color '{color.HexColor}' (expected '#RRGGBB')."));
                }

                if (!ValidPaletteRoles.Contains(color.Role))
                {
                    errors.Add(new ShowBibleValidationError("palette-color-role", $"Palette '{palette.Id}' color role '{color.Role}' is not one of base/key/accent."));
                }
            }
        }
    }

    private static void ValidateLooks(
        IReadOnlyList<Look> looks, DisplayGraph layout, EffectVocabularyCatalog vocabulary, EffectRecipeCatalog recipeCatalog,
        HashSet<string> paletteIds, MultiLayerTemplateCatalog? templates, List<ShowBibleValidationError> errors)
    {
        if (looks.Count is < MinLooks or > MaxLooks)
        {
            errors.Add(new ShowBibleValidationError("look-count", $"Expected {MinLooks}-{MaxLooks} Looks (architecture §2/§5); got {looks.Count}."));
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var validator = new EffectSettingsValidator(vocabulary);

        foreach (var look in looks)
        {
            if (string.IsNullOrWhiteSpace(look.Id))
            {
                errors.Add(new ShowBibleValidationError("look-id", "A Look has a blank id."));
            }
            else if (!seenIds.Add(look.Id))
            {
                errors.Add(new ShowBibleValidationError("look-id", $"Duplicate Look id '{look.Id}'."));
            }

            if (look.EnergyCeiling is < 0 or > 1)
            {
                errors.Add(new ShowBibleValidationError("look-energy-ceiling", $"Look '{look.Id}' EnergyCeiling {look.EnergyCeiling} is outside [0,1]."));
            }

            if (!paletteIds.Contains(look.PaletteRef))
            {
                errors.Add(new ShowBibleValidationError("look-palette-ref", $"Look '{look.Id}' references palette '{look.PaletteRef}', which is not in the declared palette set."));
            }

            ValidateReference(look.Subject, layout, "look-subject", $"Look '{look.Id}' subject", errors);

            if (templates is not null && look.TemplateRef is { } templateRef && templates.Find(templateRef) is null)
            {
                errors.Add(new ShowBibleValidationError("look-template-ref", $"Look '{look.Id}' declares TemplateRef '{templateRef}', which is not in the seed template catalog."));
            }

            if (look.Layers.Count == 0)
            {
                errors.Add(new ShowBibleValidationError("look-layers", $"Look '{look.Id}' has no layers."));
                continue;
            }

            var seenZ = new HashSet<int>();
            foreach (var layer in look.Layers)
            {
                if (!seenZ.Add(layer.Z))
                {
                    // Real-run finding (2026-10-31): nothing previously checked this, so a Look with two
                    // layers both left at the same Z (most commonly the default Z=0) passed validation
                    // cleanly and then crashed deep in SectionScriptCompiler's own Layers.ToDictionary(l
                    // => l.Z) with a raw, unhelpful framework ArgumentException instead of a real,
                    // retryable validation error. A Look's layers are Z-ordered/addressed by Z elsewhere
                    // in this codebase (PropBindings rebind "layer z=N"), so distinct Z values per Look
                    // is a real, load-bearing invariant, not a cosmetic one.
                    errors.Add(new ShowBibleValidationError("layer-duplicate-z", $"Look '{look.Id}' has more than one layer at z={layer.Z} -- every layer in a Look must have a distinct Z."));
                }

                ValidateReference(layer.Props, layout, "layer-props", $"Look '{look.Id}' layer z={layer.Z} props", errors);

                if (!ValidLookRoles.Contains(layer.Role))
                {
                    errors.Add(new ShowBibleValidationError("layer-role", $"Look '{look.Id}' layer z={layer.Z} role '{layer.Role}' is not one of subject/support/secondary."));
                }

                if (layer.Duty is < 0 or > 1)
                {
                    errors.Add(new ShowBibleValidationError("layer-duty", $"Look '{look.Id}' layer z={layer.Z} duty {layer.Duty} is outside [0,1]."));
                }

                if (!recipeCatalog.Recipes.Any(r => r.XlightsEffectName == layer.Effect))
                {
                    errors.Add(new ShowBibleValidationError("layer-effect", $"Look '{look.Id}' layer z={layer.Z} declares effect '{layer.Effect}', which no registered recipe produces."));
                    continue;
                }

                foreach (var validationError in validator.ValidateSettings(layer.Effect, layer.ParamsOrEmpty))
                {
                    errors.Add(new ShowBibleValidationError("layer-param", $"Look '{look.Id}' layer z={layer.Z}: {validationError.Message}"));
                }
            }
        }

        // Build-plan Phase 10 §6b acceptance criterion: "a real generation shows at least one
        // Tree/Matrixes Look whose layer composition visibly matches one of the catalog's named
        // patterns" -- a Director told about a non-empty template catalog, generating against a layout
        // that actually has a Tree/Matrix-family prop, but declaring zero TemplateRefs anywhere, is
        // exactly the silent-gap failure mode DirectorPromptBuilder's own "REQUIRED" instruction exists
        // to prevent. Forces a corrective retry instead of letting that instruction go unenforced.
        if (templates is { Templates.Count: > 0 } && HasTreeOrMatrixProp(layout) && !looks.Any(l => l.TemplateRef is { } r && templates.Find(r) is not null))
        {
            errors.Add(new ShowBibleValidationError("look-template-ref-required", "No Look declares a valid TemplateRef, but the layout has a Tree/Matrix-family prop and a seed template catalog was supplied -- at least one Tree/Matrixes Look must directly copy one of the named templates and declare its Id as TemplateRef."));
        }
    }

    private static bool HasTreeOrMatrixProp(DisplayGraph layout) =>
        layout.Models.Any(m => IsTreeOrMatrixName(m.XlightsName)) || layout.Groups.Any(g => IsTreeOrMatrixName(g.Name));

    private static bool IsTreeOrMatrixName(string name) =>
        name.Contains("Tree", StringComparison.OrdinalIgnoreCase) || name.Contains("Matrix", StringComparison.OrdinalIgnoreCase);

    private static void ValidateReference(string reference, DisplayGraph layout, string rule, string context, List<ShowBibleValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            errors.Add(new ShowBibleValidationError(rule, $"{context} is blank."));
            return;
        }

        try
        {
            PropGroupResolver.Resolve(reference, layout);
        }
        catch (ArgumentException)
        {
            errors.Add(new ShowBibleValidationError(rule, $"{context} references '{reference}', which does not resolve against the real layout."));
        }
    }

    private static void ValidatePropRoles(IReadOnlyList<PropRoleAssignment> propRoles, DisplayGraph layout, List<ShowBibleValidationError> errors)
    {
        foreach (var assignment in propRoles)
        {
            ValidateReference(assignment.Target, layout, "prop-role-target", $"Prop role for '{assignment.Target}'", errors);

            if (!ValidMusicalRoles.Contains(assignment.MusicalRole))
            {
                errors.Add(new ShowBibleValidationError("prop-role-value", $"Prop role '{assignment.MusicalRole}' for '{assignment.Target}' is not one of bass/melody/highs."));
            }
        }
    }

    private static void ValidateEnergyArc(IReadOnlyList<EnergyArcEntry> arc, List<ShowBibleValidationError> errors)
    {
        foreach (var entry in arc)
        {
            if (entry.EnergyCeiling is < 0 or > 1)
            {
                errors.Add(new ShowBibleValidationError("energy-arc-ceiling", $"Energy arc entry for {entry.Kind} has ceiling {entry.EnergyCeiling}, outside [0,1]."));
            }

            if (entry.TargetHueFamilyCount < 0)
            {
                errors.Add(new ShowBibleValidationError("energy-arc-hue-count", $"Energy arc entry for {entry.Kind} has a negative TargetHueFamilyCount."));
            }
        }
    }

    private static void ValidatePeakMoments(IReadOnlyList<PeakMoment> peaks, HashSet<string> lookIds, List<ShowBibleValidationError> errors)
    {
        foreach (var peak in peaks)
        {
            if (!lookIds.Contains(peak.LookId))
            {
                errors.Add(new ShowBibleValidationError("peak-look-ref", $"Peak moment references Look '{peak.LookId}', which is not in the Look library."));
            }

            if (string.IsNullOrWhiteSpace(peak.Distinction))
            {
                errors.Add(new ShowBibleValidationError("peak-distinction", $"Peak moment for Look '{peak.LookId}' has no stated distinction from other peaks."));
            }
        }
    }

    private static void ValidateSignatureGestures(IReadOnlyList<SignatureGesture> gestures, List<ShowBibleValidationError> errors)
    {
        foreach (var gesture in gestures)
        {
            if (string.IsNullOrWhiteSpace(gesture.Name) || string.IsNullOrWhiteSpace(gesture.Description))
            {
                errors.Add(new ShowBibleValidationError("signature-gesture", "A signature gesture has a blank name or description."));
            }
        }
    }

    private static void ValidateWithholding(WithholdingPlan withholding, HashSet<string> lookIds, List<ShowBibleValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(withholding.FinalChorusAce))
        {
            errors.Add(new ShowBibleValidationError("withholding-ace", "WithholdingPlan.FinalChorusAce is blank."));
        }

        foreach (var lookId in withholding.HeldBackLookIds)
        {
            if (!lookIds.Contains(lookId))
            {
                errors.Add(new ShowBibleValidationError("withholding-look-ref", $"WithholdingPlan references held-back Look '{lookId}', which is not in the Look library."));
            }
        }
    }

    private static bool IsValidHex(string value) =>
        value.Length == 7 && value[0] == '#' && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
}
