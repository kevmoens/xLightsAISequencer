using System.Diagnostics;
using System.Text;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// Real <see cref="IExternalAudioTranscoder"/>: shells out to a resolved ffmpeg executable to decode
/// <paramref name="sourcePath"/>'s first audio stream to 16-bit PCM WAV. Writes to a same-directory temp
/// file first, then atomically moves it into place, so a failed/cancelled run never leaves a partial
/// file at <paramref name="destinationPath"/> — same convention as
/// <c>XlightsAI.Studio.Distribution.AssetInstaller</c>.
///
/// <b>Output is deliberately normalized to 44.1 kHz stereo</b>, not passed through at the source's own
/// rate/channel count: the real stem-separation model this app's whole-song pipeline depends on
/// (<c>XlightsAI.Studio.Stems.SherpaOnnxSpleeterModelAsset.Recipe</c>) accepts <i>only</i> 44100 Hz,
/// 2-channel input and rejects anything else outright, so a 48 kHz or mono import would otherwise probe
/// successfully here and then fail several steps later with a confusing, far-removed error. Resampling
/// to the one rate every downstream stage already assumes is both standard and the only setting that
/// makes a transcoded import usable end to end; the resulting <see cref="SongAsset"/>'s reported
/// rate/channels honestly describe the cached WAV that is actually written and decoded, never the
/// discarded source values.
/// </summary>
public sealed class FfmpegProcessTranscoder : IExternalAudioTranscoder
{
    /// <summary>The one sample rate <c>SherpaOnnxSpleeterModelAsset.Recipe</c> accepts (see the class doc comment).</summary>
    public const int NormalizedSampleRate = 44100;

    /// <summary>The one channel count <c>SherpaOnnxSpleeterModelAsset.Recipe</c> accepts (see the class doc comment).</summary>
    public const int NormalizedChannels = 2;

    /// <inheritdoc />
    public string OutputFormatVersion => $"pcm_s16le-{NormalizedSampleRate}hz-{NormalizedChannels}ch-v1";

    public async Task TranscodeToWavAsync(string ffmpegExecutablePath, string sourcePath, string destinationPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var tempPath = $"{destinationPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegExecutablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-nostdin");
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(sourcePath);
            startInfo.ArgumentList.Add("-vn");
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("0:a:0");
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("pcm_s16le");
            // See the class doc comment: normalized, never passed through, because the real
            // stem-separation recipe downstream accepts only this exact rate/channel count.
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add(NormalizedSampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add(NormalizedChannels.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // The temp output path's real extension is ".tmp-<guid>", not ".wav" (see the class doc
            // comment on why it must not be written directly at destinationPath) -- ffmpeg infers the
            // muxer format from the output filename's extension by default, so it must be told
            // explicitly, or it fails with "Unable to choose an output format".
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("wav");
            startInfo.ArgumentList.Add(tempPath);

            using var process = new Process { StartInfo = startInfo };
            var stderr = new StringBuilder();
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stderr.AppendLine(e.Data);
                }
            };

            // Both redirected streams must be drained asynchronously, not just stderr. Leaving
            // StandardOutput unread is the classic .NET Process deadlock: the OS pipe buffer behind a
            // redirected stream is finite, so if ffmpeg ever writes enough bytes to stdout (it
            // shouldn't for a well-formed run, but some inputs/ffmpeg builds emit banners, extra
            // stream/container diagnostics, or -stats progress there), the child process blocks on a
            // full pipe write while this process blocks on WaitForExitAsync waiting for it to exit --
            // neither side can make progress and the process hangs forever with no CPU usage. There is
            // nothing useful to do with stdout here (output goes to a real file), so it is simply
            // discarded.
            process.OutputDataReceived += (_, _) => { };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            try
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation (e.g. a per-song timeout) must not leave an orphaned ffmpeg process
                // running in the background indefinitely.
                TryKill(process);
                throw;
            }

            if (process.ExitCode != 0)
            {
                throw new AudioProbeInvalidException(
                    sourcePath, $"ffmpeg could not decode this file (exit code {process.ExitCode}): {stderr}".TrimEnd());
            }

            if (!File.Exists(tempPath))
            {
                throw new AudioProbeInvalidException(sourcePath, "ffmpeg reported success but produced no output file.");
            }

            ct.ThrowIfCancellationRequested();
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup only -- the process may have already exited between the check and
            // the kill, which is not an error worth surfacing.
        }
    }
}
