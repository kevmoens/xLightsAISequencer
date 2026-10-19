# Canonical Contract Catalog and Change Boundaries

**Goal:** allow implementation agents to reason about shared concepts while keeping each Phase MD self-contained. This is a comparison index, not another code layer.

## Shared value objects

```csharp
// Shared across phases; put these in Studio.Contracts (single declarations in the codebase).
namespace XlightsAI.Studio.Contracts;
public readonly record struct ProjectId(Guid Value);
public readonly record struct SongId(Guid Value);
public readonly record struct ModelId(string Value);
public readonly record struct DocumentRevision(long Value);
public readonly record struct TimelineTick(long Value); // 100 ns ticks from song origin
public readonly record struct TimeWindow(long StartTicks, long EndTicks); // [Start, End)
```

## Phase ownership and source of truth

| Aggregate / contract | First introduced | Sole authority / rule |
|---|---:|---|
| `ProjectManifest` and `ProjectId` | 01 | Project identity/version + root; SQLite and JSON atomically recoverable |
| `DisplayGraph`, `DisplayModel`, `ModelId` | 02 | Imported xLights model identities/group info; reference layout by hash |
| `SequenceDocument`, `EffectInstance` | 02 | Editable effects/locked state provenance; never serialize business behavior directly as XML |
| `EditScope`, `EditTransaction`, `DocumentRevision` | 03 | Exact time + model boundary and user-approved reversibility |
| `SongAsset`, `IAudioTransport` | 04 | Original song hash, timebase and synchronized playback |
| `BeatGrid` | 05 | Analyzed/corrected beats and alternative tempo hypotheses |
| `StemManifest`, `StemAsset` | 06 | Derived separated sources; known model version and sample origin |
| `MelodyTrack`, `NoteEvent` | 07 | Note/onset/contour truth with human corrections |
| `SongStructure`, `MotifLink` | 08 | Related phrase identities and inheritance decisions |
| `SpatialDisplayGraph`, `SpatialPath` | 09 | Artistic routes / normalized geometry separate from imported xLights identities |
| `VisualStory`, `ChoreographyPlan` | 10 | Musical/visual intent before concrete effect rendering |
| `EditProposal` | 11 | Untrusted/optional AI idea validated before `EditTransaction` |
| `LoopRun`, `Candidate`, `QualityReport` | 12 | Bounded attempts and reproducible candidate comparisons |
| `RenderArtifact` | 13 | Actual or clearly-marked approximate preview evidence |
| `WholeSongPlan` | 14 | Section continuity and approved phrase assembly |
| `ReleaseManifest` | 15 | Platform/model assets and delivery integrity |
| `ReleaseReadiness` | 16 | Explicit community compatibility/artistic evidence and release gate |

## Interface signatures by feature (current proposed contract, not code already present)

### Phase 01 — Foundation and Reference Baselines

```csharp
using XlightsAI.Studio.Contracts;
namespace XlightsAI.Studio.Projects;
public sealed record ProjectManifest(ProjectId Id, int SchemaVersion,
    string Name, string RootDirectory, DateTimeOffset CreatedUtc);
public sealed record ProjectSummary(ProjectId Id, string Name, DateTimeOffset LastOpenedUtc);
public interface IProjectService {
    Task<ProjectManifest> CreateAsync(string directory, string name, CancellationToken ct);
    Task<ProjectManifest> OpenAsync(string directory, CancellationToken ct);
    Task SaveAsync(ProjectId id, CancellationToken ct);
    Task<IReadOnlyList<ProjectSummary>> RecentAsync(CancellationToken ct);
}
public interface IProjectStore {
    Task<ProjectManifest?> ReadManifestAsync(string directory, CancellationToken ct);
    Task WriteManifestAtomicallyAsync(ProjectManifest manifest, CancellationToken ct);
    Task MigrateDatabaseAsync(string directory, int targetSchema, CancellationToken ct);
}
public interface IAppDiagnostics { void RecordFailure(string code, Exception error); }
```

### Phase 02 — First Working xLights Sequence

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Projects;
namespace XlightsAI.Studio.Xlights;
public sealed record DisplayModel(ModelId Id, string XlightsName, string Kind,
    IReadOnlyList<string> GroupNames, IReadOnlyList<Point2> PreviewPoints);
