using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>Build-plan Phase 6 task 3: "Wire Phase 4's Look -&gt; ChoreographyIR compiler to consume SectionScript + Look instead of a hand-authored Look directly" -- via a synthetic per-section effective Look library, with <see cref="ChoreographyEngine.Input"/> itself left unmodified.</summary>
public sealed class SectionScriptCompilerTests
{
    private static PhraseCandidate Phrase(Guid sectionId, int startBar, int endBarExclusive) => new(
        Guid.NewGuid(), sectionId, startBar, endBarExclusive, startBar * 1_000_000L, endBarExclusive * 1_000_000L,
        endBarExclusive - startBar, PhraseBoundarySource.BarHypothesis, 1.0, ["test"], "hash", false);

    private static Look GoldenHold() => Phase4LookLibraryFixture.Build().Find("golden-hold")!;

    [Fact]
    public void Compile_ScriptWithNoPropBindings_UsesBaseLookIdUnchanged_NoSyntheticLookAdded()
    {
        var library = new LookLibrary([GoldenHold()]);
        var sectionId = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript> { [sectionId] = new(sectionId, "golden-hold") };

        var (effectiveLibrary, lookIdBySectionId) = SectionScriptCompiler.Compile(library, scripts);

        Assert.Equal("golden-hold", lookIdBySectionId[sectionId]);
        Assert.Single(effectiveLibrary.Looks); // No synthetic clone was minted.
    }

    [Fact]
    public void Compile_ScriptWithPropBinding_MintsSyntheticLook_WithOverriddenLayerProps_OtherLayersUnchanged()
    {
        var baseLook = GoldenHold();
        var library = new LookLibrary([baseLook]);
        var sectionId = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript>
        {
            [sectionId] = new(sectionId, "golden-hold", PropBindings: [new SectionEventBinding(LayerZ: 3, Props: "Tree")]),
        };

        var (effectiveLibrary, lookIdBySectionId) = SectionScriptCompiler.Compile(library, scripts);

        var effectiveId = lookIdBySectionId[sectionId];
        Assert.NotEqual("golden-hold", effectiveId);
        Assert.Equal(2, effectiveLibrary.Looks.Count); // base Look preserved, plus one synthetic clone.

        var effectiveLook = effectiveLibrary.Find(effectiveId)!;
        Assert.Equal("Tree", effectiveLook.Layers.Single(l => l.Z == 3).Props);
        Assert.Equal(baseLook.Layers.Single(l => l.Z == 1).Props, effectiveLook.Layers.Single(l => l.Z == 1).Props);
        Assert.Equal(baseLook.Layers.Single(l => l.Z == 2).Props, effectiveLook.Layers.Single(l => l.Z == 2).Props);

        // Every other field carries over unchanged from the base Look.
        Assert.Equal(baseLook.Intent, effectiveLook.Intent);
        Assert.Equal(baseLook.EnergyCeiling, effectiveLook.EnergyCeiling);
        Assert.Equal(baseLook.PaletteRef, effectiveLook.PaletteRef);
    }

    [Fact]
    public void Compile_TwoSectionsSameLook_DifferentBindings_MintTwoDistinctSyntheticLooks()
    {
        var library = new LookLibrary([GoldenHold()]);
        var sectionA = Guid.NewGuid();
        var sectionB = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript>
        {
            [sectionA] = new(sectionA, "golden-hold", PropBindings: [new SectionEventBinding(3, "Tree")]),
            [sectionB] = new(sectionB, "golden-hold", PropBindings: [new SectionEventBinding(3, "group:Arches")]),
        };

        var (effectiveLibrary, lookIdBySectionId) = SectionScriptCompiler.Compile(library, scripts);

        Assert.NotEqual(lookIdBySectionId[sectionA], lookIdBySectionId[sectionB]);
        Assert.Equal(3, effectiveLibrary.Looks.Count);
        Assert.Equal("Tree", effectiveLibrary.Find(lookIdBySectionId[sectionA])!.Layers.Single(l => l.Z == 3).Props);
        Assert.Equal("group:Arches", effectiveLibrary.Find(lookIdBySectionId[sectionB])!.Layers.Single(l => l.Z == 3).Props);
    }

    [Fact]
    public void Compile_ScriptReferencesUnknownLook_Throws()
    {
        var library = new LookLibrary([GoldenHold()]);
        var sectionId = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript> { [sectionId] = new(sectionId, "no-such-look") };

        Assert.Throws<ChoreographyCompilationException>(() => SectionScriptCompiler.Compile(library, scripts));
    }

    [Fact]
    public void Compile_ScriptRebindsUnknownLayerZ_Throws()
    {
        var library = new LookLibrary([GoldenHold()]);
        var sectionId = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript>
        {
            [sectionId] = new(sectionId, "golden-hold", PropBindings: [new SectionEventBinding(LayerZ: 99, Props: "Tree")]),
        };

        Assert.Throws<ChoreographyCompilationException>(() => SectionScriptCompiler.Compile(library, scripts));
    }

    // -- Build-plan Phase 10 §3e: phrase-scoped sub-section variation. --

