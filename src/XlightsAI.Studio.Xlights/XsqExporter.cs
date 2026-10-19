using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Default <see cref="IXsqExporter"/>. PHASE-02 Step 013 ("Port minimal editable XSQ writer and
/// required sequencing metadata"): serializes a validated <see cref="SequenceDocument"/> into the
/// minimal xLights <c>.xsq</c> shape this phase's own appendix documents (Section 12: <c>head</c>,
/// <c>ColorPalettes</c>, <c>EffectDB</c>, <c>DisplayElements</c>, <c>ElementEffects</c>), built with
/// <see cref="XDocument"/> (never string concatenation, per Section 12: "Use DOM/XDocument serializer
/// rather than string concatenation") so every name/value is correctly XML-escaped automatically.
///
/// <b>Deliberately does not invent xLights effect-settings parameters this build has not verified.</b>
/// No installed xLights build has been available to any PHASE-02 step so far (carried forward from
/// Steps 009-012's own disclosed gap), and the upstream pinned reference
/// (<c>reference/xlights-autosequencer/src/generator/xsq_writer.py</c>) never emits an "On" effect at
/// all — there is no verified default-parameter table for it anywhere this codebase can inspect.
/// Rather than guess plausible-looking <c>E_*</c> keys (explicitly forbidden, Section 2: "do not
/// invent xLights effect parameters"), the "On" recipe's EffectDB entry is written with an empty
/// settings string and every export carries an explicit <see cref="ExportResult.Warnings"/> entry
/// disclosing this — xLights is documented to apply its own built-in effect defaults when a settings
/// key is absent, but that has not been independently confirmed here. Capturing a real xLights-saved
/// "On" effect and replacing this placeholder is Section 12's own "golden fixture process", blocked on
/// the same missing xLights install as Step 014.
/// </summary>
public sealed class XsqExporter : IXsqExporter
{
    /// <summary>xLights' documented sequence frame grid (PHASE-02 Section 12 appendix; upstream pinned reference's own <c>FRAME_INTERVAL_MS</c>). Every effect's start/end time is rounded to this grid before being written.</summary>
    public const int FrameIntervalMs = 25;

    private const string OnEffectXlightsName = "On";

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

        var warnings = new List<string>
        {
            "The 'effect.on.v1' recipe's EffectDB settings are an empty placeholder: no real " +
            "xLights-authored 'On' effect sample has been captured yet to verify its actual default " +
            "parameters against (no xLights install is available in this environment). Do not treat " +
            "this export as proof the 'On' effect renders correctly until Step 014/015's golden " +
            "fixture process captures and substitutes a real, verified settings string.",
        };

        // Resolve each effect's target and recipe before building any XML — an export never
        // produces a partial file for a request it is about to reject (Step 013's required failure
        // path: "Invalid model/format/version must fail safely without rewriting source files").
        var elementType = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var effect in sequence.Effects)
        {
            var targetName = effect.Target.Value;
            if (modelNames.Contains(targetName))
            {
                elementType[targetName] = "model";
            }
            else if (groupNames.Contains(targetName))
            {
                elementType[targetName] = "modelGroup";
            }
            else
            {
                throw new XsqExportInvalidException(
                    $"Effect target '{targetName}' does not resolve to any model or model group in the supplied layout; no file was written.");
            }

            if (effect.RecipeId != EffectRecipeRegistry.SimpleOnRecipeId)
            {
                throw new XsqExportInvalidException(
                    $"Unknown effect recipe id '{effect.RecipeId}'. This exporter does not invent xLights XML for an unverified recipe; no file was written.");
            }
        }

        // Single shared palette and single shared EffectDB entry: every effect in this step is the
        // same verified recipe ("effect.on.v1" -> xLights "On"), so both dedup to one entry each —
        // this also means every <Effect ref="0" .../> below is guaranteed non-dangling by
        // construction, not merely by later inspection.
        const string palette = "C_BUTTON_Palette1=#FFFFFF";
        const string effectDbEntry = ""; // see class doc: no verified "On" parameter set exists yet.

        var maxEndTicks = sequence.Effects.Count > 0 ? sequence.Effects.Max(e => e.EndTicks) : 0;
        var sequenceDurationMs = RoundToFrame(TicksToMilliseconds(maxEndTicks));

        var root = new XElement(
            "xsequence",
            new XAttribute("BaseChannel", "0"),
            new XAttribute("FixedPointTiming", FrameIntervalMs.ToString()),
            new XAttribute("ModelBlending", "true"));

        root.Add(new XElement(
            "head",
            new XElement("version", targetXlightsVersion.Trim()),
            new XElement("sequenceType", "Animation"),
            new XElement("sequenceTiming", $"{FrameIntervalMs} ms"),
            new XElement("sequenceDuration", (sequenceDurationMs / 1000.0).ToString("0.000"))));

        root.Add(new XElement("ColorPalettes", new XElement("ColorPalette", palette)));
        root.Add(new XElement("EffectDB", new XElement("Effect", effectDbEntry)));

        // DisplayElements: one entry per distinct target, in first-appearance order (Section 3
        // sample shape: <Element type="model" name="..." visible="1" />).
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
                    new XAttribute("type", elementType[effect.Target.Value]),
                    new XAttribute("name", effect.Target.Value),
                    new XAttribute("visible", "1")));
            }
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
                new XAttribute("type", elementType[targetName]),
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

                    layerElement.Add(new XElement(
                        "Effect",
                        new XAttribute("ref", "0"),
                        new XAttribute("name", OnEffectXlightsName),
                        new XAttribute("startTime", startMs.ToString()),
                        new XAttribute("endTime", endMs.ToString()),
                        new XAttribute("palette", "0"),
                        new XAttribute("selected", "0")));

                    previousEndMs = endMs;
                }

                targetElement.Add(layerElement);
            }

            elementEffects.Add(targetElement);
        }

        root.Add(elementEffects);

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

    private static double TicksToMilliseconds(long ticks) => TimeSpan.FromTicks(ticks).TotalMilliseconds;

    private static long RoundToFrame(double ms) => (long)Math.Round(ms / FrameIntervalMs, MidpointRounding.AwayFromZero) * FrameIntervalMs;
}
