using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Adapts <see cref="ISpectrogramStemSeparator"/> (the real, two-single-stem-session separator this
/// repository already built and verified against the real, pinned <see cref="SherpaOnnxSpleeterModelAsset"/>
/// — see that type's own doc comment for its full download/hash-provenance disclosure) to the generic
/// <see cref="IStemSeparator"/> shape <see cref="AudioAnalysisRunner"/> depends on, so the runner's own
/// orchestration code stays decoupled from which concrete separator backs it (a cheap synthetic
/// <see cref="OnnxStemSeparator"/> fixture for fast unit tests; this adapter wrapping the real
/// <see cref="SpectrogramOnnxStemSeparator"/>/<see cref="SherpaOnnxSpleeterModelAsset"/> for production).
///
/// <b>Disclosed caveat this class does not hide:</b> <see cref="SherpaOnnxSpleeterModelAsset.Archive"/>'s
/// own <c>LicenseId</c> states the Spleeter pretrained-weight redistribution license is "genuinely
/// contested/unresolved" — using this adapter in production should wait on that question being resolved
/// (or surfaced to the user) the same way ffmpeg's LGPL license was surfaced in
/// <c>THIRD-PARTY-NOTICES.md</c>, not silently assumed clean.
/// </summary>
public sealed class SpectrogramStemSeparatorAdapter : IStemSeparator
{
    private readonly ISpectrogramStemSeparator _inner;
    private readonly SpectrogramModelBundle _bundle;
    private readonly SpectrogramSeparationRecipe _recipe;

    public SpectrogramStemSeparatorAdapter(ISpectrogramStemSeparator inner, SpectrogramModelBundle bundle, SpectrogramSeparationRecipe recipe)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        _recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
    }

    /// <exception cref="ArgumentException"><paramref name="model"/> does not identify this adapter's own configured <see cref="SpectrogramModelBundle.Archive"/> — a defensive check against a caller accidentally passing a different declared model than the bundle/recipe this adapter was actually constructed with.</exception>
    public Task<StemManifest> SeparateAsync(SongAsset source, ModelAsset model, IProgress<SeparationProgress>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!string.Equals(model.Id, _bundle.Archive.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Model '{model.Id}' does not match this adapter's configured bundle ('{_bundle.Archive.Id}').",
                nameof(model));
        }

        return _inner.SeparateAsync(source, _bundle, _recipe, progress, ct);
    }
}
