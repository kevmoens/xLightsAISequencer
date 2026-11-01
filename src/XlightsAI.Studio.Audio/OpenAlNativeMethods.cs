using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// Minimal P/Invoke surface for the OpenAL framework that ships with every macOS install, used by
/// <see cref="MacOsAudioTransport"/>. No third-party/native package is referenced.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class OpenAlNativeMethods
{
    private const string Library = "/System/Library/Frameworks/OpenAL.framework/OpenAL";

    public const int FormatMono16 = 0x1101;
    public const int Buffer = 0x1009;
    public const int SourceState = 0x1010;
    public const int SampleOffset = 0x1025;
    public const int Playing = 0x1012;
    public const int Stopped = 0x1014;
    public const int NoError = 0;

    [DllImport(Library)]
    public static extern IntPtr alcOpenDevice(IntPtr deviceName);

    [DllImport(Library)]
    public static extern IntPtr alcCreateContext(IntPtr device, IntPtr attributes);

    [DllImport(Library)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool alcMakeContextCurrent(IntPtr context);

    [DllImport(Library)]
    public static extern void alcDestroyContext(IntPtr context);

    [DllImport(Library)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool alcCloseDevice(IntPtr device);

    [DllImport(Library)]
    public static extern int alGetError();

    [DllImport(Library)]
    public static extern void alGenBuffers(int count, out uint buffer);

    [DllImport(Library)]
    public static extern void alDeleteBuffers(int count, ref uint buffer);

    [DllImport(Library)]
    public static extern void alBufferData(uint buffer, int format, IntPtr data, int size, int frequency);

    [DllImport(Library)]
    public static extern void alGenSources(int count, out uint source);

    [DllImport(Library)]
    public static extern void alDeleteSources(int count, ref uint source);

    [DllImport(Library)]
    public static extern void alSourcei(uint source, int parameter, int value);

    [DllImport(Library)]
    public static extern void alGetSourcei(uint source, int parameter, out int value);

    [DllImport(Library)]
    public static extern void alSourcePlay(uint source);

    [DllImport(Library)]
    public static extern void alSourcePause(uint source);

    [DllImport(Library)]
    public static extern void alSourceStop(uint source);
}
