using System.Runtime.InteropServices;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// Reports whether this machine has a usable audio output device, without opening a stream.
/// Headless machines (CI runners, servers) have none, and real-device playback cannot work there.
/// </summary>
public static class AudioOutputProbe
{
    [DllImport("winmm.dll")]
    private static extern uint waveOutGetNumDevs();

    public static bool IsOutputDeviceAvailable()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return waveOutGetNumDevs() > 0;
            }

            if (OperatingSystem.IsMacOS())
            {
                var device = OpenAlNativeMethods.alcOpenDevice(IntPtr.Zero);
                if (device == IntPtr.Zero)
                {
                    return false;
                }

                OpenAlNativeMethods.alcCloseDevice(device);
                return true;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
        }

        return false;
    }
}
