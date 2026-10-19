namespace XlightsAI.Studio.Melody;

/// <summary>One channel-voice event (note-on/note-off only; every other channel-voice status is skipped -- Step 048's own scope is note timing, not full General MIDI playback) at its own absolute tick position measured in raw MIDI ticks (division units), before any tempo-map conversion to canonical 100 ns ticks.</summary>
internal readonly record struct MidiRawNoteEvent(long AbsoluteMidiTicks, int Channel, int MidiNote, int Velocity, bool IsNoteOn);

/// <summary>A Set Tempo meta event (<c>FF 51 03</c>) at its own absolute tick position, in raw MIDI ticks.</summary>
internal readonly record struct MidiTempoChange(long AbsoluteMidiTicks, int MicrosecondsPerQuarterNote);

/// <summary>The fully parsed, still tempo-unconverted contents of one Standard MIDI File.</summary>
internal sealed record ParsedMidiFile(
    int TicksPerQuarterNote,
    IReadOnlyList<MidiRawNoteEvent> NoteEvents,
    IReadOnlyList<MidiTempoChange> TempoChanges);

/// <summary>
/// PHASE-07 Step 048's own implementation guidance, verbatim: "Parse MIDI note-on/note-off and tempo
/// changes." A minimal, dependency-free Standard MIDI File (SMF, <c>.mid</c>) reader -- the repository has
/// no existing MIDI NuGet dependency (Section 2: "no Python, Docker, Git or mandatory LLM subscription for
/// end users"; this step adds no new runtime dependency either), and the SMF chunk/event format is a
/// small, stable, publicly documented binary grammar well suited to a direct, auditable implementation.
/// Supports SMF format 0 and format 1 (multi-track, shared ticks-per-quarter-note timebase) with running
/// status; explicitly rejects SMPTE-based time division (format 2 / negative division) as unsupported
/// input rather than silently mis-converting it. Every note-on/note-off and Set Tempo meta event from every
/// track chunk is returned in its own track-local absolute-tick form; merging across tracks and converting
/// to canonical ticks is <see cref="StandardMidiFileTranscriptionBackend"/>'s own later responsibility.
/// </summary>
internal static class StandardMidiFileReader
{
    private static readonly byte[] HeaderChunkId = "MThd"u8.ToArray();
    private static readonly byte[] TrackChunkId = "MTrk"u8.ToArray();

