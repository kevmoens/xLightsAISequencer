using System.Text.RegularExpressions;

namespace XlightsAI.Studio.Reference.Fixtures;

/// <summary>Outcome of validating a <see cref="FixtureCorpusManifest"/>. Never throws for bad data.</summary>
public sealed record FixtureCorpusValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static FixtureCorpusValidationResult Success { get; } = new(true, []);

    public static FixtureCorpusValidationResult Failure(IReadOnlyList<string> errors) => new(false, errors);
}

/// <summary>
/// Pure domain rules for the Step 003 baseline fixture corpus. Enforces the step's required
/// acceptance ("record input hashes, permitted usage, outputs and repeatable invocation") and its
/// implementation guidance (at minimum one synthetic-notes, one drum-pulse click track, one
/// instrumental phrase, one complex mixed-source fixture, and at least two layout XML shapes).
/// </summary>
public static class FixtureCorpusValidator
{
    private const int SupportedSchemaVersion = 1;
    private static readonly Regex Sha256Pattern = new("^[0-9A-Fa-f]{64}$", RegexOptions.Compiled);

    public static FixtureCorpusValidationResult Validate(FixtureCorpusManifest? manifest)
    {
        if (manifest is null)
        {
            return FixtureCorpusValidationResult.Failure(["Manifest is null."]);
        }

        var errors = new List<string>();

        if (manifest.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add($"Unsupported SchemaVersion {manifest.SchemaVersion}; this Studio supports version {SupportedSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(manifest.GeneratedByTool))
        {
            errors.Add("GeneratedByTool must identify the repeatable-invocation tool that produced this corpus.");
        }

        if (string.IsNullOrWhiteSpace(manifest.GeneratedWithToolVersion))
        {
            errors.Add("GeneratedWithToolVersion must be recorded so a future run can detect drift.");
        }

        if (manifest.Fixtures.Count == 0)
        {
            errors.Add("Fixtures must list at least one corpus entry.");
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fixture in manifest.Fixtures)
        {
            var label = string.IsNullOrWhiteSpace(fixture.Id) ? "<missing id>" : fixture.Id;

            if (string.IsNullOrWhiteSpace(fixture.Id))
            {
                errors.Add("Every fixture entry must have a non-empty Id.");
            }
            else if (!seenIds.Add(fixture.Id))
            {
                errors.Add($"[{label}] Duplicate fixture Id; ids must be unique within the corpus.");
            }

            if (string.IsNullOrWhiteSpace(fixture.RelativePath))
            {
                errors.Add($"[{label}] RelativePath must be recorded.");
            }

            if (string.IsNullOrWhiteSpace(fixture.Sha256) || !Sha256Pattern.IsMatch(fixture.Sha256))
            {
                errors.Add($"[{label}] Sha256 must be a 64-character hex digest. Got: '{fixture.Sha256}'.");
            }

            if (string.IsNullOrWhiteSpace(fixture.PermittedUsage))
            {
                errors.Add($"[{label}] PermittedUsage must be recorded (rights statement).");
            }

            if (string.IsNullOrWhiteSpace(fixture.LicenseNote))
            {
                errors.Add($"[{label}] LicenseNote must be recorded.");
            }

            if (string.IsNullOrWhiteSpace(fixture.ReferenceVersion))
            {
                errors.Add($"[{label}] ReferenceVersion must be recorded.");
            }

            if (fixture.ExpectedOutputs.Count == 0)
            {
                errors.Add($"[{label}] ExpectedOutputs must record at least one repeatable, checkable output.");
            }
        }

        RequireAtLeast(manifest, FixtureKind.SyntheticNotes, 1, errors);
        RequireAtLeast(manifest, FixtureKind.DrumPulseClickTrack, 1, errors);
        RequireAtLeast(manifest, FixtureKind.InstrumentalPhrase, 1, errors);
        RequireAtLeast(manifest, FixtureKind.ComplexMixedSong, 1, errors);
        RequireAtLeast(manifest, FixtureKind.LayoutShape, 2, errors);

        return errors.Count == 0
            ? FixtureCorpusValidationResult.Success
            : FixtureCorpusValidationResult.Failure(errors);
    }

    private static void RequireAtLeast(FixtureCorpusManifest manifest, FixtureKind kind, int minimumCount, List<string> errors)
    {
        var actual = manifest.Fixtures.Count(f => f.Kind == kind);
        if (actual < minimumCount)
        {
            errors.Add($"Corpus must contain at least {minimumCount} fixture(s) of kind {kind}; found {actual}.");
        }
    }
}
