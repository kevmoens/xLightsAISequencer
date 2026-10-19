using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 025 — raw Win32 <c>winmm.dll</c> (<c>waveOut*</c>) P/Invoke surface used by
/// <see cref="WaveOutAudioTransport"/>: the "actual desktop audio backend" this step's own
/// implementation guidance requires ("Wrap platform audio output behind one transport interface").
/// No third-party NuGet dependency is introduced; <c>winmm.dll</c> ships with every Windows install,
/// matching Section 2's "no mandatory... runtime" principle. Internal — never exposed outside this
/// assembly; <see cref="IAudioTransport"/> is the only public playback contract.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WinmmNativeMethods
{
    internal const short WaveFormatPcm = 1;
    internal const uint CallbackNull = 0x0;
    internal const uint TimeSamples = 2;
    internal const int MmsyserrNoError = 0;

    /// <summary>Matches the native <c>WAVEFORMATEX</c> layout exactly (no <c>cbSize</c>-trailing extra data is ever used).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WaveFormatEx
    {
        public short wFormatTag;
        public short nChannels;
        public int nSamplesPerSec;
        public int nAvgBytesPerSec;
        public short nBlockAlign;
        public short wBitsPerSample;
        public short cbSize;
    }

    /// <summary>Matches the native <c>WAVEHDR</c> layout exactly. Always allocated via <see cref="Marshal.AllocHGlobal(int)"/>, never a managed/pinned array — the OS driver retains a pointer to this memory for as long as the buffer is queued, which outlives any single P/Invoke call.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WaveHdr
    {
        public IntPtr lpData;
        public int dwBufferLength;
        public int dwBytesRecorded;
        public IntPtr dwUser;
        public int dwFlags;
        public int dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    /// <summary>Matches the native <c>MMTIME</c> layout with enough trailing padding to cover its largest union member (<c>SMPTE</c>, 8 bytes) safely; only <see cref="wType"/>/<see cref="data"/> (the <c>TIME_SAMPLES</c> case) are ever read.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MmTime
    {
        public uint wType;
        public uint data;
        private uint _padding1;
        private uint _padding2;
    }

    [DllImport("winmm.dll")]
    internal static extern int waveOutOpen(out IntPtr phwo, uint deviceId, ref WaveFormatEx format, IntPtr callback, IntPtr callbackInstance, uint flags);

    [DllImport("winmm.dll")]
    internal static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    internal static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    internal static extern int waveOutWrite(IntPtr hwo, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    internal static extern int waveOutPause(IntPtr hwo);

    [DllImport("winmm.dll")]
    internal static extern int waveOutRestart(IntPtr hwo);

    [DllImport("winmm.dll")]
    internal static extern int waveOutReset(IntPtr hwo);

    [DllImport("winmm.dll")]
    internal static extern int waveOutClose(IntPtr hwo);

    [DllImport("winmm.dll")]
    internal static extern int waveOutGetPosition(IntPtr hwo, ref MmTime time, int timeSize);
}
