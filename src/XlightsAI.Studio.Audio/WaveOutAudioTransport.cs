using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 025 — real Windows <c>winmm</c> (<c>waveOut*</c>) desktop audio backend. Decodes a
/// <see cref="SongAsset"/> fully (via the already-accepted <see cref="IAudioDecoder"/>) into a
/// pinned native 16-bit PCM buffer once per <see cref="OpenAsync"/>, then drives one native wave-out
/// device handle: <see cref="PositionTicks"/> always reflects the device's own actually-played
/// sample count (<c>waveOutGetPosition</c>, <c>TIME_SAMPLES</c>) — never a UI timer — per Section 3's
/// "build synchronization around actual playback position" guidance. No third-party/native package
/// is referenced; see <see cref="WinmmNativeMethods"/>.
/// </summary>
/// <remarks>
/// Windows-only for this step (see <c>implementation-evidence/PHASE-04/STEP-25.md</c> §3): the App
/// project is already <c>WinExe</c>-only with no other RID targeted, so this does not narrow the
/// product's current actual platform support.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WaveOutAudioTransport : IAudioTransport
{
    private const long TicksPerSecond = 10_000_000L;
    private const int BytesPerSample = 2; // 16-bit PCM, fixed for this step's decoder (see §3/§6 for the disclosed "mono only" scope note).
    private const int DefaultDeviceId = -1; // WAVE_MAPPER: let Windows pick the current default output device.

    private readonly IAudioDecoder _decoder;
    private readonly int _deviceId;
    private readonly object _gate = new();

    private IntPtr _hWaveOut;
    private IntPtr _pcmBuffer;
    private int _pcmByteLength;
    private IntPtr _currentHeader;

    private long _submittedFromSample;
    private long _pausedAtSample;
    private bool _isPlaying;
    private TimeWindow? _loop;

    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

    /// <param name="decoder">Decodes the opened <see cref="SongAsset"/> into PCM samples. Required — this transport performs no decoding of its own.</param>
    /// <param name="deviceId">The winmm device ID to open, or -1 (the default) for <c>WAVE_MAPPER</c> (the system's current default output device). A caller can pass an out-of-range ID to deliberately exercise the "missing/invalid output device" failure path.</param>
    public WaveOutAudioTransport(IAudioDecoder decoder, int deviceId = DefaultDeviceId)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _deviceId = deviceId;
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

                var sample = _isPlaying ? _submittedFromSample + ReadDevicePositionSamplesLocked() : _pausedAtSample;
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
            var clamped = Math.Clamp(samples[i], -1f, 1f);
            var value = (short)Math.Round(clamped * short.MaxValue);
            BitConverter.TryWriteBytes(pcmBytes.AsSpan(i * BytesPerSample, BytesPerSample), value);
        }

        var format = new WinmmNativeMethods.WaveFormatEx
        {
            wFormatTag = WinmmNativeMethods.WaveFormatPcm,
            nChannels = 1,
            nSamplesPerSec = song.SampleRate,
            wBitsPerSample = 16,
            nBlockAlign = BytesPerSample,
            nAvgBytesPerSec = song.SampleRate * BytesPerSample,
            cbSize = 0,
        };

        var deviceId = unchecked((uint)_deviceId);
        var openResult = WinmmNativeMethods.waveOutOpen(out var handle, deviceId, ref format, IntPtr.Zero, IntPtr.Zero, WinmmNativeMethods.CallbackNull);
        if (openResult != WinmmNativeMethods.MmsyserrNoError)
        {
            throw new AudioOutputDeviceUnavailableException(DescribeMmResult(openResult), openResult);
        }

        lock (_gate)
        {
            _hWaveOut = handle;
            _pcmByteLength = pcmBytes.Length;
            _pcmBuffer = Marshal.AllocHGlobal(Math.Max(1, _pcmByteLength));
            if (_pcmByteLength > 0)
            {
                Marshal.Copy(pcmBytes, 0, _pcmBuffer, _pcmByteLength);
            }

            CurrentSong = song;
            _submittedFromSample = 0;
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

            if (_currentHeader == IntPtr.Zero)
            {
                if (!SubmitFromSampleLocked(_pausedAtSample))
                {
                    // Already at (or past) the end of the track; nothing left to submit. A later
                    // SeekAsync back into the track is required before Play can start again — the
                    // same "never silently rewind behind the user" convention as a stopped player.
                    return Task.CompletedTask;
                }
            }
            else
            {
                var restartResult = WinmmNativeMethods.waveOutRestart(_hWaveOut);
                if (restartResult != WinmmNativeMethods.MmsyserrNoError)
                {
                    throw new AudioOutputDeviceUnavailableException(DescribeMmResult(restartResult), restartResult);
                }
            }

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

            _pausedAtSample = _submittedFromSample + ReadDevicePositionSamplesLocked();
            WinmmNativeMethods.waveOutPause(_hWaveOut);
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

            var targetSample = TicksToSamples(ticks, CurrentSong.SampleRate);
            var wasPlaying = _isPlaying;

            ResetDeviceLocked();

            if (wasPlaying)
            {
                _isPlaying = SubmitFromSampleLocked(targetSample);
            }
            else
            {
                _pausedAtSample = Math.Clamp(targetSample, 0, CurrentSong.SampleCount);
                _isPlaying = false;
            }
        }

        return Task.CompletedTask;
    }

    public void SetLoop(TimeWindow? window)
    {
        lock (_gate)
        {
            if (window is { } w)
            {
                if (w.StartTicks < 0 || w.EndTicks <= w.StartTicks ||
                    (CurrentSong is not null && w.EndTicks > CurrentSong.DurationTicks))
                {
                    throw new ArgumentOutOfRangeException(nameof(window), window,
                        "Loop window must be a non-empty half-open range within the open song's duration.");
                }
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

                    var currentSample = _submittedFromSample + ReadDevicePositionSamplesLocked();

                    if (_loop is { } loop)
                    {
                        var loopEndSample = TicksToSamples(loop.EndTicks, CurrentSong.SampleRate);
                        if (currentSample >= loopEndSample)
                        {
                            var loopStartSample = TicksToSamples(loop.StartTicks, CurrentSong.SampleRate);
                            ResetDeviceLocked();
                            _isPlaying = SubmitFromSampleLocked(loopStartSample);
                            continue;
                        }
                    }

                    if (currentSample >= CurrentSong.SampleCount)
                    {
                        ResetDeviceLocked();
                        _pausedAtSample = CurrentSong.SampleCount;
                        _isPlaying = false;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Dispose/re-Open; the monitor task is torn down deliberately.
        }
    }

    /// <summary>Must be called under <see cref="_gate"/>. Prepares and writes a native header for the tail of the PCM buffer starting at <paramref name="startSample"/>; returns <see langword="false"/> (and records the clamped end-of-track position) if there is nothing left to play.</summary>
    private bool SubmitFromSampleLocked(long startSample)
    {
        UnprepareCurrentHeaderLocked();

        var totalSamples = CurrentSong!.SampleCount;
        var clampedStart = Math.Clamp(startSample, 0, totalSamples);
        _submittedFromSample = clampedStart;

        var startByte = checked((int)(clampedStart * BytesPerSample));
        var length = _pcmByteLength - startByte;
        if (length <= 0)
        {
            _pausedAtSample = totalSamples;
            return false;
        }

        var header = new WinmmNativeMethods.WaveHdr
        {
            lpData = IntPtr.Add(_pcmBuffer, startByte),
            dwBufferLength = length,
        };

        var headerSize = Marshal.SizeOf<WinmmNativeMethods.WaveHdr>();
        var headerPtr = Marshal.AllocHGlobal(headerSize);
        Marshal.StructureToPtr(header, headerPtr, fDeleteOld: false);

        var prepareResult = WinmmNativeMethods.waveOutPrepareHeader(_hWaveOut, headerPtr, headerSize);
        if (prepareResult != WinmmNativeMethods.MmsyserrNoError)
        {
            Marshal.FreeHGlobal(headerPtr);
            throw new AudioOutputDeviceUnavailableException(DescribeMmResult(prepareResult), prepareResult);
        }

        var writeResult = WinmmNativeMethods.waveOutWrite(_hWaveOut, headerPtr, headerSize);
        if (writeResult != WinmmNativeMethods.MmsyserrNoError)
        {
            WinmmNativeMethods.waveOutUnprepareHeader(_hWaveOut, headerPtr, headerSize);
            Marshal.FreeHGlobal(headerPtr);
            throw new AudioOutputDeviceUnavailableException(DescribeMmResult(writeResult), writeResult);
        }

        _currentHeader = headerPtr;
        return true;
    }

    /// <summary>Must be called under <see cref="_gate"/>.</summary>
    private void ResetDeviceLocked()
    {
        if (_hWaveOut != IntPtr.Zero)
        {
            WinmmNativeMethods.waveOutReset(_hWaveOut);
        }

        UnprepareCurrentHeaderLocked();
    }

    /// <summary>Must be called under <see cref="_gate"/>.</summary>
    private void UnprepareCurrentHeaderLocked()
    {
        if (_currentHeader == IntPtr.Zero)
        {
            return;
        }

        var headerSize = Marshal.SizeOf<WinmmNativeMethods.WaveHdr>();
        WinmmNativeMethods.waveOutUnprepareHeader(_hWaveOut, _currentHeader, headerSize);
        Marshal.FreeHGlobal(_currentHeader);
        _currentHeader = IntPtr.Zero;
    }

    /// <summary>Must be called under <see cref="_gate"/>.</summary>
    private long ReadDevicePositionSamplesLocked()
    {
        if (_hWaveOut == IntPtr.Zero)
        {
            return 0;
        }

        var mmTime = new WinmmNativeMethods.MmTime { wType = WinmmNativeMethods.TimeSamples };
        var result = WinmmNativeMethods.waveOutGetPosition(_hWaveOut, ref mmTime, Marshal.SizeOf<WinmmNativeMethods.MmTime>());
        if (result != WinmmNativeMethods.MmsyserrNoError || mmTime.wType != WinmmNativeMethods.TimeSamples)
        {
            return 0;
        }

        return mmTime.data;
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
            ResetDeviceLocked();

            if (_hWaveOut != IntPtr.Zero)
            {
                WinmmNativeMethods.waveOutClose(_hWaveOut);
                _hWaveOut = IntPtr.Zero;
            }

            if (_pcmBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_pcmBuffer);
                _pcmBuffer = IntPtr.Zero;
            }

            _pcmByteLength = 0;
            CurrentSong = null;
            _submittedFromSample = 0;
            _pausedAtSample = 0;
            _isPlaying = false;
            _loop = null;
        }
    }

    private static long TicksToSamples(long ticks, int sampleRate) =>
        (long)Math.Round(ticks * (sampleRate / (double)TicksPerSecond));

    private static long SamplesToTicks(long samples, int sampleRate) =>
        sampleRate <= 0 ? 0 : (long)Math.Round(samples * (TicksPerSecond / (double)sampleRate));

    private static string DescribeMmResult(int mmResultCode) => mmResultCode switch
    {
        2 => "the specified device ID is out of range; no such output device exists.",
        4 => "the device is already allocated by another application.",
        6 => "no audio output driver is installed on this system.",
        7 => "the system cannot allocate the resources needed for playback.",
        8 => "the specified device does not support the requested operation.",
        11 => "an invalid parameter was passed to the audio subsystem.",
        32 => "the requested audio format is not supported by this device.",
        _ => $"the Windows audio subsystem reported MMRESULT {mmResultCode}.",
    };
}
