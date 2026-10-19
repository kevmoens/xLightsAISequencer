using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// PHASE-08 Step 058 ("Build phrase relationship/propagation inspector") -- SCREEN 11 STRUCTURE's own
/// phrase relationship view model. Owns the bindable rendering of one song's effective (automatic plus
/// user-override) motif grouping, routes manual link/unlink decisions through
/// <see cref="IMotifLinkOverrideStore"/> and <see cref="MotifGroupOverrideApplier"/>, and exposes template
/// selection/preview against the already-shipped Step 057 <see cref="IChoreographyTemplatePlanner"/>.
/// Contains no Avalonia types, same convention as every other screen view model in this project
/// (<see cref="PianoRollViewModel"/>/<see cref="TimelineViewModel"/>). Mirrors <see cref="PianoRollViewModel"/>'s
/// explicit loading/empty/error/loaded state machine and its "catch, set an error property, never let an
/// exception reach the UI, never a partial mutation" convention for every mutating action.
/// </summary>
public sealed class PhraseRelationshipInspectorViewModel : ObservableObject
{
    private ScreenLoadState _loadState = ScreenLoadState.Empty;
    private string? _errorMessage;
    private string? _actionErrorMessage;

    private SongId _songId;
    private PhraseSegmentationAnalysis? _phraseAnalysis;
    private MotifClusteringAnalysis? _motifClustering;
    private IReadOnlyList<MotifLinkOverride> _overrides = [];
    private IReadOnlyList<MotifGroupCandidate> _effectiveGroups = [];
    private ChoreographyTemplateAnalysis? _templateAnalysis;
    private Guid? _selectedPhraseId;
    private Guid? _selectedTemplateId;
    private ChoreographyPreviewResult? _currentPreview;