public sealed record Point2(double X, double Y);
public sealed record DisplayGraph(string SourceLayoutHash, IReadOnlyList<DisplayModel> Models);
public sealed record EffectInstance(Guid Id, ModelId Target, int Layer, long StartTicks,
    long EndTicks, string RecipeId, string RecipeVersion, IReadOnlyDictionary<string,string> Parameters);
public sealed record SequenceDocument(Guid Id, long Revision, IReadOnlyList<EffectInstance> Effects);
public interface IShowLayoutImporter {
    Task<DisplayGraph> ImportReadOnlyAsync(string showDirectory, CancellationToken ct);
}
public interface IEffectRecipeRegistry {
    EffectInstance CreateVerified(string recipeId, ModelId target, long startTicks, long endTicks);
}
public interface IXsqExporter {
    Task<ExportResult> ExportNewAsync(DisplayGraph layout, SequenceDocument sequence,
        string outputPath, string targetXlightsVersion, CancellationToken ct);
}
public sealed record ExportResult(string Path, string Sha256, IReadOnlyList<string> Warnings);
```

### Phase 03 — Visual Timeline and Manual Editing

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;
namespace XlightsAI.Studio.Editing;
public sealed record EditScope(TimeWindow Time, IReadOnlySet<ModelId> Models,
    IReadOnlySet<Guid>? EffectIds, bool RespectLocks = true);
public abstract record EditCommand(DocumentRevision ExpectedRevision, EditScope Scope);
public sealed record MoveEffect(DocumentRevision ExpectedRevision, EditScope Scope,
    Guid EffectId, long NewStartTicks) : EditCommand(ExpectedRevision, Scope);
public sealed record EditTransaction(Guid Id, long BeforeRevision, long AfterRevision,
    string SerializedBeforePatch, string SerializedAfterPatch, DateTimeOffset AcceptedUtc);
public interface IEditTransactionService {
    Task<EditTransaction> ApplyAsync(EditCommand command, CancellationToken ct);
    Task<SequenceDocument> UndoAsync(Guid projectId, CancellationToken ct);
    Task<SequenceDocument> RedoAsync(Guid projectId, CancellationToken ct);
}
public interface IEffectLockPolicy { void AssertEditable(SequenceDocument document, EditScope scope); }
public interface ITimelineViewport { long TimeFromX(double pixel); double XFromTime(long ticks); }
```

### Phase 04 — Audio Waveform and Synchronized Playback

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
namespace XlightsAI.Studio.Audio;
public sealed record SongAsset(SongId Id, string FilePath, string Sha256,
    long SampleCount, int SampleRate, int Channels, long OriginTicks);
public sealed record WaveformEnvelope(long StartSample, long EndSample,
    float Min, float Max, float Rms);
public interface IAudioDecoder {
    Task<SongAsset> ProbeAsync(string path, CancellationToken ct);
    IAsyncEnumerable<ReadOnlyMemory<float>> DecodeMonoAsync(SongAsset song,
        int targetSampleRate, CancellationToken ct);
}
public interface IWaveformBuilder {
    Task<string> BuildPyramidAsync(SongAsset song, CancellationToken ct);
}
public interface IAudioTransport {
    long PositionTicks { get; }
    Task OpenAsync(SongAsset song, CancellationToken ct);
    Task PlayAsync(CancellationToken ct);
    Task PauseAsync(CancellationToken ct);
    Task SeekAsync(long ticks, CancellationToken ct);
    void SetLoop(TimeWindow? window);
}
```

### Phase 05 — Beat Measures and Rhythm Detection

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Xlights;
namespace XlightsAI.Studio.Rhythm;
public sealed record OnsetEvent(long TimeTicks, float Strength, string SourceAlgorithm);
public sealed record TempoHypothesis(double Bpm, double Confidence, string MeterGuess);
public sealed record BeatMarker(Guid Id, long TimeTicks, int BeatWithinBar, double Confidence, bool UserCorrected);
public sealed record BeatGrid(Guid VersionId, IReadOnlyList<BeatMarker> Beats,
    IReadOnlyList<TempoHypothesis> Candidates, string SourceHash);
public interface IRhythmAnalyzer {
    Task<BeatGrid> AnalyzeAsync(SongAsset song, RhythmSettings settings, CancellationToken ct);
}
public sealed record RhythmSettings(int AnalysisSampleRate, int FftSize, int HopSamples);
public interface IBeatGridEditor {
    Task<BeatGrid> MoveBeatAsync(Guid beatId, long newTimeTicks, long expectedVersion, CancellationToken ct);
    Task<BeatGrid> SetDownbeatAsync(Guid beatId, CancellationToken ct);
}
public interface IRhythmGesturePlanner {
    IReadOnlyList<EffectInstance> BuildPulses(BeatGrid grid, IReadOnlySet<ModelId> targets,
        string verifiedRecipeId, TimeWindow range);
}
```