    public static ParsedMidiFile Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < 14)
        {
            throw new MidiImportInvalidException(
                $"MIDI data is too short ({data.Length} bytes) to contain a valid MThd header.");
        }

        var pos = 0;
        RequireChunkId(data, ref pos, HeaderChunkId, "MThd");
        var headerLength = ReadUInt32BigEndian(data, ref pos);
        if (headerLength != 6)
        {
            throw new MidiImportInvalidException($"MThd header length must be 6, was {headerLength}.");
        }

        var format = ReadUInt16BigEndian(data, ref pos);
        var trackCount = ReadUInt16BigEndian(data, ref pos);
        var division = ReadUInt16BigEndian(data, ref pos);

        if (format is not (0 or 1))
        {
            throw new MidiImportInvalidException($"Unsupported MIDI format {format} -- only format 0 and format 1 are supported.");
        }

        if ((division & 0x8000) != 0)
        {
            throw new MidiImportInvalidException("SMPTE-based MIDI time division is not supported -- only ticks-per-quarter-note division is supported.");
        }

        if (division == 0)
        {
            throw new MidiImportInvalidException("MIDI header declares a ticks-per-quarter-note division of 0.");
        }

        if (trackCount == 0)
        {
            throw new MidiImportInvalidException("MIDI file declares 0 tracks.");
        }

        var noteEvents = new List<MidiRawNoteEvent>();
        var tempoChanges = new List<MidiTempoChange>();

        for (var trackIndex = 0; trackIndex < trackCount; trackIndex++)
        {
            ParseTrackChunk(data, ref pos, noteEvents, tempoChanges);
        }

        return new ParsedMidiFile(division, noteEvents, tempoChanges);
    }

    private static void ParseTrackChunk(byte[] data, ref int pos, List<MidiRawNoteEvent> noteEvents, List<MidiTempoChange> tempoChanges)
    {
        RequireChunkId(data, ref pos, TrackChunkId, "MTrk");
        var trackLength = ReadUInt32BigEndian(data, ref pos);

        if (trackLength > int.MaxValue || pos + trackLength > data.Length)
        {
            throw new MidiImportInvalidException(
                $"MTrk chunk at byte offset {pos - 8} declares a length ({trackLength}) extending past the end of the file.");
        }

        var trackEnd = pos + (int)trackLength;
        long absoluteTicks = 0;
        byte runningStatus = 0;

        while (pos < trackEnd)
        {
            absoluteTicks += ReadVariableLengthQuantity(data, ref pos, trackEnd);

            if (pos >= trackEnd)
            {
                throw new MidiImportInvalidException("MTrk chunk ended in the middle of an event.");
            }

            var statusByte = data[pos];
            if (statusByte < 0x80)
            {
                // Running status: this byte is actually the first data byte of a repeated channel-voice event.
                if (runningStatus == 0)
                {
                    throw new MidiImportInvalidException("MTrk chunk uses running status before any status byte has been seen.");
                }

                statusByte = runningStatus;
            }
            else
            {
                pos++;
                runningStatus = statusByte is >= 0x80 and < 0xF0 ? statusByte : runningStatus;
            }

            if (statusByte == 0xFF)
            {
                ParseMetaEvent(data, ref pos, trackEnd, absoluteTicks, tempoChanges);
            }
            else if (statusByte is 0xF0 or 0xF7)
            {
                var sysexLength = ReadVariableLengthQuantity(data, ref pos, trackEnd);
                RequireRemaining(pos, sysexLength, trackEnd, "sysex event");
                pos += (int)sysexLength;
            }
            else if (statusByte is >= 0x80 and <= 0xEF)
            {
                ParseChannelVoiceEvent(data, ref pos, trackEnd, statusByte, absoluteTicks, noteEvents);
            }
            else
            {
                throw new MidiImportInvalidException($"Unsupported/invalid MIDI status byte 0x{statusByte:X2} at byte offset {pos}.");
            }
        }
    }

    private static void ParseChannelVoiceEvent(byte[] data, ref int pos, int trackEnd, byte statusByte, long absoluteTicks, List<MidiRawNoteEvent> noteEvents)
    {
        var highNibble = statusByte & 0xF0;
        var channel = statusByte & 0x0F;
        var dataByteCount = highNibble is 0xC0 or 0xD0 ? 1 : 2;

        RequireRemaining(pos, dataByteCount, trackEnd, "channel voice event");
        var data1 = data[pos++];
        var data2 = dataByteCount == 2 ? data[pos++] : 0;

        switch (highNibble)
        {
            case 0x90 when data2 > 0:
                noteEvents.Add(new MidiRawNoteEvent(absoluteTicks, channel, data1, data2, IsNoteOn: true));
                break;
            case 0x90: // note-on with velocity 0 is conventionally a note-off
            case 0x80:
                noteEvents.Add(new MidiRawNoteEvent(absoluteTicks, channel, data1, data2, IsNoteOn: false));
                break;
            default:
                // Poly pressure, control change, program change, channel pressure, pitch bend -- Step 048's
                // own scope is note timing/tempo only; every other channel-voice event is intentionally skipped.
                break;
        }
    }

    private static void ParseMetaEvent(byte[] data, ref int pos, int trackEnd, long absoluteTicks, List<MidiTempoChange> tempoChanges)
    {
        RequireRemaining(pos, 1, trackEnd, "meta event type");
        var metaType = data[pos++];
        var length = ReadVariableLengthQuantity(data, ref pos, trackEnd);
        RequireRemaining(pos, length, trackEnd, "meta event data");

        if (metaType == 0x51) // Set Tempo
        {
            if (length != 3)
            {
                throw new MidiImportInvalidException($"Set Tempo meta event must have length 3, was {length}.");
            }

            var microsecondsPerQuarterNote = (data[pos] << 16) | (data[pos + 1] << 8) | data[pos + 2];
            if (microsecondsPerQuarterNote <= 0)
            {
                throw new MidiImportInvalidException($"Set Tempo meta event declares a non-positive tempo ({microsecondsPerQuarterNote} microseconds/quarter note).");
            }

            tempoChanges.Add(new MidiTempoChange(absoluteTicks, microsecondsPerQuarterNote));
        }

        pos += (int)length;
    }

    private static void RequireChunkId(byte[] data, ref int pos, byte[] expectedId, string chunkName)
    {
        RequireRemaining(pos, 4, data.Length, $"{chunkName} chunk id");
        if (data.AsSpan(pos, 4).SequenceEqual(expectedId) == false)
        {
            throw new MidiImportInvalidException($"Expected '{chunkName}' chunk id at byte offset {pos}, found other data.");
        }

        pos += 4;
    }

    private static void RequireRemaining(int pos, long needed, int limit, string context)
    {
        if (needed < 0 || pos + needed > limit)
        {
            throw new MidiImportInvalidException($"MIDI data ended unexpectedly while reading {context} at byte offset {pos}.");
        }
    }

    private static uint ReadUInt32BigEndian(byte[] data, ref int pos)
    {
        RequireRemaining(pos, 4, data.Length, "32-bit big-endian value");
        var value = (uint)((data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3]);
        pos += 4;
        return value;
    }

    private static int ReadUInt16BigEndian(byte[] data, ref int pos)
    {
        RequireRemaining(pos, 2, data.Length, "16-bit big-endian value");
        var value = (data[pos] << 8) | data[pos + 1];
        pos += 2;
        return value;
    }

    private static long ReadVariableLengthQuantity(byte[] data, ref int pos, int limit)
    {
        long value = 0;
        for (var i = 0; i < 4; i++)
        {
            RequireRemaining(pos, 1, limit, "variable-length quantity");
            var b = data[pos++];
            value = (value << 7) | (uint)(b & 0x7F);
            if ((b & 0x80) == 0)
            {
                return value;
            }
        }

        throw new MidiImportInvalidException("Variable-length quantity exceeded the maximum of 4 bytes.");
    }
}
