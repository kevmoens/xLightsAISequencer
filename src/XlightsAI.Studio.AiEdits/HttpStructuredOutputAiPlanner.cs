using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11 Step 075's own real <see cref="IOptionalAiPlanner"/>: one generic, Ollama-compatible
/// structured-JSON HTTP adapter, reused for both <see cref="AiProviderKind.OllamaLocal"/> and
/// <see cref="AiProviderKind.CloudByok"/> (Step heading, verbatim: "Add optional Ollama/cloud provider
/// behind opt-in structured-output adapter") -- both targets accept the exact same redacted request
/// envelope and return the exact same proposal envelope; only the endpoint and (for BYOK) an
/// <c>Authorization</c> header differ, which the caller supplies via <paramref name="apiKey"/> at
/// construction. Never constructed directly by application code for a disabled provider -- use
/// <see cref="OptionalAiPlannerFactory.Create"/>.
///
/// <para>
/// Section 2's own rule, verbatim: "Optional local/cloud LLM receives a redacted, least-privilege
/// context containing just selected props/time and available op schema" -- <see cref="BuildRedactedRequest"/>
/// sends only the selection's time window, model ids, lock flag, the closed <see cref="StructuredEditOperationKinds"/>
/// vocabulary and the instruction text itself; it never sends note/phrase/path ids, lyrics, or any other
/// document content Section 2 calls "untrusted data."
/// </para>
///
/// <para>
/// Every response is treated as untrusted input (Section 2): JSON is deserialized with
/// <see cref="JsonUnmappedMemberHandling.Disallow"/> (reject unknown properties), every operation
/// <c>Kind</c> is checked against the closed vocabulary (reject unknown models/recipes), every
/// operation's time range and targets are checked against the originating
/// <see cref="SelectionContext.Scope"/> (reject time-scope extension/out-of-scope targets), and the
/// declared/actual response size is checked against <see cref="AiProviderOptions.MaxResponseBytes"/>
/// before parsing. Any failure throws <see cref="AiProposalRejectedException"/> -- no partial proposal,
/// no document mutation. A transient transport failure (timeout, network error, 5xx) is retried up to
/// <see cref="AiProviderOptions.MaxRetries"/> times with exponential backoff; a schema/validation
/// rejection is never retried (the response itself is wrong, not the network).
/// </para>
///
/// <para>
/// Known, disclosed limitation (see <c>implementation-evidence/PHASE-11/STEP-075.md</c>): the response
/// size cap is enforced against the declared <c>Content-Length</c> header and the fully-buffered body's
/// actual byte count, not against a true streaming read limit -- a provider that lies about its length
/// under chunked transfer-encoding could still force a large in-memory buffer before rejection. A
/// streaming bound was judged out of scope for this step's "offline local or opt-in BYOK cloud, small
/// bounded JSON envelope" use case.
/// </para>
/// </summary>
public sealed class HttpStructuredOutputAiPlanner : IOptionalAiPlanner, IDisposable
{
    private static readonly JsonSerializerOptions ResponseOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly IReadOnlyList<string> PermittedOperationKinds =
    [
        StructuredEditOperationKinds.SetBrightness,
        StructuredEditOperationKinds.SetDirection,
        StructuredEditOperationKinds.SetSpeed,
        StructuredEditOperationKinds.SnapToGrid,
        StructuredEditOperationKinds.ReplaceRole,
    ];

    private static readonly HashSet<string> PermittedOperationKindSet = new(PermittedOperationKinds, StringComparer.Ordinal);

    private readonly AiProviderOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string? _apiKey;

    /// <param name="options">Must have <see cref="AiProviderOptions.Enabled"/> set -- construct a disabled planner via <see cref="OptionalAiPlannerFactory.Create"/>, never this type, directly.</param>
    /// <param name="httpClient">The transport to use. Never disposed by this instance unless <paramref name="ownsHttpClient"/> is <see langword="true"/>.</param>
    /// <param name="apiKey">A BYOK cloud API key already retrieved from <see cref="ICredentialStore"/>, or <see langword="null"/> for <see cref="AiProviderKind.OllamaLocal"/>. Never logged; attached only as a bearer <c>Authorization</c> header on the outbound request.</param>
    public HttpStructuredOutputAiPlanner(AiProviderOptions options, HttpClient httpClient, bool ownsHttpClient = false, string? apiKey = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (!options.Enabled)
        {
            throw new ArgumentException(
                "AiProviderOptions.Enabled must be true to construct an outbound-calling planner; use OptionalAiPlannerFactory.Create for a possibly-disabled configuration.",
                nameof(options));
        }

        _options = options;
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _apiKey = apiKey;
    }

    public async Task<EditProposal> ProposeAsync(
        string instruction,
        SelectionContext context,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);

        // Gate 0: cancellation -- honored before any other gate, so a caller that cancels mid-request
        // never observes a half-built proposal or a started-but-abandoned outbound call.
        ct.ThrowIfCancellationRequested();

        // Gate 1: stale revision -- checked before any outbound request is sent at all, so a stale
        // selection never even reaches the provider (Step 075's own required failure/edge case, verbatim:
        // "stale persisted state must not corrupt the project").
        if (context.BaseDocumentRevision.Value != document.Revision)
        {
            throw new AiProposalRejectedException(
                AiProposalRejectionReason.StaleRevision,
                $"Expected revision {context.BaseDocumentRevision.Value} does not match the document's current revision {document.Revision}; no outbound request was made.");
        }

        var requestPayload = BuildRedactedRequest(instruction, context);

