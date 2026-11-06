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
///
/// <paramref name="LastProjectRootDirectory"/> is additive, same reasoning as
/// <paramref name="LastShowDirectory"/>: the root directory of the project most recently open in
/// this installation (see <see cref="XlightsAI.Studio.Projects.ProjectManifest.RootDirectory"/>),
/// restored — never auto-reopened — into <see cref="ProjectsViewModel.WorkspaceDirectory"/> the
/// next time the Projects screen is shown, same "restore the typed value, require an explicit
/// action to actually open it" convention <see cref="ShowImportViewModel.RestoreLastDirectory"/>
/// and <see cref="FirstRunViewModel.RestoreLastWorkspaceDirectory"/> already use. A settings.json
/// predating this field has no such JSON property, which deserializes as <see langword="null"/> —
/// "no project has ever been opened yet," correct for an upgrading installation.
///
/// <paramref name="CorpusRootDirectory"/> is additive, build-plan Phase 10 §3f ("Activate corpus
/// mining"): the last directory typed into the Generate &amp; Export screen's Developer Tools
/// "Run corpus mining" field (a raw <c>C:\xLights</c>-shaped corpus root, or a representative
/// subset of one), restored — same "restore the typed value, require an explicit action to
/// actually run it" convention as <see cref="LastShowDirectory"/>/<see cref="LastProjectRootDirectory"/>
/// — the next time that screen is shown. A settings.json predating this field has no such JSON
/// property, which deserializes as <see langword="null"/> — "corpus mining has never been
/// configured yet," correct for an upgrading installation.
///
/// <paramref name="RecentShowDirectories"/> is additive, user feedback (2026-11-01: "when opening an
/// existing show, can that be a drop down and select existing and it sets the directory too"). Every
/// show directory a successful <c>RunDiscoverShowDirectoryAsync</c> call has ever used, most-recent
/// first, capped at <see cref="MaxRecentShowDirectories"/> entries -- unlike the single
/// <see cref="LastShowDirectory"/> value, this is the full pick-list the Show Import screen's dropdown
/// offers. A settings.json predating this field has no such JSON property, which deserializes as an
/// empty list -- "no show directories remembered yet," correct for an upgrading installation.
///
/// <paramref name="XlightsExecutablePath"/> is additive, user feedback (2026-11-03): "Can't that be
/// loaded on start up" -- previously <see cref="ShowImportViewModel.XlightsExecutablePath"/> was a
/// plain, never-persisted bindable field that reset to empty on every app restart, so the quality-pass
/// toggle's own <c>QualityPassUnavailableException</c> re-triggered every session even after the user
/// had already typed/browsed to a working path once. Restored (never auto-validated) the same "restore
/// the typed value" convention as <see cref="LastShowDirectory"/>. A settings.json predating this field
/// has no such JSON property, which deserializes as <see langword="null"/> -- "never configured yet,"
/// correct for an upgrading installation.
/// </summary>
public sealed record AppSettings(
    int SchemaVersion,
    ShellScreen LastScreen,
    string? LastShowDirectory = null,
    string? WorkspaceDirectory = null,
    bool HasCompletedFirstRun = false,
    string? LastProjectRootDirectory = null,
    string? CorpusRootDirectory = null,
    IReadOnlyList<string>? RecentShowDirectories = null,
    string? XlightsExecutablePath = null)
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Oldest entries beyond this are dropped when a new directory is recorded -- a pick-list, not an unbounded history.</summary>
    public const int MaxRecentShowDirectories = 8;

    public IReadOnlyList<string> RecentShowDirectoriesOrEmpty => RecentShowDirectories ?? [];

    public static AppSettings Default => new(
        CurrentSchemaVersion, ShellScreen.Welcome, LastShowDirectory: null, WorkspaceDirectory: null,
        HasCompletedFirstRun: false, LastProjectRootDirectory: null, CorpusRootDirectory: null,
        RecentShowDirectories: null);
}
