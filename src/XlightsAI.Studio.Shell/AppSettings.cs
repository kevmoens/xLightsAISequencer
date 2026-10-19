namespace XlightsAI.Studio.Shell;

/// <summary>
/// App-level (not project-level) settings: survives app restart, independent of any opened
/// project. Deliberately minimal for this step — the Settings screen displays these read-only;
/// there is no settings-editing UI yet.
///
/// <paramref name="LastShowDirectory"/> is new in PHASE-02 Step 009 ("persist last successful
/// folder selection"): an additive, nullable property. A settings.json written by a pre-Step-009
/// build simply has no such JSON property, which <see cref="System.Text.Json"/> deserializes as
/// <see langword="null"/> — no schema version bump needed for this purely-additive, backward
/// compatible field (unlike a breaking/removed field, which would require one).
///
/// <paramref name="WorkspaceDirectory"/>/<paramref name="HasCompletedFirstRun"/> are new in
/// PHASE-15 Step 095 ("show-folder onboarding and safe defaults"), same additive/backward-
/// compatible reasoning as <paramref name="LastShowDirectory"/>: a settings.json written by a
/// pre-Step-095 build has neither JSON property, which deserializes as <see langword="null"/>/
/// <see langword="false"/> respectively — i.e. "first run has not completed yet," the correct
/// behavior for an existing installation upgrading into this step (never silently skips the new
/// onboarding flow for a settings file that genuinely predates it).
/// </summary>
public sealed record AppSettings(
    int SchemaVersion,
    ShellScreen LastScreen,
    string? LastShowDirectory = null,
    string? WorkspaceDirectory = null,
    bool HasCompletedFirstRun = false)
{
    public const int CurrentSchemaVersion = 1;

    public static AppSettings Default => new(
        CurrentSchemaVersion, ShellScreen.Welcome, LastShowDirectory: null, WorkspaceDirectory: null, HasCompletedFirstRun: false);
}
