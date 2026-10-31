using Avalonia;
using Avalonia.Controls;

namespace XlightsAI.Studio.App.Views.Screens;

/// <summary>
/// Placeholder for a screen that is fully wired behind the glass but has no view.
/// <para>
/// Originally covered <see cref="XlightsAI.Studio.Shell.ShellScreen.PianoRoll"/>,
/// <see cref="XlightsAI.Studio.Shell.ShellScreen.StructureInspector"/> and
/// <see cref="XlightsAI.Studio.Shell.ShellScreen.DisplayPathEditor"/> — each had a view model, unit
/// tests, a navigation command, an <c>IsShowing…</c> flag and a case in the shell's own screen
/// activation switch, but no markup at all, so navigating to one painted an empty content area
/// (exactly the failure the rest of the shell takes care to avoid: every other screen has an
/// explicit loading / empty / error / loaded state so it is "never silently blank"). All three now
/// have real views (see docs/UI-CLEANUP-PLAN.md item 2, Phase 2a) and no longer use this control.
/// </para>
/// <para>
/// Kept for the dozen further view models Phase 2b/2c names (<c>StemMixerViewModel</c>,
/// <c>CandidateCompareViewModel</c>, and so on) that are not yet wired into the shell at all. This
/// control is the honest intermediate state once each gets a <see cref="XlightsAI.Studio.Shell.ShellScreen"/>
/// entry — delete each use as its own real view lands, same as the first three.
/// </para>
/// </summary>
public partial class UnbuiltScreenView : UserControl
{
    public static readonly StyledProperty<string> ScreenTitleProperty =
        AvaloniaProperty.Register<UnbuiltScreenView, string>(nameof(ScreenTitle), defaultValue: string.Empty);

    public static readonly StyledProperty<string> SummaryProperty =
        AvaloniaProperty.Register<UnbuiltScreenView, string>(nameof(Summary), defaultValue: string.Empty);

    public UnbuiltScreenView() => InitializeComponent();

    /// <summary>The screen's name, as the navigation that reaches it calls it.</summary>
    public string ScreenTitle
    {
        get => GetValue(ScreenTitleProperty);
        set => SetValue(ScreenTitleProperty, value);
    }

    /// <summary>What this screen will do, in the user's terms — not the view model's.</summary>
    public string Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }
}
