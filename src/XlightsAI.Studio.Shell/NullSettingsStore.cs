namespace XlightsAI.Studio.Shell;

/// <summary>
/// No-op settings store used as the default collaborator so Step 004's
/// <c>ShellViewModel(IShellNavigator, Func&lt;IReadOnlyList&lt;string&gt;&gt;)</c> call sites keep
/// compiling and behaving identically (always starts on Welcome, nothing persisted) without being
/// rewired to this step's new settings contract. Real composition (the App project) always
/// supplies a real <see cref="SettingsStore"/>.
/// </summary>
public sealed class NullSettingsStore : ISettingsStore
{
    public static readonly NullSettingsStore Instance = new();

    private NullSettingsStore()
    {
    }

    public AppSettings Load() => AppSettings.Default;

    public void Save(AppSettings settings)
    {
    }
}
