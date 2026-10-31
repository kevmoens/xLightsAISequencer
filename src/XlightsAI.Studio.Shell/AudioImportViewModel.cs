using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-04 Step 024 — SCREEN 06 AUDIO, import-only (no waveform/playback yet; those are Steps
/// 025-030's own scope). Mirrors <see cref="ShowImportViewModel"/>'s loading/empty/error/loaded and
/// busy/action-error conventions: a thrown exception from the decoder or the asset store is always
/// surfaced as an explicit, user-readable error, never an unhandled exception or a silently blank
/// screen.
/// </summary>
public sealed class AudioImportViewModel : ObservableObject
{
    private string _audioFilePath = string.Empty;
    private bool _isBusy;
    private string? _errorMessage;
    private SongAsset? _probedAsset;
    private string? _savedAssetPath;
    private bool _isPlaybackOpen;
    private bool _isPlaying;
    private long _playbackPositionTicks;
    private string? _playbackErrorMessage;
    private string _seekTargetTicksText = "0";
    private bool _isBuildingWaveform;
    private string? _waveformErrorMessage;
    private string? _waveformCachePath;
    private int _waveformResolutionLevelCount;
    private string? _waveformContentHash;
    private WaveformPyramidCache? _loadedWaveformCache;
    private bool _requiresFfmpegDownload;
    private bool _isDownloadingFfmpeg;
    private string? _ffmpegStatusMessage;

    /// <summary>Bindable path text for the file picker form.</summary>
    public string AudioFilePath
    {
        get => _audioFilePath;
        set => SetField(ref _audioFilePath, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    /// <summary>The most recently successfully probed asset (Step 024 required acceptance: "duration/sample-rate verified"); <see langword="null"/> until a Probe attempt succeeds.</summary>
    public SongAsset? ProbedAsset
    {
        get => _probedAsset;
        private set
        {
            if (SetField(ref _probedAsset, value))
            {
                OnPropertyChanged(nameof(HasProbedAsset));
            }
        }
    }

    public bool HasProbedAsset => ProbedAsset is not null;

    /// <summary>
    /// <see langword="true"/> when the last <see cref="ProbeAudioFileAsync"/> attempt failed because the
    /// file is not WAV and no local ffmpeg installation exists to transcode it — the Audio screen shows a
    /// "Download ffmpeg" action instead of a dead-end error while this is set. Cleared at the start of
    /// every new probe attempt and on a successful ffmpeg install.
    /// </summary>
    public bool RequiresFfmpegDownload
    {
        get => _requiresFfmpegDownload;
        private set => SetField(ref _requiresFfmpegDownload, value);
    }

    public bool IsDownloadingFfmpeg
    {
        get => _isDownloadingFfmpeg;
        private set => SetField(ref _isDownloadingFfmpeg, value);
    }

    /// <summary>Human-readable outcome of the most recent <see cref="DownloadFfmpegAsync"/> attempt.</summary>
    public string? FfmpegStatusMessage
    {
        get => _ffmpegStatusMessage;
        private set => SetField(ref _ffmpegStatusMessage, value);
    }

    /// <summary>The real on-disk path <see cref="ProbedAsset"/> was last persisted to (Step 024 user-visible outcome: "Saved audio/analysis artifact with timestamp provenance").</summary>
    public string? SavedAssetPath
    {
        get => _savedAssetPath;
        private set => SetField(ref _savedAssetPath, value);
    }

    /// <summary>PHASE-04 Step 025: <see langword="true"/> once <see cref="OpenPlaybackAsync"/> has succeeded against the real device, until the next successful probe replaces it.</summary>
    public bool IsPlaybackOpen
    {
        get => _isPlaybackOpen;
        private set => SetField(ref _isPlaybackOpen, value);
    }

    /// <summary>PHASE-04 Step 025: mirrors <c>IAudioTransport.IsPlaying</c> after the most recent Play/Pause/Seek action.</summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetField(ref _isPlaying, value);
    }

    /// <summary>PHASE-04 Step 025: <c>IAudioTransport.PositionTicks</c> as of the most recent Play/Pause/Seek/Refresh action — the real device-reported position, not a UI timer estimate.</summary>
    public long PlaybackPositionTicks
    {
        get => _playbackPositionTicks;
        private set => SetField(ref _playbackPositionTicks, value);
    }

    /// <summary>PHASE-04 Step 025: set whenever a playback action fails (e.g. "missing output device handled") instead of throwing to the UI; cleared on the next successful action.</summary>
    public string? PlaybackErrorMessage
    {
        get => _playbackErrorMessage;
        private set => SetField(ref _playbackErrorMessage, value);
    }

    /// <summary>PHASE-04 Step 025: bindable text for the "Seek to (100 ns ticks)" field consumed by <see cref="SeekAsync"/>.</summary>
    public string SeekTargetTicksText
    {
        get => _seekTargetTicksText;
        set => SetField(ref _seekTargetTicksText, value);
    }

    /// <summary>
    /// Opens the real device transport against <see cref="ProbedAsset"/>. Requires a successful
    /// probe first; any failure (including "missing output device") sets
    /// <see cref="PlaybackErrorMessage"/> and returns <see langword="false"/> rather than throwing.
    /// </summary>
    public async Task<bool> OpenPlaybackAsync(IAudioTransport transport, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transport);

        if (ProbedAsset is null)
        {
            PlaybackErrorMessage = "Probe an audio file before opening playback.";
            return false;
        }

        try
        {
            await transport.OpenAsync(ProbedAsset, ct).ConfigureAwait(true);
            IsPlaybackOpen = true;
            IsPlaying = transport.IsPlaying;
            PlaybackPositionTicks = transport.PositionTicks;
            PlaybackErrorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            IsPlaybackOpen = false;
            IsPlaying = false;
            PlaybackPositionTicks = 0;
            PlaybackErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Starts/resumes playback. Required acceptance: "Play/pause/seek works without Python."</summary>
    public async Task<bool> PlayAsync(IAudioTransport transport, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        try
        {
            await transport.PlayAsync(ct).ConfigureAwait(true);
            IsPlaying = transport.IsPlaying;
            PlaybackPositionTicks = transport.PositionTicks;
            PlaybackErrorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            PlaybackErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Pauses playback; position does not reset.</summary>
    public async Task<bool> PauseAsync(IAudioTransport transport, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        try
        {
            await transport.PauseAsync(ct).ConfigureAwait(true);
            IsPlaying = transport.IsPlaying;
            PlaybackPositionTicks = transport.PositionTicks;
            PlaybackErrorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            PlaybackErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Seeks to <see cref="SeekTargetTicksText"/> (parsed as a whole number of 100 ns ticks); an out-of-range/unparsable value is reported via <see cref="PlaybackErrorMessage"/>, never an unhandled exception.</summary>
    public async Task<bool> SeekAsync(IAudioTransport transport, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transport);

        if (!long.TryParse(SeekTargetTicksText, out var ticks))
        {
            PlaybackErrorMessage = "Seek target must be a whole number of 100 ns ticks.";
            return false;
        }

        try
        {
            await transport.SeekAsync(ticks, ct).ConfigureAwait(true);
            IsPlaying = transport.IsPlaying;
            PlaybackPositionTicks = transport.PositionTicks;
            PlaybackErrorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            PlaybackErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Re-reads the real device position/playing state without issuing a new transport command (e.g. a manual "Refresh" button, since this step does not yet wire a continuous UI clock — Step 028's own scope).</summary>
    public void RefreshPlaybackPosition(IAudioTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        PlaybackPositionTicks = transport.PositionTicks;
        IsPlaying = transport.IsPlaying;
    }

    /// <summary>PHASE-04 Step 026: <see langword="true"/> while <see cref="BuildWaveformCacheAsync"/> is in flight.</summary>
    public bool IsBuildingWaveform
    {
        get => _isBuildingWaveform;
        private set => SetField(ref _isBuildingWaveform, value);
    }

    /// <summary>PHASE-04 Step 026: set whenever building the waveform pyramid fails, instead of throwing to the UI; cleared on the next successful build.</summary>
    public string? WaveformErrorMessage
    {
        get => _waveformErrorMessage;
        private set => SetField(ref _waveformErrorMessage, value);
    }

    /// <summary>PHASE-04 Step 026: the real on-disk path the most recently built/reopened waveform pyramid cache lives at (the step's own "Versioned, inspectable project output" user-visible outcome).</summary>
    public string? WaveformCachePath
    {
        get => _waveformCachePath;
        private set
        {
            if (SetField(ref _waveformCachePath, value))
            {
                OnPropertyChanged(nameof(HasWaveformCache));
            }
        }
    }

    public bool HasWaveformCache => WaveformCachePath is not null;

    /// <summary>PHASE-04 Step 026: how many distinct zoom levels (<see cref="WaveformResolutionLevel"/>s) the most recently built/reopened pyramid has.</summary>
    public int WaveformResolutionLevelCount
    {
        get => _waveformResolutionLevelCount;
        private set => SetField(ref _waveformResolutionLevelCount, value);
    }

    /// <summary>PHASE-04 Step 026: the content hash (<see cref="WaveformPyramidCache.ContentHash"/>) the displayed pyramid was built from — lets the user confirm which source audio this cache actually reflects.</summary>
    public string? WaveformContentHash
    {
        get => _waveformContentHash;
        private set => SetField(ref _waveformContentHash, value);
    }

    /// <summary>
    /// PHASE-04 Step 027: the full <see cref="WaveformPyramidCache"/> most recently built/reopened by
    /// <see cref="BuildWaveformCacheAsync"/> — unlike <see cref="WaveformCachePath"/>/
    /// <see cref="WaveformResolutionLevelCount"/>/<see cref="WaveformContentHash"/> (plain display
    /// summaries), this is the real object <c>TimelineViewModel.LoadTimeline</c> needs to render the
    /// synchronized waveform beneath the ruler. <see langword="null"/> whenever no cache has been
    /// successfully built/reopened yet, or the most recent attempt failed.
    /// </summary>
    public WaveformPyramidCache? LoadedWaveformCache
    {
        get => _loadedWaveformCache;
        private set => SetField(ref _loadedWaveformCache, value);
    }

    /// <summary>
    /// Builds (or reopens an already-valid) waveform pyramid cache for <see cref="ProbedAsset"/> via
    /// <paramref name="builder"/>, then reloads it via <paramref name="store"/> to populate the
    /// bindable summary fields above. Requires a successful probe first; any failure (missing
    /// media, an invalid/stale asset record, or a save/load I/O error) sets
    /// <see cref="WaveformErrorMessage"/> and returns <see langword="false"/> rather than throwing.
    /// </summary>
    public async Task<bool> BuildWaveformCacheAsync(IWaveformBuilder builder, IWaveformPyramidStore store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(store);

        if (ProbedAsset is null)
        {
            WaveformErrorMessage = "Probe an audio file before building a waveform cache.";
            return false;
        }

        if (_isBuildingWaveform)
        {
            return false;
        }

        IsBuildingWaveform = true;
        WaveformErrorMessage = null;
        try
        {
            var path = await builder.BuildPyramidAsync(ProbedAsset, ct).ConfigureAwait(true);
            var cache = await store.LoadAsync(path, ct).ConfigureAwait(true);

            WaveformCachePath = path;
            WaveformResolutionLevelCount = cache.Resolutions.Length;
            WaveformContentHash = cache.ContentHash;
            LoadedWaveformCache = cache;
            return true;
        }
        catch (Exception ex)
        {
            WaveformCachePath = null;
            WaveformResolutionLevelCount = 0;
            WaveformContentHash = null;
            LoadedWaveformCache = null;
            WaveformErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBuildingWaveform = false;
        }
    }

    /// <summary>
    /// Probes <see cref="AudioFilePath"/> via <paramref name="decoder"/>, then persists the result
    /// via <paramref name="store"/>. Returns <see langword="true"/> (and sets <see cref="ProbedAsset"/>/
    /// <see cref="SavedAssetPath"/>) only on full success; any failure (missing media, corrupt/
    /// unsupported content, or a save I/O error) sets <see cref="ErrorMessage"/> and returns
    /// <see langword="false"/> without ever producing or persisting a partial/invalid asset record
    /// — the required failure path: "Invalid/corrupt files fail clearly."
    /// </summary>
    public async Task<bool> ProbeAudioFileAsync(IAudioDecoder decoder, ISongAssetStore store, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        ArgumentNullException.ThrowIfNull(store);

        // Reentrancy guard: same synchronous check-and-set pattern as ShowImportViewModel.DiscoverAsync.
        if (_isBusy)
        {
            return false;
        }

        IsBusy = true;
        ErrorMessage = null;
        RequiresFfmpegDownload = false;
        try
        {
            var asset = await decoder.ProbeAsync(AudioFilePath, ct).ConfigureAwait(true);
            var savedPath = await store.SaveAsync(asset, ct).ConfigureAwait(true);

            ProbedAsset = asset;
            SavedAssetPath = savedPath;

            // A new probe invalidates any previously opened playback session: the real transport
            // itself is closed/reopened by the next OpenPlaybackAsync call (IAudioTransport.OpenAsync
            // always replaces whatever song was previously open), but this view model's own bindable
            // playback state must not keep showing stale "open"/position values for an asset that is
            // no longer the probed one.
            IsPlaybackOpen = false;
            IsPlaying = false;
            PlaybackPositionTicks = 0;
            PlaybackErrorMessage = null;

            // A new probe invalidates any previously displayed waveform-cache summary for the same
            // reason: it describes an asset that is no longer the probed one, even if a cache file
            // for the old SongId still exists on disk (BuildWaveformCacheAsync for the new asset
            // simply resolves to a different path).
            WaveformCachePath = null;
            WaveformResolutionLevelCount = 0;
            WaveformContentHash = null;
            WaveformErrorMessage = null;
            LoadedWaveformCache = null;
            return true;
        }
        catch (FfmpegNotAvailableException ex)
        {
            ProbedAsset = null;
            SavedAssetPath = null;
            RequiresFfmpegDownload = true;
            ErrorMessage = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            ProbedAsset = null;
            SavedAssetPath = null;
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Downloads/verifies/extracts ffmpeg via <paramref name="installer"/> so a subsequent
    /// <see cref="ProbeAudioFileAsync"/> call can transcode non-WAV files. The user clicking the "Download
    /// ffmpeg" action this is wired to is the explicit consent to download — no separate persisted consent
    /// flag. Any failure (cancellation, hash mismatch, offline/network failure) is reported via
    /// <see cref="FfmpegStatusMessage"/> instead of throwing.
    /// </summary>
    public async Task<bool> DownloadFfmpegAsync(IFfmpegInstaller installer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(installer);

        if (_isDownloadingFfmpeg)
        {
            return false;
        }

        IsDownloadingFfmpeg = true;
        try
        {
            var path = await installer.EnsureInstalledAsync(progress: null, ct).ConfigureAwait(true);
            FfmpegStatusMessage = $"Installed ffmpeg at '{path}'. Click Probe Audio File again.";
            RequiresFfmpegDownload = false;
            return true;
        }
        catch (OperationCanceledException)
        {
            FfmpegStatusMessage = "Downloading ffmpeg was cancelled; it was not installed. You can try again later.";
            return false;
        }
        catch (AssetInstallationHashMismatchException ex)
        {
            FfmpegStatusMessage = ex.Message;
            return false;
        }
        catch (AssetInstallationFailedException ex)
        {
            FfmpegStatusMessage = ex.Message;
            return false;
        }
        finally
        {
            IsDownloadingFfmpeg = false;
        }
    }
}
