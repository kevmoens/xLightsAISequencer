using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// macOS desktop audio backend built on the OpenAL framework Apple ships with the OS. Mirrors
/// <see cref="WaveOutAudioTransport"/>: the <see cref="SongAsset"/> is decoded once per
/// <see cref="OpenAsync"/> into one 16-bit mono PCM buffer, and <see cref="PositionTicks"/> always
/// reflects the device's own played-sample count (<c>AL_SAMPLE_OFFSET</c>), never a UI timer.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOsAudioTransport : IAudioTransport
{
    private const long TicksPerSecond = 10_000_000L;
    private const int BytesPerSample = 2;

    private readonly IAudioDecoder _decoder;
    private readonly object _gate = new();

    private IntPtr _device;
    private IntPtr _context;
    private uint _buffer;
    private uint _source;
    private bool _hasBuffer;
    private bool _hasSource;

    private long _pausedAtSample;
    private bool _isPlaying;
    private TimeWindow? _loop;

    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

    public MacOsAudioTransport(IAudioDecoder decoder)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
    }

    public SongAsset? CurrentSong { get; private set; }

    public bool IsPlaying
    {
        get { lock (_gate) { return _isPlaying; } }
    }

    public TimeWindow? Loop
    {
        get { lock (_gate) { return _loop; } }
    }

    public long PositionTicks
    {
        get
        {
            lock (_gate)
            {
                if (CurrentSong is null)
                {
                    return 0;
                }

                var sample = _isPlaying ? ReadPlayingSampleLocked() : _pausedAtSample;
                return SamplesToTicks(sample, CurrentSong.SampleRate);
            }
        }
    }

    public async Task OpenAsync(SongAsset song, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(song);

        await StopAndCloseAsync().ConfigureAwait(false);

        var samples = new List<float>();
        await foreach (var block in _decoder.DecodeMonoAsync(song, song.SampleRate, ct).ConfigureAwait(false))
        {
            samples.AddRange(block.ToArray());
        }

        var pcmBytes = new byte[samples.Count * BytesPerSample];
        for (var i = 0; i < samples.Count; i++)
        {
            var value = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            BitConverter.TryWriteBytes(pcmBytes.AsSpan(i * BytesPerSample, BytesPerSample), value);
        }

        lock (_gate)
        {
            try
            {
                OpenDeviceLocked(pcmBytes, song.SampleRate);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                CloseDeviceLocked();
                throw new AudioOutputDeviceUnavailableException("the macOS OpenAL framework could not be loaded.", -1);
            }
            catch
            {
                CloseDeviceLocked();
                throw;
            }

            CurrentSong = song;
            _pausedAtSample = 0;
            _isPlaying = false;
            _loop = null;
        }

        _monitorCts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoopAsync(_monitorCts.Token), CancellationToken.None);
    }

    public Task PlayAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (CurrentSong is null)
            {
                throw new InvalidOperationException("No song is open; call OpenAsync before PlayAsync.");
            }

            if (_isPlaying)
            {
                return Task.CompletedTask;
            }

            if (_pausedAtSample >= CurrentSong.SampleCount)
            {
                // At the end of the track: same convention as the Windows backend — a later SeekAsync is required.
                return Task.CompletedTask;
            }

            OpenAlNativeMethods.alSourceStop(_source);
            OpenAlNativeMethods.alSourcei(_source, OpenAlNativeMethods.SampleOffset, checked((int)_pausedAtSample));
            OpenAlNativeMethods.alSourcePlay(_source);
            ThrowIfAlError("start playback");
            _isPlaying = true;
        }

        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (CurrentSong is null || !_isPlaying)
            {
                return Task.CompletedTask;
            }

            _pausedAtSample = ReadPlayingSampleLocked();
            OpenAlNativeMethods.alSourcePause(_source);
            _isPlaying = false;
        }

        return Task.CompletedTask;
    }

    public Task SeekAsync(long ticks, CancellationToken ct)
    {
        lock (_gate)
        {
            if (CurrentSong is null)
            {
                throw new InvalidOperationException("No song is open; call OpenAsync before SeekAsync.");
            }

            if (ticks < 0 || ticks > CurrentSong.DurationTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks), ticks,
                    $"Seek position must be within the song's valid timing boundary [0, {CurrentSong.DurationTicks}].");
            }

            var target = Math.Clamp(TicksToSamples(ticks, CurrentSong.SampleRate), 0, CurrentSong.SampleCount);
            var wasPlaying = _isPlaying;

            OpenAlNativeMethods.alSourceStop(_source);
            _pausedAtSample = target;
            _isPlaying = false;

            if (wasPlaying && target < CurrentSong.SampleCount)
            {
                OpenAlNativeMethods.alSourcei(_source, OpenAlNativeMethods.SampleOffset, checked((int)target));
                OpenAlNativeMethods.alSourcePlay(_source);
                _isPlaying = true;
            }
        }

        return Task.CompletedTask;
    }

    public void SetLoop(TimeWindow? window)
    {
        lock (_gate)
        {
            if (window is { } w &&
                (w.StartTicks < 0 || w.EndTicks <= w.StartTicks ||
                 (CurrentSong is not null && w.EndTicks > CurrentSong.DurationTicks)))
            {
                throw new ArgumentOutOfRangeException(nameof(window), window,
                    "Loop window must be a non-empty half-open range within the open song's duration.");
            }

            _loop = window;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAndCloseAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task MonitorLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(20, token).ConfigureAwait(false);

                lock (_gate)
                {
                    if (!_isPlaying || CurrentSong is null)
                    {
                        continue;
                    }

                    var currentSample = ReadPlayingSampleLocked();

                    if (_loop is { } loop &&
                        currentSample >= TicksToSamples(loop.EndTicks, CurrentSong.SampleRate))
                    {
                        var loopStart = TicksToSamples(loop.StartTicks, CurrentSong.SampleRate);
                        OpenAlNativeMethods.alSourceStop(_source);
                        OpenAlNativeMethods.alSourcei(_source, OpenAlNativeMethods.SampleOffset, checked((int)loopStart));
                        OpenAlNativeMethods.alSourcePlay(_source);
                        continue;
                    }

                    if (currentSample >= CurrentSong.SampleCount)
                    {
                        OpenAlNativeMethods.alSourceStop(_source);
                        _pausedAtSample = CurrentSong.SampleCount;
                        _isPlaying = false;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Dispose/re-Open.
        }
    }

    /// <summary>Must be called under <see cref="_gate"/>. A source that ran to the end reports state Stopped, in which case the offset is no longer meaningful.</summary>
    private long ReadPlayingSampleLocked()
    {
        OpenAlNativeMethods.alGetSourcei(_source, OpenAlNativeMethods.SourceState, out var state);
        if (state == OpenAlNativeMethods.Stopped)
        {
            return CurrentSong!.SampleCount;
        }

        OpenAlNativeMethods.alGetSourcei(_source, OpenAlNativeMethods.SampleOffset, out var offset);
        return offset;
    }

    private void OpenDeviceLocked(byte[] pcmBytes, int sampleRate)
    {
        _device = OpenAlNativeMethods.alcOpenDevice(IntPtr.Zero);
        if (_device == IntPtr.Zero)
        {
            throw new AudioOutputDeviceUnavailableException("no audio output device could be opened.", 1);
        }

        _context = OpenAlNativeMethods.alcCreateContext(_device, IntPtr.Zero);
        if (_context == IntPtr.Zero || !OpenAlNativeMethods.alcMakeContextCurrent(_context))
        {
            throw new AudioOutputDeviceUnavailableException("an audio playback context could not be created.", 2);
        }

        OpenAlNativeMethods.alGetError();
        OpenAlNativeMethods.alGenBuffers(1, out _buffer);
        _hasBuffer = true;

        var pinned = GCHandle.Alloc(pcmBytes, GCHandleType.Pinned);
        try
        {
            OpenAlNativeMethods.alBufferData(_buffer, OpenAlNativeMethods.FormatMono16, pinned.AddrOfPinnedObject(), pcmBytes.Length, sampleRate);
        }
        finally
        {
            pinned.Free();
        }

        ThrowIfAlError("load the audio buffer");

        OpenAlNativeMethods.alGenSources(1, out _source);
        _hasSource = true;
        OpenAlNativeMethods.alSourcei(_source, OpenAlNativeMethods.Buffer, checked((int)_buffer));
        ThrowIfAlError("attach the audio buffer");
    }

    private static void ThrowIfAlError(string action)
    {
        var error = OpenAlNativeMethods.alGetError();
        if (error != OpenAlNativeMethods.NoError)
        {
            throw new AudioOutputDeviceUnavailableException($"OpenAL could not {action}.", error);
        }
    }

    private async Task StopAndCloseAsync()
    {
        if (_monitorCts is not null)
        {
            _monitorCts.Cancel();
            try
            {
                if (_monitorTask is not null)
                {
                    await _monitorTask.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }

            _monitorCts.Dispose();
            _monitorCts = null;
            _monitorTask = null;
        }

        lock (_gate)
        {
            CloseDeviceLocked();
            CurrentSong = null;
            _pausedAtSample = 0;
            _isPlaying = false;
            _loop = null;
        }
    }

    /// <summary>Must be called under <see cref="_gate"/>.</summary>
    private void CloseDeviceLocked()
    {
        if (_hasSource)
        {
            OpenAlNativeMethods.alSourceStop(_source);
            OpenAlNativeMethods.alDeleteSources(1, ref _source);
            _hasSource = false;
        }

        if (_hasBuffer)
        {
            OpenAlNativeMethods.alDeleteBuffers(1, ref _buffer);
            _hasBuffer = false;
        }

        if (_context != IntPtr.Zero)
        {
            OpenAlNativeMethods.alcMakeContextCurrent(IntPtr.Zero);
            OpenAlNativeMethods.alcDestroyContext(_context);
            _context = IntPtr.Zero;
        }

        if (_device != IntPtr.Zero)
        {
            OpenAlNativeMethods.alcCloseDevice(_device);
            _device = IntPtr.Zero;
        }
    }

    private static long TicksToSamples(long ticks, int sampleRate) =>
        (long)Math.Round(ticks * (sampleRate / (double)TicksPerSecond));

    private static long SamplesToTicks(long samples, int sampleRate) =>
        sampleRate <= 0 ? 0 : (long)Math.Round(samples * (TicksPerSecond / (double)sampleRate));
}
