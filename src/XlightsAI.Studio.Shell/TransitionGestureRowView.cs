using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only bindable projection of one <see cref="TransitionGesture"/> for SCREEN 12 CHOREOGRAPHY's
/// build/silence/transition/release/phrase-ending recipe table. Same "Avalonia-free Shell layer projects pure
/// domain data into something directly bindable" convention <see cref="SectionVisualDirectionRowView"/>
/// already established.
/// </summary>
/// <param name="Id">The gesture this row describes.</param>
/// <param name="Kind">Which reusable semantic recipe produced this gesture.</param>
/// <param name="TargetPropCount">The number of props this gesture targets -- for <see cref="TransitionKind.Blackout"/> this equals the number of permitted props supplied at plan time.</param>
/// <param name="StartIntensityPercent"><see cref="TransitionGesture.StartIntensity"/> rounded to the nearest whole percent.</param>
/// <param name="EndIntensityPercent"><see cref="TransitionGesture.EndIntensity"/> rounded to the nearest whole percent.</param>
/// <param name="IsIntentionalBlackout"><see langword="true"/> exactly when <see cref="Kind"/> is <see cref="TransitionKind.Blackout"/> -- a directly bindable flag for a UI to visually distinguish an intentional darkening from every other recipe.</param>
public sealed record TransitionGestureRowView(
    Guid Id,
    TransitionKind Kind,
    IReadOnlyList<ModelId> TargetProps,
    int TargetPropCount,
    int StartIntensityPercent,
    int EndIntensityPercent,
    bool IsIntentionalBlackout);
