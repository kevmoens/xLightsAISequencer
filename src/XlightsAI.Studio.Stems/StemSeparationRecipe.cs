namespace XlightsAI.Studio.Stems;

/// <summary>
/// Step 040's own required implementation guidance, verbatim: "Implement preprocessing, backend
/// selection and postprocessing as a single versioned processing recipe." Ties the exact
/// preprocessing (resample/normalize expectations), the ONNX tensor contract (names/stem order) and
/// the postprocessing (de-normalization) together as one inspectable, versioned unit, so a later
/// recipe revision cannot silently drift between preprocessing and postprocessing halves.
/// </summary>
/// <param name="RecipeVersion">Opaque version string for this exact preprocessing/tensor-contract/postprocessing combination. Changing any of the fields below requires bumping this (Section 4: "every cache key includes algorithm/model/settings versions").</param>
/// <param name="ExpectedSampleRate">The sample rate in Hz this recipe's model was declared/verified for. A source song at a different rate is rejected as incompatible (<see cref="StemSeparationIncompatibleSourceException"/>) rather than silently resampled -- Step 024's own decoder does not yet implement real resampling (disclosed gap), so this step never pretends to.</param>
/// <param name="ExpectedChannels">Channel count this recipe's model input expects. Step 040's own pipeline always decodes mono (<see cref="XlightsAI.Studio.Audio.IAudioDecoder.DecodeMonoAsync"/>), so this is <c>1</c> for every recipe actually used by this step (disclosed, deliberate scope boundary -- see evidence §3).</param>
/// <param name="StemOrder">The stem kind each index of the model's stem axis (output tensor axis 1) represents, in order. Its count is the model's declared stem count.</param>
/// <param name="InputTensorName">ONNX graph input tensor name.</param>
/// <param name="OutputTensorName">ONNX graph output tensor name.</param>
/// <param name="NormalizationEpsilon">Smallest peak magnitude treated as non-silent during preprocessing's peak-normalization step; prevents a divide-by-zero on true digital silence (full-phase test matrix item 5: "Silent source yields finite output/no NaNs").</param>
/// <param name="ChunkSamples">PHASE-06 Step 041 ("Add chunking/overlap, caching, progress, cancellation and bounded resource usage"): the maximum number of samples run through a single ONNX inference call. Bounds the inference working-set buffer independently of total song length. Defaults to <see cref="int.MaxValue"/>, which reproduces Step 040's own whole-clip, single-pass behavior exactly (one chunk covering the whole signal) -- purely additive, non-breaking for every recipe constructed before Step 041 existed.</param>
/// <param name="OverlapSamples">Step 041: samples of overlap between consecutive chunks, cross-faded (weighted overlap-add) back together during final assembly so a chunk boundary never produces an audible seam. Must be strictly less than <see cref="ChunkSamples"/>. Defaults to <c>0</c> (no overlap), matching Step 040's own behavior when combined with the default <see cref="ChunkSamples"/>.</param>
public sealed record StemSeparationRecipe(
    string RecipeVersion,
    int ExpectedSampleRate,
    int ExpectedChannels,
    IReadOnlyList<StemKind> StemOrder,
    string InputTensorName,
    string OutputTensorName,
    float NormalizationEpsilon = 1e-6f,
    int ChunkSamples = int.MaxValue,
    int OverlapSamples = 0);
