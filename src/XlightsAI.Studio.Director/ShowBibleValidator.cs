using System.Globalization;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Structure;
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

    /// <summary>
    /// Build-plan Phase 24 §2a: a <see cref="SectionKind"/> with fewer than this many real occurrences in
    /// a song has nothing real to diversify -- exempt from the per-Kind Look-diversity floor below. Reused
    /// by <see cref="ValidateExemplarReuse"/> (build-plan Phase 25 Part E.2) as the same "nothing real to
    /// diversify yet" floor for a repeatable-piece group's own total draws.
    /// </summary>
    public const int MinOccurrencesForDiversityFloor = 3;

    /// <summary>
    /// Build-plan Phase 25 Part E.3: the ceiling on how many times a single real exemplar may be quoted
    /// into one generation run's own "## Retrieved exemplars" prompt section before it counts as
    /// over-reused -- measured as <c>CorpusTrainingReport.ReuseRate.P90OccurrencesPerReusedBucket</c> from
    /// a real <c>CorpusTrainer</c> run over 31 external sequences at <c>C:\xLights</c> (2026-11-11): 400
    /// distinct phrase-length/section-kind/energy-tier buckets, 899 total phrase windows, 150 buckets
    /// (55.5% of all windows) recurring within their own song, median 3 occurrences per reused bucket,
    /// P90 10 -- the same "measured, not guessed" discipline as <see cref="MinLooks"/>/<see cref="MaxLooks"/>.
    /// </summary>
    public const int MaxExemplarReuseCount = 10;

    /// <summary>
    /// User feedback (2026-11-12): the baseline craft guidance built into <see cref="DirectorPromptBuilder"/>
    /// already states a hard floor -- "a Chorus/Drop/peak-energy Look's subject prop should carry 3 or more
    /// concurrent layers" -- but nothing previously enforced it, so a generation that ignored the prose
    /// still passed validation cleanly. This is that floor made real: any Look energetic enough to be a
    /// genuine peak/featured moment (<see cref="PeakLookEnergyCeilingThreshold"/>, the same cutoff
    /// <c>DirectorPromptBuilder.ComplexityRequestCeilingThreshold</c> uses to decide which SectionKinds are
    /// Chorus/Solo-tier) must actually declare <see cref="MinPeakLookLayers"/>+ layers, same tier/mechanism
    /// as <see cref="MinLooks"/>/<see cref="MaxLooks"/> -- a hard, retryable validation failure rather than
    /// a hope that the model followed the prose.
    /// </summary>
    public const double PeakLookEnergyCeilingThreshold = 0.75;

    public const int MinPeakLookLayers = 3;

    public static IReadOnlyList<ShowBibleValidationError> Validate(
        ShowBibleContent content, DisplayGraph layout, EffectVocabularyCatalog vocabulary, EffectRecipeCatalog recipeCatalog,
        MultiLayerTemplateCatalog? templates = null, IReadOnlyDictionary<SectionKind, int>? sectionKindOccurrenceCounts = null,
        ExemplarReuseReport? exemplarReuse = null)
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

        if (sectionKindOccurrenceCounts is not null)
        {
            ValidateLookDiversityPerKind(content.Looks.Looks, sectionKindOccurrenceCounts, errors);
        }

        errors.AddRange(ValidateExemplarReuseOnly(exemplarReuse));

        return errors;
    }

    /// <summary>
    /// Build-plan Phase 26 bugfix: public entry point onto <see cref="ValidateExemplarReuse"/> alone, for
    /// <see cref="ShowBibleGenerator"/> to check BEFORE spending any attempt against the LLM. Real-run
    /// finding (2026-11-11, project "Bang"): <paramref name="report"/> is a property of retrieval (which
    /// real exemplars <c>ExemplarIndex.FindSimilar</c>/<c>FindVaried</c> drew while assembling the
    /// -- unchanging-across-retries -- stable prefix), never of the LLM's own returned content, so any
    /// violation this produces is identical on every attempt no matter what the model returns: a Look
    /// library the LLM could not possibly write its way out of. Checking it only inside the main
    /// generate-and-correct loop (alongside genuinely model-fixable violations like a malformed Look)
    /// silently burned the entire retry budget on calls that were mathematically guaranteed to fail before
    /// the first one was ever sent, and buried the real, actionable root cause (a corpus/retrieval gap,
    /// not a model mistake) inside a wall of unrelated per-Look content errors.
    /// </summary>
    public static IReadOnlyList<ShowBibleValidationError> ValidateExemplarReuseOnly(ExemplarReuseReport? report)
    {
        var errors = new List<ShowBibleValidationError>();
        ValidateExemplarReuse(report, errors);
        return errors;
    }

    /// <summary>
    /// Build-plan Phase 25 Part E.3: validates <paramref name="report"/> (this run's own
    /// <see cref="ExemplarReuseTracker"/> recording, built while assembling the Director's own stable
    /// prefix) the same way <see cref="ValidateLookDiversityPerKind"/> validates Look diversity -- a single
    /// exemplar drawn beyond <see cref="MaxExemplarReuseCount"/> times is a hard failure, and a
    /// "repeatable piece" group (<see cref="RepeatablePieceGrouper"/>) drawn <see cref="MinOccurrencesForDiversityFloor"/>+
    /// times total must have drawn at least <c>ceil(totalDraws/2)</c> distinct members -- the same
    /// occurrences-to-required-distinct-count ratio <see cref="ValidateLookDiversityPerKind"/> already
    /// applies to Looks. Appends nothing when <paramref name="report"/> is <see langword="null"/> (no
    /// reuse tracking was supplied -- every pre-existing caller before this phase).
    /// </summary>
    private static void ValidateExemplarReuse(ExemplarReuseReport? report, List<ShowBibleValidationError> errors)
    {
        if (report is null)
        {
            return;
        }

        foreach (var (key, count) in report.DrawCountByExemplarKey)
        {
            if (count > MaxExemplarReuseCount)
            {
                errors.Add(new ShowBibleValidationError(
                    "exemplar-reuse-max",
                    $"Exemplar '{key}' was drawn {count} time(s) while assembling this run's own retrieved-exemplar prompt section, exceeding the measured reuse budget of {MaxExemplarReuseCount} -- the corpus needs more real variety for this structural bucket, not the same passage quoted repeatedly."));
            }
        }

        foreach (var (group, stats) in report.GroupStats)
        {
            if (stats.TotalDraws < MinOccurrencesForDiversityFloor)
            {
                continue;
            }

            var requiredDistinct = (int)Math.Ceiling(stats.TotalDraws / 2.0);
            if (stats.DistinctKeysDrawn < requiredDistinct)
            {
                errors.Add(new ShowBibleValidationError(
                    "exemplar-reuse-diversity",
                    $"Repeatable-piece group '{group}' was drawn {stats.TotalDraws} time(s) across this run's retrieved exemplars, but only {stats.DistinctKeysDrawn} distinct exemplar(s) from that group were used -- need at least {requiredDistinct} (ceil({stats.TotalDraws}/2)) distinct exemplars, not the same one or two repeated."));
            }
        }
    }

    /// <summary>
    /// Build-plan Phase 24 §2a: for any <see cref="SectionKind"/> with
    /// <see cref="MinOccurrencesForDiversityFloor"/>+ real occurrences in the song, at least
    /// <c>ceil(N/2)</c> distinct Looks must be eligible for that Kind -- a Look is eligible either because
    /// its <see cref="Look.AllowedSectionKindsOrEmpty"/> names the Kind explicitly, or because it is left
    /// unrestricted (empty), the same eligibility rule <see cref="XlightsAI.Studio.Director.SectionScriptPromptBuilder"/>
    /// already uses at generation time -- a hard validation failure, same tier as <see cref="MinLooks"/>/
    /// <see cref="MaxLooks"/>, because nothing previously checked that the existing "spread Look design"
    /// prose guidance was ever actually followed.
    /// </summary>
    private static void ValidateLookDiversityPerKind(
        IReadOnlyList<Look> looks, IReadOnlyDictionary<SectionKind, int> sectionKindOccurrenceCounts, List<ShowBibleValidationError> errors)
    {
        foreach (var (kind, occurrenceCount) in sectionKindOccurrenceCounts)
        {
            if (occurrenceCount < MinOccurrencesForDiversityFloor)
            {
                continue;
            }

            var requiredDistinctLooks = (int)Math.Ceiling(occurrenceCount / 2.0);
            var eligibleLooks = looks.Count(l => l.AllowedSectionKindsOrEmpty.Count == 0 || l.AllowedSectionKindsOrEmpty.Contains(kind));
            if (eligibleLooks < requiredDistinctLooks)
            {
                errors.Add(new ShowBibleValidationError(
                    "look-diversity-per-kind",
                    $"SectionKind '{kind}' occurs {occurrenceCount} times in this song, but only {eligibleLooks} Look(s) are eligible for it (AllowedSectionKinds names '{kind}', or is left unrestricted) -- need at least {requiredDistinctLooks} (ceil({occurrenceCount}/2)) distinct eligible Looks so repeats of this Kind are not forced to reuse the same one or two Looks."));
            }
        }
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

            // User feedback (2026-11-13): Travel.Timescale is deliberately NOT checked against this
            // song's own real timing tracks here -- same precedent as a plain layer's own Timescale,
            // which TimescaleResolver's graceful fallback resolves (or safely ignores) at compile time,
            // not at Show-Bible-validation time. Order is a closed, cheap-to-check, always-knowable set.
            if (look.Travel is { } travel && travel.Order != LookTravel.OrderLeftToRight && travel.Order != LookTravel.OrderRightToLeft)
            {
                errors.Add(new ShowBibleValidationError("look-travel-order", $"Look '{look.Id}' declares Travel.Order '{travel.Order}', which must be exactly '{LookTravel.OrderLeftToRight}' or '{LookTravel.OrderRightToLeft}'."));
            }

            if (look.Travel is { Timescale.Length: 0 })
            {
                errors.Add(new ShowBibleValidationError("look-travel-timescale", $"Look '{look.Id}' declares Travel with a blank Timescale -- a real timing track name (e.g. 'Beats', 'Transients', 'VocalNotes') is required."));
            }

            if (look.Layers.Count == 0)
            {
                errors.Add(new ShowBibleValidationError("look-layers", $"Look '{look.Id}' has no layers."));
                continue;
            }

            if (look.EnergyCeiling >= PeakLookEnergyCeilingThreshold && look.Layers.Count < MinPeakLookLayers)
            {
                errors.Add(new ShowBibleValidationError("look-peak-layer-floor", $"Look '{look.Id}' has EnergyCeiling {look.EnergyCeiling}, a Chorus/Solo-tier peak moment, but only {look.Layers.Count} layer(s) -- a peak Look's subject prop must carry {MinPeakLookLayers}+ concurrent layers (the multi-layer template catalog and layer-complexity guidance above exist to help with this)."));
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

    /// <summary>
    /// Shared with <see cref="ChoreographyGenerationPipeline"/>'s own layer-density diagnostic logging and
    /// (build-plan Phase 19 §2c) <see cref="XlightsAI.Studio.Pipeline.CraftRuleInducer"/>'s corpus-mined
    /// layer-floor rule -- a thin forwarding alias kept here for existing Director call sites; the real,
    /// single definition now lives in <see cref="XlightsAI.Studio.Knowledge.TreeMatrixPropNameConvention"/>,
    /// which Pipeline can also reference (Director cannot be referenced the other way around).
    /// </summary>
    internal static bool IsTreeOrMatrixName(string name) =>
        XlightsAI.Studio.Knowledge.TreeMatrixPropNameConvention.IsTreeOrMatrixName(name);

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
