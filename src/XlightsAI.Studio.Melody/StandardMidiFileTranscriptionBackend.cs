namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 048's own real <see cref="IMidiTranscriptionBackend"/> implementation. Pure, synchronous,
/// no file I/O (the caller already owns reading bytes off disk -- consistent with
/// <see cref="MelodyTrackValidator"/>/<see cref="MelodicContourClassifier"/>'s own "pure domain transform,
/// no ambient I/O" shape). Pipeline: <see cref="StandardMidiFileReader.Parse"/> the raw bytes -&gt; select
/// one melody channel -&gt; pair note-on/note-off events into <see cref="NoteEvent"/>s with their MIDI-tick
/// timing converted to canonical 100 ns ticks via a tempo map built from every Set Tempo meta event found
/// (implementation guidance, verbatim: "Parse MIDI note-on/note-off and tempo changes") -&gt;
/// <see cref="MelodyTrackValidator.Validate"/> -&gt; <see cref="MelodicContourClassifier.Classify"/> with
/// <see cref="GestureClassificationSettings.Default"/>, so an imported MIDI track is a complete,
/// gesture-classified, drop-in <see cref="MelodyTrack"/> -- "create adapter to same track" (implementation
/// guidance, verbatim) -- usable anywhere a pitch-tracking-derived track would be.
/// </summary>
public sealed class StandardMidiFileTranscriptionBackend : IMidiTranscriptionBackend
{
    public Task<MelodyTrack> ImportAsync(byte[] midiData, string sourceAssetHash, MidiImportSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(midiData);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(sourceAssetHash))
        {
            throw new MidiImportInvalidException("sourceAssetHash is empty -- missing media.");
        }

        if (settings.MelodyChannel is < 0 or > 15)
        {
            throw new MidiImportInvalidException(
                $"MidiImportSettings.MelodyChannel ({settings.MelodyChannel}) must be in [0, 15] when specified.");
        }

        if (settings.MinNoteConfidence is < 0.0 or > 1.0)
        {
            throw new MidiImportInvalidException(
                $"MidiImportSettings.MinNoteConfidence ({settings.MinNoteConfidence}) must be in [0, 1].");
        }

        ct.ThrowIfCancellationRequested();

        // Stale/invalid input is rejected here -- before any note is built or anything persisted by a
        // caller -- never partially imported (this step's own failure/edge case, verbatim: "Invalid
        // input, cancellation or stale persisted state must not corrupt the project").
        var parsed = StandardMidiFileReader.Parse(midiData);

        ct.ThrowIfCancellationRequested();

        var channel = settings.MelodyChannel ?? AutoSelectMelodyChannel(parsed.NoteEvents);
        var channelEvents = parsed.NoteEvents
            .Where(e => e.Channel == channel)
            .OrderBy(e => e.AbsoluteMidiTicks)
            .ToList();

        if (channelEvents.Count == 0)
        {
            throw new MidiImportInvalidException($"MIDI channel {channel} has no note events to import.");
        }

        var tempoMap = new MidiTempoMap(parsed.TicksPerQuarterNote, parsed.TempoChanges);
        var notes = PairNoteOnOffEvents(channelEvents, tempoMap, settings.MinNoteConfidence);

        ct.ThrowIfCancellationRequested();

        var rawTrack = new MelodyTrack(Guid.NewGuid(), sourceAssetHash, notes, Gestures: []);
        var validated = MelodyTrackValidator.Validate(rawTrack);
        var withGestures = MelodicContourClassifier.Classify(validated, GestureClassificationSettings.Default);

