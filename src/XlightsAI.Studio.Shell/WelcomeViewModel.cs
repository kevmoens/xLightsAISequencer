namespace XlightsAI.Studio.Shell;

/// <summary>Static welcome/landing screen content; no state machine of its own.</summary>
public sealed class WelcomeViewModel : ObservableObject
{
    public string Title => "xLights AI Choreography Studio";

    public string Subtitle => "No Python runtime. No Docker. Your show files stay untouched.";
}
