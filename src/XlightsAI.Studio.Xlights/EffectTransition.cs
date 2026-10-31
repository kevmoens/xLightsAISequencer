namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1b/1c (architecture §4 Look layers: <c>transitionIn</c>; §7 "Transition settings respect the
/// capability matrix"): one in/out transition carried on an <see cref="EffectInstance"/>. This is an IR
/// value only for now — <see cref="XsqExporter"/> does not yet serialize it into xLights'
/// <c>T_</c>-namespace settings string, since that requires Phase 2's generated effect-metadata schema
/// to know the real key names and which transition types accept <c>Adjust</c>/<c>Reverse</c>/<c>Blur</c>
/// (architecture §7). Capturing it here now means the IR can already express it before Phase 2 lands.
/// </summary>
public sealed record EffectTransition(string Type, double DurationSeconds, bool Reverse = false);