### Phase 06 — Instrument Separation and Activity Detection

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Audio;
namespace XlightsAI.Studio.Stems;
public enum StemKind { Drums, Bass, Vocals, Guitar, Piano, Other }
public sealed record ModelAsset(string Id, string Version, Uri Source, string Sha256,
    string LicenseId, string InferenceBackend);
public sealed record StemAsset(Guid Id, StemKind Kind, string FilePath, int SampleRate,
    long SampleCount, long SourceOriginTicks, string Sha256);
public sealed record StemManifest(Guid RunId, string OriginalSongHash, string ModelId,
    IReadOnlyList<StemAsset> Stems, IReadOnlyList<string> Warnings);
public interface IModelAssetManager {
    Task<string> EnsureVerifiedAsync(ModelAsset asset, bool userConsent, CancellationToken ct);
}
public interface IStemSeparator {
    Task<StemManifest> SeparateAsync(SongAsset source, ModelAsset model,
        IProgress<SeparationProgress>? progress, CancellationToken ct);
}
public sealed record SeparationProgress(double Fraction, string Stage, long ProcessedSamples);
public interface IStemActivityDetector {
    Task<IReadOnlyList<StemActivity>> DetectAsync(StemAsset stem, CancellationToken ct);
}
public sealed record StemActivity(StemKind Kind, long StartTicks, long EndTicks, double Score);
```

### Phase 07 — Melody Pitch and Note Analysis

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Xlights;
namespace XlightsAI.Studio.Melody;
public sealed record NoteEvent(Guid Id, long StartTicks, long EndTicks,
    int MidiNote, double Velocity, double Confidence, bool UserCorrected);
public enum PitchMotion { Repeat, StepUp, StepDown, LeapUp, LeapDown, Rest, Hold }
public sealed record MelodicGesture(Guid NoteId, PitchMotion Motion, int IntervalSemitones,
    long StartTicks, long EndTicks, double Confidence);
public sealed record MelodyTrack(Guid Id, string SourceAssetHash,
    IReadOnlyList<NoteEvent> Notes, IReadOnlyList<MelodicGesture> Gestures);
public interface IMelodyAnalyzer {
    Task<MelodyTrack> AnalyzeAsync(StemAsset source, MelodySettings options, CancellationToken ct);
}
public interface INoteTrackEditor {
    Task<MelodyTrack> CorrectAsync(Guid trackId, NoteEvent corrected, CancellationToken ct);
}
public interface IMelodicGestureMapper {
    IReadOnlyList<EffectInstance> MapToEffects(MelodyTrack track,
        IReadOnlyList<ModelId> orderedPath, TimeWindow window, GestureMapSettings settings);
}

public sealed record MelodySettings(int TargetSampleRate, double MinConfidence);
public sealed record GestureMapSettings(string PitchPolicy, string RecipeId, bool RespectRests);
```

### Phase 08 — Song Structure and Repeated Phrases

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Stems;
namespace XlightsAI.Studio.Structure;
public enum SectionKind { Unknown, Intro, Verse, PreChorus, Chorus, Bridge, Solo, Outro }
public sealed record SongSection(Guid Id, SectionKind Kind, long StartTicks, long EndTicks,
    double Confidence, bool UserCorrected);
public sealed record MusicalPhrase(Guid Id, Guid SectionId, long StartTicks, long EndTicks,
    int? Bars, string SignatureHash);
public sealed record MotifLink(Guid SourcePhraseId, Guid TargetPhraseId,
    double Similarity, bool UserApproved, string InheritanceMask);
