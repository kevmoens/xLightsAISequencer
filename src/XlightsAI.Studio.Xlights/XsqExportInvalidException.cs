namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Thrown by <see cref="IXsqExporter"/> when a requested export is malformed or unsafe: a blank/
/// non-<c>.xsq</c>/already-existing output path, a blank target xLights version, an effect whose
/// target does not resolve against the supplied <see cref="DisplayGraph"/>, or an effect whose
/// recipe id this exporter does not recognize. Always thrown before any file is created or written —
/// PHASE-02 Step 013's required failure path: "Invalid model/format/version must fail safely without
/// rewriting source files."
/// </summary>
public sealed class XsqExportInvalidException : Exception
{
    public XsqExportInvalidException(string message)
        : base(message)
    {
    }
}
