using System.Globalization;
using System.Text.RegularExpressions;
using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Default <see cref="IStructuredEditInterpreter"/>. A small, closed, regular-expression grammar —
/// never a general natural-language parser and never a call to any optional AI planner (Section 2:
/// "Parse deterministic commands... without a model when possible"). Every recognized instruction maps
/// to exactly one <see cref="StructuredEditOperationKinds"/> value with its own named, range/vocabulary-
/// checked parameter; anything else is <see cref="EditRejectionReason.UnrecognizedInstruction"/>.
/// </summary>
public sealed class StructuredCommandInterpreter : IStructuredEditInterpreter
{
    private const int DimLevel = 20;
    private const int BrightenLevel = 100;
    private const decimal FasterMultiplier = 1.5m;
    private const decimal SlowerMultiplier = 0.5m;
    private const decimal MinSpeedMultiplier = 0.1m;
    private const decimal MaxSpeedMultiplier = 8m;

    private static readonly Regex BrightnessLevel = new(@"^brightness\s+(?<level>-?\d{1,3})%?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DirectionNamed = new(@"^direction\s+(?<direction>left-to-right|right-to-left|reverse|follow-lead)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SpeedMultiplier = new(@"^speed\s+(?<multiplier>\d+(\.\d+)?)x$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SnapToGridPattern = new(@"^snap(\s+to\s+grid)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FollowRole = new(@"^follow\s+(?<role>lead|rhythm|bass|harmony|accent|atmosphere)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IEffectLockPolicy _lockPolicy;

    public StructuredCommandInterpreter(IEffectLockPolicy lockPolicy)
    {
        ArgumentNullException.ThrowIfNull(lockPolicy);
        _lockPolicy = lockPolicy;
    }

    public EditProposal ParseKnownCommand(
        string instruction,
        SelectionContext context,
        SequenceDocument document,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);

        // Gate 0: cancellation — honored before any other gate runs, so a caller that cancels mid-
        // request never observes a half-built proposal (Step 073's own required failure/edge case:
        // "cancellation... must not corrupt the project").
        ct.ThrowIfCancellationRequested();

        // Gate 1: stale revision — the selection this instruction targets was captured against a
        // document view that no longer matches the document's current state.
        if (context.BaseDocumentRevision.Value != document.Revision)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.StaleRevision,
                $"Expected revision {context.BaseDocumentRevision.Value} does not match the document's current revision {document.Revision}; no proposal was built.");
        }

        // Gate 2: locks — an instruction can never even be turned into a proposal that would reach a
        // locked effect, independent of which specific operation it describes.
        _lockPolicy.AssertEditable(document, context.Scope);

        // Gate 3: grammar + parameter validation — every recognized verb below either returns a fully
        // valid (Kind, Parameters, Explanation) triple or throws; no partial result crosses this boundary.
        var trimmed = instruction.Trim();
        var (kind, parameters, explanation) = Interpret(trimmed);

        var operation = new StructuredEditOperation(
            Guid.NewGuid().ToString("N"),
            kind,
            context.Scope.Time,
            context.Scope.Models.ToArray(),
            parameters);

        return new EditProposal(Guid.NewGuid(), document.Revision, [operation], explanation);
    }

