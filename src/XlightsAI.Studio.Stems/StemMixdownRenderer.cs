using System.Security.Cryptography;
using System.Text;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 042 — real (no third-party dependency) implementation of
/// <see cref="IStemMixdownRenderer"/>: sums the currently-audible stems (per
/// <see cref="StemMixRules"/>) sample-for-sample into one mono mix and writes it to a
/// deterministically-keyed on-disk cache, so re-applying the same solo/mute combination (e.g. toggling
/// mute off and back on) never re-decodes/re-sums audio it has already produced once -- the same
/// content-addressed caching discipline Step 041's <see cref="FileSystemStemSeparationChunkCache"/>
/// already established for separation chunks (Section 4: "every cache key includes algorithm/model/
/// settings versions").
/// </summary>
public sealed class StemMixdownRenderer : IStemMixdownRenderer
{
    private readonly IAudioDecoder _decoder;
    private readonly string _cacheDirectory;

    public StemMixdownRenderer(IAudioDecoder decoder, string cacheDirectory)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        _cacheDirectory = cacheDirectory;
    }

    public async Task<SongAsset> RenderMixAsync(StemManifest manifest, IReadOnlyList<StemChannelState> channels, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(channels);

        ct.ThrowIfCancellationRequested();

        if (manifest.Stems.Count == 0)
        {
            throw new StemMixdownIncompatibleStemsException($"Manifest '{manifest.RunId}' has no stems to mix.");
        }

        var reference = manifest.Stems[0];
        foreach (var stem in manifest.Stems)
        {
            if (stem.SampleRate != reference.SampleRate || stem.SampleCount != reference.SampleCount ||
                stem.SourceOriginTicks != reference.SourceOriginTicks)
            {
                throw new StemMixdownIncompatibleStemsException(
                    $"Stem '{stem.Id}' ({stem.SampleRate} Hz / {stem.SampleCount} samples / origin {stem.SourceOriginTicks}) " +
                    $"does not match stem '{reference.Id}' ({reference.SampleRate} Hz / {reference.SampleCount} samples / " +
                    $"origin {reference.SourceOriginTicks}) -- stems must be aligned to mix.");
            }
        }

        var activeStemIds = StemMixRules.ResolveActiveStemIds(manifest, channels);
        var cacheKey = ComputeCacheKey(manifest.RunId, activeStemIds);
        var cacheDir = Path.Combine(_cacheDirectory, ".mixdown-cache");
        var cachePath = Path.Combine(cacheDir, $"{cacheKey}.wav");

        if (await TryReuseCachedMixAsync(cachePath, reference, ct).ConfigureAwait(false) is { } cached)
        {
            return WithDeterministicIdentity(cached, cacheKey, reference.SourceOriginTicks);
        }

        var mixed = new float[reference.SampleCount];
        if (activeStemIds.Count > 0)
        {
            var stemsById = manifest.Stems.ToDictionary(s => s.Id);
            foreach (var stemId in activeStemIds)
            {
                var stem = stemsById[stemId];
                var songAsset = await StemSongAssetFactory.ProbeAsync(stem, _decoder, ct).ConfigureAwait(false);

                var index = 0;
                await foreach (var block in _decoder.DecodeMonoAsync(songAsset, reference.SampleRate, ct).ConfigureAwait(false))
                {
                    var span = block.Span;
                    for (var i = 0; i < span.Length; i++)
                    {
                        mixed[index++] += span[i];
                    }
                }
            }

            for (var i = 0; i < mixed.Length; i++)
            {
                mixed[i] = Math.Clamp(mixed[i], -1f, 1f);
            }
        }

        Directory.CreateDirectory(cacheDir);
        var tempPath = $"{cachePath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await StemWavWriter.WriteMonoFloatAsync(tempPath, mixed, reference.SampleRate, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            File.Move(tempPath, cachePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }

        var written = await _decoder.ProbeAsync(cachePath, ct).ConfigureAwait(false);
        return WithDeterministicIdentity(written, cacheKey, reference.SourceOriginTicks);
    }

    /// <summary>
    /// Reuses an already-rendered mix if a file already exists at <paramref name="cachePath"/> and
    /// re-probing it still matches <paramref name="reference"/>'s declared shape. A corrupt/stale cache
    /// file (probe failure or shape mismatch) is discarded and silently treated as a miss -- the same
    /// "stale persisted state must not corrupt the project" guarantee Step 041 established for the
    /// chunk cache -- so a corrupted cache entry never blocks a remix, it is simply recomputed.
    /// </summary>
    private async Task<SongAsset?> TryReuseCachedMixAsync(string cachePath, StemAsset reference, CancellationToken ct)
    {
        if (!File.Exists(cachePath))
        {
            return null;
        }

        try
        {
            var probed = await _decoder.ProbeAsync(cachePath, ct).ConfigureAwait(false);
            if (probed.SampleRate == reference.SampleRate && probed.SampleCount == reference.SampleCount)
            {
                return probed;
            }
        }
        catch (AudioProbeInvalidException)
        {
            // Corrupt/truncated cache file -- fall through and recompute.
        }

        File.Delete(cachePath);
        return null;
    }

    private static SongAsset WithDeterministicIdentity(SongAsset probed, string cacheKey, long originTicks)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey));
        var idBytes = hashBytes[..16];
        return probed with { Id = new SongId(new Guid(idBytes)), OriginTicks = originTicks };
    }

    private static string ComputeCacheKey(Guid runId, IReadOnlyList<Guid> activeStemIds)
    {
        var canonical = $"{runId}|{string.Join(",", activeStemIds.OrderBy(id => id))}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
