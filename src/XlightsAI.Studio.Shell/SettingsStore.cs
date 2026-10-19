using System.Text.Json;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// JSON-file-backed <see cref="ISettingsStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace), matching the architecture's manifest
/// write guidance. A missing file (clean machine / fresh install) is a normal empty case, not an
/// error; a corrupt or newer-than-supported file is reported to <see cref="IAppDiagnostics"/> and
/// treated as "fall back to defaults," never a thrown exception or silent data loss of the fact
/// that something was wrong.
/// </summary>
public sealed class SettingsStore : ISettingsStore
{
    private readonly string _directory;
    private readonly string _filePath;
    private readonly IAppDiagnostics _diagnostics;

    public SettingsStore(string directory, IAppDiagnostics diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _filePath = Path.Combine(directory, "settings.json");
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return AppSettings.Default;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings is null)
            {
                _diagnostics.RecordEvent(
                    DiagnosticEventLevel.Error,
                    "settings-empty-or-invalid",
                    $"Settings file at '{_filePath}' deserialized to null; falling back to defaults.");
                return AppSettings.Default;
            }

            if (settings.SchemaVersion > AppSettings.CurrentSchemaVersion)
            {
                // Analogue of "unsupported runtime": a settings file written by a newer,
                // unsupported version of this app must not crash or silently mis-load — report
                // it and fall back, same as the project-schema rule in PHASE-01 Section 4.
                _diagnostics.RecordEvent(
                    DiagnosticEventLevel.Error,
                    "settings-unsupported-schema",
                    $"Settings file at '{_filePath}' has schema version {settings.SchemaVersion}, " +
                    $"newer than the supported {AppSettings.CurrentSchemaVersion}. Falling back to defaults.");
                return AppSettings.Default;
            }

            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _diagnostics.RecordFailure("settings-load-failed", ex);
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            Directory.CreateDirectory(_directory);
            // Unique per call: two concurrent Save calls must never share a temp file.
            var tempPath = $"{_filePath}.tmp-{Guid.NewGuid():N}";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings));
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _diagnostics.RecordFailure("settings-save-failed", ex);
        }
    }
}
