using System.Text.Json;
using System.Text.Json.Serialization;

namespace XlightsAI.Studio.Director;

/// <summary>One structured call's full result -- the strictly-deserialized value plus the usage the underlying provider reported, so a caller can instrument cache behavior without re-parsing anything itself.</summary>
public sealed record StructuredAiCallResult<TResponse>(TResponse Value, AiUsage Usage, string RawJson, RequestDiagnostics? Diagnostics = null);

/// <summary>
/// Wraps an <see cref="IChatCompletionClient"/> with the same "strict schema deserialization" discipline
/// <c>XlightsAI.Studio.AiEdits.HttpStructuredOutputAiPlanner</c> already established
/// (<c>JsonUnmappedMemberHandling.Disallow</c> -- an LLM inventing an extra field is a rejection, never a
/// silently-ignored one) -- generalized here to any <typeparamref name="TResponse"/> shape rather than
/// one hardcoded envelope DTO.
/// </summary>
public sealed class StructuredJsonAiClient<TResponse>
{
    private static readonly JsonSerializerOptions ResponseOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = true,
        // Without this, an enum like SectionKind deserializes only from its raw numeric index (e.g. `4`
        // for Chorus) -- something no model has any realistic way to produce correctly from a prompt
        // without the schema spelling out every enum's own ordinal mapping. A real model (confirmed
        // against real Anthropic/Ollama calls) naturally writes the enum's own name ("Chorus") instead.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IChatCompletionClient _inner;

    public StructuredJsonAiClient(IChatCompletionClient inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <exception cref="AiCallRejectedException"><see cref="AiCallRejectionReason.MalformedJson"/> -- no JSON value found, or it does not deserialize into <typeparamref name="TResponse"/> (an unknown field, a type mismatch, or a missing required member).</exception>
    public async Task<StructuredAiCallResult<TResponse>> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        var result = await _inner.CompleteAsync(request, ct).ConfigureAwait(false);
        var json = StructuredJsonExtractor.ExtractJson(result.Text);

        TResponse? value;
        try
        {
            value = JsonSerializer.Deserialize<TResponse>(json, ResponseOptions);
        }
        catch (JsonException ex)
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, $"Model's JSON output did not deserialize into {typeof(TResponse).Name}: {ex.Message}", ex);
        }

        if (value is null)
        {
            throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, $"Model's JSON output deserialized to null for {typeof(TResponse).Name}.");
        }

        return new StructuredAiCallResult<TResponse>(value, result.Usage, json, result.Diagnostics);
    }
}
