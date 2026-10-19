using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 080's own real <see cref="ISeededCandidateGenerator"/>: a deterministic parameter
/// variation and seeded gesture generation (this step's own implementation guidance, verbatim), every
/// generated <see cref="TransitionGesture"/> constrained to <see cref="LoopRun.GestureFamily"/>'s own
/// single <see cref="TransitionKind"/> (this step's own heading: "constrained to selected gesture
/// family") and to exactly the props named in <see cref="LoopRun.Scope"/>'s own
/// <see cref="XlightsAI.Studio.Editing.EditScope.Models"/> (this step's own implementation guidance:
/// "preserve... edit permissions" -- a generated gesture can never target a prop outside the run's own
/// permitted scope). Reuses PHASE-10's own already-accepted <see cref="TransitionGesture"/>/
/// <see cref="TransitionKind"/> shapes unmodified rather than inventing a parallel "plan" type (Step
/// 079's own disclosed ADR, STEP-079.md Section 3.1).
/// </summary>
public sealed class DeterministicGestureCandidateGenerator : ISeededCandidateGenerator
{
    public Task<CandidateAttempt> GenerateAsync(LoopRun run, CandidateAttempt? prior, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ct.ThrowIfCancellationRequested();

        if (run.GestureFamily is not { } family)
        {
            throw new RefinementRunInvalidException(
                $"LoopRun '{run.Id}' has no GestureFamily selected; a seeded candidate generator requires one.");
        }

        var attempt = (prior?.Attempt ?? -1) + 1;

        // A large odd stride keeps successive attempts from aliasing onto the same seed while remaining a
        // pure, reproducible function of (run.Seed, attempt) alone -- no Guid.NewGuid()/wall clock/random
        // source is consulted anywhere in this method.
        var attemptSeed = unchecked(run.Seed + (attempt * 7_919));
        var recordedAtUtc = DateTimeOffset.UtcNow;

        // EditScope.Models is an unordered set -- its own enumeration order is not a reproducibility
        // guarantee. Sorting by value is this generator's own deterministic tie-break, applied identically
        // every time regardless of set insertion history.
        var sortedModelValues = run.Scope.Models
            .Select(m => m.Value)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        var identityKey = $"{run.Id:N}|{attempt}|{attemptSeed}|{family}";

        if (sortedModelValues.Length == 0)
        {
            // This step's own required failure/edge case, verbatim: "Malformed proposal... must not
            // auto-commit." An edit scope naming no permitted model cannot produce any gesture at all
            // (TransitionGesture.Validate() itself requires at least one target prop) -- recorded as a
            // violating, Rejected attempt rather than thrown, the same shape Step 079's own hard-gate
            // rejection path already expects (CandidateAttempt.Violations non-empty).
            return Task.FromResult(new CandidateAttempt(
                Id: DeterministicContent.GuidFrom($"attempt|{identityKey}|empty-scope"),
                RunId: run.Id,
                Attempt: attempt,
                Seed: attemptSeed,
                SnapshotHash: DeterministicContent.Hash($"empty-scope|{identityKey}"),
                Status: RefinementStatus.Rejected,
                Violations: ["Edit scope names no permitted models; a candidate requires at least one target model."],
                RecordedAtUtc: recordedAtUtc,
                Gestures: []));
        }

        var rng = new Random(attemptSeed);
        var (startIntensity, endIntensity) = IntensityRangeFor(family, rng);
        var targetProps = sortedModelValues.Select(v => new ModelId(v)).ToArray();

        var gesture = new TransitionGesture(
            Id: DeterministicContent.GuidFrom($"gesture|{identityKey}"),
            Kind: family,
            Window: run.Scope.Time,
            TargetProps: targetProps,
            StartIntensity: startIntensity,
            EndIntensity: endIntensity);

        var violations = new List<string>();
        try
        {
            gesture.Validate();
        }
        catch (CompositionInvalidException ex)
        {
            violations.Add(ex.Message);
        }

        var gestures = violations.Count == 0
            ? new[] { gesture }
            : Array.Empty<TransitionGesture>();

        var snapshotHash = DeterministicContent.Hash(string.Join(
            '|',
            "gesture-plan",
            identityKey,
            string.Join(',', sortedModelValues),
            run.Scope.Time.StartTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            run.Scope.Time.EndTicks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            startIntensity.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            endIntensity.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

        var candidate = new CandidateAttempt(
            Id: DeterministicContent.GuidFrom($"attempt|{identityKey}"),
            RunId: run.Id,
            Attempt: attempt,
            Seed: attemptSeed,
            SnapshotHash: snapshotHash,
            Status: violations.Count == 0 ? RefinementStatus.PreviewReady : RefinementStatus.Rejected,
            Violations: violations,
            RecordedAtUtc: recordedAtUtc,
            Gestures: gestures);

        candidate.Validate();
        return Task.FromResult(candidate);
    }

    /// <summary>
    /// One deterministic start/end intensity recipe per <see cref="TransitionKind"/> -- the concrete
    /// mechanism behind "constrained to selected gesture family": a <see cref="TransitionKind.Build"/>
    /// run can never generate a <see cref="TransitionKind.Release"/>-shaped ramp, because the family is
    /// fixed for the whole run and each family maps to exactly one shape here, mirroring
    /// PHASE-10 Step 070's own <c>TransitionRecipePlanner</c> convention (one deterministic recipe per
    /// <see cref="TransitionKind"/>) rather than letting the seed pick the family too.
    /// </summary>
    private static (double Start, double End) IntensityRangeFor(TransitionKind kind, Random rng) => kind switch
    {
        TransitionKind.Build => (Lerp(rng, 0.0, 0.4), Lerp(rng, 0.6, 1.0)),
        TransitionKind.Release => (Lerp(rng, 0.6, 1.0), Lerp(rng, 0.0, 0.4)),
        TransitionKind.Transition => Steady(Lerp(rng, 0.3, 0.7)),
        TransitionKind.PhraseEnding => (Lerp(rng, 0.3, 0.7), Lerp(rng, 0.05, 0.3)),
        TransitionKind.Blackout => (0.0, 0.0),
        _ => throw new RefinementRunInvalidException($"Unsupported gesture family '{kind}'."),
    };

    private static double Lerp(Random rng, double min, double max) => min + (rng.NextDouble() * (max - min));

    private static (double, double) Steady(double value) => (value, value);
}
