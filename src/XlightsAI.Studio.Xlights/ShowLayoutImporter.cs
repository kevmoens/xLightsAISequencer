using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Default <see cref="IShowLayoutImporter"/>. Parses <c>xlights_rgbeffects.xml</c>'s
/// <c>&lt;models&gt;</c>/<c>&lt;modelGroups&gt;</c> elements into a normalized <see cref="DisplayGraph"/>.
///
/// PHASE-02 Section 2 ("Import xlights_rgbeffects.xml as untrusted external XML with limits on
/// DTD/entity expansion, path traversal and large documents"): the document is never read via
/// <see cref="Directory"/>/<see cref="Path"/> traversal beyond the single expected filename under
/// the caller-supplied directory, DTDs are outright prohibited (also closes the XXE/external-entity
/// vector — there is no entity to expand once no DOCTYPE is permitted at all), and the document is
/// rejected outright past <see cref="MaxDocumentCharacters"/>.
///
/// PHASE-02 Step 011 migration (accepted; see <c>implementation-evidence/PHASE-02/STEP-11.md</c>
/// §9a/§9b): the real xLights C++ source (<c>src-core/XmlSerializer/XmlNodeKeys.h</c>,
/// <c>app-shell/TabSequence.cpp</c>) writes and validates this file's root element as <c>xrgb</c>
/// (with a legacy <c>xlights</c> root also accepted by xLights itself) — <c>xrgbEffects</c>, this
/// class's original root-element assumption, never appears anywhere in the real xLights codebase and
/// was an unverified guess. <see cref="ParseModels"/> now accepts either real root name. Separately, a
/// real user-owned show file was found to carry a stray, non-xLights-written <c>&lt;!DOCTYPE html&gt;</c>
/// declaration (confirmed absent from xLights's own writer); <see cref="StripBenignDoctypeIfPresent"/>
/// tolerates exactly that narrow shape (no internal subset, no external reference) by removing it
/// before parsing, while anything carrying an internal subset or a SYSTEM/PUBLIC external reference —
/// the actual XXE vectors — is still rejected outright, never evaluated.
/// </summary>
public sealed partial class ShowLayoutImporter : IShowLayoutImporter
{
    /// <summary>
    /// Safety bound on total parsed characters (PHASE-02 Section 2: "limits on... large documents").
    /// No real xLights-authored <c>xlights_rgbeffects.xml</c> approaches this size; a file beyond it
    /// is rejected rather than fully buffered/parsed.
    /// </summary>
    internal const long MaxDocumentCharacters = 5_000_000;

    /// <summary>
    /// The real root element names xLights itself reads/writes for this file (PHASE-02 Step 011
    /// migration): current (<c>xrgb</c>) and legacy (<c>xlights</c>), confirmed against the real
    /// xLights source (<c>XmlNodeKeys.h</c>; <c>TabSequence.cpp</c>'s own
    /// <c>root.name() != "xrgb"</c> validation; <c>iPadRenderContext.cpp</c>'s <c>xrgb</c>-then-
    /// <c>xlights</c> fallback). Comparison is ordinal/case-sensitive, matching xLights's own
    /// case-sensitive check.
    /// </summary>
    private static readonly string[] AcceptedRootElementNames = ["xrgb", "xlights"];

    /// <summary>Bound on how far into the document a DOCTYPE is looked for (PHASE-02 Step 011 migration): it can only legally appear in the prolog, so a small fixed window is sufficient and keeps the scan cheap.</summary>
    private const int DoctypeScanWindowChars = 4096;

