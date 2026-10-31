using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Default <see cref="IXsqExporter"/>. PHASE-02 Step 013 ("Port minimal editable XSQ writer and
/// required sequencing metadata"): serializes a validated <see cref="SequenceDocument"/> into the
/// minimal xLights <c>.xsq</c> shape this phase's own appendix documents (Section 12: <c>head</c>,
/// <c>ColorPalettes</c>, <c>EffectDB</c>, <c>DisplayElements</c>, <c>ElementEffects</c>), built with
/// <see cref="XDocument"/> (never string concatenation, per Section 12: "Use DOM/XDocument serializer
/// rather than string concatenation") so every name/value is correctly XML-escaped automatically.
///
/// PHASE-00 (de-risk spike): the shape below was verified against real xLights-authored <c>.xsq</c>
/// files in <c>C:\xLights</c> (20 sequences inspected) rather than guessed — see
/// <c>docs/PHASE0-FINDINGS.md</c> for the evidence. Confirmed real-xLights facts baked in here:
/// <c>FixedPointTiming</c> is a boolean-ish flag always written as <c>"1"</c>, never the frame
/// interval; <c>ChanCtrlBasic</c>/<c>ChanCtrlColor</c> root attributes are always present; every
/// <c>DisplayElements</c>/<c>ElementEffects</c> <c>&lt;Element&gt;</c> — model AND model group alike —
/// is <c>type="model"</c> (<c>type="modelGroup"</c> was never observed in any of the 20 real files);
/// <c>selected</c> is omitted unless an effect is actually selected; every effect after the first in a
/// layer carries a 1-based <c>id</c>; and the community/corpus frame interval is 50ms, not 25ms.
///
/// <b>Deliberately does not invent xLights effect-settings parameters this build has not verified.</b>
/// The upstream pinned reference (<c>reference/xlights-autosequencer/src/generator/xsq_writer.py</c>)
/// never emits an "On" effect at all — there is no verified default-parameter table for it anywhere
/// this codebase can inspect. Rather than guess plausible-looking <c>E_*</c> keys (explicitly
/// forbidden, Section 2: "do not invent xLights effect parameters"), the "On" recipe's EffectDB entry
/// is written with an empty settings string. This is now independently confirmed correct and
/// idiomatic (architecture doc §8a/§12): real xLights omits default-valued keys entirely, and an
/// all-default effect genuinely serializes to <c>&lt;Effect&gt;&lt;/Effect&gt;</c> — see
/// <c>docs/PHASE0-FINDINGS.md</c>. The warning below is retained only because this exporter still has
/// no generated effect-metadata schema (Phase 2) to validate against, not because the empty-string
/// shape itself is suspect.
/// </summary>
public sealed class XsqExporter : IXsqExporter
{
    /// <summary>
    /// xLights' sequence frame grid. Every effect's start/end time is rounded to this grid before
    /// being written. PHASE-00 finding (docs/PHASE0-FINDINGS.md): the corpus/community default is
    /// 50ms, not 25ms — confirmed against every real .xsq in C:\xLights. <b>Deliberately left at 25
    /// for this spike</b>: this constant is shared (via direct reference) by
    /// <c>XlightsAI.Studio.Editing.XLightsFrameGrid</c>, <c>ScopedXsqExporter</c>,
    /// <c>XlightsAI.Studio.Rhythm.RhythmGesturePlanner</c> and <c>Shell.TimelineViewModel</c>'s own
    /// tests, several of which hardcode 25ms-grid expectations; changing the value here is correct but
    /// cascades well outside this spike's scoped files. Flip to 50 as a dedicated follow-up that also
    /// updates those dependents' tests, not bundled into Phase 0.
    /// </summary>
    public const int FrameIntervalMs = 25;

    /// <summary>
    /// xLights' <c>FixedPointTiming</c> root attribute is a boolean-ish flag, not the frame interval
    /// in milliseconds — every real .xsq inspected writes exactly <c>"1"</c> here regardless of its
    /// actual frame rate. The real frame interval lives only in <c>head/sequenceTiming</c>.
    /// </summary>
    private const string FixedPointTimingFlag = "1";

    private readonly EffectRecipeCatalog _recipeCatalog;
    private readonly EffectVocabularyCatalog? _vocabulary;
    private readonly EffectSettingsSerializer? _settingsSerializer;

    public XsqExporter()
        : this(EffectRecipeCatalog.CreateDefault())
    {
    }

    /// <summary>PHASE-1b: accepts a caller-supplied catalog (e.g. one with additional recipes registered) so this exporter can resolve any registered recipe's real xLights effect name, not just the built-in "On" recipe.</summary>
    public XsqExporter(EffectRecipeCatalog recipeCatalog)
        : this(recipeCatalog, vocabulary: null)
    {
    }

    /// <summary>
    /// PHASE-4: accepts the generated effect vocabulary (architecture §8a) so this exporter can
    /// serialize a real, verified settings string per effect (parameters + value curves, via
    /// <see cref="EffectSettingsSerializer"/>) and real content-addressed <c>EffectDB</c> dedup, instead
    /// of every effect sharing the single hardcoded empty placeholder entry. <paramref name="vocabulary"/>
    /// is optional and backward compatible: when <see langword="null"/> (the parameterless/<see cref="XsqExporter(EffectRecipeCatalog)"/>
    /// constructors), this exporter reproduces its previous exact behavior byte-for-byte (the golden
    /// fixture stays unaffected) — this is a deliberate compatibility seam, not a deprecated path, since
    /// no vocabulary is available in every caller's environment (it requires a real xLights install).
    /// </summary>
    public XsqExporter(EffectRecipeCatalog recipeCatalog, EffectVocabularyCatalog? vocabulary)
    {
        _recipeCatalog = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
        _vocabulary = vocabulary;
        _settingsSerializer = vocabulary is null ? null : new EffectSettingsSerializer(vocabulary);
    }

    public Task<ExportResult> ExportNewAsync(
        DisplayGraph layout,
        SequenceDocument sequence,
        string outputPath,
        string targetXlightsVersion,
        CancellationToken ct) =>
        ExportAsync(layout, sequence, outputPath, targetXlightsVersion, XsqExportPolicy.RejectIfExists, ct);

    public async Task<ExportResult> ExportAsync(
        DisplayGraph layout,
        SequenceDocument sequence,
        string outputPath,
        string targetXlightsVersion,
        XsqExportPolicy policy,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(sequence);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new XsqExportInvalidException("An export output path must be supplied.");
        }

        if (!outputPath.EndsWith(".xsq", StringComparison.OrdinalIgnoreCase))
        {
            throw new XsqExportInvalidException($"Export output path '{outputPath}' must end in '.xsq'.");
        }

        if (string.IsNullOrWhiteSpace(targetXlightsVersion))
        {
            throw new XsqExportInvalidException("A target xLights version must be supplied.");
        }

        // PHASE-02 Step 015: resolve a pre-existing outputPath per the caller's explicit policy —
        // never a silent overwrite (Section 2: "Never overwrite source show files silently", applied
        // here to any pre-existing export target too, same as Step 013's own original rule). Resolved
        // before any XML is built, so a rejected/misconfigured request never produces a partial file.
        string? backupPath = null;
        if (File.Exists(outputPath))
        {
            switch (policy)
            {
                case XsqExportPolicy.RejectIfExists:
                    throw new XsqExportInvalidException(
                        $"'{outputPath}' already exists; export never overwrites an existing file without an explicit policy.");

                case XsqExportPolicy.AppendCollisionSuffix:
                    outputPath = NextAvailablePath(outputPath);
                    break;

                case XsqExportPolicy.OverwriteWithBackup:
                    // The real pre-existing bytes are preserved in full before anything is replaced —
                    // this policy value is itself the caller's own explicit overwrite approval; it is
                    // never selected implicitly by this exporter.
                    backupPath = NextAvailablePath($"{outputPath}.bak");
                    File.Copy(outputPath, backupPath);
                    break;

                default:
                    throw new XsqExportInvalidException($"Unknown export policy '{policy}'.");
            }
        }

        var modelNames = new HashSet<string>(layout.Models.Select(m => m.XlightsName), StringComparer.Ordinal);
        var groupNames = new HashSet<string>(layout.Groups.Select(g => g.Name), StringComparer.Ordinal);

        var warnings = new List<string>();
        if (_vocabulary is null)
        {
            warnings.Add(
                "No EffectVocabularyCatalog was supplied to this exporter: every effect's EffectDB " +
                "settings are the single hardcoded empty placeholder, regardless of its own Parameters/ " +
                "ValueCurves -- construct this exporter with XsqExporter(EffectRecipeCatalog, " +
                "EffectVocabularyCatalog) to emit real, verified settings strings.");
        }

        // Resolve each effect's target and recipe before building any XML — an export never
        // produces a partial file for a request it is about to reject (Step 013's required failure
        // path: "Invalid model/format/version must fail safely without rewriting source files").
        // PHASE-00 finding (docs/PHASE0-FINDINGS.md): every real .xsq inspected types BOTH models and
        // model groups as type="model" in DisplayElements/ElementEffects; type="modelGroup" was never
        // observed. Model vs. group membership is still validated below (a target must resolve to
        // something real in the layout), but the emitted element type no longer distinguishes them.
        var recipeNamesByTarget = new Dictionary<Guid, string>();
        foreach (var effect in sequence.Effects)
        {
            var targetName = effect.Target.Value;
            if (!modelNames.Contains(targetName) && !groupNames.Contains(targetName))
            {
                throw new XsqExportInvalidException(
                    $"Effect target '{targetName}' does not resolve to any model or model group in the supplied layout; no file was written.");
            }

            if (!_recipeCatalog.TryGet(effect.RecipeId, out var recipe))
            {
                throw new XsqExportInvalidException(
                    $"Unknown effect recipe id '{effect.RecipeId}'. This exporter does not invent xLights XML for an unverified recipe; no file was written.");
            }

            recipeNamesByTarget[effect.Id] = recipe.XlightsEffectName;
        }

        // PHASE-1c: a real palette table (architecture §5/§12 blocker 3), falling back to exactly the
        // previous single-default-white-palette shape when the caller supplies none (keeps the golden
        // fixture byte-for-byte unchanged). Every effect's own PaletteIndex (defaulting to 0) must
        // resolve into this table -- validated below so no <Effect palette="N"/> can ever dangle.
        var palettes = sequence.PalettesOrDefault;
        foreach (var effect in sequence.Effects)
        {
            var paletteIndex = effect.PaletteIndex ?? 0;
            if (paletteIndex < 0 || paletteIndex >= palettes.Count)
            {
                throw new XsqExportInvalidException(
                    $"Effect on '{effect.Target.Value}' references palette index {paletteIndex}, but only {palettes.Count} palette(s) were supplied; no file was written.");
            }
        }

        // PHASE-4: EffectDB dedups purely by settings-string content (real xLights behavior, architecture
        // §8a: "xLights deduplicates identical setting strings aggressively"). When a vocabulary is
        // supplied, each effect's real settings string (parameters + value curves, via
        // EffectSettingsSerializer) is computed and content-addressed into EffectDB; effects sharing an
        // identical settings string (including the common "every parameter is default" case) share one
        // entry, exactly as real xLights does. Without a vocabulary, every effect falls back to the
        // single shared empty placeholder entry (previous, unchanged behavior).
        var effectDbEntries = new List<string>();
        var effectDbIndexByString = new Dictionary<string, int>(StringComparer.Ordinal);
        var effectDbRefByEffectId = new Dictionary<Guid, int>();

        foreach (var effect in sequence.Effects)
        {
            var settingsString = _settingsSerializer is null
                ? ""
                : BuildSettingsString(effect, recipeNamesByTarget[effect.Id], _vocabulary!, _settingsSerializer);

            if (!effectDbIndexByString.TryGetValue(settingsString, out var index))
            {
                index = effectDbEntries.Count;
                effectDbEntries.Add(settingsString);
                effectDbIndexByString[settingsString] = index;
            }

            effectDbRefByEffectId[effect.Id] = index;
        }

        if (effectDbEntries.Count == 0)
        {
            effectDbEntries.Add("");
        }

        var maxEffectEndTicks = sequence.Effects.Count > 0 ? sequence.Effects.Max(e => e.EndTicks) : 0;
        var maxTimingMarkEndTicks = sequence.TimingTracksOrEmpty
            .SelectMany(t => t.Marks)
            .Select(m => (long?)m.EndTicks)
            .Max() ?? 0;
        var maxEndTicks = Math.Max(maxEffectEndTicks, maxTimingMarkEndTicks);
        var sequenceDurationMs = RoundToFrame(TicksToMilliseconds(maxEndTicks));

        var root = new XElement(
            "xsequence",
            new XAttribute("BaseChannel", "0"),
            new XAttribute("ChanCtrlBasic", "0"),
            new XAttribute("ChanCtrlColor", "0"),
            new XAttribute("FixedPointTiming", FixedPointTimingFlag),
            new XAttribute("ModelBlending", "true"));

        // Head field set and order verified against 20 real .xsq files in C:\xLights (docs/PHASE0-FINDINGS.md).
        // PHASE-1c: fields now source from sequence.Metadata when the caller supplies it; falls back to
        // empty strings (matching the shape -- not the content -- of a real xLights head) exactly as
        // before when no metadata is supplied, so the golden fixture is unaffected.
        var metadata = sequence.MetadataOrDefault;
        root.Add(new XElement(
            "head",
            new XElement("version", targetXlightsVersion.Trim()),
            new XElement("author", metadata.Author),
            new XElement("author-email", metadata.AuthorEmail),
            new XElement("author-website", metadata.AuthorWebsite),
            new XElement("song", metadata.Song),
            new XElement("artist", metadata.Artist),
            new XElement("album", metadata.Album),
            new XElement("MusicURL", metadata.MusicUrl),
            new XElement("comment", metadata.Comment),
            new XElement("sequenceTiming", $"{FrameIntervalMs} ms"),
            // Confirmed against a real working .xsq vs. a real generated one that opened fine but played
            // no audio: xLights uses sequenceType, not just a non-empty mediaFile, to decide whether a
            // sequence is audio-driven -- a real song-backed sequence is "Media", never "Animation"
            // (which the golden fixture's own empty mediaFile confirms is the correct value for a
            // sequence with no real audio at all).
            new XElement("sequenceType", string.IsNullOrEmpty(metadata.MediaFile) ? "Animation" : "Media"),
            new XElement("mediaFile", metadata.MediaFile),
            new XElement("sequenceDuration", (sequenceDurationMs / 1000.0).ToString("0.000")),
            new XElement("imageDir")));

        root.Add(new XElement("nextid", "1"));
        root.Add(new XElement("Jukebox"));

        root.Add(new XElement(
            "ColorPalettes",
            palettes.Select(p => new XElement("ColorPalette", p.RawSettings))));
        root.Add(new XElement("EffectDB", effectDbEntries.Select(entry => new XElement("Effect", entry))));
        root.Add(new XElement("DataLayers"));

        // DisplayElements: one entry per distinct target, in first-appearance order (verified real-file
        // shape: <Element type="model" name="..." visible="1" /> — both models and model groups use
        // type="model"; see docs/PHASE0-FINDINGS.md).
        var displayElements = new XElement("DisplayElements");
        var orderedTargets = new List<string>();
        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var effect in sequence.Effects)
        {
            if (seenTargets.Add(effect.Target.Value))
            {
                orderedTargets.Add(effect.Target.Value);
                displayElements.Add(new XElement(
                    "Element",
                    new XAttribute("type", "model"),
                    new XAttribute("name", effect.Target.Value),
                    new XAttribute("visible", "1")));
            }
        }

        // PHASE-1c (architecture §6, "timing tracks are first-class output, not metadata"): each timing
        // track is its own <Element type="timing"> in DisplayElements (self-closed, same shape as a
        // model element -- verified against real xLights output, docs/PHASE0-FINDINGS.md) ...
        var timingTracks = sequence.TimingTracksOrEmpty;
        var seenTimingTrackNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var track in timingTracks)
        {
            if (string.IsNullOrWhiteSpace(track.Name))
            {
                throw new XsqExportInvalidException("A timing track's name must not be blank; no file was written.");
            }

            if (!seenTimingTrackNames.Add(track.Name))
            {
                throw new XsqExportInvalidException($"Timing track name '{track.Name}' is used more than once; no file was written.");
            }

            displayElements.Add(new XElement(
                "Element",
                new XAttribute("type", "timing"),
                new XAttribute("name", track.Name),
                new XAttribute("visible", "1")));
        }

        root.Add(displayElements);

        // ElementEffects: one <Element> per target, one <EffectLayer> per distinct EffectInstance.Layer
        // used on that target, effects within a layer sorted by start time (mirrors the pinned
        // upstream reference's own layers_map convention).
        var elementEffects = new XElement("ElementEffects");
        foreach (var targetName in orderedTargets)
        {
            var targetElement = new XElement(
                "Element",
                new XAttribute("type", "model"),
                new XAttribute("name", targetName));

            var byLayer = sequence.Effects
                .Where(e => e.Target.Value == targetName)
                .GroupBy(e => e.Layer)
                .OrderBy(g => g.Key);

            foreach (var layerGroup in byLayer)
            {
                var layerElement = new XElement("EffectLayer");

                // PHASE-07 Step 051 ("Handle... frame quantization gracefully"; Section 12's own
                // instruction: "verify xLights frame snapping doesn't reverse event order"). Rounding a
                // single effect's own Start/EndTicks independently to the FrameIntervalMs grid can never
                // by itself reverse the order of two effects that were already non-overlapping in raw
                // ticks -- rounding-to-nearest-grid is a monotonic non-decreasing function of its input,
                // so raw StartTicks_B >= EndTicks_A always implies roundedStart_B >= roundedEnd_A too.
                // The real, previously-unguarded gap is a SequenceDocument whose two EffectInstances on
                // the same target+layer already genuinely overlap in raw ticks (nothing upstream of this
                // exporter enforces non-overlap the way MelodyTrackValidator does for NoteEvents) -- that
                // case was previously written to the exported XSQ completely unchecked. previousEndMs
                // tracks the last emitted effect's own rounded end and pushes a colliding start forward to
                // meet it (never backward, never reordering, preserving the effect's own rounded
                // duration) rather than silently writing an overlapping pair into the exported file.
                long? previousEndMs = null;
                var effectIndexInLayer = 0;
                foreach (var effect in layerGroup.OrderBy(e => e.StartTicks))
                {
                    var startMs = RoundToFrame(TicksToMilliseconds(effect.StartTicks));
                    var endMs = RoundToFrame(TicksToMilliseconds(effect.EndTicks));
                    if (endMs <= startMs)
                    {
                        // Step 013/015's own original, unmodified failure path: this one effect's own
                        // duration independently collapses at the frame grid -- not a cross-effect
                        // collision (handled below), so still rejected outright rather than fabricating a
                        // visible duration the source note never had.
                        throw new XsqExportInvalidException(
                            $"Effect on '{targetName}' ({effect.StartTicks}-{effect.EndTicks} ticks) rounds to a non-positive duration at the {FrameIntervalMs}ms xLights frame grid; no file was written.");
                    }

                    if (previousEndMs is { } floor && startMs < floor)
                    {
                        // Step 051 ("frame quantization gracefully"; Section 12: "verify xLights frame
                        // snapping doesn't reverse event order"): this effect's own rounded duration was
                        // fine in isolation, but it collided with the immediately preceding same-
                        // target/layer effect once both were snapped to the grid -- push forward (never
                        // backward, never reordering) to meet it rather than writing an overlapping or
                        // order-reversed pair into the exported XSQ.
                        var shift = floor - startMs;
                        startMs += shift;
                        endMs += shift;
                    }

                    // Verified real-file shape (docs/PHASE0-FINDINGS.md): `selected` is omitted unless
                    // an effect is actually selected (this exporter never pre-selects anything), and
                    // `id` is a 1-based index present on every effect after the first in a layer.
                    var effectElement = new XElement(
                        "Effect",
                        new XAttribute("ref", effectDbRefByEffectId[effect.Id].ToString()),
                        new XAttribute("name", recipeNamesByTarget[effect.Id]));
                    if (effectIndexInLayer > 0)
                    {
                        effectElement.Add(new XAttribute("id", effectIndexInLayer.ToString()));
                    }

                    effectElement.Add(
                        new XAttribute("startTime", startMs.ToString()),
                        new XAttribute("endTime", endMs.ToString()),
                        new XAttribute("palette", (effect.PaletteIndex ?? 0).ToString()));

                    layerElement.Add(effectElement);

                    previousEndMs = endMs;
                    effectIndexInLayer++;
                }

                targetElement.Add(layerElement);
            }

            elementEffects.Add(targetElement);
        }

        // ... and its marks are written as a second <Element type="timing"> in ElementEffects, one
        // <EffectLayer> directly containing a flat run of <Effect label=".." startTime=".." endTime=".."/>
        // marks (no ref/palette/name -- verified real-file shape, e.g. C:\xLights's own Beats/Bars
        // tracks). Marks must already be sorted and non-overlapping; this exporter never silently
        // reorders or shifts timing marks the way it does for model effects (a timing track is the
        // ground truth other effects beat-lock to -- shifting it to resolve a collision would be
        // exactly backwards).
        foreach (var track in timingTracks)
        {
            var trackElement = new XElement(
                "Element",
                new XAttribute("type", "timing"),
                new XAttribute("name", track.Name));

            var layerElement = new XElement("EffectLayer");
            long? previousEndMs = null;
            foreach (var mark in track.Marks.OrderBy(m => m.StartTicks))
            {
                var startMs = RoundToFrame(TicksToMilliseconds(mark.StartTicks));
                var endMs = RoundToFrame(TicksToMilliseconds(mark.EndTicks));
                if (endMs <= startMs)
                {
                    throw new XsqExportInvalidException(
                        $"Timing track '{track.Name}' mark ({mark.StartTicks}-{mark.EndTicks} ticks) rounds to a non-positive duration at the {FrameIntervalMs}ms xLights frame grid; no file was written.");
                }

                if (previousEndMs is { } floor && startMs < floor)
                {
                    throw new XsqExportInvalidException(
                        $"Timing track '{track.Name}' has overlapping marks after frame-grid rounding (a mark starting at {startMs}ms overlaps the previous mark ending at {floor}ms); no file was written.");
                }

                layerElement.Add(new XElement(
                    "Effect",
                    new XAttribute("label", mark.Label),
                    new XAttribute("startTime", startMs.ToString()),
                    new XAttribute("endTime", endMs.ToString())));

                previousEndMs = endMs;
            }

            trackElement.Add(layerElement);
            elementEffects.Add(trackElement);
        }

        root.Add(elementEffects);

        // Verified real-file shape (docs/PHASE0-FINDINGS.md): every real .xsq ends with a 10-entry
        // <TimingTags> block, each tag unset (position="-1").
        var timingTags = new XElement("TimingTags");
        for (var tagNumber = 0; tagNumber < 10; tagNumber++)
        {
            timingTags.Add(new XElement(
                "Tag",
                new XAttribute("number", tagNumber.ToString()),
                new XAttribute("position", "-1")));
        }

        root.Add(timingTags);

        var document = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);

        ct.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{outputPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await using var writer = System.Xml.XmlWriter.Create(
                    stream,
                    new System.Xml.XmlWriterSettings
                    {
                        Indent = true,
                        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                        Async = true,
                    });
                await document.SaveAsync(writer, ct).ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();

            // Closes the TOCTOU window between this method's own earlier existence check and this
            // move: for RejectIfExists/AppendCollisionSuffix, a file now present at outputPath means a
            // concurrent writer won the race, and it is never silently clobbered. For
            // OverwriteWithBackup, a file being present is expected (it is the very file already
            // backed up above, or possibly a newer concurrent write) — the caller's explicit approval
            // already covers replacing whatever real bytes are at outputPath at the moment of the move.
            if (policy != XsqExportPolicy.OverwriteWithBackup && File.Exists(outputPath))
            {
                File.Delete(tempPath);
                throw new XsqExportInvalidException(
                    $"'{outputPath}' already exists; export never overwrites an existing file without an explicit policy.");
            }

            File.Move(tempPath, outputPath, overwrite: policy == XsqExportPolicy.OverwriteWithBackup);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(outputPath, ct).ConfigureAwait(false)));
        return new ExportResult(outputPath, sha256, warnings, backupPath);
    }

    /// <summary>
    /// PHASE-02 Step 015 "filename safety": the next path near <paramref name="path"/> that does not
    /// already exist, using the familiar "name (1).ext", "name (2).ext", ... convention. Used both for
    /// <see cref="XsqExportPolicy.AppendCollisionSuffix"/>'s own output path and for
    /// <see cref="XsqExportPolicy.OverwriteWithBackup"/>'s backup path (so two overwrites of the same
    /// file in the same run never clobber an earlier backup either).
    /// </summary>
    private static string NextAvailablePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path);
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 1; i <= 9999; i++)
        {
            var candidate = string.IsNullOrEmpty(directory)
                ? $"{stem} ({i}){extension}"
                : Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new XsqExportInvalidException($"Could not find an available filename near '{path}' after 9999 attempts.");
    }

    /// <summary>
    /// PHASE-4: the one real settings string an effect serializes to — its plain <see cref="EffectInstance.Parameters"/>
    /// (already keyed by real <c>SettingKey</c>s, per <c>GeneratedEffectRecipe.CreateDefaultParameters</c>'s
    /// own convention) plus its <see cref="EffectInstance.ValueCurvesOrEmpty"/>, each resolved from its
    /// own plain <see cref="XlightsAI.Studio.Xlights.EffectValueCurve.ParameterKey"/> to the real
    /// <c>ValueCurveSettingKey</c> the vocabulary declares for that property (never invented — an
    /// unresolvable/ineligible key is an export failure, not a silently dropped curve).
    /// <see cref="BlendMode"/>/<see cref="EffectTransition"/> are deliberately NOT serialized here: the
    /// generated vocabulary's own <c>LayerMethod</c>/transition-type rows are modeled as UI-only "custom"
    /// controls with no verified choice list (see <c>EffectVocabularyBuilder</c>'s own disclosed gap) --
    /// serializing them would mean inventing xLights enum values this build has never verified, which
    /// this exporter's own documented discipline forbids. They remain IR-only, same as before.
    /// </summary>
    private static string BuildSettingsString(
        EffectInstance effect, string xlightsEffectName, EffectVocabularyCatalog vocabulary, EffectSettingsSerializer serializer)
    {
        IReadOnlyDictionary<string, Knowledge.EffectValueCurve>? valueCurves = null;
        if (effect.ValueCurvesOrEmpty.Count > 0)
        {
            var resolved = new Dictionary<string, Knowledge.EffectValueCurve>(StringComparer.Ordinal);
            foreach (var curve in effect.ValueCurvesOrEmpty)
            {
                if (!vocabulary.TryGetProperty(xlightsEffectName, curve.ParameterKey, out var property) || !property.SupportsValueCurve)
                {
                    throw new XsqExportInvalidException(
                        $"Effect '{xlightsEffectName}' on '{effect.Target.Value}' declares a value curve for parameter " +
                        $"'{curve.ParameterKey}', which is not a value-curve-eligible setting for that effect; no file was written.");
                }

                resolved[property.ValueCurveSettingKey!] = new Knowledge.EffectValueCurve(
                    curve.CurveType, curve.Min, curve.Max, curve.P1, curve.P2, curve.Reverse,
                    curve.TimingTrack, curve.AudioTrack, curve.FilterLabel);
            }

            valueCurves = resolved;
        }

        try
        {
            return serializer.Serialize(xlightsEffectName, effect.Parameters, valueCurves);
        }
        catch (ArgumentException ex)
        {
            throw new XsqExportInvalidException(
                $"Effect '{xlightsEffectName}' on '{effect.Target.Value}' has an unverified setting: {ex.Message}; no file was written.");
        }
    }

    private static double TicksToMilliseconds(long ticks) => TimeSpan.FromTicks(ticks).TotalMilliseconds;

    private static long RoundToFrame(double ms) => (long)Math.Round(ms / FrameIntervalMs, MidpointRounding.AwayFromZero) * FrameIntervalMs;
}
