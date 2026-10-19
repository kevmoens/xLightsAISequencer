using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong.Tests;

/// <summary>
/// PHASE-14 Step 091 -- "Assemble accepted phrase candidates with transition-aware overlap/gap handling."
/// Required acceptance: "No unintended timing holes, conflicts or truncated effects." Implementation
/// guidance: "Assemble canonical phrase plans with explicit seam policies (trim, hold, release, bridge);
/// respect half-open ranges and layer boundaries." Failure/edge case: "Malformed proposal, no-improvement
/// and exhausted retry budget must not auto-commit." These tests exercise the real
/// <see cref="PhraseSeamAssembler"/>/<see cref="EffectLockPolicy"/>/<see cref="JsonSequenceDocumentStore"/>
/// (no mocking) against real <see cref="SequenceDocument"/>/<see cref="EffectInstance"/> production types.
/// </summary>
public sealed class Step091AcceptanceTests
{
    private static readonly ModelId TreeModel = new("Tree");
    private static readonly ModelId ArchModel = new("Arch");

    private static EditScope FullScope() => new(
        new TimeWindow(0, 1_000_000),
        new HashSet<ModelId> { TreeModel, ArchModel });

    private static EffectInstance Effect(ModelId target, long start, long end, int layer = 0, bool locked = false) =>
        new(Guid.NewGuid(), target, layer, start, end, "fixture-recipe", "v1",
            new Dictionary<string, string>(), locked);

    [Fact]
    public async Task Step091_Acceptance_Passes()
    {
        // Arrange: the phase-gate's own required demo shape -- three consecutive phrases (e.g. Verse,
        // Chorus, Verse) assembled with one seam of each distinct policy family: a clean Hold-closed gap,
        // and a Trim-resolved cross-phrase overlap.
        var phraseA = new PhraseAssemblyCandidate(
            Guid.NewGuid(), new TimeWindow(0, 1000),
            [Effect(TreeModel, 0, 1000)]);

        // Phrase B starts after a 50-tick silent gap from phrase A -- Hold should close it by extending
        // phrase A's own boundary-touching effect forward.
        var phraseB = new PhraseAssemblyCandidate(
            Guid.NewGuid(), new TimeWindow(1050, 2000),
            [Effect(ArchModel, 1050, 2000)]);

        // Phrase C is perfectly adjacent to phrase B, but phrase B's own trailing Arch effect bleeds 30
        // ticks past its own phrase boundary into phrase C's territory -- the "cross-phrase overlapping
        // effect" required test scenario (Section 7 item 4) -- Trim should clip it exactly at the boundary.
        var bleedingEffect = Effect(ArchModel, 1900, 2030);
        var phraseC = new PhraseAssemblyCandidate(
            Guid.NewGuid(), new TimeWindow(2000, 3000),
            [Effect(TreeModel, 2000, 3000)]);
        var phraseBWithBleed = phraseB with { Effects = [Effect(ArchModel, 1050, 1900), bleedingEffect] };

        var phrases = new List<PhraseAssemblyCandidate> { phraseA, phraseBWithBleed, phraseC };
        var seamRequests = new List<PhraseSeamRequest>
        {
            new(phraseA.PhraseId, phraseBWithBleed.PhraseId, SeamStrategy.Hold),
            new(phraseBWithBleed.PhraseId, phraseC.PhraseId, SeamStrategy.Trim),
        };

        var baseDocument = new SequenceDocument(Guid.NewGuid(), Revision: 3, Effects: []);
        var assembler = new PhraseSeamAssembler(new EffectLockPolicy());

        // Act.
        var result = assembler.Assemble(baseDocument, phrases, seamRequests, FullScope());

        // Assert: a new, versioned document was produced.
        Assert.Equal(4, result.Document.Revision);
        Assert.NotSame(baseDocument, result.Document);

        // Assert: "No unintended timing holes" -- the Hold seam closed the gap: phrase A's Tree effect was
        // extended to meet phrase B's own window start exactly.
        var treeEffects = result.Document.Effects.Where(e => e.Target == TreeModel).OrderBy(e => e.StartTicks).ToList();
        Assert.Equal(2, treeEffects.Count);
        Assert.Equal(0, treeEffects[0].StartTicks);
        Assert.Equal(1050, treeEffects[0].EndTicks); // extended to close the 50-tick gap, not left at 1000.
        Assert.Equal(2000, treeEffects[1].StartTicks);
        Assert.Equal(3000, treeEffects[1].EndTicks);

        // Assert: "...or truncated effects" is satisfied deliberately, not accidentally -- the Trim seam
        // clipped the bleeding Arch effect exactly at the phrase boundary (2000), never left overlapping.
        var archEffects = result.Document.Effects.Where(e => e.Target == ArchModel).OrderBy(e => e.StartTicks).ToList();
        Assert.Equal(2, archEffects.Count);
        Assert.Equal(1900, archEffects[1].StartTicks);
        Assert.Equal(2000, archEffects[1].EndTicks); // clipped from 2030 down to the boundary.

        // Assert: no unintended cross-phrase layer conflict -- every effect on a given model+layer is
        // non-overlapping.
        foreach (var group in result.Document.Effects.GroupBy(e => (e.Target, e.Layer)))
        {
            var ordered = group.OrderBy(e => e.StartTicks).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                Assert.True(ordered[i].StartTicks >= ordered[i - 1].EndTicks,
                    $"Effects '{ordered[i - 1].Id}' and '{ordered[i].Id}' on '{group.Key.Target.Value}' layer {group.Key.Layer} unexpectedly overlap.");
            }
        }

