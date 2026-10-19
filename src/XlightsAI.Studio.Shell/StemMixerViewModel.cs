using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Shell;

/// <summary>One bindable row of SCREEN 08 STEMS' mixer: a stem's kind, mute/solo state and (once built) waveform cache path.</summary>
public sealed class StemChannelViewModel : ObservableObject
{
    private bool _muted;
    private bool _soloed;
    private string? _waveformCachePath;
    private IReadOnlyList<StemActivity> _activities = Array.Empty<StemActivity>();

    public StemChannelViewModel(Guid stemId, StemKind kind)
    {
        StemId = stemId;
        Kind = kind;
    }

    public Guid StemId { get; }

    public StemKind Kind { get; }

    public bool Muted
    {
        get => _muted;
        set => SetField(ref _muted, value);
    }

    public bool Soloed
    {
        get => _soloed;
        set => SetField(ref _soloed, value);
    }

    /// <summary>The real on-disk waveform pyramid cache path for this stem, once <see cref="StemMixerViewModel.BuildWaveformsAsync"/> has succeeded for it; <see langword="null"/> until then.</summary>
    public string? WaveformCachePath
    {
        get => _waveformCachePath;
        internal set => SetField(ref _waveformCachePath, value);
    }

    /// <summary>
    /// This stem's detected activity regions (PHASE-06 Step 043), once
    /// <see cref="StemMixerViewModel.BuildActivityAsync"/> has succeeded for it; empty until then.
    /// SCREEN 08 STEMS overlays these on this channel's waveform track.
    /// </summary>
    public IReadOnlyList<StemActivity> Activities
    {
        get => _activities;
        internal set => SetField(ref _activities, value);
    }

    internal StemChannelState ToState() => new(StemId, Muted, Soloed);
}

/// <summary>
/// PHASE-06 Step 042 ("Add waveform tracks, solo/mute and synchronized stem playback") — SCREEN 08
/// STEMS' view model. Mirrors <see cref="AudioImportViewModel"/>'s loading/empty/error conventions: a
/// thrown exception from the renderer/builder/transport is always surfaced as an explicit,
/// user-readable error, never an unhandled exception or a silently blank screen. Per Section 3's own
/// architectural note ("Solo/mute UI is built on existing shared audio transport, not six independent
/// drifting clocks"), exactly one <see cref="IAudioTransport"/> is ever opened at a time, re-pointed at
/// a fresh <see cref="IStemMixdownRenderer"/> mix whenever the mute/solo selection changes.
/// </summary>
public sealed class StemMixerViewModel : ObservableObject
{
    private StemManifest? _manifest;
    private bool _isBusy;
    private string? _errorMessage;
    private bool _isMixOpen;
    private bool _isPlaying;
    private long _playbackPositionTicks;
    private string? _playbackErrorMessage;
    private bool _isBuildingWaveforms;
    private string? _waveformErrorMessage;
    private bool _isDetectingActivity;
    private string? _activityErrorMessage;

    public StemManifest? Manifest
    {
        get => _manifest;
        private set => SetField(ref _manifest, value);
    }

    public IReadOnlyList<StemChannelViewModel> Channels { get; private set; } = Array.Empty<StemChannelViewModel>();

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

