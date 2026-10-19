namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 051 — "Handle polyphony... gracefully." Pure, synchronous, no file I/O, no ambient
/// clock -- the same shape as <see cref="MelodyTrackValidator"/>/<see cref="MelodicContourClassifier"/>.
///
/// <see cref="MelodyTrackValidator.Validate"/> already enforces a strictly monophonic single melody
/// line (Step 045's own already-committed, immutable negative path:
/// <c>FullyContainedOverlap_CannotBeNormalized_ThrowsMelodyTrackInvalidException_NegativePath</c> --
/// a note that is fully contained by/coincides with another cannot be "trimmed to touching" and is
/// rejected outright). A genuinely polyphonic source (two or more simultaneous real onsets -- a chord,
/// or ordinary polyphonic interference bleeding into an isolated-lead stem; Section 7's own named
/// "known technical risk") would hit that exact throw on first validation. Reducing a raw, possibly-
/// polyphonic candidate set down to one deterministic monophonic "lead" line *before* it ever reaches
/// <see cref="MelodyTrackValidator"/> is this type's one job -- "gracefully" means producing a usable,
/// validator-safe monophonic track instead of raising <see cref="MelodyTrackInvalidException"/> on
/// real-world chordal input, not promising polyphonic transcription (Section 1: "no promise of
/// polyphonic note-perfect transcription" is explicitly out of this phase's scope).
/// </summary>
public static class MelodyPolyphonyReducer
{
    /// <summary>
    /// Sweeps <paramref name="track"/>'s own <see cref="MelodyTrack.Notes"/> in start-time order, keeping
    /// exactly one "lead" note active at any instant. When an incoming note's window truly overlaps the
    /// current lead (not merely touches it), the two are compared by the declared dominance policy
    /// (<see cref="Dominates"/>): the losing note is either entirely dropped (it never surfaces, e.g. a
    /// harmony note fully inside the lead's own span) or the current lead is truncated to the newly
    /// dominant note's own start (the lead's own tail is trimmed away, mirroring
    /// <see cref="MelodyTrackValidator"/>'s own "trim the earlier note's end" normalization, just applied
    /// *before* validation so a genuinely overlapping pair never reaches it in the first place). Any
    /// gesture already present on <paramref name="track"/> is discarded (same precedent as
    /// <see cref="MelodicContourClassifier.Classify"/> -- a caller re-classifies the reduced result
    /// afterward; a gesture computed against the pre-reduction note set could otherwise dangle). Throws
    /// <see cref="MelodyTrackInvalidException"/> only for the same already-proven unit/media failures
    /// <see cref="MelodyTrackValidator.Validate"/> itself enforces (invoked here as the final pass, so a
    /// genuinely invalid note is still never silently accepted) -- never for an overlap this type itself
    /// just resolved.
    /// </summary>
    public static MelodyTrack Reduce(MelodyTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (track.Notes.Count == 0)
        {
            return MelodyTrackValidator.Validate(track with { Gestures = [] });
        }

        var sorted = track.Notes.OrderBy(n => n.StartTicks).ToList();
        var result = new List<NoteEvent>(sorted.Count);

        var lead = sorted[0];
        for (var i = 1; i < sorted.Count; i++)
        {
            var candidate = sorted[i];

            if (candidate.StartTicks >= lead.EndTicks)
            {
                // No overlap with the current lead at all -- the lead is final, move on.
                result.Add(lead);
                lead = candidate;
                continue;
            }

            // Genuine overlap (polyphony): exactly one of the two survives as the lead going forward.
            if (Dominates(candidate, lead))
            {
                // The candidate wins -- the current lead's own tail is trimmed away at the point the
                // dominant note actually starts sounding, rather than dropping it outright, so a real
                // note that *started* the phrase is never erased entirely, only shortened.
                if (candidate.StartTicks > lead.StartTicks)
                {
                    result.Add(lead with { EndTicks = candidate.StartTicks });
                }
                // else: candidate starts at/before the lead's own start (full coincidence) -- the lead
                // contributes nothing observable and is dropped entirely.

                lead = candidate;
            }
            // else: the candidate loses outright and is dropped -- a harmony/interference note fully
            // subordinate to the already-sounding lead never surfaces in the reduced, monophonic result.
        }

        result.Add(lead);

        return MelodyTrackValidator.Validate(track with { Notes = result, Gestures = [] });
    }

    /// <summary>
    /// Deterministic dominance order for two simultaneous candidate notes: higher
    /// <see cref="NoteEvent.Confidence"/> wins; a tie breaks on higher <see cref="NoteEvent.Velocity"/>;
    /// a further tie breaks on the earlier <see cref="NoteEvent.StartTicks"/> (the note that was already
    /// sounding keeps precedence); a complete tie keeps the existing lead (never reshuffles two otherwise
    /// indistinguishable notes against each other).
    /// </summary>
    private static bool Dominates(NoteEvent candidate, NoteEvent currentLead)
    {
        if (candidate.Confidence != currentLead.Confidence)
        {
            return candidate.Confidence > currentLead.Confidence;
        }

        if (candidate.Velocity != currentLead.Velocity)
        {
            return candidate.Velocity > currentLead.Velocity;
        }

        return candidate.StartTicks < currentLead.StartTicks;
    }
}