        // Assert: seams are reported in phrase order with the exact strategy applied, and the Trim seam's
        // own clip is disclosed via a warning (never a silent truncation).
        Assert.Equal(2, result.Seams.Count);
        Assert.Equal(SeamStrategy.Hold, result.Seams[0].Strategy);
        Assert.Equal(SeamStrategy.Trim, result.Seams[1].Strategy);
        Assert.Contains(result.Warnings, w => w.Contains("clipped from 2030 to 2000", StringComparison.Ordinal));

        // Act: persist -- "Versioned, inspectable project output" (User-visible outcome, verbatim).
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step091-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonSequenceDocumentStore(cacheDirectory);
            var path = await store.SaveAsync(result.Document, CancellationToken.None);

            // Assert: persisted domain state survives a restart (brand-new store instance over the same
            // on-disk directory) -- not only a mock invocation. "Reopen and export approved full song
            // without recomposition" (Section 7 item 7) starts from this same round trip.
            var reloaded = await new JsonSequenceDocumentStore(cacheDirectory).LoadAsync(path, CancellationToken.None);
            Assert.Equal(result.Document.Id, reloaded.Id);
            Assert.Equal(4, reloaded.Revision);
            Assert.Equal(4, reloaded.Effects.Count);
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Step091_BridgeSeam_LeavesGapDisclosedRatherThanSilent()
    {
        var phraseA = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(0, 1000), [Effect(TreeModel, 0, 1000)]);
        var phraseB = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(1200, 2000), [Effect(TreeModel, 1200, 2000)]);
        var seamRequests = new List<PhraseSeamRequest> { new(phraseA.PhraseId, phraseB.PhraseId, SeamStrategy.Bridge) };
        var baseDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var assembler = new PhraseSeamAssembler(new EffectLockPolicy());

        var result = assembler.Assemble(baseDocument, [phraseA, phraseB], seamRequests, FullScope());

        // The gap is left unfilled (no effect was invented to bridge it), but disclosed, not silent.
        Assert.Single(result.Seams);
        Assert.Equal(SeamStrategy.Bridge, result.Seams[0].Strategy);
        Assert.Equal(200, result.Seams[0].Window.EndTicks - result.Seams[0].Window.StartTicks);
        Assert.Contains(result.Warnings, w => w.Contains("gap of 200 tick(s)", StringComparison.Ordinal));
        // Assert: nothing was fabricated to fill the gap -- effect count is unchanged from input.
        Assert.Equal(2, result.Document.Effects.Count);
    }

    [Fact]
    public void Step091_LockedBaseDocumentEffect_RejectsEntireAssembly_BeforeAnyMutation()
    {
        var lockedEffect = Effect(TreeModel, 0, 500, locked: true);
        var baseDocument = new SequenceDocument(Guid.NewGuid(), 1, [lockedEffect]);
        var phraseA = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(0, 1000), [Effect(TreeModel, 0, 1000)]);
        var assembler = new PhraseSeamAssembler(new EffectLockPolicy());

        Assert.Throws<EditCommandRejectedException>(
            () => assembler.Assemble(baseDocument, [phraseA], [], FullScope()));
    }

    [Fact]
    public void Step091_NegativePath_MalformedProposal_OverlappingPhraseWindows_Throws_BeforeAnyMutation()
    {
        // "Malformed proposal... must not auto-commit" -- required failure/edge case, verbatim.
        var phraseA = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(0, 1000), [Effect(TreeModel, 0, 1000)]);
        var phraseB = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(900, 2000), [Effect(TreeModel, 900, 2000)]);
        var seamRequests = new List<PhraseSeamRequest> { new(phraseA.PhraseId, phraseB.PhraseId, SeamStrategy.Trim) };
        var baseDocument = new SequenceDocument(Guid.NewGuid(), 5, []);
        var assembler = new PhraseSeamAssembler(new EffectLockPolicy());

        var ex = Assert.Throws<PhraseAssemblyInvalidException>(
            () => assembler.Assemble(baseDocument, [phraseA, phraseB], seamRequests, FullScope()));
        Assert.Contains("strictly ascending", ex.Reason, StringComparison.Ordinal);

        // No partial mutation: the caller's own baseDocument instance is provably untouched (same revision).
        Assert.Equal(5, baseDocument.Revision);
    }

    [Fact]
    public void Step091_NegativePath_MissingSeamRequest_Throws()
    {
        var phraseA = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(0, 1000), [Effect(TreeModel, 0, 1000)]);
        var phraseB = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(1000, 2000), [Effect(TreeModel, 1000, 2000)]);
        var baseDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var assembler = new PhraseSeamAssembler(new EffectLockPolicy());

        Assert.Throws<PhraseAssemblyInvalidException>(
            () => assembler.Assemble(baseDocument, [phraseA, phraseB], [], FullScope()));
    }

    [Fact]
    public void Step091_NegativePath_HoldRequestedAgainstOverlappingSeam_Throws()
    {
        // Hold cannot "hold through" an actual overlap -- that is a policy/data mismatch, a malformed
        // proposal, not something Hold can silently paper over.
        var phraseA = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(0, 1000), [Effect(TreeModel, 0, 1030)]);
        var phraseB = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(1000, 2000), [Effect(TreeModel, 1000, 2000)]);
        var seamRequests = new List<PhraseSeamRequest> { new(phraseA.PhraseId, phraseB.PhraseId, SeamStrategy.Hold) };
        var baseDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var assembler = new PhraseSeamAssembler(new EffectLockPolicy());

        Assert.Throws<PhraseAssemblyInvalidException>(
            () => assembler.Assemble(baseDocument, [phraseA, phraseB], seamRequests, FullScope()));
    }

    [Fact]
    public void Step091_NegativePath_EmptyPhrases_Throws()
    {
        var baseDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var assembler = new PhraseSeamAssembler(new EffectLockPolicy());

        Assert.Throws<PhraseAssemblyInvalidException>(
            () => assembler.Assemble(baseDocument, [], [], FullScope()));
    }

    [Fact]
    public void Step091_NegativePath_EffectOutsideAllowedScope_Throws()
    {
        var phraseA = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(0, 1000), [Effect(TreeModel, 0, 1000)]);
        var baseDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var assembler = new PhraseSeamAssembler(new EffectLockPolicy());
        var narrowScope = new EditScope(new TimeWindow(0, 1000), new HashSet<ModelId> { ArchModel }); // Tree not permitted.

        Assert.Throws<PhraseAssemblyInvalidException>(
            () => assembler.Assemble(baseDocument, [phraseA], [], narrowScope));
    }

    [Fact]
    public void Step091_NegativePath_ExhaustedRetryBudget_ThrowsWithoutCommitting()
    {
        // "...exhausted retry budget must not auto-commit" -- required failure/edge case, verbatim.
        // Seed the base document with MaxLayers already-occupied, fully overlapping layers on the same
        // model/window, leaving no free layer for the new phrase effect to land on.
        var window = new TimeWindow(0, 1000);
        var existing = Enumerable.Range(0, PhraseSeamAssembler.MaxLayers)
            .Select(layer => Effect(TreeModel, 0, 1000, layer))
            .ToList();
        var baseDocument = new SequenceDocument(Guid.NewGuid(), 7, existing);

        var phraseA = new PhraseAssemblyCandidate(Guid.NewGuid(), window, [Effect(TreeModel, 0, 1000)]);
        var assembler = new PhraseSeamAssembler(new EffectLockPolicy());
        var scope = new EditScope(new TimeWindow(0, 1000), new HashSet<ModelId> { TreeModel }, RespectLocks: false);

        var ex = Assert.Throws<PhraseAssemblyExhaustedException>(
            () => assembler.Assemble(baseDocument, [phraseA], [], scope));

        Assert.Equal(TreeModel, ex.Target);
        Assert.Equal(PhraseSeamAssembler.MaxLayers, ex.MaxLayersAttempted);

        // No partial commit: the caller's own baseDocument is provably unchanged (same revision, same
        // effect count) -- the exception is thrown before any new SequenceDocument is ever constructed.
        Assert.Equal(7, baseDocument.Revision);
        Assert.Equal(PhraseSeamAssembler.MaxLayers, baseDocument.Effects.Count);
    }

    [Fact]
    public void Step091_NegativePath_NoOpTrimRequest_IsDisclosedNotSilent()
    {
        // Trim requested where no overlap actually exists must not be silently accepted as if it did
        // something -- the caller must be able to tell a real clip from a no-op.
        var phraseA = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(0, 1000), [Effect(TreeModel, 0, 1000)]);
        var phraseB = new PhraseAssemblyCandidate(Guid.NewGuid(), new TimeWindow(1000, 2000), [Effect(TreeModel, 1000, 2000)]);
        var seamRequests = new List<PhraseSeamRequest> { new(phraseA.PhraseId, phraseB.PhraseId, SeamStrategy.Trim) };
        var baseDocument = new SequenceDocument(Guid.NewGuid(), 0, []);
        var assembler = new PhraseSeamAssembler(new EffectLockPolicy());

        var result = assembler.Assemble(baseDocument, [phraseA, phraseB], seamRequests, FullScope());

        Assert.Contains(result.Warnings, w => w.Contains("no-op", StringComparison.Ordinal));
        // Neither effect was truncated.
        Assert.Equal(1000, result.Document.Effects.Single(e => e.StartTicks == 0).EndTicks);
    }
}
