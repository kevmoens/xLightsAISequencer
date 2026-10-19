namespace XlightsAI.Studio.Shell;

/// <summary>
/// Owns the shell's startup state machine and current-screen navigation. Framework-agnostic
/// (no Avalonia dependency) so it is directly unit-testable; the Avalonia shell binds to it.
/// </summary>
public interface IShellNavigator
{
    StartupState State { get; }
    StartupFailure? Failure { get; }
    ShellScreen CurrentScreen { get; }

    event EventHandler? Changed;

    /// <summary>Transition Starting -> Ready. Invalid from any other state.</summary>
    void CompleteStartup();

    /// <summary>Transition Starting -> Failed with an explicit, user-readable reason. Invalid from any other state.</summary>
    void FailStartup(string code, string message);

    /// <summary>Navigate to a screen. Only valid once <see cref="State"/> is <see cref="StartupState.Ready"/>.</summary>
    void NavigateTo(ShellScreen screen);
}
