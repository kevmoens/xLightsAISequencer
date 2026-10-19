namespace XlightsAI.Studio.Shell;

/// <summary>
/// App-level settings persistence, independent of <c>IProjectStore</c> (Step 006), since
/// settings like "last screen" must survive even before any project exists.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Loads persisted settings, or <see cref="AppSettings.Default"/> if none exist yet or they could not be read. Never throws.</summary>
    AppSettings Load();

    /// <summary>Persists settings atomically. Never throws; a write failure is reported to diagnostics instead.</summary>
    void Save(AppSettings settings);
}