    /// <summary><see langword="true"/> once <see cref="ApplyMixAndReopenAsync"/> has opened a mix on the transport, until the next successful <see cref="LoadManifest"/>.</summary>
    public bool IsMixOpen
    {
        get => _isMixOpen;
        private set => SetField(ref _isMixOpen, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetField(ref _isPlaying, value);
    }

    public long PlaybackPositionTicks
    {
        get => _playbackPositionTicks;
        private set => SetField(ref _playbackPositionTicks, value);
    }

    public string? PlaybackErrorMessage
    {
        get => _playbackErrorMessage;
        private set => SetField(ref _playbackErrorMessage, value);
    }

    public bool IsBuildingWaveforms
    {
        get => _isBuildingWaveforms;
        private set => SetField(ref _isBuildingWaveforms, value);
    }

    public string? WaveformErrorMessage
    {
        get => _waveformErrorMessage;
        private set => SetField(ref _waveformErrorMessage, value);
    }

    public bool IsDetectingActivity
    {
        get => _isDetectingActivity;
        private set => SetField(ref _isDetectingActivity, value);
    }

    public string? ActivityErrorMessage
    {
        get => _activityErrorMessage;
        private set => SetField(ref _activityErrorMessage, value);
    }

    /// <summary>Loads a new manifest, replacing <see cref="Channels"/> with one fresh (unmuted/unsoloed) channel per stem and clearing any previously open mix/waveform state.</summary>
    public void LoadManifest(StemManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        Manifest = manifest;
        Channels = manifest.Stems.Select(s => new StemChannelViewModel(s.Id, s.Kind)).ToArray();
        IsMixOpen = false;
        IsPlaying = false;
        PlaybackPositionTicks = 0;
        PlaybackErrorMessage = null;
        ErrorMessage = null;
        WaveformErrorMessage = null;
        ActivityErrorMessage = null;
    }

    public void ToggleMute(Guid stemId)
    {
        var channel = Channels.FirstOrDefault(c => c.StemId == stemId);
        if (channel is not null)
        {
            channel.Muted = !channel.Muted;
        }
    }

    public void ToggleSolo(Guid stemId)
    {
        var channel = Channels.FirstOrDefault(c => c.StemId == stemId);
        if (channel is not null)
        {
            channel.Soloed = !channel.Soloed;
        }
    }

    /// <summary>
    /// Renders the mix for the current <see cref="Channels"/> selection via <paramref name="renderer"/>
    /// and (re)opens it on <paramref name="transport"/> -- the single shared transport, never a
    /// per-stem transport. If a mix was already open and playing, the equivalent canonical position
    /// (the mix's origin offset is identical across every solo/mute combination, so the song-relative
    /// position carries over unchanged) is restored and playback resumes, so toggling mute/solo never
    /// resets the user's place in the song. Any failure sets <see cref="ErrorMessage"/> and returns
    /// <see langword="false"/> rather than throwing; the previously open mix (if any) is left as-is.
    /// </summary>
    public async Task<bool> ApplyMixAndReopenAsync(IStemMixdownRenderer renderer, IAudioTransport transport, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(transport);

        if (Manifest is not { } manifest)
        {
            ErrorMessage = "Load a stem manifest before applying a mix.";
            return false;
        }

        if (_isBusy)
        {
            return false;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var wasPlaying = IsMixOpen && transport.IsPlaying;
            var resumePositionTicks = IsMixOpen ? transport.PositionTicks : 0;

            var states = Channels.Select(c => c.ToState()).ToArray();
            var mixed = await renderer.RenderMixAsync(manifest, states, ct).ConfigureAwait(true);

            await transport.OpenAsync(mixed, ct).ConfigureAwait(true);
            IsMixOpen = true;

            if (resumePositionTicks > 0)
            {
                var clamped = Math.Clamp(resumePositionTicks, 0, mixed.DurationTicks);
                await transport.SeekAsync(clamped, ct).ConfigureAwait(true);
            }

            if (wasPlaying)
            {
                await transport.PlayAsync(ct).ConfigureAwait(true);
            }

            IsPlaying = transport.IsPlaying;
            PlaybackPositionTicks = transport.PositionTicks;
            PlaybackErrorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

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

    public void RefreshPlaybackPosition(IAudioTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        PlaybackPositionTicks = transport.PositionTicks;
        IsPlaying = transport.IsPlaying;
    }

    /// <summary>
    /// Builds (or reopens an already-valid) waveform pyramid cache for every stem in <see cref="Manifest"/>
    /// via <paramref name="builder"/>, populating each <see cref="StemChannelViewModel.WaveformCachePath"/>.
    /// A single stem's failure sets <see cref="WaveformErrorMessage"/> and stops further stems (so a
    /// partial result is still visible/inspectable rather than silently discarded) and returns
    /// <see langword="false"/>; all stems succeeding returns <see langword="true"/>.
    /// </summary>
    public async Task<bool> BuildWaveformsAsync(IAudioDecoder decoder, IWaveformBuilder builder, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        ArgumentNullException.ThrowIfNull(builder);

        if (Manifest is not { } manifest)
        {
            WaveformErrorMessage = "Load a stem manifest before building waveforms.";
            return false;
        }

        if (_isBuildingWaveforms)
        {
            return false;
        }

        IsBuildingWaveforms = true;
        WaveformErrorMessage = null;
        try
        {
            var stemsById = manifest.Stems.ToDictionary(s => s.Id);
            foreach (var channel in Channels)
            {
                var stem = stemsById[channel.StemId];
                var songAsset = await StemSongAssetFactory.ProbeAsync(stem, decoder, ct).ConfigureAwait(true);
                var path = await builder.BuildPyramidAsync(songAsset, ct).ConfigureAwait(true);
                channel.WaveformCachePath = path;
            }

            return true;
        }
        catch (Exception ex)
        {
            WaveformErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBuildingWaveforms = false;
        }
    }

    /// <summary>
    /// PHASE-06 Step 043 -- detects (or reopens an already-valid cached) activity regions for every
    /// stem in <see cref="Manifest"/> via <paramref name="detector"/>, populating each
    /// <see cref="StemChannelViewModel.Activities"/>. A single stem's failure sets
    /// <see cref="ActivityErrorMessage"/> and stops further stems (so a partial result is still
    /// visible/inspectable rather than silently discarded) and returns <see langword="false"/>; all
    /// stems succeeding returns <see langword="true"/>.
    /// </summary>
    public async Task<bool> BuildActivityAsync(IStemActivityDetector detector, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(detector);

        if (Manifest is not { } manifest)
        {
            ActivityErrorMessage = "Load a stem manifest before detecting activity.";
            return false;
        }

        if (_isDetectingActivity)
        {
            return false;
        }

        IsDetectingActivity = true;
        ActivityErrorMessage = null;
        try
        {
            var stemsById = manifest.Stems.ToDictionary(s => s.Id);
            foreach (var channel in Channels)
            {
                var stem = stemsById[channel.StemId];
                channel.Activities = await detector.DetectAsync(stem, ct).ConfigureAwait(true);
            }

            return true;
        }
        catch (Exception ex)
        {
            ActivityErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsDetectingActivity = false;
        }
    }
}