    public ScreenLoadState LoadState
    {
        get => _loadState;
        private set
        {
            if (SetField(ref _loadState, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsError));
                OnPropertyChanged(nameof(IsLoaded));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    /// <summary>Set whenever a link/unlink/template-selection/preview action fails (invalid input, unknown phrase, or a stale template analysis); cleared on the next successful action. Nothing already persisted or rendered is mutated on failure.</summary>
    public string? ActionErrorMessage
    {
        get => _actionErrorMessage;
        private set => SetField(ref _actionErrorMessage, value);
    }

    public bool IsEmpty => LoadState == ScreenLoadState.Empty;

    public bool IsError => LoadState == ScreenLoadState.Error;

    public bool IsLoaded => LoadState == ScreenLoadState.Loaded;

    public Guid? SelectedPhraseId
    {
        get => _selectedPhraseId;
        private set
        {
            if (SetField(ref _selectedPhraseId, value))
            {
                OnPropertyChanged(nameof(Rows));
            }
        }
    }

    public Guid? SelectedTemplateId => _selectedTemplateId;

    public ChoreographyPreviewResult? CurrentPreview
    {
        get => _currentPreview;
        private set
        {
            if (SetField(ref _currentPreview, value))
            {
                OnPropertyChanged(nameof(PreviewEffectCount));
                OnPropertyChanged(nameof(PreviewExcludedReasons));
            }
        }
    }

    public int PreviewEffectCount => _currentPreview?.Effects.Count ?? 0;

    public IReadOnlyList<string> PreviewExcludedReasons => _currentPreview?.ExcludedReasons ?? [];

    /// <summary>
    /// The real, currently-rendered phrase rows -- empty exactly when <see cref="IsLoaded"/> is
    /// <see langword="false"/>, never a stale leftover from a previously opened song.
    /// </summary>
    public IReadOnlyList<PhraseRelationshipRowView> Rows
    {
        get
        {
            if (_phraseAnalysis is not { } phraseAnalysis)
            {
                return [];
            }

            var views = new List<PhraseRelationshipRowView>(phraseAnalysis.Phrases.Count);
            foreach (var phrase in phraseAnalysis.Phrases.OrderBy(p => p.StartBarIndex))
            {
                var phraseId = phrase.Id;
                var group = _effectiveGroups.FirstOrDefault(g => g.PhraseIds.Contains(phraseId));
                var linked = group?.PhraseIds.Where(id => id != phraseId).ToList() ?? [];
                var isTemplateSource = _templateAnalysis?.Templates.Any(t => t.SourcePhraseId == phraseId) ?? false;

                views.Add(new PhraseRelationshipRowView(
                    phraseId,
                    phrase.StartBarIndex,
                    group?.Id,
                    linked,
                    isTemplateSource,
                    IsSelected: phraseId == SelectedPhraseId,
                    SelectCommand: new DelegateCommand(() => SelectPhrase(phraseId))));
            }

            return views;
        }
    }

    /// <summary>Selects <paramref name="phraseId"/> -- a no-op (not a crash, not a dangling selection) when no song is loaded or <paramref name="phraseId"/> does not exist in it.</summary>
    public void SelectPhrase(Guid phraseId)
    {
        if (_phraseAnalysis is null || !_phraseAnalysis.Phrases.Any(p => p.Id == phraseId))
        {
            return;
        }

        SelectedPhraseId = phraseId;
    }

    /// <summary>
    /// Opens the inspector for <paramref name="songId"/>: loads this song's persisted link/unlink decisions
    /// from <paramref name="overrideStore"/> and merges them onto <paramref name="motifClustering"/>'s own
    /// automatic links via <see cref="MotifGroupOverrideApplier.Apply"/>. <paramref name="templateAnalysis"/>
    /// (already loaded by the caller, or <see langword="null"/> when none is persisted yet) is carried
    /// through unchanged so <see cref="Rows"/> can render which phrase (if any) is already a defined
    /// template's own source. Re-opening the same song after a restart, with the same files still on disk,
    /// reproduces the exact same <see cref="Rows"/> -- "whole-song persistence after reopen" (this step's
    /// own UI behavior line, verbatim).
    /// </summary>
    public async Task<bool> OpenAsync(
        IMotifLinkOverrideStore overrideStore,
        SongId songId,
        PhraseSegmentationAnalysis phraseAnalysis,
        MotifClusteringAnalysis motifClustering,
        ChoreographyTemplateAnalysis? templateAnalysis,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(overrideStore);
        ArgumentNullException.ThrowIfNull(phraseAnalysis);
        ArgumentNullException.ThrowIfNull(motifClustering);

        ErrorMessage = null;
        ActionErrorMessage = null;
        try
        {
            var overrides = await overrideStore.LoadAsync(songId, ct).ConfigureAwait(true);
            var effectiveGroups = MotifGroupOverrideApplier.Apply(songId, phraseAnalysis.Phrases, motifClustering.Links, overrides);

            _songId = songId;
            _phraseAnalysis = phraseAnalysis;
            _motifClustering = motifClustering;
            _overrides = overrides;
            _effectiveGroups = effectiveGroups;
            _templateAnalysis = templateAnalysis;
            _selectedTemplateId = null;
            _currentPreview = null;

            if (_selectedPhraseId is { } selected && !phraseAnalysis.Phrases.Any(p => p.Id == selected))
            {
                _selectedPhraseId = null;
            }

            LoadState = phraseAnalysis.Phrases.Count == 0 ? ScreenLoadState.Empty : ScreenLoadState.Loaded;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (Exception ex)
        {
            _phraseAnalysis = null;
            _motifClustering = null;
            _overrides = [];
            _effectiveGroups = [];
            _templateAnalysis = null;
            _selectedPhraseId = null;
            ErrorMessage = ex.Message;
            LoadState = ScreenLoadState.Error;
            RaiseStateDerivedChanged();
            return false;
        }
    }

    /// <summary>
    /// "Unlink" (this step's own required acceptance, verbatim: "User links/unlinks motifs..."): removes
    /// <paramref name="phraseId"/> from every other phrase it is currently, effectively grouped with --
    /// recorded as one explicit <c>ForceLinked: false</c> decision per remaining sibling, so every sibling
    /// stays linked to every other sibling ("User can unlink one chorus without affecting others," the
    /// phase's own hard-gate test, verbatim). Returns <see langword="false"/> (via
    /// <see cref="ActionErrorMessage"/>, nothing persisted) when no song is open or
    /// <paramref name="phraseId"/> is not currently a member of any effective group.
    /// </summary>
    public async Task<bool> UnlinkPhraseAsync(IMotifLinkOverrideStore overrideStore, Guid phraseId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(overrideStore);

        if (_phraseAnalysis is null || _motifClustering is null)
        {
            ActionErrorMessage = "Open a song before linking/unlinking motifs.";
            return false;
        }

        var group = _effectiveGroups.FirstOrDefault(g => g.PhraseIds.Contains(phraseId));
        if (group is null)
        {
            ActionErrorMessage = $"Phrase '{phraseId}' is not currently linked to any motif group.";
            return false;
        }

        var siblings = group.PhraseIds.Where(id => id != phraseId).ToList();
        var decisions = siblings.Select(sibling => new MotifLinkOverride(phraseId, sibling, ForceLinked: false));

        return await ApplyDecisionsAsync(overrideStore, decisions, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// "Link" (this step's own required acceptance, verbatim): records that <paramref name="phraseAId"/>
    /// and <paramref name="phraseBId"/> share a motif, regardless of any automatic proposal. Returns
    /// <see langword="false"/> (via <see cref="ActionErrorMessage"/>, nothing persisted) when no song is
    /// open, the two ids are the same phrase, or either id is not a phrase in the currently open song.
    /// </summary>
    public async Task<bool> LinkPhrasesAsync(IMotifLinkOverrideStore overrideStore, Guid phraseAId, Guid phraseBId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(overrideStore);

        if (_phraseAnalysis is null || _motifClustering is null)
        {
            ActionErrorMessage = "Open a song before linking/unlinking motifs.";
            return false;
        }

        if (phraseAId == phraseBId)
        {
            ActionErrorMessage = "Cannot link a phrase to itself.";
            return false;
        }

        if (!_phraseAnalysis.Phrases.Any(p => p.Id == phraseAId) || !_phraseAnalysis.Phrases.Any(p => p.Id == phraseBId))
        {
            ActionErrorMessage = "Both phrases must belong to the currently open song.";
            return false;
        }

        return await ApplyDecisionsAsync(overrideStore, [new MotifLinkOverride(phraseAId, phraseBId, ForceLinked: true)], ct).ConfigureAwait(true);
    }

    private async Task<bool> ApplyDecisionsAsync(IMotifLinkOverrideStore overrideStore, IEnumerable<MotifLinkOverride> decisions, CancellationToken ct)
    {
        var decisionKeys = decisions.Select(d => (UnorderedKey(d.PhraseAId, d.PhraseBId), d)).ToList();

        var merged = _overrides
            .Where(existing => decisionKeys.All(d => UnorderedKey(existing.PhraseAId, existing.PhraseBId) != d.Item1))
            .Concat(decisionKeys.Select(d => d.d))
            .ToList();

        IReadOnlyList<MotifGroupCandidate> recomputed;
        try
        {
            // Validate before persisting -- "invalid input... must not corrupt the project" (verbatim):
            // a malformed decision is rejected without ever reaching the override store.
            recomputed = MotifGroupOverrideApplier.Apply(_songId, _phraseAnalysis!.Phrases, _motifClustering!.Links, merged);
        }
        catch (MotifClusteringInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }

        await overrideStore.SaveAsync(_songId, merged, ct).ConfigureAwait(true);

        _overrides = merged;
        _effectiveGroups = recomputed;
        ActionErrorMessage = null;
        RaiseStateDerivedChanged();
        return true;
    }

    private static (Guid, Guid) UnorderedKey(Guid a, Guid b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    /// <summary>
    /// Defines (or refreshes) a choreography template from <paramref name="sourcePhraseId"/>'s own
    /// occurrence of the effective motif group it currently belongs to ("template selection," this step's
    /// own UI behavior line, verbatim) -- built atop the already-shipped, unmodified Step 057
    /// <see cref="IChoreographyTemplatePlanner"/>, against an effective <see cref="MotifClusteringAnalysis"/>
    /// view (the original analysis with <see cref="MotifClusteringAnalysis.Groups"/> replaced by this
    /// inspector's own post-override <see cref="MotifGroupOverrideApplier"/> result) so a group a user just
    /// linked/unlinked is immediately selectable, without waiting for a fresh automatic re-cluster. Returns
    /// <see langword="false"/> (via <see cref="ActionErrorMessage"/>, nothing persisted, <see cref="SelectedTemplateId"/>
    /// unchanged) on any <see cref="ChoreographyTemplateInvalidException"/> -- including a stale
    /// already-persisted analysis ("stale persisted state must not corrupt the project," verbatim).
    /// </summary>
    public async Task<bool> SelectTemplateSourceAsync(
        IChoreographyTemplatePlanner planner,
        SequenceDocument sourceDocument,
        Guid sourcePhraseId,
        ChoreographyInheritanceMask mask,
        IReadOnlySet<int> rhythmLayers,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(sourceDocument);
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(rhythmLayers);

        if (_phraseAnalysis is null || _motifClustering is null)
        {
            ActionErrorMessage = "Open a song before selecting a template source.";
            return false;
        }

        var group = _effectiveGroups.FirstOrDefault(g => g.PhraseIds.Contains(sourcePhraseId));
        if (group is null)
        {
            ActionErrorMessage = $"Phrase '{sourcePhraseId}' is not currently a member of any effective motif group.";
            return false;
        }

        var effectiveClustering = _motifClustering with { Groups = _effectiveGroups };
        var context = new ChoreographyTemplateContext(_songId, _motifClustering.SourceHash, effectiveClustering, _phraseAnalysis, sourceDocument);

        try
        {
            var analysis = await planner.DefineTemplateAsync(
                _templateAnalysis, context, group.Id, sourcePhraseId, mask, rhythmLayers, ct).ConfigureAwait(true);

            _templateAnalysis = analysis;
            _selectedTemplateId = analysis.Templates.First(t => t.MotifGroupId == group.Id && t.SourcePhraseId == sourcePhraseId).Id;
            ActionErrorMessage = null;
            RaiseStateDerivedChanged();
            return true;
        }
        catch (ChoreographyTemplateInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Computes (and exposes via <see cref="CurrentPreview"/>) the real adapted effect list
    /// <paramref name="targetPhraseId"/>'s own occurrence of <see cref="SelectedTemplateId"/> would receive
    /// if accepted -- "previews related effects" (this step's own required acceptance, verbatim). Pure/
    /// read-only, same as the underlying <see cref="IChoreographyTemplatePlanner.PreviewInstance"/>. Returns
    /// <see langword="false"/> (via <see cref="ActionErrorMessage"/>, <see cref="CurrentPreview"/> left
    /// unchanged) when no template has been selected yet or the planner rejects the request.
    /// </summary>
    public bool PreviewTarget(IChoreographyTemplatePlanner planner, SequenceDocument destinationDocument, Guid targetPhraseId)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(destinationDocument);

        if (_templateAnalysis is null || _selectedTemplateId is not { } templateId || _phraseAnalysis is null)
        {
            ActionErrorMessage = "Select a template source before previewing a target phrase.";
            return false;
        }

        try
        {
            CurrentPreview = planner.PreviewInstance(_templateAnalysis, templateId, targetPhraseId, _phraseAnalysis, destinationDocument);
            ActionErrorMessage = null;
            return true;
        }
        catch (ChoreographyTemplateInvalidException ex)
        {
            ActionErrorMessage = ex.Message;
            return false;
        }
    }

    private void RaiseStateDerivedChanged()
    {
        OnPropertyChanged(nameof(SelectedPhraseId));
        OnPropertyChanged(nameof(SelectedTemplateId));
        OnPropertyChanged(nameof(Rows));
    }
}
