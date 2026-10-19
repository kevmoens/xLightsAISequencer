namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 3 proposed contract: serializes an already-accepted <see cref="SequenceDocument"/>
/// against its originating <see cref="DisplayGraph"/> into a new, editable xLights <c>.xsq</c> XML
/// file. Never overwrites an existing file (Section 2: "Never overwrite source show files silently" —
/// applied here to any pre-existing export target too) and never invents an xLights effect parameter
/// this build has not explicitly, deliberately chosen to emit (Section 2: "do not invent xLights
/// effect parameters").
/// </summary>
public interface IXsqExporter
{
    /// <exception cref="XsqExportInvalidException">
    /// <paramref name="outputPath"/> is blank, does not end in <c>.xsq</c>, or already exists;
    /// <paramref name="targetXlightsVersion"/> is blank; or <paramref name="sequence"/> contains an
    /// effect whose <see cref="EffectInstance.Target"/> does not resolve against
    /// <paramref name="layout"/>, or whose <see cref="EffectInstance.RecipeId"/> this exporter does
    /// not know how to translate into xLights XML. No file is ever written when this is thrown.
    /// </exception>
    /// <remarks>
    /// PHASE-02 Step 013's own original method; unchanged. Exactly equivalent to calling
    /// <see cref="ExportAsync"/> with <see cref="XsqExportPolicy.RejectIfExists"/> — kept as its own
    /// method (rather than removed in favor of a required-policy overload) so every existing Step
    /// 009-013 call site keeps compiling and behaving identically.
    /// </remarks>
    Task<ExportResult> ExportNewAsync(
        DisplayGraph layout,
        SequenceDocument sequence,
        string outputPath,
        string targetXlightsVersion,
        CancellationToken ct);

    /// <summary>
    /// PHASE-02 Step 015 ("Implement atomic export, filename safety, backups..."): the same export as
    /// <see cref="ExportNewAsync"/>, but with an explicit, caller-chosen <paramref name="policy"/> for
    /// how to resolve an already-existing <paramref name="outputPath"/> — never a silent overwrite.
    /// </summary>
    /// <exception cref="XsqExportInvalidException">
    /// Same conditions as <see cref="ExportNewAsync"/>; additionally, for
    /// <see cref="XsqExportPolicy.RejectIfExists"/>, an already-existing <paramref name="outputPath"/>.
    /// No file is ever written, overwritten, or backed up when this is thrown.
    /// </exception>
    Task<ExportResult> ExportAsync(
        DisplayGraph layout,
        SequenceDocument sequence,
        string outputPath,
        string targetXlightsVersion,
        XsqExportPolicy policy,
        CancellationToken ct);
}
