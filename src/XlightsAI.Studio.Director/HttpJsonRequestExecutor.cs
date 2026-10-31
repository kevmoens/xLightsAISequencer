using System.Text;

namespace XlightsAI.Studio.Director;

/// <summary>
/// The provider-agnostic HTTP mechanics shared by every <see cref="IChatCompletionClient"/>
/// implementation: POST a JSON body, retry transient failures with exponential backoff, enforce a
/// response size cap against both the declared <c>Content-Length</c> and the actually-read body, and
/// surface a non-success status/credential rejection as a typed <see cref="AiCallRejectedException"/>.
/// This is the exact same proven shape <c>XlightsAI.Studio.AiEdits.HttpStructuredOutputAiPlanner</c>
/// already established and tested (per-attempt timeout via a linked, cancel-after token; 200ms*2^n
/// backoff; never retrying a non-transient rejection) -- factored out here as its own class, scoped to
/// this project, rather than risking a change to that already-shipped, already-tested sealed class for
/// a different payload shape it was never designed to carry.
/// </summary>
public sealed class HttpJsonRequestExecutor : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HttpJsonRequestExecutor(HttpClient httpClient, bool ownsHttpClient = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = ownsHttpClient;
    }

    /// <summary>
    /// POSTs <paramref name="jsonBody"/> to <paramref name="uri"/> with <paramref name="headers"/>,
    /// retrying up to <paramref name="maxRetries"/> times on a transient failure (exponential backoff,
    /// 200ms base) and timing out each individual attempt after <paramref name="requestTimeout"/>.
    /// Returns the raw response body text on a 2xx response within <paramref name="maxResponseBytes"/>.
    /// </summary>
    /// <exception cref="AiCallRejectedException"><see cref="AiCallRejectionReason.ProviderUnavailable"/> (every retry exhausted, or a non-401/403 non-success status), <see cref="AiCallRejectionReason.Unauthorized"/> (401/403), or <see cref="AiCallRejectionReason.ResponseTooLarge"/>.</exception>
    public async Task<string> PostAsync(
        Uri uri, string jsonBody, IReadOnlyDictionary<string, string> headers,
        TimeSpan requestTimeout, int maxRetries, int maxResponseBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonBody);
        ArgumentNullException.ThrowIfNull(headers);

        Exception? lastTransient = null;

        for (var attempt = 0; ; attempt++)
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(requestTimeout);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
                };
                foreach (var (key, value) in headers)
                {
                    request.Headers.TryAddWithoutValidation(key, value);
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token)
                    .ConfigureAwait(false);

                if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > maxResponseBytes)
                {
                    throw new AiCallRejectedException(
                        AiCallRejectionReason.ResponseTooLarge,
                        $"Response declared Content-Length {declaredLength} bytes, exceeding the configured cap of {maxResponseBytes} bytes.");
                }

                var body = await response.Content.ReadAsStringAsync(attemptCts.Token).ConfigureAwait(false);

                if (Encoding.UTF8.GetByteCount(body) > maxResponseBytes)
                {
                    throw new AiCallRejectedException(
                        AiCallRejectionReason.ResponseTooLarge,
                        $"Response body is {Encoding.UTF8.GetByteCount(body)} bytes, exceeding the configured cap of {maxResponseBytes} bytes.");
                }

                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                {
                    throw new AiCallRejectedException(
                        AiCallRejectionReason.Unauthorized,
                        $"Provider rejected the configured credential (HTTP {(int)response.StatusCode}).");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new AiCallRejectedException(
                        AiCallRejectionReason.ProviderUnavailable,
                        $"Provider returned HTTP {(int)response.StatusCode}: {Truncate(body, 500)}");
                }

                return body;
            }
            catch (AiCallRejectedException)
            {
                throw; // Never retried -- the response itself is wrong, not the network.
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                lastTransient = ex;
                if (attempt >= maxRetries)
                {
                    throw new AiCallRejectedException(
                        AiCallRejectionReason.ProviderUnavailable,
                        $"Provider unreachable after {maxRetries + 1} attempt(s): {lastTransient.Message}", lastTransient);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Same retry/size-cap/status-code discipline as <see cref="PostAsync"/>, for the GET requests the
    /// Anthropic Message Batches API needs (polling a batch's own status, then fetching its
    /// <c>results_url</c>) -- <see cref="PostAsync"/> itself cannot be reused directly since it always
    /// sends a request body.
    /// </summary>
    /// <exception cref="AiCallRejectedException"><see cref="AiCallRejectionReason.ProviderUnavailable"/>, <see cref="AiCallRejectionReason.Unauthorized"/>, or <see cref="AiCallRejectionReason.ResponseTooLarge"/> -- same meanings as <see cref="PostAsync"/>.</exception>
    public async Task<string> GetAsync(
        Uri uri, IReadOnlyDictionary<string, string> headers,
        TimeSpan requestTimeout, int maxRetries, int maxResponseBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(headers);

        Exception? lastTransient = null;

        for (var attempt = 0; ; attempt++)
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            attemptCts.CancelAfter(requestTimeout);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                foreach (var (key, value) in headers)
                {
                    request.Headers.TryAddWithoutValidation(key, value);
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token)
                    .ConfigureAwait(false);

                if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > maxResponseBytes)
                {
                    throw new AiCallRejectedException(
                        AiCallRejectionReason.ResponseTooLarge,
                        $"Response declared Content-Length {declaredLength} bytes, exceeding the configured cap of {maxResponseBytes} bytes.");
                }

                var body = await response.Content.ReadAsStringAsync(attemptCts.Token).ConfigureAwait(false);

                if (Encoding.UTF8.GetByteCount(body) > maxResponseBytes)
                {
                    throw new AiCallRejectedException(
                        AiCallRejectionReason.ResponseTooLarge,
                        $"Response body is {Encoding.UTF8.GetByteCount(body)} bytes, exceeding the configured cap of {maxResponseBytes} bytes.");
                }

                if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                {
                    throw new AiCallRejectedException(
                        AiCallRejectionReason.Unauthorized,
                        $"Provider rejected the configured credential (HTTP {(int)response.StatusCode}).");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new AiCallRejectedException(
                        AiCallRejectionReason.ProviderUnavailable,
                        $"Provider returned HTTP {(int)response.StatusCode}: {Truncate(body, 500)}");
                }

                return body;
            }
            catch (AiCallRejectedException)
            {
                throw;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                lastTransient = ex;
                if (attempt >= maxRetries)
                {
                    throw new AiCallRejectedException(
                        AiCallRejectionReason.ProviderUnavailable,
                        $"Provider unreachable after {maxRetries + 1} attempt(s): {lastTransient.Message}", lastTransient);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or TimeoutException;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
