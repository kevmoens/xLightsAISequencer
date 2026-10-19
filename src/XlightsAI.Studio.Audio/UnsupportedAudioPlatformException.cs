namespace XlightsAI.Studio.Audio;

/// <summary>
/// Thrown by <see cref="UnsupportedPlatformAudioTransport"/> on every platform that has no real
/// <see cref="IAudioTransport"/> backend yet (everything except Windows — see
/// <see cref="WaveOutAudioTransport"/>). Exists so a user/developer on an unsupported platform sees
/// one clear, specific, typed message naming exactly what is missing, instead of a raw native-interop
/// failure (e.g. a "library not found" exception) or a silently-ignored no-op. Tracked as a known,
/// intentional gap — see <c>docs/ARCHITECTURE-AND-GUARDRAILS.md</c> ("Platform coverage") and
/// <c>implementation-evidence/PHASE-04/STEP-25.md</c> §6/§3.
/// </summary>
public sealed class UnsupportedAudioPlatformException : NotSupportedException
{
    public UnsupportedAudioPlatformException(string platformDescription)
        : base(
            $"Real audio playback is not yet implemented on this platform ({platformDescription}). " +
            "Only Windows has a real playback backend today (WaveOutAudioTransport, via winmm.dll). " +
            "Audio import/probing/metadata still work on every platform; only Open/Play/Pause/Seek/" +
            "loop playback is unavailable here. This is a known, intentionally tracked gap, not a bug " +
            "— see docs/ARCHITECTURE-AND-GUARDRAILS.md's 'Platform coverage' section for the plan to " +
            "add a real backend for this platform.")
    {
        PlatformDescription = platformDescription;
    }

    /// <summary><see cref="System.Runtime.InteropServices.RuntimeInformation.OSDescription"/> at the time this was thrown.</summary>
    public string PlatformDescription { get; }
}
