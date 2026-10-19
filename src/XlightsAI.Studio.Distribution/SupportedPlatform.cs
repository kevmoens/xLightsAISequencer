namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Step 097 ("Add supported macOS/Linux deliverables only after validating inference and xLights
/// compatibility"). One advertisable release target: a runtime identifier a real <c>dotnet publish -r
/// &lt;RuntimeIdentifier&gt; --self-contained true</c> can actually produce, the host-OS substring/architecture
/// <see cref="RuntimeCapability"/> must match before this platform's own apphost can be executed on the current
/// machine, the real apphost file name that publish produces for this platform, and the real native asset file
/// names a genuine publish of <c>XlightsAI.Studio.App</c> was actually observed to bundle for this platform
/// (see <see cref="KnownPlatforms"/> for the real, inspected values -- never invented names).
/// </summary>
public sealed record SupportedPlatform(
    string RuntimeIdentifier,
    string OperatingSystemSubstring,
    string Architecture,
    string ApphostFileName,
    IReadOnlyList<string> RequiredNativeAssetFileNames);
