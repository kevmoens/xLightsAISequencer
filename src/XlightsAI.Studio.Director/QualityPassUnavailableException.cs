namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" Phase 3 (2026-11-02): thrown when
/// <see cref="CustomGenerationPrompt.EnableQualityPass"/> was requested but the real render pipeline it
/// needs (<see cref="Rendering.RenderPipeline"/>, via an actual xLights install) has no configured
/// executable/show directory to run against -- the same "disclosed, actionable, never a silent no-op"
/// convention as <see cref="AiProviderNotConfiguredException"/>, not a generic exception.
/// </summary>
public sealed class QualityPassUnavailableException : Exception
{
    public QualityPassUnavailableException()
        : base("Quality pass was requested, but no xLights executable/show directory is configured to render against. Configure the xLights executable path (Generate & export screen, \"Open in xLights\" section) or turn off the quality pass toggle on the Creative direction screen.")
    {
    }
}
