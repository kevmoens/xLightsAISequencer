namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// Build-plan Phase 11 §2a/§1a: the real configured endpoint from the incident this phase fixes --
/// <c>https://&lt;resource&gt;.openai.azure.com/openai/responses?api-version=2025-04-01-preview</c> -- traced
/// to a request that actually went to <c>https://&lt;resource&gt;.openai.azure.com/openai/v1/chat/completions</c>,
/// silently dropping both the <c>/openai/responses</c> path and the <c>api-version</c> query string. These
/// tests pin that bug (against <see cref="ProviderEndpointUri.Build"/>, unchanged -- it still has no entry
/// for this shape) and then prove the fix: the new recognizer/builder resolves the exact same configured
/// string to the correct, verbatim Responses API URL instead.
/// </summary>
public sealed class ProviderEndpointUriTests
{
    private const string RealIncidentEndpoint = "https://my-resource.openai.azure.com/openai/responses?api-version=2025-04-01-preview";

    [Fact]
    public void Build_RealIncidentEndpoint_ReproducesTheBug_SilentlyDropsPathAndApiVersion()
    {
        // ProviderEndpointUri.Build itself is deliberately left unchanged -- the fix is routing a
        // Responses API endpoint around it entirely (see IsResponsesApiEndpoint/BuildResponses below and
        // ChatCompletionClientFactory), never trying to make Build itself understand every API shape.
        var uri = ProviderEndpointUri.Build(RealIncidentEndpoint, "v1/chat/completions");

        Assert.Equal("https://my-resource.openai.azure.com/openai/v1/chat/completions", uri.ToString());
    }

    [Fact]
    public void IsResponsesApiEndpoint_RealIncidentEndpoint_RecognizedAsResponsesApiShape()
    {
        Assert.True(ProviderEndpointUri.IsResponsesApiEndpoint(RealIncidentEndpoint));
    }

    [Fact]
    public void BuildResponses_RealIncidentEndpoint_PreservesPathAndApiVersionQueryStringVerbatim()
    {
        var uri = ProviderEndpointUri.BuildResponses(RealIncidentEndpoint);

        Assert.Equal(RealIncidentEndpoint, uri.ToString());
    }

    [Theory]
    [InlineData("https://api.openai.com")]
    [InlineData("https://api.anthropic.com")]
    [InlineData("https://api.anthropic.com/v1/messages")]
    [InlineData("https://my-resource.openai.azure.com")]
    [InlineData("http://127.0.0.1:11434")]
    public void IsResponsesApiEndpoint_NonResponsesShapes_NotRecognized(string endpoint)
    {
        Assert.False(ProviderEndpointUri.IsResponsesApiEndpoint(endpoint));
    }

    [Fact]
    public void IsResponsesApiEndpoint_TrailingSlashBeforeQueryString_StillRecognized()
    {
        Assert.True(ProviderEndpointUri.IsResponsesApiEndpoint("https://my-resource.openai.azure.com/openai/responses/?api-version=2025-04-01-preview"));
    }
}
