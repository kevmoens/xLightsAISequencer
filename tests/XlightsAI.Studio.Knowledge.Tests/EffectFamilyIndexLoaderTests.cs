namespace XlightsAI.Studio.Knowledge.Tests;

/// <summary>
/// Build-plan Phase 30 step 30.4: the real-corpus-mined effect-family index must load from this
/// assembly's own embedded resource, and <see cref="EffectFamilyIndex.FamilyOf"/> must fall back to the
/// effect's own name (a singleton family) for any real effect this mining run found no repeated
/// substitution evidence for -- never a fabricated shared bucket.
/// </summary>
public sealed class EffectFamilyIndexLoaderTests
{
    [Fact]
    public void Load_ReturnsARealNonEmptyIndex()
    {
        var index = EffectFamilyIndexLoader.Load();

        Assert.NotEmpty(index.FamilyByEffectName);
    }

    [Fact]
    public void Load_IsCached_ReturnsTheSameInstanceAcrossCalls()
    {
        var first = EffectFamilyIndexLoader.Load();
        var second = EffectFamilyIndexLoader.Load();

        Assert.Same(first, second);
    }

    [Fact]
    public void FamilyOf_MinedPair_SharesTheSameFamilyId()
    {
        var index = EffectFamilyIndexLoader.Load();

        Assert.Equal(index.FamilyOf("Pinwheel"), index.FamilyOf("Shockwave"));
        Assert.Equal(index.FamilyOf("Color Wash"), index.FamilyOf("On"));
    }

    [Fact]
    public void FamilyOf_EffectWithNoMinedEvidence_ReturnsItsOwnNameAsASingletonFamily()
    {
        var index = EffectFamilyIndexLoader.Load();

        Assert.Equal("Twinkle", index.FamilyOf("Twinkle"));
        Assert.Equal("Shader", index.FamilyOf("Shader"));
    }
}
