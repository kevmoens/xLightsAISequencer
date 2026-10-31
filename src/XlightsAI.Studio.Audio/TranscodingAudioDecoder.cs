using System.Security.Cryptography;
using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// Decorating <see cref="IAudioDecoder"/> that extends <see cref="WavAudioDecoder"/> to non-WAV
/// containers (MP3, etc.) by transcoding them to WAV via ffmpeg first. Dispatch is based on the file's
/// actual RIFF/WAVE magic bytes, never its extension (<see cref="IAudioDecoder"/>'s own contract:
/// "Probing must inspect actual file contents, never trust the filename/extension alone") — a
/// misnamed-but-genuinely-WAV file still probes directly, and a <c>.wav</c>-named non-WAV file still
/// routes through ffmpeg.
///
/// A transcoded file is cached under <c>cacheDirectory</c>, keyed by the source file's own SHA-256, so
/// re-probing the same source never re-invokes ffmpeg. The returned <see cref="SongAsset.FilePath"/>
/// always points at that cached WAV (not the original source) — which is also why
/// <see cref="DecodeMonoAsync"/> can unconditionally delegate to the wrapped <see cref="WavAudioDecoder"/>:
/// every <see cref="SongAsset"/> this type ever produces already names a real WAV file.
/// <see cref="SongAsset.OriginalFilePath"/> is corrected back to the real pre-transcode source, for a
/// caller that needs the user's own file rather than the internal analysis WAV (e.g. a <c>.xsq</c>'s
/// <c>mediaFile</c> -- confirmed a real bug against a real export: the un-corrected <c>FilePath</c> named
/// an internal cache file xLights has no reason to know about).
///
/// Never downloads ffmpeg itself (consent to download is the caller's responsibility, same convention as
/// <see cref="IFfmpegInstaller"/>): when transcoding is needed and no local ffmpeg installation exists,
/// this throws <see cref="FfmpegNotAvailableException"/> rather than silently reaching out to the
/// network.
/// </summary>
public sealed class TranscodingAudioDecoder : IAudioDecoder
{
    private readonly WavAudioDecoder _inner;
    private readonly IFfmpegInstaller _ffmpegInstaller;
    private readonly string _cacheDirectory;
    private readonly IExternalAudioTranscoder _transcoder;

    public TranscodingAudioDecoder(
        WavAudioDecoder inner,
        IFfmpegInstaller ffmpegInstaller,
        string cacheDirectory,
        IExternalAudioTranscoder? transcoder = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(ffmpegInstaller);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        _inner = inner;
        _ffmpegInstaller = ffmpegInstaller;
        _cacheDirectory = cacheDirectory;
        _transcoder = transcoder ?? new FfmpegProcessTranscoder();
    }

    public async Task<SongAsset> ProbeAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No audio file exists at '{path}'.", path);
        }

        if (await LooksLikeWaveAsync(path, ct).ConfigureAwait(false))
        {
            return await _inner.ProbeAsync(path, ct).ConfigureAwait(false);
        }

        var ffmpegPath = _ffmpegInstaller.TryGetInstalledExecutablePath();
        if (ffmpegPath is null)
        {
            throw new FfmpegNotAvailableException(path);
        }

        var cachedWavPath = await GetCachedWavPathAsync(path, ct).ConfigureAwait(false);
        if (!File.Exists(cachedWavPath))
        {
            Directory.CreateDirectory(_cacheDirectory);
            await _transcoder.TranscodeToWavAsync(ffmpegPath, path, cachedWavPath, ct).ConfigureAwait(false);
        }

        var probed = await _inner.ProbeAsync(cachedWavPath, ct).ConfigureAwait(false);

        // The transcoder's own output-format version travels with the asset for the same reason it is
        // part of the cache key above: a later consumer (waveform cache, analysis cache) keyed on
        // DecoderVersion must be able to tell an asset transcoded under different settings apart.
        // OriginalFilePath is corrected back to the real source here: _inner.ProbeAsync (WavAudioDecoder)
        // only ever sees cachedWavPath, so it has no way to know the pre-transcode path on its own.
        return probed with { DecoderVersion = $"{probed.DecoderVersion}+ffmpeg/{_transcoder.OutputFormatVersion}", OriginalFilePath = path };
    }

    /// <summary>
    /// Every <see cref="SongAsset"/> this type produces already names a real cached WAV file in
    /// <see cref="SongAsset.FilePath"/> (either the original source, when it was already WAV, or the
    /// transcoded cache entry), so decoding never needs to re-dispatch on content.
    /// </summary>
    public IAsyncEnumerable<ReadOnlyMemory<float>> DecodeMonoAsync(SongAsset song, int targetSampleRate, CancellationToken ct = default) =>
        _inner.DecodeMonoAsync(song, targetSampleRate, ct);

    private async Task<string> GetCachedWavPathAsync(string sourcePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(sourcePath);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        var sha256 = Convert.ToHexString(hash).ToLowerInvariant();

        // The transcoder's own output-format version is part of this cache key, not just the source
        // content hash (Section 4: "every cache key includes algorithm/model/settings versions") -- a
        // changed output setting (sample rate, channel count, codec) must never silently resolve to a
        // file produced under the previous settings, which is exactly what a source-hash-only key did.
        var formatVersion = Sanitize(_transcoder.OutputFormatVersion);
        return Path.Combine(_cacheDirectory, $"{sha256}-{formatVersion}.wav");
    }

    private static string Sanitize(string value) =>
        new(value.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    private static async Task<bool> LooksLikeWaveAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        if (stream.Length < 12)
        {
            return false;
        }

        var header = new byte[12];
        var read = await stream.ReadAsync(header, ct).ConfigureAwait(false);
        if (read < 12)
        {
            return false;
        }

        return header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
            && header[8] == 'W' && header[9] == 'A' && header[10] == 'V' && header[11] == 'E';
    }
}