    [Fact]
    public void Compile_NoPhraseVariations_ProducesNoPhraseAssignments()
    {
        var library = new LookLibrary([GoldenHold()]);
        var sectionId = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript> { [sectionId] = new(sectionId, "golden-hold") };
        var phrases = new Dictionary<Guid, IReadOnlyList<PhraseCandidate>> { [sectionId] = [Phrase(sectionId, 0, 4), Phrase(sectionId, 4, 8)] };

        var (_, _, phraseAssignments) = SectionScriptCompiler.Compile(library, scripts, phrases);

        Assert.False(phraseAssignments.ContainsKey(sectionId));
    }

    [Fact]
    public void Compile_OnePhraseVariation_PartitionsEveryPhrase_DeclaredPhraseUsesOwnLayerSubset_OthersUseSectionBaseLook()
    {
        var library = new LookLibrary([GoldenHold()]);
        var sectionId = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript>
        {
            [sectionId] = new(sectionId, "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 2, ActiveLayerZs: [3])]),
        };
        var phrases = new Dictionary<Guid, IReadOnlyList<PhraseCandidate>>
        {
            [sectionId] = [Phrase(sectionId, 0, 4), Phrase(sectionId, 4, 8), Phrase(sectionId, 8, 12)],
        };

        var (effectiveLibrary, lookIdBySectionId, phraseAssignmentsBySectionId) = SectionScriptCompiler.Compile(library, scripts, phrases);

        var assignments = phraseAssignmentsBySectionId[sectionId];
        Assert.Equal(3, assignments.Count);

        // Phrase 1 and phrase 3 (not named by any declared variation) use the section's own base
        // effective Look (here, the base "golden-hold" Look verbatim -- no section-wide prop bindings).
        Assert.Equal(lookIdBySectionId[sectionId], assignments[0].LookId);
        Assert.Equal(lookIdBySectionId[sectionId], assignments[2].LookId);
        Assert.Equal(new BarIndex(0), assignments[0].StartBar);
        Assert.Equal(new BarIndex(4), assignments[0].EndBarExclusive);

        // Phrase 2 (PhraseIndex 2) compiles against its own synthetic Look with only layer z=3 active.
        Assert.NotEqual(lookIdBySectionId[sectionId], assignments[1].LookId);
        Assert.Equal(new BarIndex(4), assignments[1].StartBar);
        Assert.Equal(new BarIndex(8), assignments[1].EndBarExclusive);

        var phraseLook = effectiveLibrary.Find(assignments[1].LookId)!;
        Assert.Single(phraseLook.Layers);
        Assert.Equal(3, phraseLook.Layers[0].Z);
    }

    [Fact]
    public void Compile_PhraseVariationPropBindingOverridesSectionWideBindingForThatPhraseOnly()
    {
        var library = new LookLibrary([GoldenHold()]);
        var sectionId = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript>
        {
            [sectionId] = new(
                sectionId, "golden-hold",
                PropBindings: [new SectionEventBinding(LayerZ: 3, Props: "Tree")],
                PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 1, PropBindings: [new SectionEventBinding(LayerZ: 3, Props: "group:Arches")])]),
        };
        var phrases = new Dictionary<Guid, IReadOnlyList<PhraseCandidate>> { [sectionId] = [Phrase(sectionId, 0, 4)] };

        var (effectiveLibrary, _, phraseAssignmentsBySectionId) = SectionScriptCompiler.Compile(library, scripts, phrases);

        var phraseLook = effectiveLibrary.Find(phraseAssignmentsBySectionId[sectionId][0].LookId)!;
        Assert.Equal("group:Arches", phraseLook.Layers.Single(l => l.Z == 3).Props);
        // The other (non-rebound) layers still carry over from the section's own effective Look unchanged.
        Assert.Equal("group:MiniTrees", phraseLook.Layers.Single(l => l.Z == 1).Props);
    }

    [Fact]
    public void Compile_PhraseVariationDeclaresPhraseIndexBeyondSectionsRealPhraseCount_Throws()
    {
        var library = new LookLibrary([GoldenHold()]);
        var sectionId = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript>
        {
            [sectionId] = new(sectionId, "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 5)]),
        };
        var phrases = new Dictionary<Guid, IReadOnlyList<PhraseCandidate>> { [sectionId] = [Phrase(sectionId, 0, 4)] };

        Assert.Throws<ChoreographyCompilationException>(() => SectionScriptCompiler.Compile(library, scripts, phrases));
    }

    [Fact]
    public void Compile_PhraseVariationsDeclaredButNoPhraseDataSupplied_Throws()
    {
        var library = new LookLibrary([GoldenHold()]);
        var sectionId = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript>
        {
            [sectionId] = new(sectionId, "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 1)]),
        };

        Assert.Throws<ChoreographyCompilationException>(() => SectionScriptCompiler.Compile(library, scripts, phrasesBySectionId: null));
    }

    [Fact]
    public void Compile_PhraseVariationActiveLayerZUnknown_Throws()
    {
        var library = new LookLibrary([GoldenHold()]);
        var sectionId = Guid.NewGuid();
        var scripts = new Dictionary<Guid, SectionScript>
        {
            [sectionId] = new(sectionId, "golden-hold", PhraseVariations: [new SectionPhraseVariation(PhraseIndex: 1, ActiveLayerZs: [99])]),
        };
        var phrases = new Dictionary<Guid, IReadOnlyList<PhraseCandidate>> { [sectionId] = [Phrase(sectionId, 0, 4)] };

        Assert.Throws<ChoreographyCompilationException>(() => SectionScriptCompiler.Compile(library, scripts, phrases));
    }
}
