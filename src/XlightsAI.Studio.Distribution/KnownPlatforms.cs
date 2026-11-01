namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Step 097. The three <see cref="SupportedPlatform"/> definitions this step actually validated by
/// running a real <c>dotnet publish -r &lt;rid&gt; --self-contained true</c> of <c>XlightsAI.Studio.App</c> and
/// inspecting its real on-disk output (recorded in <c>implementation-evidence/PHASE-15/STEP-097.md</c>
/// §3.4/§5) -- every native asset file name below was actually observed in that output, never guessed:
///
/// - <b>win-x64</b>: apphost <c>XlightsAI.Studio.exe</c>; bundles <c>libSkiaSharp.dll</c> (Skia rendering) and
///   <c>av_libglesv2.dll</c> (ANGLE GL ES, Avalonia's Windows rendering backend).
/// - <b>osx-x64</b>: apphost <c>XlightsAI.Studio</c> (no extension); bundles <c>libSkiaSharp.dylib</c> and
///   <c>libAvaloniaNative.dylib</c> (Avalonia's native macOS windowing shim -- has no equivalent on the other
///   two platforms).
/// - <b>linux-x64</b>: apphost <c>XlightsAI.Studio</c> (no extension); bundles <c>libSkiaSharp.so</c> only --
///   a real self-contained <c>linux-x64</c> publish of this exact application was observed to bundle *no*
///   native windowing shim at all (Avalonia's Linux/X11 backend is managed-code only), so no such file is
///   required here; this asymmetry with macOS is a genuine, disclosed finding (see evidence §7), not an
///   invented requirement.
/// </summary>
public static class KnownPlatforms
{
    public static readonly SupportedPlatform WindowsX64 = new(
        RuntimeIdentifier: "win-x64",
        OperatingSystemSubstring: "Windows",
        Architecture: "X64",
        ApphostFileName: "XlightsAI.Studio.exe",
        RequiredNativeAssetFileNames: ["XlightsAI.Studio.exe", "libSkiaSharp.dll", "av_libglesv2.dll"]);

    public static readonly SupportedPlatform MacOsX64 = new(
        RuntimeIdentifier: "osx-x64",
        OperatingSystemSubstring: "Darwin",
        Architecture: "X64",
        ApphostFileName: "XlightsAI.Studio",
        RequiredNativeAssetFileNames: ["XlightsAI.Studio", "libSkiaSharp.dylib", "libAvaloniaNative.dylib"]);

    public static readonly SupportedPlatform MacOsArm64 = new(
        RuntimeIdentifier: "osx-arm64",
        OperatingSystemSubstring: "Darwin",
        Architecture: "Arm64",
        ApphostFileName: "XlightsAI.Studio",
        RequiredNativeAssetFileNames: ["XlightsAI.Studio", "libSkiaSharp.dylib", "libAvaloniaNative.dylib"]);

    public static readonly SupportedPlatform LinuxX64 = new(
        RuntimeIdentifier: "linux-x64",
        OperatingSystemSubstring: "Linux",
        Architecture: "X64",
        ApphostFileName: "XlightsAI.Studio",
        RequiredNativeAssetFileNames: ["XlightsAI.Studio", "libSkiaSharp.so"]);

    public static readonly IReadOnlyList<SupportedPlatform> All = [WindowsX64, MacOsX64, MacOsArm64, LinuxX64];
}
