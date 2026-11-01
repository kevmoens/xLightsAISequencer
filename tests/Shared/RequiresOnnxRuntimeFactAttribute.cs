using System.Runtime.InteropServices;

namespace XlightsAI.Studio.Tests.Shared;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that load the ONNX Runtime native library. Microsoft.ML.OnnxRuntime
/// ships no native binaries for Intel Macs (osx-x64), so these tests are skipped there.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresOnnxRuntimeFactAttribute : FactAttribute
{
    public RequiresOnnxRuntimeFactAttribute()
    {
        if (OperatingSystem.IsMacOS() && RuntimeInformation.OSArchitecture == Architecture.X64)
        {
            Skip = "ONNX Runtime has no native build for Intel macOS.";
        }
    }
}
