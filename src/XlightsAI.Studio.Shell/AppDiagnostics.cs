using System.Text.Json;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// File-backed structured diagnostics sink. Every event gets a short correlation ID and has the
/// current user's home directory path and username redacted from its message before it is kept
/// in memory or written to disk, per PHASE-01's "redact user filesystem paths from shared
/// diagnostics where practical" guidance.
///
/// Recording an event never throws: if the persisted log file can't be written (missing/locked
/// directory, simulated I/O failure, etc.), the event is still retained in
/// <see cref="RecentEvents"/> so the caller's failure report is never silently dropped — only the
/// durability of that one record is degraded, and that degradation itself does not crash the app.
/// </summary>
public sealed class AppDiagnostics : IAppDiagnostics
{
    private const int MaxRecentEvents = 200;

    /// <summary>
    /// The persisted log file is rotated (truncated to the most recent <see cref="MaxRecentEvents"/>
    /// lines) once it grows past this many lines, so a long-lived real installation's
    /// <c>diagnostics.log</c> does not grow without bound and does not become increasingly expensive
    /// to re-read on every startup.
    /// </summary>
    private const int MaxPersistedLinesBeforeRotation = 2000;

    private readonly string _logDirectory;
    private readonly string _logFilePath;
    private readonly List<DiagnosticEvent> _recentEvents = new();
    private readonly object _gate = new();
    private int _persistedLineCount;

    public AppDiagnostics(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _logDirectory = logDirectory;
        _logFilePath = Path.Combine(logDirectory, "diagnostics.log");
        var existing = TryReadExistingEvents();
        _recentEvents.AddRange(existing);
        _persistedLineCount = CountExistingLines();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<DiagnosticEvent> RecentEvents
    {
        get
        {
            lock (_gate)
            {
                return _recentEvents.ToArray();
            }
        }
    }

    public void RecordEvent(DiagnosticEventLevel level, string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(message);

        var entry = new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N")[..12],
            level,
            code,
            Redact(message));

        lock (_gate)
        {
            _recentEvents.Add(entry);
            if (_recentEvents.Count > MaxRecentEvents)
            {
                _recentEvents.RemoveAt(0);
            }
        }

        TryAppendToLogFile(entry);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RecordFailure(string code, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        RecordEvent(DiagnosticEventLevel.Error, code, error.Message);
    }

    private static string Redact(string message)
    {
        var redacted = message;

        string? userProfile = null;
        try
        {
            userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        catch (PlatformNotSupportedException)
        {
            // Unsupported runtime/platform for this folder lookup: fall through with no path
            // redaction rather than letting diagnostics recording itself throw.
        }

        if (!string.IsNullOrEmpty(userProfile))
        {
            redacted = redacted.Replace(userProfile, "<home>", StringComparison.OrdinalIgnoreCase);
        }

        var userName = Environment.UserName;
        if (!string.IsNullOrEmpty(userName))
        {
            redacted = redacted.Replace(userName, "<user>", StringComparison.OrdinalIgnoreCase);
        }

        return redacted;
    }

    private List<DiagnosticEvent> TryReadExistingEvents()
    {
        var events = new List<DiagnosticEvent>();

        try
        {
            if (!File.Exists(_logFilePath))
            {
                // No prior log on a clean machine/fresh install is expected, not a failure.
                return events;
            }

            foreach (var line in File.ReadAllLines(_logFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var record = JsonSerializer.Deserialize<DiagnosticEvent>(line);
                    if (record is not null)
                    {
                        events.Add(record);
                    }
                }
                catch (JsonException)
                {
                    // One corrupt line (e.g. from a prior interrupted write) must not take down
                    // the whole diagnostics subsystem; skip it and keep the rest.
                }
            }
        }
        catch (IOException)
        {
            return new List<DiagnosticEvent>();
        }
        catch (UnauthorizedAccessException)
        {
            return new List<DiagnosticEvent>();
        }

        return events.Count > MaxRecentEvents
            ? events.GetRange(events.Count - MaxRecentEvents, MaxRecentEvents)
            : events;
    }

    private void TryAppendToLogFile(DiagnosticEvent entry)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            File.AppendAllText(_logFilePath, JsonSerializer.Serialize(entry) + Environment.NewLine);

            var lineCount = Interlocked.Increment(ref _persistedLineCount);
            if (lineCount > MaxPersistedLinesBeforeRotation)
            {
                RotateLogFile();
            }
        }
        catch (IOException)
        {
            // Persisted sink unavailable; the event is already retained in RecentEvents.
        }
        catch (UnauthorizedAccessException)
        {
            // Persisted sink unavailable; the event is already retained in RecentEvents.
        }
    }

    /// <summary>
    /// Truncates the persisted log file down to its most recent <see cref="MaxRecentEvents"/> lines.
    /// Best-effort: a failure here leaves the (larger, but not corrupt) existing file in place and
    /// does not throw, consistent with every other persistence failure in this class.
    /// </summary>
    private void RotateLogFile()
    {
        try
        {
            var allLines = File.ReadAllLines(_logFilePath);
            if (allLines.Length <= MaxRecentEvents)
            {
                _persistedLineCount = allLines.Length;
                return;
            }

            var kept = allLines[^MaxRecentEvents..];
            var tempPath = $"{_logFilePath}.tmp-{Guid.NewGuid():N}";
            File.WriteAllLines(tempPath, kept);
            File.Move(tempPath, _logFilePath, overwrite: true);
            _persistedLineCount = kept.Length;
        }
        catch (IOException)
        {
            // Rotation failed; the file is simply larger than ideal, not corrupt.
        }
        catch (UnauthorizedAccessException)
        {
            // Rotation failed; the file is simply larger than ideal, not corrupt.
        }
    }

    private int CountExistingLines()
    {
        try
        {
            return File.Exists(_logFilePath) ? File.ReadAllLines(_logFilePath).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
