namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Pinned, declared <see cref="DownloadableAsset"/> for the optional ffmpeg binary used to transcode
/// non-WAV audio (MP3, etc.) to WAV before <c>XlightsAI.Studio.Audio.WavAudioDecoder</c> probes it (see
/// <c>XlightsAI.Studio.Audio.TranscodingAudioDecoder</c>). A Windows x86-64, LGPL-3.0-licensed "shared"
/// build (BtbN/FFmpeg-Builds) — LGPL specifically so this asset's license imposes no copyleft
/// obligation on the rest of the codebase, even though ffmpeg is only ever downloaded at runtime by the
/// user's own machine, never bundled/redistributed by this project (see THIRD-PARTY-NOTICES.md). Pinned
/// to one immutable, date-stamped GitHub release tag (not the "latest" rolling tag, whose asset bytes
/// are replaced over time) so <see cref="DownloadableAsset.Sha256"/> stays valid indefinitely; both the
/// URL and hash below were verified against the real downloaded archive bytes before being committed
/// here.
/// </summary>
public static class FfmpegAsset
{
    public static readonly DownloadableAsset Current = new(
        Id: "ffmpeg-win64-lgpl-shared",
        Version: "9.0.2",
        Url: new Uri(
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-10-22-13-06/" +
            "ffmpeg-n9.0.2-33-g4cf298e66a-win64-lgpl-shared-9.0.zip"),
        Sha256: "14d2da681ed1999a6232de61f99d869a46df437dd910d69c1fb5c9c83172242e",
        SizeBytes: 77_231_703,
        LicenseId: "LGPL-3.0-or-later",
        Required: false);
}