        var attempt = 0;
        while (true)
        {
            attempt++;
            ct.ThrowIfCancellationRequested();

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(_options.RequestTimeout);

            try
            {
                var envelope = await SendAsync(requestPayload, attemptCts.Token).ConfigureAwait(false);
                return MapToProposal(envelope, context, document);
            }
            catch (AiProposalRejectedException)
            {
                // The response itself is wrong (bad JSON, unknown kind, out-of-scope) -- never transient,
                // never retried.
                throw;
            }
            catch (Exception ex) when (IsTransient(ex) && !ct.IsCancellationRequested)
            {
                if (attempt > _options.MaxRetries)
                {
                    throw new AiProposalRejectedException(
                        AiProposalRejectionReason.ProviderUnavailable,
                        $"The AI provider did not return a usable response after {attempt} attempt(s): {ex.Message}",
                        ex);
                }

                var backoff = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));
                await Task.Delay(backoff, ct).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(Exception ex) => ex is HttpRequestException or TaskCanceledException or TimeoutException;

    private async Task<AiProposalEnvelopeDto> SendAsync(AiPlannerRequestPayload payload, CancellationToken ct)
    {
        var requestJson = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json"),
        };

        if (!string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"AI provider responded with status {(int)response.StatusCode}.");
        }

        var declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength is { } declared && declared > _options.MaxResponseBytes)
        {
            throw new AiProposalRejectedException(
                AiProposalRejectionReason.ResponseTooLarge,
                $"AI provider declared a {declared}-byte response, exceeding the configured {_options.MaxResponseBytes}-byte limit.");
        }

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (Encoding.UTF8.GetByteCount(body) > _options.MaxResponseBytes)
        {
            throw new AiProposalRejectedException(
                AiProposalRejectionReason.ResponseTooLarge,
                $"AI provider response exceeded the configured {_options.MaxResponseBytes}-byte limit.");
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<AiProposalEnvelopeDto>(body, ResponseOptions);
            return envelope ?? throw new AiProposalRejectedException(
                AiProposalRejectionReason.MalformedJson,
                "AI provider returned a JSON body that deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new AiProposalRejectedException(
                AiProposalRejectionReason.MalformedJson,
                "AI provider returned JSON that could not be parsed, or that carried a property outside the declared schema.",
                ex);
        }
    }

    private static AiPlannerRequestPayload BuildRedactedRequest(string instruction, SelectionContext context) => new(
        context.BaseDocumentRevision.Value,
        context.Scope.Time.StartTicks,
        context.Scope.Time.EndTicks,
        context.Scope.Models.Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToArray(),
        context.Scope.RespectLocks,
        PermittedOperationKinds,
        instruction);

    private static EditProposal MapToProposal(AiProposalEnvelopeDto envelope, SelectionContext context, SequenceDocument document)
    {
        if (envelope.Operations is not { Count: > 0 } operations)
        {
            throw new AiProposalRejectedException(
                AiProposalRejectionReason.EmptyOperations,
                "AI provider returned a proposal with no operations.");
        }

        var mapped = new List<StructuredEditOperation>(operations.Count);
        foreach (var op in operations)
        {
            if (string.IsNullOrWhiteSpace(op.OperationId) || string.IsNullOrWhiteSpace(op.Kind))
            {
                throw new AiProposalRejectedException(
                    AiProposalRejectionReason.MalformedJson,
                    "AI provider operation is missing a required OperationId or Kind field.");
            }

            if (!PermittedOperationKindSet.Contains(op.Kind))
            {
                throw new AiProposalRejectedException(
                    AiProposalRejectionReason.UnknownOperationKind,
                    $"AI provider proposed unknown operation kind '{op.Kind}'; no proposal was built.");
            }

            if (op.StartTicks < context.Scope.Time.StartTicks || op.EndTicks > context.Scope.Time.EndTicks || op.StartTicks >= op.EndTicks)
            {
                throw new AiProposalRejectedException(
                    AiProposalRejectionReason.OutOfScopeTime,
                    $"AI provider operation '{op.OperationId}' time range [{op.StartTicks}, {op.EndTicks}) is outside the selected [{context.Scope.Time.StartTicks}, {context.Scope.Time.EndTicks}) scope; no proposal was built.");
            }

            var targets = (op.Targets ?? []).Select(t => new ModelId(t)).ToArray();
            if (targets.Length == 0 || targets.Any(t => !context.Scope.Models.Contains(t)))
            {
                throw new AiProposalRejectedException(
                    AiProposalRejectionReason.OutOfScopeTarget,
                    $"AI provider operation '{op.OperationId}' targets a model outside the selected scope; no proposal was built.");
            }

            mapped.Add(new StructuredEditOperation(
                op.OperationId,
                op.Kind,
                new TimeWindow(op.StartTicks, op.EndTicks),
                targets,
                op.Parameters ?? new Dictionary<string, string>(StringComparer.Ordinal)));
        }

        return new EditProposal(Guid.NewGuid(), document.Revision, mapped, envelope.Explanation ?? string.Empty);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed record AiPlannerRequestPayload(
        long BaseDocumentRevision,
        long StartTicks,
        long EndTicks,
        IReadOnlyList<string> ModelIds,
        bool RespectLocks,
        IReadOnlyList<string> PermittedOperationKinds,
        string Instruction);

    private sealed class AiProposalEnvelopeDto
    {
        public string? Explanation { get; set; }

        public List<AiProposalOperationDto>? Operations { get; set; }
    }

    private sealed class AiProposalOperationDto
    {
        public string? OperationId { get; set; }

        public string? Kind { get; set; }

        public long StartTicks { get; set; }

        public long EndTicks { get; set; }

        public List<string>? Targets { get; set; }

        public Dictionary<string, string>? Parameters { get; set; }
    }
}
