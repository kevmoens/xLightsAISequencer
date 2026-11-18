using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>User feedback (2026-11-13): <see cref="PropGroupResolver.OrderByPhysicalPosition"/> -- the real-position ordering a <see cref="Look.Travel"/> sweep needs.</summary>
public sealed class PropGroupResolverTests
{
    private static DisplayGraph BuildLayout(params (string Name, double? X)[] models) => new(
        "TEST-HASH",
        Models: models.Select(m => new DisplayModel(
            new ModelId(m.Name), m.Name, "Arches", [], [],
            Bounds: m.X is { } x ? new DisplayBounds(x, 0, 0) : null)).ToList(),
        Groups: [],
        Warnings: []);

    [Fact]
    public void OrderByPhysicalPosition_LeftToRight_SortsAscendingByRealX()
    {
        var layout = BuildLayout(("Arch3", 300), ("Arch1", 100), ("Arch2", 200));
        var models = new[] { new ModelId("Arch3"), new ModelId("Arch1"), new ModelId("Arch2") };

        var ordered = PropGroupResolver.OrderByPhysicalPosition(models, layout, leftToRight: true);

        Assert.Equal([new ModelId("Arch1"), new ModelId("Arch2"), new ModelId("Arch3")], ordered);
    }

    [Fact]
    public void OrderByPhysicalPosition_RightToLeft_SortsDescendingByRealX()
    {
        var layout = BuildLayout(("Arch3", 300), ("Arch1", 100), ("Arch2", 200));
        var models = new[] { new ModelId("Arch3"), new ModelId("Arch1"), new ModelId("Arch2") };

        var ordered = PropGroupResolver.OrderByPhysicalPosition(models, layout, leftToRight: false);

        Assert.Equal([new ModelId("Arch3"), new ModelId("Arch2"), new ModelId("Arch1")], ordered);
    }

    [Fact]
    public void OrderByPhysicalPosition_ModelWithNoBounds_NeverGuessed_AppendedAfterPositionedModels()
    {
        var layout = BuildLayout(("Arch1", 100), ("ArchUnknown", null), ("Arch2", 200));
        var models = new[] { new ModelId("ArchUnknown"), new ModelId("Arch2"), new ModelId("Arch1") };

        var ordered = PropGroupResolver.OrderByPhysicalPosition(models, layout, leftToRight: true);

        Assert.Equal([new ModelId("Arch1"), new ModelId("Arch2"), new ModelId("ArchUnknown")], ordered);
    }
}
