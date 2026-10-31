namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// One value curve attached to a single effect parameter, typed for the pipe-delimited blob
/// architecture §8a documents:
/// <c>Active=TRUE|Id=ID_VALUECURVE_Brightness|Type=Ramp|Min=0.00|Max=400.00|P1=400.00|P2=300.00|RV=TRUE|</c>.
/// This mirrors <c>XlightsAI.Studio.Xlights.EffectValueCurve</c> in shape, but lives here rather than
/// reusing that type: <c>XlightsAI.Studio.Xlights</c> is the project that references
/// <c>XlightsAI.Studio.Knowledge</c> (not the other way around — see §12 blocker 2's "no parallel edits"
/// extension point), so this project cannot depend on it without creating a cycle.
/// </summary>
/// <param name="CurveType">The curve family (e.g. <c>Ramp</c>, <c>Sine</c>, <c>Flat</c>). <c>Type=</c> is omitted from the serialized blob when this is <c>Flat</c>.</param>
/// <param name="Min">The value-curve's own minimum (may differ from the property's slider <c>min</c> — metadata's <c>vcMin</c>).</param>
/// <param name="Max">The value-curve's own maximum (metadata's <c>vcMax</c>).</param>
/// <param name="P1">First curve control point, when the curve type uses one (e.g. <c>Ramp</c>'s start value).</param>
/// <param name="P2">Second curve control point, when the curve type uses one (e.g. <c>Ramp</c>'s end value).</param>
/// <param name="Reverse">Whether the curve plays reversed; serializes as <c>RV=TRUE|</c> only when true (the one concrete architecture example always has it true; no documented behavior for the false case, so it is simply omitted).</param>
/// <param name="TimingTrack">Binds the curve to a named timing track's marks (<c>TT=</c>), for the "Timing Track Fade/Toggle" families.</param>
/// <param name="AudioTrack">Binds the curve to a named audio track's envelope (<c>AT=</c>), for the "Music"/"Inverted Music" families.</param>
/// <param name="FilterLabel">Restricts a timing-track binding to marks carrying this label (<c>FT=</c>).</param>
public sealed record EffectValueCurve(
    string CurveType,
    double Min,
    double Max,
    double? P1 = null,
    double? P2 = null,
    bool Reverse = false,
    string? TimingTrack = null,
    string? AudioTrack = null,
    string? FilterLabel = null);