        return Task.FromResult(withGestures);
    }

    private static int AutoSelectMelodyChannel(IReadOnlyList<MidiRawNoteEvent> noteEvents)
    {
        var best = noteEvents
            .Where(e => e.IsNoteOn && e.Channel != 9) // channel 9 (0-based) is the General MIDI percussion channel -- never melodic pitch content.
            .GroupBy(e => e.Channel)
            .Select(g => (Channel: g.Key, Count: g.Count()))
            .OrderByDescending(g => g.Count)
            .ToList();

        if (best.Count == 0)
        {
            throw new MidiImportInvalidException(
                "No note-on events found on any non-percussion MIDI channel to auto-select as the melody line.");
        }

        return best[0].Channel;
    }

    private static List<NoteEvent> PairNoteOnOffEvents(IReadOnlyList<MidiRawNoteEvent> channelEvents, MidiTempoMap tempoMap, double confidence)
    {
        var openNotesByPitch = new Dictionary<int, Queue<MidiRawNoteEvent>>();
        var notes = new List<NoteEvent>(channelEvents.Count / 2);

        foreach (var evt in channelEvents)
        {
            if (evt.IsNoteOn)
            {
                if (!openNotesByPitch.TryGetValue(evt.MidiNote, out var queue))
                {
                    queue = new Queue<MidiRawNoteEvent>();
                    openNotesByPitch[evt.MidiNote] = queue;
                }

                queue.Enqueue(evt);
                continue;
            }

            if (!openNotesByPitch.TryGetValue(evt.MidiNote, out var openQueue) || openQueue.Count == 0)
            {
                throw new MidiImportInvalidException(
                    $"Note-off for MIDI note {evt.MidiNote} at raw tick {evt.AbsoluteMidiTicks} has no matching prior note-on on this channel.");
            }

            var onEvent = openQueue.Dequeue();
            notes.Add(new NoteEvent(
                Id: Guid.NewGuid(),
                StartTicks: tempoMap.ToCanonicalTicks(onEvent.AbsoluteMidiTicks),
                EndTicks: tempoMap.ToCanonicalTicks(evt.AbsoluteMidiTicks),
                MidiNote: onEvent.MidiNote,
                Velocity: onEvent.Velocity / 127.0,
                Confidence: confidence,
                UserCorrected: false));
        }

        var unterminated = openNotesByPitch.Values.Sum(q => q.Count);
        if (unterminated > 0)
        {
            throw new MidiImportInvalidException(
                $"{unterminated} MIDI note-on event(s) have no matching note-off before the end of the track.");
        }

        return notes.OrderBy(n => n.StartTicks).ToList();
    }

    /// <summary>
    /// Converts raw MIDI ticks (division units, per <see cref="ParsedMidiFile.TicksPerQuarterNote"/>) to
    /// canonical 100 ns ticks, honoring every Set Tempo meta event in the file (implementation guidance,
    /// verbatim: "tempo changes") via a piecewise-linear tempo map: microseconds-per-quarter-note is
    /// constant within each segment between two consecutive tempo changes (or from tick 0 to the first
    /// change), defaulting to 500,000 microseconds/quarter note (120 BPM, the MIDI specification's own
    /// default) until the first explicit Set Tempo event, if any.
    /// </summary>
    private sealed class MidiTempoMap
    {
        private readonly int _ticksPerQuarterNote;
        private readonly List<(long MidiTick, double CumulativeMicroseconds, int MicrosecondsPerQuarterNote)> _segments;

        public MidiTempoMap(int ticksPerQuarterNote, IReadOnlyList<MidiTempoChange> tempoChanges)
        {
            _ticksPerQuarterNote = ticksPerQuarterNote;
            _segments = [];

            var ordered = tempoChanges.OrderBy(t => t.AbsoluteMidiTicks).ToList();

            const int defaultMicrosecondsPerQuarterNote = 500_000;
            var currentMicrosPerQuarter = defaultMicrosecondsPerQuarterNote;

            if (ordered.Count == 0 || ordered[0].AbsoluteMidiTicks > 0)
            {
                _segments.Add((0, 0.0, currentMicrosPerQuarter));
            }

            double cumulativeMicros = 0;
            long previousTick = 0;

            foreach (var change in ordered)
            {
                cumulativeMicros += (change.AbsoluteMidiTicks - previousTick) * currentMicrosPerQuarter / (double)ticksPerQuarterNote;
                _segments.Add((change.AbsoluteMidiTicks, cumulativeMicros, change.MicrosecondsPerQuarterNote));
                previousTick = change.AbsoluteMidiTicks;
                currentMicrosPerQuarter = change.MicrosecondsPerQuarterNote;
            }
        }

        public long ToCanonicalTicks(long absoluteMidiTicks)
        {
            var segment = _segments[0];
            foreach (var candidate in _segments)
            {
                if (candidate.MidiTick > absoluteMidiTicks)
                {
                    break;
                }

                segment = candidate;
            }

            var microseconds = segment.CumulativeMicroseconds
                + (absoluteMidiTicks - segment.MidiTick) * segment.MicrosecondsPerQuarterNote / (double)_ticksPerQuarterNote;

            return (long)Math.Round(microseconds * 10.0); // 1 microsecond == 10 canonical 100 ns ticks.
        }
    }
}
