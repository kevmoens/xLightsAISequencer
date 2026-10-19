namespace XlightsAI.Studio.Projects;

/// <summary>
/// Thrown when a project's <c>studio.db</c> on-disk schema version (<see cref="FoundVersion"/>) is
/// newer than this build supports (<see cref="SupportedVersion"/>). The database file is guaranteed
/// untouched when this is thrown: no backup is taken and no transaction is opened for a rejected
/// downgrade. Mirrors <see cref="ProjectSchemaNotSupportedException"/>'s manifest-level contract for
/// the SQLite layer (PHASE-01 Section 6, Step 007 failure case: "Out-of-scope ... revision must be
/// rejected without partial mutation").
/// </summary>
public sealed class ProjectDatabaseSchemaNotSupportedException : Exception
{
    public int FoundVersion { get; }

    public int SupportedVersion { get; }

    public string DatabasePath { get; }

    public ProjectDatabaseSchemaNotSupportedException(int foundVersion, int supportedVersion, string databasePath)
        : base(
            $"Project database schema version {foundVersion} at '{databasePath}' is newer than the " +
            $"supported version {supportedVersion}. This build will not downgrade or modify it.")
    {
        FoundVersion = foundVersion;
        SupportedVersion = supportedVersion;
        DatabasePath = databasePath;
    }
}
