namespace XlightsAI.Studio.Shell;

/// <summary>
/// No-op diagnostics sink used as the default collaborator so Step 004's
/// <c>ShellViewModel(IShellNavigator, Func&lt;IReadOnlyList&lt;string&gt;&gt;)</c> call sites keep
/// compiling and behaving identically without being rewired to this step's new diagnostics
/// contract. Real composition (the App project) always supplies a real <see cref="AppDiagnostics"/>.
/// </summary>
public sealed class NullAppDiagnostics : IAppDiagnostics
{
    public static readonly NullAppDiagnostics Instance = new();

    private NullAppDiagnostics()
    {
    }

    public IReadOnlyList<DiagnosticEvent> RecentEvents => Array.Empty<DiagnosticEvent>();

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public void RecordEvent(DiagnosticEventLevel level, string code, string message)
    {
    }

    public void RecordFailure(string code, Exception error)
    {
    }
}
