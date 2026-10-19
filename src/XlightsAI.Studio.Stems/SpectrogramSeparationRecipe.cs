namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06A Step 044C. Disclosed sibling to the waveform-domain <see cref="StemSeparationRecipe"/> --
/// never a redefinition or breaking change to it. Ties the real preprocessing (stereo STFT, bin
/// truncation, time-axis zero-pad to a multiple of <see cref="TimeSplitFrames"/>,
/// per-channel-as-axis-0 tensor framing), the ONNX tensor contract (both single-stem sessions share
/// input name <see cref="InputTensorName"/> / output name <see cref="OutputTensorName"/>), and the real
/// soft-mask postprocessing together as one inspectable, versioned unit -- the same "single versioned
/// processing recipe" principle <see cref="StemSeparationRecipe"/> already established for the
/// waveform-domain pipeline.
/// </summary>
/// <param name="RecipeVersion">Opaque version string for this exact preprocessing/tensor-contract/postprocessing combination.</param>
/// <param name="ExpectedSampleRate">44100 -- the real model's own required sample rate.</param>
/// <param name="ExpectedChannels">2 -- stereo is this recipe's own required input shape, unlike <see cref="StemSeparationRecipe.ExpectedChannels"/>'s mono-only constraint.</param>
/// <param name="Stft">The real STFT configuration (<see cref="StftConfig.SpleeterReference"/>).</param>
/// <param name="RetainedFrequencyBins">1024 of 2049 -- the real model's own disclosed, permanent frequency-domain truncation.</param>
/// <param name="TimeSplitFrames">512 -- the real model's own required time-axis split granularity.</param>
/// <param name="InputTensorName">"x" -- identical for both the vocals and accompaniment sessions.</param>
/// <param name="OutputTensorName">"y" -- identical for both sessions.</param>
/// <param name="SoftMaskEpsilon">1e-10 -- the real published epsilon for the Wiener-like soft-mask formula's numerical stability against digital silence.</param>
public sealed record SpectrogramSeparationRecipe(
    string RecipeVersion,
    int ExpectedSampleRate,
    int ExpectedChannels,
    StftConfig Stft,
    int RetainedFrequencyBins,
    int TimeSplitFrames,
    string InputTensorName,
    string OutputTensorName,
    float SoftMaskEpsilon = 1e-10f);
