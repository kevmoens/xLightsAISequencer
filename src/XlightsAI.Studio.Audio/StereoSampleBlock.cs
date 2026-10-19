namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-06A Step 044A — one decoded stereo sample block: independently addressable left/right
/// channels, never averaged/downmixed the way <see cref="IAudioDecoder.DecodeMonoAsync"/>'s own
/// mono output already is. Both spans always have the same length (one sample count per block).
/// </summary>
public readonly record struct StereoSampleBlock(ReadOnlyMemory<float> Left, ReadOnlyMemory<float> Right);
