using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>Default <see cref="IScopedXsqExporter"/>. See the interface doc for its purpose.</summary>
public sealed class ScopedXsqExporter : IScopedXsqExporter
{
    private readonly IXsqExporter _exporter;
    private readonly IXsqStructuralReader _structuralReader;

    public ScopedXsqExporter(IXsqExporter exporter, IXsqStructuralReader structuralReader)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(structuralReader);
        _exporter = exporter;
        _structuralReader = structuralReader;
    }

    public async Task<ScopedExportResult> ExportScopedAsync(
        DisplayGraph layout,
        SequenceDocument before,
        SequenceDocument after,
        EditScope scope,
        string outputPath,
        string targetXlightsVersion,
        XsqExportPolicy policy,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(scope);

        ct.ThrowIfCancellationRequested();

        // Step 023's own gate, checked entirely in memory before IXsqExporter ever runs: every effect
        // this scope does NOT cover must be the exact same effect, byte-for-semantic-byte, in both
        // documents. This is deliberately computed from the domain objects themselves — not from
        // re-reading a previously exported file — so it works the very first time a document is ever
        // exported, with no pre-existing XSQ required.
        var beforeOutOfScope = before.Effects.Where(e => !IsInScope(e, scope)).ToDictionary(e => e.Id);
        var afterOutOfScope = after.Effects.Where(e => !IsInScope(e, scope)).ToDictionary(e => e.Id);

        if (!beforeOutOfScope.Keys.ToHashSet().SetEquals(afterOutOfScope.Keys))
        {
            throw new ScopedExportContractViolatedException(
                "The set of effects outside the permitted edit scope changed between 'before' and " +
                "'after' (an effect outside the scope was added, removed, or moved into/out of scope); " +
                "no file was written.");
        }

        var outOfScopeHashes = new Dictionary<Guid, string>();
        foreach (var (id, beforeEffect) in beforeOutOfScope)
        {
            var afterEffect = afterOutOfScope[id];
            var beforeHash = ComputeEffectHash(beforeEffect);
            var afterHash = ComputeEffectHash(afterEffect);
            if (!string.Equals(beforeHash, afterHash, StringComparison.Ordinal))
            {
                throw new ScopedExportContractViolatedException(
                    $"Effect '{id}' is outside the permitted edit scope but changed between 'before' " +
                    "and 'after'; a scoped export must never regenerate effects outside its own scope. " +
                    "No file was written.");
            }

            outOfScopeHashes[id] = afterHash;
        }

        // Only after every out-of-scope effect is verified unchanged does the real, already-validated
        // IXsqExporter run — it owns its own "Invalid model/format/version must fail safely without
        // rewriting source files" guarantee (PHASE-02 Step 013/015), reused here rather than duplicated.
        var exportResult = await _exporter.ExportAsync(layout, after, outputPath, targetXlightsVersion, policy, ct)
            .ConfigureAwait(false);

        // "Inspect XSQ after round-trip" (Section 6, Step 023 implementation guidance): read the real
        // bytes just written back with the independent structural reader and confirm every out-of-scope
        // effect's timing actually materialized unchanged in the file xLights would load — not merely
        // that the in-memory SequenceDocument looked right before export.
        var structuralDocument = _structuralReader.Read(exportResult.Path);
        VerifyOutOfScopeEffectsSurvivedExport(structuralDocument, afterOutOfScope.Values, outputPath);

        return new ScopedExportResult(exportResult, outOfScopeHashes);
    }

    private static bool IsInScope(EffectInstance effect, EditScope scope) =>
        scope.Models.Contains(effect.Target) && (scope.EffectIds is null || scope.EffectIds.Contains(effect.Id));

    /// <summary>
    /// Canonical, order-independent content hash of every field that identifies "the same effect,
    /// unchanged" — deliberately excludes nothing an edit command could plausibly touch
    /// (<see cref="EffectInstance.Target"/>, <see cref="EffectInstance.Layer"/>, timing, recipe
    /// identity, lock state, and every parameter key/value, sorted so dictionary enumeration order
    /// never affects the hash).
    /// </summary>
    private static string ComputeEffectHash(EffectInstance effect)
    {
        var sortedParameters = string.Join(
            ';',
            effect.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));

        var canonical = string.Join(
            '|',
            effect.Target.Value,
            effect.Layer.ToString(CultureInfo.InvariantCulture),
            effect.StartTicks.ToString(CultureInfo.InvariantCulture),
            effect.EndTicks.ToString(CultureInfo.InvariantCulture),
            effect.RecipeId,
            effect.RecipeVersion,
            effect.Locked.ToString(CultureInfo.InvariantCulture),
            sortedParameters);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Confirms every out-of-scope effect's own [start, end) ticks round to the exact start/end
    /// millisecond pair present in the real exported file's <c>&lt;ElementEffects&gt;</c> section for
    /// its target. Mirrors <c>XsqExporter</c>'s own frame-rounding convention
    /// (<see cref="XsqExporter.FrameIntervalMs"/>) deliberately rather than reaching into that class's
    /// private rounding helpers — Step 013's own exporter contract is treated as immutable here, not
    /// modified to expose new internals for this step's sake.
    /// </summary>
    private static void VerifyOutOfScopeEffectsSurvivedExport(
        XsqStructuralDocument structuralDocument, IEnumerable<EffectInstance> outOfScopeEffects, string outputPath)
    {
        var elementsByName = structuralDocument.ElementEffects.ToDictionary(e => e.Name, StringComparer.Ordinal);

        foreach (var effect in outOfScopeEffects)
        {
            if (!elementsByName.TryGetValue(effect.Target.Value, out var element))
            {
                throw new ScopedExportContractViolatedException(
                    $"'{outputPath}' has no <ElementEffects><Element name=\"{effect.Target.Value}\"> for an " +
                    "out-of-scope effect that was expected to survive this export unchanged.");
            }

            var expectedStartMs = RoundToFrame(TicksToMilliseconds(effect.StartTicks));
            var expectedEndMs = RoundToFrame(TicksToMilliseconds(effect.EndTicks));

            var survived = element.Effects.Any(p => p.StartTimeMs == expectedStartMs && p.EndTimeMs == expectedEndMs);
            if (!survived)
            {
                throw new ScopedExportContractViolatedException(
                    $"'{outputPath}' does not contain an unchanged [{expectedStartMs}, {expectedEndMs}) " +
                    $"effect placement on '{effect.Target.Value}'; the out-of-scope effect's timing did not " +
                    "survive the export round trip unchanged.");
            }
        }
    }

    private static double TicksToMilliseconds(long ticks) => TimeSpan.FromTicks(ticks).TotalMilliseconds;

    private static long RoundToFrame(double ms) =>
        (long)Math.Round(ms / XsqExporter.FrameIntervalMs, MidpointRounding.AwayFromZero) * XsqExporter.FrameIntervalMs;
}