public sealed record SongStructure(Guid VersionId, IReadOnlyList<SongSection> Sections,
    IReadOnlyList<MusicalPhrase> Phrases, IReadOnlyList<MotifLink> Links);
public interface ISongStructureAnalyzer {
    Task<SongStructure> AnalyzeAsync(SongAnalysisContext context, CancellationToken ct);
}
public interface IMotifMatcher { IReadOnlyList<MotifLink> ProposeLinks(SongStructure structure); }
public interface IMotifInheritancePlanner {
    IReadOnlyList<EffectInstance> AdaptApprovedChoreography(MusicalPhrase source,
        MusicalPhrase target, string mask, SequenceDocument document);
}

public sealed record SongAnalysisContext(string SourceHash, BeatGrid BeatGrid,
    IReadOnlyList<MelodyTrack> MelodyTracks, IReadOnlyList<StemActivity> Activity);
```

### Phase 09 — Spatial Choreography and Display Paths

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;
using XlightsAI.Studio.Melody;
namespace XlightsAI.Studio.Display;
public sealed record DisplayNode(ModelId Id, string XlightsName, double X, double Y,
    string ModelKind, IReadOnlySet<string> Tags);
public sealed record SpatialEdge(ModelId From, ModelId To, string Relation,
    double Distance, bool UserDefined);
public sealed record SpatialPath(Guid Id, string Name, IReadOnlyList<ModelId> OrderedModels,
    bool IsClosed, int Version);
public sealed record SpatialDisplayGraph(string SourceLayoutHash,
    IReadOnlyList<DisplayNode> Nodes, IReadOnlyList<SpatialEdge> Edges,
    IReadOnlyList<SpatialPath> Paths);
public interface IDisplayGraphBuilder {
    SpatialDisplayGraph Build(DisplayGraph imported, GeometryPolicy policy);
}
public interface ISpatialPathEditor {
    SpatialPath Reorder(SpatialPath oldPath, IReadOnlyList<ModelId> newOrder);
    SpatialPath Reverse(SpatialPath oldPath);
}
public interface ISpatialGestureResolver {
    IReadOnlyList<ResolvedGesture> Resolve(MelodicGesture gesture,
        SpatialPath path, SpatialDisplayGraph display, ResolutionSettings settings);
}
public sealed record ResolvedGesture(ModelId Target, long StartTicks,
    long EndTicks, string GestureKind, double Intensity);

public sealed record GeometryPolicy(double MaxNeighborDistance, bool RespectGroups);
public sealed record ResolutionSettings(string BoundaryPolicy, bool SkipUnavailable);
```

### Phase 10 — Layered Musical Composition

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Xlights;
using XlightsAI.Studio.Editing;
namespace XlightsAI.Studio.Composition;
public enum MusicalRole { Lead, Rhythm, Bass, Harmony, Accent, Atmosphere }
public sealed record RoleAssignment(MusicalRole Role, string SourceTrackId,
    Guid SpatialPathId, int Priority, double Weight);
public sealed record VisualBudget(int MaxConcurrentFocalProps, double MaxMeanBrightness,
    double MaxEventDensityPerSecond, double DarknessFractionTarget);
public sealed record SectionVisualDirection(Guid SectionId, string PaletteId,
    double Intensity, ModelId? FocalProp, IReadOnlyList<RoleAssignment> Roles);
public sealed record VisualStory(Guid VersionId, IReadOnlyList<SectionVisualDirection> Sections);
public sealed record PlannedGesture(Guid Id, MusicalRole Role, string GestureType,
    TimeWindow Window, IReadOnlyList<ModelId> Targets, string RecipeId);
public sealed record ChoreographyPlan(Guid Id, string InputsHash,
    IReadOnlyList<PlannedGesture> Gestures, IReadOnlyList<string> Warnings);
public interface ICompositionPlanner {
    ChoreographyPlan PlanPhrase(PhraseContext phrase, SectionVisualDirection direction,
        SpatialDisplayGraph display, VisualBudget budget);
}
public interface IChoreographyCompiler {
    SequenceDocument Compile(ChoreographyPlan plan, SequenceDocument baseDocument,
        EditScope allowedScope);
}

