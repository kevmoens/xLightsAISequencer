namespace XlightsAI.Studio.Tests.Shared;

/// <summary>A <see cref="FactAttribute"/> for tests that exercise Windows-only APIs (e.g. winmm); skipped elsewhere.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows-only test.";
        }
    }
}
