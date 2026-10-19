namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Real <see cref="IReleaseAssetDownloader"/> backed by <see cref="HttpClient"/>. Streams the response body
/// directly to disk (never buffers a multi-gigabyte asset fully in memory, same convention as
/// <c>XlightsAI.Studio.Stems.HttpModelAssetDownloader</c>) and never swallows a failed HTTP status or
/// transport error -- the caller (<see cref="AssetInstaller"/>) is responsible for deleting any partial file
/// this throws out of. Reports fractional progress via <see cref="IProgress{T}"/> whenever the server
/// declares a <c>Content-Length</c>; silently omits progress reporting otherwise (an unknown total is not a
/// failure).
/// </summary>
public sealed class HttpReleaseAssetDownloader : IReleaseAssetDownloader, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HttpReleaseAssetDownloader(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task DownloadAsync(Uri source, string destinationPath, IProgress<double>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        using var response = await _httpClient
            .GetAsync(source, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        await using var httpStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await httpStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            totalRead += read;

            if (totalBytes is > 0)
            {
                progress?.Report(Math.Min(1.0, (double)totalRead / totalBytes.Value));
            }
        }

        if (totalBytes is null or 0)
        {
            progress?.Report(1.0);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