    public async Task<DisplayGraph> ImportReadOnlyAsync(string showDirectory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(showDirectory);

        ct.ThrowIfCancellationRequested();

        var fullDirectory = Path.GetFullPath(showDirectory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new DirectoryNotFoundException($"Show directory '{fullDirectory}' does not exist.");
        }

        var rgbEffectsPath = Path.Combine(fullDirectory, ShowDirectoryFileNames.RgbEffects);
        if (!File.Exists(rgbEffectsPath))
        {
            throw new FileNotFoundException(
                $"Required file '{ShowDirectoryFileNames.RgbEffects}' was not found under '{fullDirectory}'.",
                rgbEffectsPath);
        }

        byte[] bytes;
        await using (var fileStream = new FileStream(
            rgbEffectsPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
        {
            using var buffer = new MemoryStream();
            await fileStream.CopyToAsync(buffer, ct).ConfigureAwait(false);
            bytes = buffer.ToArray();
        }

        var sourceLayoutHash = Convert.ToHexString(SHA256.HashData(bytes));

        // The hash above is always computed from the real, original bytes (identifies *which*
        // on-disk file this is); only the bytes actually fed to the parser may be adjusted below.
        var parseableBytes = StripBenignDoctypeIfPresent(bytes, rgbEffectsPath);
        var document = ParseSafely(parseableBytes, rgbEffectsPath);
        var (models, geometryWarnings) = ParseModels(document, rgbEffectsPath);
        var (groups, warnings) = ParseGroupsAndValidateReferences(document, models, rgbEffectsPath);

        return new DisplayGraph(sourceLayoutHash, models, groups, [.. geometryWarnings, .. warnings]);
    }

    /// <summary>
    /// PHASE-02 Step 011 migration: tolerates a benign, prolog-area DOCTYPE with no internal subset
    /// and no SYSTEM/PUBLIC external reference (confirmed absent from real xLights's own writer, but
    /// observed in a real user's show file — almost certainly an editor/tool artifact) by removing it
    /// before the bytes reach the hardened XML reader, which keeps rejecting DTDs outright
    /// (<see cref="XmlReaderSettings.DtdProcessing"/> stays <see cref="DtdProcessing.Prohibit"/>
    /// unconditionally). A DOCTYPE carrying an internal subset (<c>[...]</c>, where entity
    /// declarations live) or any <c>SYSTEM</c>/<c>PUBLIC</c> keyword — the actual XXE vectors — is
    /// rejected immediately here, before any further parsing is attempted.
    /// </summary>
    private static byte[] StripBenignDoctypeIfPresent(byte[] bytes, string sourcePath)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var scanLength = Math.Min(text.Length, DoctypeScanWindowChars);
        var doctypeStart = text.IndexOf("<!DOCTYPE", 0, scanLength, StringComparison.Ordinal);
        if (doctypeStart < 0)
        {
            return bytes;
        }

        var windowEnd = Math.Min(text.Length, doctypeStart + DoctypeScanWindowChars);
        var declaration = text[doctypeStart..windowEnd];
        var closeIndex = declaration.IndexOf('>');
        var bracketIndex = declaration.IndexOf('[');
        var hasInternalSubset = bracketIndex >= 0 && (closeIndex < 0 || bracketIndex < closeIndex);
        var hasExternalReference =
            declaration.Contains("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
            declaration.Contains("PUBLIC", StringComparison.OrdinalIgnoreCase);

        if (closeIndex < 0 || hasInternalSubset || hasExternalReference)
        {
            throw new ShowLayoutMalformedException(
                $"'{sourcePath}' declares a DOCTYPE with an internal subset or an external SYSTEM/PUBLIC reference, which is never permitted (XXE protection).");
        }

        var doctypeEnd = doctypeStart + closeIndex + 1;
        var stripped = text.Remove(doctypeStart, doctypeEnd - doctypeStart);
        return Encoding.UTF8.GetBytes(stripped);
    }

    private static XDocument ParseSafely(byte[] bytes, string sourcePath)
    {
        using var byteStream = new MemoryStream(bytes);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, // no DOCTYPE at all: closes the XXE/entity-expansion vector outright.
            XmlResolver = null, // never resolve external DTD/entity/schema references over the network or filesystem.
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = MaxDocumentCharacters,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        try
        {
            using var reader = XmlReader.Create(byteStream, settings);
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new ShowLayoutMalformedException(
                $"'{sourcePath}' is not a safely parseable XML document: {ex.Message}", ex);
        }
    }

    private static (IReadOnlyList<DisplayModel> Models, IReadOnlyList<string> Warnings) ParseModels(XDocument document, string sourcePath)
    {
        var root = document.Root;
        if (root is null || !AcceptedRootElementNames.Contains(root.Name.LocalName, StringComparer.Ordinal))
        {
            throw new ShowLayoutMalformedException(
                $"'{sourcePath}' is not a recognized xLights rgbeffects document (expected root element 'xrgb', or legacy 'xlights'; found '{root?.Name.LocalName}').");
        }

        // Group membership first, by raw model name, so it can be looked up per model below without
        // flattening/deduplicating overlapping membership (PHASE-02 Section 3: "Groups may overlap;
        // do not flatten user group membership").
        var groupsByModelName = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var groupElement in root.Element("modelGroups")?.Elements("modelGroup") ?? Enumerable.Empty<XElement>())
        {
            var groupName = RequireAttribute(groupElement, "name", sourcePath, "modelGroup");
            var memberNames = (string?)groupElement.Attribute("models") ?? string.Empty;
            foreach (var rawMemberName in memberNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!groupsByModelName.TryGetValue(rawMemberName, out var groupsForModel))
                {
                    groupsForModel = [];
                    groupsByModelName[rawMemberName] = groupsForModel;
                }

                groupsForModel.Add(groupName);
            }
        }

        var models = new List<DisplayModel>();
        var warnings = new List<string>();
        foreach (var modelElement in root.Element("models")?.Elements("model") ?? Enumerable.Empty<XElement>())
        {
            var name = RequireAttribute(modelElement, "name", sourcePath, "model").Trim();
            var kind = (string?)modelElement.Attribute("DisplayAs") ?? string.Empty;
            var groupNames = groupsByModelName.TryGetValue(name, out var found)
                ? (IReadOnlyList<string>)found
                : [];

            var category = ModelGeometryCalculator.NormalizeCategory(kind);
            var stringType = (string?)modelElement.Attribute("StringType") ?? string.Empty;
            // Confirmed against a real, currently-installed xLights version's own export (SourceVersion
            // "2026.17"): ordinary model families (Single Line, Star, Tree, Matrix, ...) no longer emit
            // parm1/parm2/parm3 at all -- they use the equivalent, unambiguously-named NumStrings/
            // NodesPerString/StrandsPerString instead, which silently produced a zero node count for
            // every model in a real show (parm1/parm2 defaulting to 0 when absent) before this fix.
            // NumStrings/NodesPerString/StrandsPerString are the SAME quantities parm1/parm2/parm3 name
            // (confirmed by ModelGeometryCalculator's own cross-checked formula: parm1=string count,
            // parm2=nodes/string) -- preferred when present, falling back to parm1/parm2/parm3 for an
            // older export that still uses them.
            // Real-run finding (2026-11-01): the "Arches" family (ModelCategory.Arches) carries its own
            // differently-named pair -- NumArches/NodesPerArch -- instead of NumStrings/NodesPerString
            // (confirmed against a real show's own SourceVersion "2026.17" export: an Arches model has
            // neither NumStrings/NodesPerString nor parm1/parm2 at all). Without this, every Arches model
            // silently computed to 0 nodes, which this importer (and everything downstream) correctly
            // treats as "no real pixels to address" -- so the model never reached the compiled show at
            // all, not merely "received no effects." Checked unconditionally (not gated on category),
            // same as NumStrings/NodesPerString above: these names are specific enough to the Arches
            // family that they are simply absent on every other model, so this never misfires.
            var parm1 = ParseOptionalInt(modelElement, "NumStrings") ?? ParseOptionalInt(modelElement, "NumArches") ?? ParseOptionalInt(modelElement, "parm1") ?? 0;
            var parm2 = ParseOptionalInt(modelElement, "NodesPerString") ?? ParseOptionalInt(modelElement, "NodesPerArch") ?? ParseOptionalInt(modelElement, "parm2") ?? 0;
            var parm3 = ParseOptionalInt(modelElement, "StrandsPerString") ?? ParseOptionalInt(modelElement, "parm3") ?? 0;
            var windowRows = ParseOptionalInt(modelElement, "WindowRows");
            var windowColumns = ParseOptionalInt(modelElement, "WindowColumns");
            var nodeCount = ModelGeometryCalculator.ComputeNodeCount(category, parm1, parm2, windowRows, windowColumns);

            // Cross-check (PHASE-1a task 5, "a verified pixel table... cross-checked against the
            // XML's own node count attributes"): multi-string models (Matrix family) additionally
            // enumerate one `StringN` connection attribute per physical string. When present, the
            // count of those attributes is an independent confirmation of `parm1` straight from the
            // XML itself -- a mismatch is reported as a non-fatal warning rather than silently trusted
            // or silently overridden.
            var stringAttributeCount = modelElement.Attributes()
                .Count(a => StringNAttributeRegex().IsMatch(a.Name.LocalName));
            if (stringAttributeCount > 0 && stringAttributeCount != parm1)
            {
                warnings.Add(
                    $"Model '{name}' declares parm1={parm1} but has {stringAttributeCount} 'StringN' connection attributes; node count may be inaccurate.");
            }

            var bounds = ParseBounds(modelElement);
            var submodels = ParseSubmodels(modelElement);

            models.Add(new DisplayModel(
                new ModelId(name),
                name,
                kind,
                groupNames,
                ParsePreviewPoints(modelElement),
                category,
                stringType,
                parm1,
                parm2,
                parm3,
                windowRows,
                windowColumns,
                nodeCount,
                bounds,
                submodels));
        }

        return (models, warnings);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^String\d+$")]
    private static partial System.Text.RegularExpressions.Regex StringNAttributeRegex();

    private static int? ParseOptionalInt(XElement element, string attributeName)
    {
        var text = (string?)element.Attribute(attributeName);
        return text is not null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// PHASE-1a task 3: the model's full real-world transform exactly as xLights stores it. Returns
    /// <see langword="null"/> only when the source carries none of these attributes at all (never
    /// invents a partial/default transform for a model the XML genuinely has no geometry for).
    /// </summary>
    private static DisplayBounds? ParseBounds(XElement modelElement)
    {
        var x = ParseOptionalDouble(modelElement, "WorldPosX");
        var y = ParseOptionalDouble(modelElement, "WorldPosY");
        if (x is null || y is null)
        {
            return null;
        }

        return new DisplayBounds(
            x.Value,
            y.Value,
            ParseOptionalDouble(modelElement, "WorldPosZ") ?? 0,
            ParseOptionalDouble(modelElement, "ScaleX"),
            ParseOptionalDouble(modelElement, "ScaleY"),
            ParseOptionalDouble(modelElement, "ScaleZ"),
            ParseOptionalDouble(modelElement, "RotateX"),
            ParseOptionalDouble(modelElement, "RotateY"),
            ParseOptionalDouble(modelElement, "RotateZ"),
            ParseOptionalDouble(modelElement, "X2"),
            ParseOptionalDouble(modelElement, "Y2"),
            ParseOptionalDouble(modelElement, "Z2"));
    }

    private static double? ParseOptionalDouble(XElement element, string attributeName)
    {
        var text = (string?)element.Attribute(attributeName);
        return text is not null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// PHASE-1a task 4: enumerates every <c>&lt;subModel&gt;</c> child as its own addressable target.
    /// A submodel's own pixel count is only ever derived from its <c>layout</c> attribute (the comma-
    /// separated node-index list xLights itself writes) when present and non-trivial; otherwise
    /// <see cref="DisplaySubmodel.NodeCount"/> is left <see langword="null"/> rather than guessed.
    /// </summary>
    private static IReadOnlyList<DisplaySubmodel> ParseSubmodels(XElement modelElement)
    {
        var submodels = new List<DisplaySubmodel>();
        foreach (var subModelElement in modelElement.Elements("subModel"))
        {
            var name = (string?)subModelElement.Attribute("name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var layout = (string?)subModelElement.Attribute("layout");
            int? nodeCount = null;
            if (!string.IsNullOrWhiteSpace(layout))
            {
                var tokens = layout.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(t => t != "-1")
                    .ToList();
                nodeCount = tokens.Count > 0 ? tokens.Count : null;
            }

            submodels.Add(new DisplaySubmodel(name, nodeCount));
        }

        return submodels;
    }

    /// <summary>
    /// PHASE-02 Step 011 ("Validate referenced models, channel/network data where relevant and
    /// missing layout elements"; implementation guidance: "Identify targetable models versus
    /// groups/submodels, reject unresolved refs... Support nested/overlapping groups without
    /// flattening"): exposes every <c>&lt;modelGroup&gt;</c> as its own targetable entity with each
    /// raw member classified against this same document's real models and group names, and reports
    /// every finding as a non-destructive warning rather than failing the whole import (required
    /// acceptance: "Fixture with broken references generates warnings rather than partial destructive
    /// output"). A group's member list is never expanded/flattened — a nested group reference stays a
    /// reference (<see cref="DisplayGroupMemberKind.Group"/>), never substituted with its own members.
    /// </summary>
    private static (IReadOnlyList<DisplayModelGroup> Groups, IReadOnlyList<string> Warnings) ParseGroupsAndValidateReferences(
        XDocument document, IReadOnlyList<DisplayModel> models, string sourcePath)
    {
        var root = document.Root!; // ParseModels already validated root is non-null and named 'xrgb' (or legacy 'xlights').
        var warnings = new List<string>();

        if (root.Element("models") is null)
        {
            warnings.Add($"'{sourcePath}' has no '<models>' element.");
        }

        var modelGroupsElement = root.Element("modelGroups");
        if (modelGroupsElement is null)
        {
            warnings.Add($"'{sourcePath}' has no '<modelGroups>' element.");
        }

        var modelGroupElements = modelGroupsElement?.Elements("modelGroup").ToList() ?? [];

        var modelNames = new HashSet<string>(models.Select(m => m.XlightsName), StringComparer.Ordinal);
        var groupNames = new HashSet<string>(
            modelGroupElements.Select(g => RequireAttribute(g, "name", sourcePath, "modelGroup")),
            StringComparer.Ordinal);

        var groups = new List<DisplayModelGroup>();
        foreach (var groupElement in modelGroupElements)
        {
            var groupName = RequireAttribute(groupElement, "name", sourcePath, "modelGroup");
            var memberNames = (string?)groupElement.Attribute("models") ?? string.Empty;

            var members = new List<DisplayGroupMember>();
            foreach (var rawMemberName in memberNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kind = ClassifyGroupMember(rawMemberName, modelNames, groupNames);
                if (kind == DisplayGroupMemberKind.Unresolved)
                {
                    warnings.Add($"Model group '{groupName}' references unresolved target '{rawMemberName}'.");
                }

                members.Add(new DisplayGroupMember(rawMemberName, kind));
            }

            groups.Add(new DisplayModelGroup(groupName, members));
        }

        return (groups, warnings);
    }

    /// <summary>
    /// Classifies one raw <c>&lt;modelGroup&gt;</c> member entry: a real model name
    /// (<see cref="DisplayGroupMemberKind.Model"/>), another group's own name — a nested group
    /// reference (<see cref="DisplayGroupMemberKind.Group"/>), the xLights <c>ModelName/SubModelName</c>
    /// shape whose model half resolves (<see cref="DisplayGroupMemberKind.Submodel"/> — the submodel
    /// half itself is not independently verifiable from this document), or otherwise
    /// <see cref="DisplayGroupMemberKind.Unresolved"/> — a broken reference.
    /// </summary>
    private static DisplayGroupMemberKind ClassifyGroupMember(string rawName, HashSet<string> modelNames, HashSet<string> groupNames)
    {
        if (modelNames.Contains(rawName))
        {
            return DisplayGroupMemberKind.Model;
        }

        if (groupNames.Contains(rawName))
        {
            return DisplayGroupMemberKind.Group;
        }

        var slashIndex = rawName.IndexOf('/');
        if (slashIndex > 0 && modelNames.Contains(rawName[..slashIndex]))
        {
            return DisplayGroupMemberKind.Submodel;
        }

        return DisplayGroupMemberKind.Unresolved;
    }

    /// <summary>
    /// Extracts the model's own world-position geometry when the source XML actually carries it
    /// (both <c>WorldPosX</c> and <c>WorldPosY</c> attributes present and numeric); otherwise returns
    /// an empty list rather than inventing or inferring a position (PHASE-02 Section 3: "geometry
    /// where present"; "Avoid guessing total pixel counts or channel mappings from screen coordinates.").
    /// </summary>
    private static IReadOnlyList<Point2> ParsePreviewPoints(XElement modelElement)
    {
        var xAttr = (string?)modelElement.Attribute("WorldPosX");
        var yAttr = (string?)modelElement.Attribute("WorldPosY");
        if (xAttr is null || yAttr is null)
        {
            return [];
        }

        if (!double.TryParse(xAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(yAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            return [];
        }

        return [new Point2(x, y)];
    }

    private static string RequireAttribute(XElement element, string attributeName, string sourcePath, string elementDescription)
    {
        var value = (string?)element.Attribute(attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ShowLayoutMalformedException(
                $"'{sourcePath}' contains a '{elementDescription}' element missing its required '{attributeName}' attribute.");
        }

        return value;
    }
}
