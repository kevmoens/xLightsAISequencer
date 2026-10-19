namespace XlightsAI.Studio.Shell;

/// <summary>
/// Explicit startup/navigation state machine: Starting -&gt; Ready | Failed; navigation is only
/// permitted once Ready. Invalid transitions throw rather than silently no-op, so a caller bug
/// (e.g. a view navigating before startup completed) is never masked as "nothing happened."
/// </summary>
public sealed class ShellNavigator : IShellNavigator
{
    public StartupState State { get; private set; } = StartupState.Starting;
    public StartupFailure? Failure { get; private set; }
    public ShellScreen CurrentScreen { get; private set; } = ShellScreen.Welcome;

    public event EventHandler? Changed;

    public void CompleteStartup()
    {
        if (State != StartupState.Starting)
        {
            throw new InvalidOperationException(
                $"Cannot complete startup from state '{State}'; startup already resolved.");
        }

        State = StartupState.Ready;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void FailStartup(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (State != StartupState.Starting)
        {
            throw new InvalidOperationException(
                $"Cannot fail startup from state '{State}'; startup already resolved.");
        }

        State = StartupState.Failed;
        Failure = new StartupFailure(code, message);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void NavigateTo(ShellScreen screen)
    {
        if (State != StartupState.Ready)
        {
            throw new InvalidOperationException(
                $"Cannot navigate to '{screen}' while shell state is '{State}'; navigation requires Ready.");
        }

        if (CurrentScreen == screen)
        {
            return;
        }

        CurrentScreen = screen;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
