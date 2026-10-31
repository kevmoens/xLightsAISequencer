using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director.Tests;

/// <summary>
/// <see cref="AiChoreographyWholeSongGenerator"/> fixes two real bugs in the old <c>WholeSongGenerator</c>
/// path: an empty exported <c>&lt;mediaFile&gt;</c> (nothing in that path ever set
/// <see cref="SequenceMetadata.MediaFile"/>) and every model landing on a single flat, uniform "On"
/// effect (that path's only registered recipe). Neither requires a real LLM call to test: the
/// AI-provider-not-configured failure happens before any network call is attempted, and the MediaFile fix
/// is a pure, extracted static helper.
/// </summary>
public sealed class AiChoreographyWholeSongGeneratorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-ai-choreography-gen-" + Guid.NewGuid().ToString("N"));

    public AiChoreographyWholeSongGeneratorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Dir(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Never invoked in the "not configured" test: the role check happens before any audio decode/separation is attempted.</summary>
    private sealed class NeverCalledAudioDecoder : IAudioDecoder
    {
        public Task<SongAsset> ProbeAsync(string path, CancellationToken ct) => throw new InvalidOperationException("Must not be invoked.");

        public IAsyncEnumerable<ReadOnlyMemory<float>> DecodeMonoAsync(SongAsset song, int targetSampleRate, CancellationToken ct) =>
            throw new InvalidOperationException("Must not be invoked.");
    }

    private sealed class NeverCalledStemSeparator : IStemSeparator
    {
        public Task<StemManifest> SeparateAsync(SongAsset source, ModelAsset model, IProgress<SeparationProgress>? progress, CancellationToken ct) =>
            throw new InvalidOperationException("Must not be invoked.");
    }

    /// <summary>A config store where every role is disabled -- the "user has never configured a provider" default <see cref="RoleProviderConfig.Disabled"/> itself represents.</summary>
    private sealed class AllRolesDisabledConfigStore : IRoleProviderConfigStore
    {
        public string GetPath(AiRole role) => throw new InvalidOperationException("Must not be invoked.");

        public Task<string> SaveAsync(RoleProviderConfig config, CancellationToken ct) => throw new InvalidOperationException("Must not be invoked.");

        public Task<RoleProviderConfig> LoadAsync(AiRole role, CancellationToken ct) => Task.FromResult(RoleProviderConfig.Disabled(role));
    }

    private sealed class NeverCalledCredentialStore : ICredentialStore
    {
        public Task StoreAsync(string name, string secret, CancellationToken ct = default) => throw new InvalidOperationException("Must not be invoked.");

        public Task<string?> TryGetAsync(string name, CancellationToken ct = default) => throw new InvalidOperationException("Must not be invoked.");

        public Task DeleteAsync(string name, CancellationToken ct = default) => throw new InvalidOperationException("Must not be invoked.");
    }

    private AiChoreographyWholeSongGenerator BuildGeneratorWithDisabledRoles()
    {
        var audioAnalysis = new AudioAnalysisRunner(
            new NeverCalledAudioDecoder(), new NeverCalledStemSeparator(),
            new ModelAsset("never-used", "1.0.0", new Uri("https://example.invalid/m.onnx"), new string('0', 64), "n/a", "n/a"),
            new JsonOnsetAnalysisStore(Dir("onsets")), new JsonTempoAnalysisStore(Dir("tempo")),
            new JsonBeatGridStore(Dir("beatgrid")), new JsonEnergyEnvelopeStore(Dir("energy")),
            new JsonPitchTrackStore(Dir("pitch")));
        var structureAnalysis = new StructureAnalysisRunner(
            new JsonBarFingerprintStore(Dir("fingerprints")), new JsonSectionBoundaryStore(Dir("boundaries")),
            new JsonSectionLabelStore(Dir("labels")), new JsonSectionLabelOverrideStore(Dir("label-overrides")),
            new JsonPhraseSegmentationStore(Dir("phrases")), new JsonPhraseBoundaryOverrideStore(Dir("phrase-overrides")),
            new JsonMotifClusteringStore(Dir("motifs")));
        var roleClientResolver = new AiRoleClientResolver(new AllRolesDisabledConfigStore(), new NeverCalledCredentialStore(), new HttpClient());

        return new AiChoreographyWholeSongGenerator(
            audioAnalysis, structureAnalysis, roleClientResolver,
            effectMetadataDirectory: @"C:\Program Files\xLights\effectmetadata", // never reached
            projectDirectory: Dir("project"));
    }

    private static SongAsset BuildSongAsset(string filePath, string? originalFilePath = null) => new(
        new SongId(Guid.NewGuid()), filePath, originalFilePath ?? filePath, Sha256: new string('a', 64),
        SampleCount: 44_100 * 10, SampleRate: 44_100, Channels: 2, OriginTicks: 0,
        BitsPerSample: 16, EncoderDelaySamples: 0, DecoderVersion: "test-1.0");

    private static DisplayGraph BuildLayout() => new(
        SourceLayoutHash: "TEST-HASH-NOT-A-REAL-FILE",
        Models: [new DisplayModel(new ModelId("Arches"), "Arches", "ModelGroup", [], [])],
        Groups: [],
        Warnings: []);

    [Fact]
    public async Task GenerateAsync_NoRolesConfigured_ThrowsAiProviderNotConfiguredException_ForDirectorRole()
    {
        var generator = BuildGeneratorWithDisabledRoles();
        var song = BuildSongAsset(Path.Combine(_root, "song.wav"));
        var layout = BuildLayout();

        var ex = await Assert.ThrowsAsync<AiProviderNotConfiguredException>(
            () => generator.GenerateAsync(song, layout, CancellationToken.None));

        Assert.Equal(AiRole.Director, ex.Role);
    }

    [Fact]
    public void WithMediaFile_SetsMediaFileFromSongAssetOriginalFilePath_NotTheTranscodedAnalysisPath()
    {
        var document = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: []);
        // FilePath deliberately differs from OriginalFilePath here, the same way TranscodingAudioDecoder's
        // real output does for any non-WAV source (an internal transcoded-WAV cache entry, not the user's
        // own file) -- confirmed as a real bug against a real export before this fix.
        var song = BuildSongAsset(
            filePath: @"C:\Users\someone\AppData\Roaming\XlightsAI.Studio\audio-transcode-cache\abc123.wav",
            originalFilePath: @"C:\Users\someone\Music\My Christmas Song.mp3");

        var fixedDocument = AiChoreographyWholeSongGenerator.WithMediaFile(document, song);

        Assert.Equal(@"C:\Users\someone\Music\My Christmas Song.mp3", fixedDocument.MetadataOrDefault.MediaFile);
    }

    [Fact]
    public void WithMediaFile_PreservesOtherAlreadySetMetadataFields()
    {
        var document = new SequenceDocument(Guid.NewGuid(), Revision: 1, Effects: [], Metadata: new SequenceMetadata(Author: "Kevin", Song: "Jingle Bells"));
        var song = BuildSongAsset(@"C:\audio\jingle-bells.wav");

        var fixedDocument = AiChoreographyWholeSongGenerator.WithMediaFile(document, song);

        Assert.Equal("Kevin", fixedDocument.MetadataOrDefault.Author);
        Assert.Equal("Jingle Bells", fixedDocument.MetadataOrDefault.Song);
        Assert.Equal(@"C:\audio\jingle-bells.wav", fixedDocument.MetadataOrDefault.MediaFile);
    }

    [Fact]
    public void BuildLookTemplateUsage_OneLookPerTemplateAndOneWithNone_MapsEachByIdAndName()
    {
        var content = ShowBibleContentFixture.BuildValid(lookCount: 3);
        var looks = content.Looks.Looks;
        var templated = looks[0] with { TemplateRef = "edm-dense-canvas-tree-matrix" };
        var untouched = looks[1];
        var noTemplate = looks[2] with { TemplateRef = null };
        var withTemplates = content with { Looks = new Choreography.LookLibrary([templated, untouched, noTemplate]) };
        var bible = new ShowBible(Guid.NewGuid(), Guid.NewGuid(), withTemplates, DateTimeOffset.UtcNow);

        var usage = AiChoreographyWholeSongGenerator.BuildLookTemplateUsage(bible);

        Assert.Equal(3, usage.Count);
        Assert.Equal(("look-0", "Look 0", "edm-dense-canvas-tree-matrix"), (usage[0].LookId, usage[0].LookName, usage[0].TemplateId));
        Assert.Null(usage[2].TemplateId);
    }
}
