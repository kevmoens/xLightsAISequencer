namespace XlightsAI.Studio.Melody.Tests;

/// <summary>
/// Hand-rolled, dependency-free Standard MIDI File (format 0, single track) byte builder for Step 048's
/// own acceptance tests -- the repository has no MIDI-authoring NuGet dependency (by design; see
/// <c>StandardMidiFileReader</c>), so test fixtures are built directly from the same documented SMF
/// chunk/event grammar the real parser consumes, the same "construct the real binary fixture, not a mock"
/// precedent <c>DeterministicSyntheticAudioGenerator</c> already established for WAV fixtures in Step 046.
/// </summary>
internal static class MidiFixtureBuilder
{
    public readonly record struct Event(long Tick, byte[] Bytes);

    public static Event NoteOn(long tick, int channel, int note, int velocity) =>
        new(tick, [(byte)(0x90 | channel), (byte)note, (byte)velocity]);

    public static Event NoteOff(long tick, int channel, int note, int velocity = 64) =>
        new(tick, [(byte)(0x80 | channel), (byte)note, (byte)velocity]);

    public static Event Tempo(long tick, int microsecondsPerQuarterNote) => new(
        tick,
        [0xFF, 0x51, 0x03, (byte)((microsecondsPerQuarterNote >> 16) & 0xFF), (byte)((microsecondsPerQuarterNote >> 8) & 0xFF), (byte)(microsecondsPerQuarterNote & 0xFF)]);

    public static byte[] BuildSingleTrackFile(int ticksPerQuarterNote, IEnumerable<Event> events, int format = 0)
    {
        var ordered = events.OrderBy(e => e.Tick).ToList();
        var track = new List<byte>();
        long previousTick = 0;

        foreach (var evt in ordered)
        {
            WriteVariableLengthQuantity(track, evt.Tick - previousTick);
            previousTick = evt.Tick;
            track.AddRange(evt.Bytes);
        }

        WriteVariableLengthQuantity(track, 0);
        track.AddRange([0xFF, 0x2F, 0x00]); // End of Track meta event.

        var file = new List<byte>();
        file.AddRange("MThd"u8.ToArray());
        WriteUInt32BigEndian(file, 6);
        WriteUInt16BigEndian(file, (ushort)format);
        WriteUInt16BigEndian(file, 1);
        WriteUInt16BigEndian(file, (ushort)ticksPerQuarterNote);

        file.AddRange("MTrk"u8.ToArray());
        WriteUInt32BigEndian(file, (uint)track.Count);
        file.AddRange(track);

        return file.ToArray();
    }

    /// <summary>Appends a raw, already fully-formed extra byte sequence directly onto a previously built single-track file's own track chunk body -- used only to construct a deliberately truncated/corrupt fixture.</summary>
    public static byte[] WithDeclaredTrackLengthLongerThanActualData(byte[] validSingleTrackFile)
    {
        var copy = (byte[])validSingleTrackFile.Clone();
        // MTrk length is the 4 big-endian bytes immediately after the "MThd"(14-byte header)+"MTrk"(4 bytes) = offset 18.
        const int lengthOffset = 18;
        var declaredLength = (uint)((copy[lengthOffset] << 24) | (copy[lengthOffset + 1] << 16) | (copy[lengthOffset + 2] << 8) | copy[lengthOffset + 3]);
        var inflated = declaredLength + 1000;
        copy[lengthOffset] = (byte)(inflated >> 24);
        copy[lengthOffset + 1] = (byte)(inflated >> 16);
        copy[lengthOffset + 2] = (byte)(inflated >> 8);
        copy[lengthOffset + 3] = (byte)inflated;
        return copy;
    }

    private static void WriteVariableLengthQuantity(List<byte> buffer, long value)
    {
        var stack = new Stack<byte>();
        stack.Push((byte)(value & 0x7F));
        value >>= 7;
        while (value > 0)
        {
            stack.Push((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        while (stack.Count > 0)
        {
            buffer.Add(stack.Pop());
        }
    }

    private static void WriteUInt32BigEndian(List<byte> buffer, uint value)
    {
        buffer.Add((byte)(value >> 24));
        buffer.Add((byte)(value >> 16));
        buffer.Add((byte)(value >> 8));
        buffer.Add((byte)value);
    }

    private static void WriteUInt16BigEndian(List<byte> buffer, ushort value)
    {
        buffer.Add((byte)(value >> 8));
        buffer.Add((byte)value);
    }
}
