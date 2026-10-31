namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1b/1c/4 (architecture §6 "value-curve families"; §8a <c>C_VALUECURVE_*</c> serialization): one
/// value curve bound to a single effect parameter. Mirrors <c>XlightsAI.Studio.Knowledge.EffectValueCurve</c>'s
/// fuller shape (that project cannot reference this one without a cycle — see its own doc comment) so
/// <see cref="XsqExporter"/> can serialize a real pipe-delimited <c>C_VALUECURVE_*</c>/<c>E_VALUECURVE_*</c>
/// blob — including the <see cref="TimingTrack"/>/<see cref="FilterLabel"/> bindings PHASE-4's "Mode A —
/// bound" emission strategy (architecture §6) needs to beat-lock a parameter to a labelled timing track.
/// </summary>
/// <param name="ParameterKey">The plain <c>SettingKey</c> (e.g. <c>E_SLIDER_Brightness</c>) this curve attaches to — <see cref="XsqExporter"/> resolves the real <c>ValueCurveSettingKey</c> for it via the effect's generated vocabulary.</param>
/// <param name="CurveType">The curve family (e.g. <c>Ramp</c>, <c>Sine</c>, <c>Flat</c>). <c>Type=</c> is omitted from the serialized blob when this is <c>Flat</c>.</param>
/// <param name="Min">The value-curve's own minimum.</param>
/// <param name="Max">The value-curve's own maximum.</param>
/// <param name="P1">First curve control point, when the curve type uses one.</param>
/// <param name="P2">Second curve control point, when the curve type uses one.</param>
/// <param name="Reverse">Whether the curve plays reversed; serializes as <c>RV=TRUE|</c> only when true.</param>
/// <param name="TimingTrack">Binds the curve to a named timing track's marks (<c>TT=</c>), for the "Timing Track Fade/Toggle" families.</param>
/// <param name="AudioTrack">Binds the curve to a named audio track's envelope (<c>AT=</c>), for the "Music"/"Inverted Music" families.</param>
/// <param name="FilterLabel">Restricts a timing-track binding to marks carrying this label (<c>FT=</c>).</param>
public sealed record EffectValueCurve(
    string ParameterKey,
    string CurveType,
    double Min,
    double Max,
    double? P1 = null,
    double? P2 = null,
    bool Reverse = false,
    string? TimingTrack = null,
    string? AudioTrack = null,
    string? FilterLabel = null);
