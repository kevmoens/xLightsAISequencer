namespace XlightsAI.Studio.Stems;

/// <summary>
/// Real <see cref="IModelAssetDownloader"/> backed by <see cref="HttpClient"/>. Streams the response body
/// directly to disk (never buffers a multi-gigabyte model fully in memory, per Section 3's "2-8 GB+
/// asset sizes" risk) and never swallows a failed HTTP status or transport error — the caller
/// (<see cref="ModelAssetManager"/>) is responsible for deleting any partial file this throws out of.
/// </summary>
public sealed class HttpModelAssetDownloader : IModelAssetDownloader, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HttpModelAssetDownloader(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        using var response = await _httpClient
            .GetAsync(source, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var httpStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await httpStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