    private static (string Kind, IReadOnlyDictionary<string, string> Parameters, string Explanation) Interpret(string trimmed)
    {
        if (trimmed.Length == 0)
        {
            throw Unrecognized(trimmed);
        }

        if (string.Equals(trimmed, "brighten", StringComparison.OrdinalIgnoreCase))
        {
            return Brightness(BrightenLevel);
        }

        if (string.Equals(trimmed, "dim", StringComparison.OrdinalIgnoreCase))
        {
            return Brightness(DimLevel);
        }

        var brightnessMatch = BrightnessLevel.Match(trimmed);
        if (brightnessMatch.Success)
        {
            var level = int.Parse(brightnessMatch.Groups["level"].Value, CultureInfo.InvariantCulture);
            if (level is < 0 or > 100)
            {
                throw new EditCommandRejectedException(
                    EditRejectionReason.UnsupportedParameter,
                    $"Brightness level {level} is outside the supported 0-100 range; no proposal was built.");
            }

            return Brightness(level);
        }

        if (string.Equals(trimmed, "reverse", StringComparison.OrdinalIgnoreCase))
        {
            return Direction("Reverse");
        }

        var directionMatch = DirectionNamed.Match(trimmed);
        if (directionMatch.Success)
        {
            return Direction(ToDirectionValue(directionMatch.Groups["direction"].Value));
        }

        if (string.Equals(trimmed, "faster", StringComparison.OrdinalIgnoreCase))
        {
            return Speed(FasterMultiplier);
        }

        if (string.Equals(trimmed, "slower", StringComparison.OrdinalIgnoreCase))
        {
            return Speed(SlowerMultiplier);
        }

        var speedMatch = SpeedMultiplier.Match(trimmed);
        if (speedMatch.Success)
        {
            var multiplier = decimal.Parse(speedMatch.Groups["multiplier"].Value, CultureInfo.InvariantCulture);
            if (multiplier < MinSpeedMultiplier || multiplier > MaxSpeedMultiplier)
            {
                throw new EditCommandRejectedException(
                    EditRejectionReason.UnsupportedParameter,
                    $"Speed multiplier {multiplier.ToString(CultureInfo.InvariantCulture)}x is outside the supported {MinSpeedMultiplier.ToString(CultureInfo.InvariantCulture)}x-{MaxSpeedMultiplier.ToString(CultureInfo.InvariantCulture)}x range; no proposal was built.");
            }

            return Speed(multiplier);
        }

        if (SnapToGridPattern.IsMatch(trimmed))
        {
            return (
                StructuredEditOperationKinds.SnapToGrid,
                new Dictionary<string, string>(StringComparer.Ordinal),
                "Snap selected effects onto the beat grid.");
        }

        var roleMatch = FollowRole.Match(trimmed);
        if (roleMatch.Success)
        {
            var role = Enum.Parse<MusicalRole>(roleMatch.Groups["role"].Value, ignoreCase: true);
            return (
                StructuredEditOperationKinds.ReplaceRole,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Role"] = role.ToString() },
                $"Follow the {role} musical role instead of the current source.");
        }

        throw Unrecognized(trimmed);
    }

    private static (string, IReadOnlyDictionary<string, string>, string) Brightness(int level) => (
        StructuredEditOperationKinds.SetBrightness,
        new Dictionary<string, string>(StringComparer.Ordinal) { ["Level"] = level.ToString(CultureInfo.InvariantCulture) },
        $"Set brightness to {level}%.");

    private static (string, IReadOnlyDictionary<string, string>, string) Direction(string direction) => (
        StructuredEditOperationKinds.SetDirection,
        new Dictionary<string, string>(StringComparer.Ordinal) { ["Direction"] = direction },
        $"Set direction to {direction}.");

    private static (string, IReadOnlyDictionary<string, string>, string) Speed(decimal multiplier) => (
        StructuredEditOperationKinds.SetSpeed,
        new Dictionary<string, string>(StringComparer.Ordinal) { ["Multiplier"] = multiplier.ToString(CultureInfo.InvariantCulture) },
        $"Set speed to {multiplier.ToString(CultureInfo.InvariantCulture)}x.");

    private static string ToDirectionValue(string token) => token.ToLowerInvariant() switch
    {
        "left-to-right" => "LeftToRight",
        "right-to-left" => "RightToLeft",
        "reverse" => "Reverse",
        "follow-lead" => "FollowLead",
        _ => throw Unrecognized(token),
    };

    private static EditCommandRejectedException Unrecognized(string instruction) => new(
        EditRejectionReason.UnrecognizedInstruction,
        $"Instruction '{instruction}' does not match any known local command; no proposal was built.");
}
