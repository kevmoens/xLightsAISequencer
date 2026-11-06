using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure.Tests;

/// <summary>Build-plan "richer analysis tools" Phase 7 (2026-11-02): <see cref="KeyDetector"/>.</summary>
public sealed class KeyDetectorTests
{
    private const long TicksPerSecond = 10_000_000L;
    private static readonly double[] MajorProfile = [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88];

    /// <summary>Same rotation KeyDetector itself applies internally -- index 0 ends up at pitch class <paramref name="root"/>.</summary>
    private static double[] RotateMajor(int root)
    {
        var rotated = new double[12];
        for (var i = 0; i < 12; i++)
        {
            rotated[i] = MajorProfile[(i - root + 12) % 12];
        }

        return rotated;
    }

    private static BarFingerprint BuildBar(int index, long startTicks, long endTicks, double[] chroma, bool isSilent = false)
    {
        var features = new double[2 + chroma.Length];
        Array.Copy(chroma, 0, features, 2, chroma.Length);
        return new BarFingerprint(index, new TimeWindow(startTicks, endTicks), features, Confidence: 1.0, IsSilent: isSilent);
    }

    private static SongFingerprintSet BuildFingerprintSet(IReadOnlyList<BarFingerprint> bars, int chromaBins = 12) =>
        new(Guid.NewGuid(), new SongId(Guid.NewGuid()), "hash", Guid.NewGuid(), "test/1",
            new BarFingerprintSettings(chromaBins, "energy+chroma/1.0.0"), bars);

    [Fact]
    public void DetectKeyChanges_AllBarsStronglyCMajor_DetectsOneChangeAtStart()
    {
        var bars = Enumerable.Range(0, 6)
            .Select(i => BuildBar(i, i * TicksPerSecond, (i + 1) * TicksPerSecond, RotateMajor(0)))
            .ToList();

        var changes = KeyDetector.DetectKeyChanges(BuildFingerprintSet(bars));

        var change = Assert.Single(changes);
        Assert.Equal("C Major", change.KeyName);
        Assert.Equal(0, change.TimeTicks);
    }

    [Fact]
    public void DetectKeyChanges_StableModulationMidSong_DetectsBothKeys()
    {
        var bars = new List<BarFingerprint>();
        for (var i = 0; i < 4; i++)
        {
            bars.Add(BuildBar(i, i * TicksPerSecond, (i + 1) * TicksPerSecond, RotateMajor(0))); // C Major
        }

        for (var i = 4; i < 8; i++)
        {
            bars.Add(BuildBar(i, i * TicksPerSecond, (i + 1) * TicksPerSecond, RotateMajor(7))); // G Major
        }

        var changes = KeyDetector.DetectKeyChanges(BuildFingerprintSet(bars));

        Assert.Equal(2, changes.Count);
        Assert.Equal("C Major", changes[0].KeyName);
        Assert.Equal(0, changes[0].TimeTicks);
        Assert.Equal("G Major", changes[1].KeyName);
        Assert.Equal(4 * TicksPerSecond, changes[1].TimeTicks);
    }

    [Fact]
    public void DetectKeyChanges_SingleBarFlicker_NotReportedAsAModulation()
    {
        var bars = new List<BarFingerprint>
        {
            BuildBar(0, 0, TicksPerSecond, RotateMajor(0)),
            BuildBar(1, TicksPerSecond, 2 * TicksPerSecond, RotateMajor(0)),
            BuildBar(2, 2 * TicksPerSecond, 3 * TicksPerSecond, RotateMajor(7)), // one-bar flicker to G Major
            BuildBar(3, 3 * TicksPerSecond, 4 * TicksPerSecond, RotateMajor(0)),
            BuildBar(4, 4 * TicksPerSecond, 5 * TicksPerSecond, RotateMajor(0)),
        };

        var changes = KeyDetector.DetectKeyChanges(BuildFingerprintSet(bars));

        Assert.DoesNotContain(changes, c => c.KeyName == "G Major");
    }

    [Fact]
    public void DetectKeyChanges_SilentBars_SkippedEntirely()
    {
        var bars = new List<BarFingerprint>
        {
            BuildBar(0, 0, TicksPerSecond, new double[12], isSilent: true),
            BuildBar(1, TicksPerSecond, 2 * TicksPerSecond, RotateMajor(0)),
            BuildBar(2, 2 * TicksPerSecond, 3 * TicksPerSecond, RotateMajor(0)),
        };

        var changes = KeyDetector.DetectKeyChanges(BuildFingerprintSet(bars));

        var change = Assert.Single(changes);
        Assert.Equal("C Major", change.KeyName);
        Assert.Equal(TicksPerSecond, change.TimeTicks);
    }

    [Fact]
    public void DetectKeyChanges_NonTwelveChromaBins_ReturnsEmpty()
    {
        var bars = new List<BarFingerprint> { BuildBar(0, 0, TicksPerSecond, new double[24]) };

        Assert.Empty(KeyDetector.DetectKeyChanges(BuildFingerprintSet(bars, chromaBins: 24)));
    }

    [Fact]
    public void DetectKeyChanges_NoBars_ReturnsEmpty()
    {
        Assert.Empty(KeyDetector.DetectKeyChanges(BuildFingerprintSet([])));
    }

    [Fact]
    public void DetectKeyChanges_NullFingerprints_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => KeyDetector.DetectKeyChanges(null!));
    }
}