public sealed record PhraseContext(Guid PhraseId, BeatGrid Beats,
    IReadOnlyList<MelodyTrack> Melodies, IReadOnlyList<StemActivity> Activity);
```

### Phase 11 — Selective AI Editing

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Xlights;
namespace XlightsAI.Studio.AiEdits;
public sealed record EditContext(long BaseDocumentRevision, EditScope Scope,
    IReadOnlyList<NoteEvent> Notes, IReadOnlyList<PlannedGesture> CurrentGestures,
    IReadOnlyList<string> PermittedRecipeIds);
public sealed record StructuredEditOperation(string OperationId, string Kind,
    TimeWindow Range, IReadOnlyList<ModelId> Targets,
    IReadOnlyDictionary<string, string> Parameters);
public sealed record EditProposal(Guid Id, long BaseRevision,
    IReadOnlyList<StructuredEditOperation> Operations, string Explanation);
public sealed record ProposalValidation(bool Allowed, IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
public interface IStructuredEditInterpreter {
    EditProposal ParseKnownCommand(string instruction, EditContext context);
}
public interface IOptionalAiPlanner {
    Task<EditProposal> ProposeAsync(string instruction, EditContext context, CancellationToken ct);
}
public interface IEditProposalValidator {
    ProposalValidation Validate(EditProposal proposal, EditContext context, SequenceDocument document);
}
public interface IEditApprovalService {
    Task<EditTransaction> ApproveAsync(Guid proposalId, CancellationToken ct);
}
```

### Phase 12 — Loop-Engineered Refinement

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Composition;
namespace XlightsAI.Studio.Refinement;
public enum RefinementStatus { Queued, Generating, Validating, PreviewReady,
    AwaitingApproval, Accepted, Rejected, Blocked, Cancelled }
public sealed record MetricValue(string Name, double Value, double Weight,
    bool HardGate, string Explanation);
public sealed record QualityReport(Guid CandidateId, string EvaluatorVersion,
    IReadOnlyList<MetricValue> Metrics, IReadOnlyList<string> Violations);
public sealed record Candidate(Guid Id, Guid RunId, int Attempt, int Seed,
    string SnapshotHash, ChoreographyPlan Plan, QualityReport? Report);
public sealed record LoopRun(Guid Id, long BaseRevision, EditScope Scope,
    int MaxAttempts, int AttemptsMade, RefinementStatus Status, Guid? BestCandidateId);
public interface ICandidateGenerator {
    Task<Candidate> GenerateAsync(LoopRun run, Candidate? prior, CancellationToken ct);
}
public interface IChoreographyQualityEvaluator {
    Task<QualityReport> EvaluateAsync(Candidate candidate, CancellationToken ct);
}
public interface IRefinementOrchestrator {
    Task<LoopRun> ExecuteAsync(Guid runId, CancellationToken ct);
    Task CancelAsync(Guid runId, CancellationToken ct);
}
```

### Phase 13 — xLights Rendering and Visual Evaluation

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;
using XlightsAI.Studio.Display;
using XlightsAI.Studio.Editing;
namespace XlightsAI.Studio.Rendering;
public enum RenderAuthority { ApproximateStudioPreview, ActualXlights }
public sealed record RenderCapability(string XlightsVersion, string ExecutablePath,
    bool SupportsHeadless, bool SupportsOutputDirectory);
public sealed record RenderRequest(Guid SequenceId, string ShowDirectory,
    string ExportedXsqPath, string OutputDirectory, TimeWindow? Selection,
    RenderAuthority RequestedAuthority);
public sealed record RenderArtifact(Guid Id, RenderAuthority Authority, bool Success,
    string RendererVersion, string? FseqPath, IReadOnlyList<string> Warnings,
    string EvidenceHash);
public interface IRenderCapabilityProbe {
    Task<RenderCapability> ProbeAsync(string executablePath, CancellationToken ct);
}
public interface IRenderAdapter {
    Task<RenderArtifact> RenderAsync(RenderRequest request, CancellationToken ct);
}
public interface ILocalPreviewRenderer {
    IAsyncEnumerable<PreviewFrame> PreviewAsync(SequenceDocument sequence,
        SpatialDisplayGraph layout, TimeWindow window, CancellationToken ct);
}
public sealed record PreviewFrame(long TimeTicks, IReadOnlyList<PixelPoint> Illuminated);
public sealed record PixelPoint(ModelId Model, double X, double Y, byte R, byte G, byte B);
```

### Phase 14 — Whole Song Composition and Continuity

```csharp
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Composition;
using XlightsAI.Studio.Editing;
using XlightsAI.Studio.Xlights;
namespace XlightsAI.Studio.WholeSong;
public sealed record SongSectionPlan(Guid SectionId, double TargetEnergy,
    string PaletteId, IReadOnlyList<Guid> PhrasePlanIds);
public sealed record TransitionPlan(Guid FromPhraseId, Guid ToPhraseId,
    TimeWindow Window, string Strategy, IReadOnlyList<string> Warnings);
public sealed record WholeSongPlan(Guid Id, Guid SongId,
    IReadOnlyList<SongSectionPlan> Sections, IReadOnlyList<TransitionPlan> Transitions,
    long BaseRevision);
public sealed record ContinuityFinding(string Code, string Severity,
    TimeWindow Window, IReadOnlyList<ModelId> Models, string Explanation);
public interface IWholeSongPlanner {
    WholeSongPlan Plan(SongStructure structure, VisualStory story,
        IReadOnlyList<ChoreographyPlan> acceptedPhrasePlans);
}
public interface IWholeSongAssembler {
    SequenceDocument Assemble(WholeSongPlan plan, SequenceDocument baseDocument);
}
public interface ISongContinuityEvaluator {
    IReadOnlyList<ContinuityFinding> Evaluate(SequenceDocument candidate,
        WholeSongPlan plan, SongStructure structure);
}
```

### Phase 15 — Packaging and Distribution

```csharp
using XlightsAI.Studio.Contracts;
namespace XlightsAI.Studio.Distribution;
public sealed record RuntimeCapability(string OperatingSystem, string Architecture,
    bool CpuInferenceAvailable, bool XlightsDetected, string? XlightsVersion);
public sealed record DownloadableAsset(string Id, string Version, Uri Url,
    string Sha256, long SizeBytes, string LicenseId, bool Required);
public sealed record ReleaseManifest(string ProductVersion, string Channel,
    IReadOnlyList<DownloadableAsset> Assets, string SignatureAlgorithm);
public interface IRuntimeCapabilityDetector {
    Task<RuntimeCapability> ProbeAsync(CancellationToken ct);
}
public interface IAssetInstaller {
    Task<string> EnsureInstalledAsync(DownloadableAsset asset,
        IProgress<double>? progress, CancellationToken ct);
}
public interface IUpdateVerifier {
    bool VerifyManifest(ReadOnlySpan<byte> signedManifest, ReadOnlySpan<byte> trustedKey);
}
public interface IStartupHealthChecks {
    Task<IReadOnlyList<string>> CheckAsync(CancellationToken ct);
}
```

### Phase 16 — Release Validation and Community Testing

```csharp
using XlightsAI.Studio.Contracts;
namespace XlightsAI.Studio.Release;
public sealed record CompatibilityCase(string Os, string Architecture,
    string XlightsVersion, string LayoutFixtureId, string AudioFixtureId);
public sealed record TestEvidence(string TestId, bool Passed, string ArtifactPath,
    string ToolVersion, DateTimeOffset RecordedUtc);
public sealed record AestheticRating(string AnonymousTrialId, string CandidateId,
    int Musicality, int Clarity, int Contrast, int Excitement, double CleanupMinutes);
public sealed record ReleaseReadiness(string Version, bool Eligible,
    IReadOnlyList<string> Blockers, IReadOnlyList<TestEvidence> Evidence);
public interface ICompatibilityMatrixRunner {
    Task<IReadOnlyList<TestEvidence>> RunAsync(IReadOnlyList<CompatibilityCase> cases,
        CancellationToken ct);
}
public interface IAestheticTrialCollector {
    Task RecordAsync(AestheticRating rating, CancellationToken ct);
}
public interface IReleaseGate {
    ReleaseReadiness Evaluate(IReadOnlyList<TestEvidence> evidence,
        IReadOnlyList<string> unresolvedRisks);
}
```

