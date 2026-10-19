using System.Text.Json;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// Default <see cref="IEditTransactionService"/>. Step 019: <see cref="MoveEffect"/>. Step 020 adds
/// <see cref="ResizeEffect"/> and <see cref="SetEffectParameters"/>. See interface doc for the
/// disclosed signature deviation.
/// </summary>
public sealed class EditTransactionService : IEditTransactionService
{
    private readonly IEffectLockPolicy _lockPolicy;
    private readonly IEffectRecipeRegistry _recipeRegistry;
    private readonly IEditTransactionHistoryStore? _historyStore;

    // PHASE-03 Step 022: per-document undo/redo stacks, keyed by SequenceDocument.Id (see the
    // "reconciled deviation" note on IEditTransactionService.UndoAsync/RedoAsync for why "projectId"
    // is interpreted as the document's own id). In-memory for this service instance's lifetime;
    // mirrored to _historyStore on every mutation when one is configured, so a fresh instance can
    // rehydrate a document's history instead of starting empty (see GetHydratedStackAsync).
    private readonly Dictionary<Guid, List<EditTransactionHistoryEntry>> _undoStacks = [];
    private readonly Dictionary<Guid, List<EditTransactionHistoryEntry>> _redoStacks = [];

    /// <summary>
    /// Step 019's own constructor, kept unchanged so every existing call site built against it
    /// (e.g. <c>Step019AcceptanceTests</c>) keeps compiling without modification — "treat all
    /// preceding completed step contracts as immutable." Defaults the recipe registry Step 020
    /// newly depends on to the one real, known-good <see cref="EffectRecipeRegistry"/> implementation
    /// already shipped since PHASE-02 Step 012.
    /// </summary>
    public EditTransactionService(IEffectLockPolicy lockPolicy)
        : this(lockPolicy, new EffectRecipeRegistry())
    {
    }

    /// <summary>PHASE-03 Step 020: explicit recipe registry, for callers that need a specific/test double instead of the real default.</summary>
    public EditTransactionService(IEffectLockPolicy lockPolicy, IEffectRecipeRegistry recipeRegistry)
    {
        ArgumentNullException.ThrowIfNull(lockPolicy);
        ArgumentNullException.ThrowIfNull(recipeRegistry);
        _lockPolicy = lockPolicy;
        _recipeRegistry = recipeRegistry;
    }

    /// <summary>
    /// PHASE-03 Step 022: same collaborators as the two-argument constructor above, plus an optional
    /// <see cref="IEditTransactionHistoryStore"/> so undo/redo history survives this service instance
    /// being discarded and a new one constructed (e.g. an app restart) — Section 4: "Undo/redo history
    /// survives restart within a retention limit." Additive: does not change either existing
    /// constructor's behavior.
    /// </summary>
    public EditTransactionService(IEffectLockPolicy lockPolicy, IEffectRecipeRegistry recipeRegistry, IEditTransactionHistoryStore historyStore)
        : this(lockPolicy, recipeRegistry)
    {
        ArgumentNullException.ThrowIfNull(historyStore);
        _historyStore = historyStore;
    }

    public async Task<EditApplyResult> ApplyAsync(SequenceDocument document, EditCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(command);

        ct.ThrowIfCancellationRequested();

        var result = command switch
        {
            MoveEffect move => ApplyMove(document, move),
            ResizeEffect resize => ApplyResize(document, resize),
            SetEffectParameters setParameters => ApplySetParameters(document, setParameters),
            SetEffectLocked setLocked => ApplySetLocked(document, setLocked),
            _ => throw new EditCommandRejectedException(
                EditRejectionReason.OutOfScope,
                $"Unsupported edit command type '{command.GetType().Name}'."),
        };

        await RecordHistoryAsync(document, result, ct).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// PHASE-03 Step 022: pops the most recently applied transaction for this document off its undo
    /// stack, restores the document to its exact pre-transaction snapshot ("Undo restores exact
    /// snapshot" — this step's own required acceptance), and pushes the popped entry onto the redo
    /// stack. "projectId" is this document's own <see cref="SequenceDocument.Id"/> (see the interface
    /// doc's "reconciled deviation" note).
    /// </summary>
    /// <exception cref="EditCommandRejectedException"><see cref="EditRejectionReason.NothingToUndo"/> when no applied edit exists to undo — no mutation occurs.</exception>
    public async Task<SequenceDocument> UndoAsync(Guid projectId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var undo = await GetHydratedStackAsync(_undoStacks, projectId, ct).ConfigureAwait(false);
        if (undo.Count == 0)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.NothingToUndo,
                $"No applied edit exists to undo for document '{projectId}'; no state was changed.");
        }

        var entry = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        var redo = GetOrCreateStack(_redoStacks, projectId);
        redo.Add(entry);

        await PersistAsync(projectId, undo, redo, ct).ConfigureAwait(false);
        return entry.Before;
    }

    /// <summary>
    /// PHASE-03 Step 022: re-applies the most recently undone transaction for this document, pushing
    /// it back onto the undo stack. Rejects as stale when the redo stack is empty — either nothing was
    /// ever undone, or a new edit was applied after the last undo, which clears pending redo history
    /// (see <see cref="RecordHistoryAsync"/>) because that new edit's "before" document state has
    /// already diverged from what the cleared redo entry assumed.
    /// </summary>
    /// <exception cref="EditCommandRejectedException"><see cref="EditRejectionReason.NothingToRedo"/> when no undone edit exists to redo — no mutation occurs.</exception>
    public async Task<SequenceDocument> RedoAsync(Guid projectId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var redo = await GetHydratedStackAsync(_redoStacks, projectId, ct).ConfigureAwait(false);
        if (redo.Count == 0)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.NothingToRedo,
                $"No undone edit exists to redo for document '{projectId}'; no state was changed.");
        }

        var entry = redo[^1];
        redo.RemoveAt(redo.Count - 1);
        var undo = GetOrCreateStack(_undoStacks, projectId);
        undo.Add(entry);

        await PersistAsync(projectId, undo, redo, ct).ConfigureAwait(false);
        return entry.After;
    }

    /// <summary>
    /// Records a successfully applied command onto <paramref name="before"/>'s own undo stack and
    /// clears its redo stack ("a new edit invalidates pending redo history" — the standard undo/redo
    /// correctness rule proven by <c>NewEditAfterUndo_ClearsRedoHistory_NegativePath</c>). Persists the
    /// updated snapshot when a history store is configured.
    /// </summary>
    private async Task RecordHistoryAsync(SequenceDocument before, EditApplyResult result, CancellationToken ct)
    {
        var entry = new EditTransactionHistoryEntry(before, result.Document, result.Transaction);
        var undo = GetOrCreateStack(_undoStacks, before.Id);
        undo.Add(entry);
        var redo = GetOrCreateStack(_redoStacks, before.Id);
        redo.Clear();

        await PersistAsync(before.Id, undo, redo, ct).ConfigureAwait(false);
    }

    private async Task PersistAsync(Guid documentId, List<EditTransactionHistoryEntry> undo, List<EditTransactionHistoryEntry> redo, CancellationToken ct)
    {
        if (_historyStore is null)
        {
            return;
        }

        await _historyStore.SaveAsync(documentId, new EditTransactionHistorySnapshot(undo.ToList(), redo.ToList()), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the in-memory stack for <paramref name="documentId"/>, rehydrating both the undo and
    /// redo stacks from <see cref="_historyStore"/> first if this service instance has never touched
    /// this document id in memory yet (the "survives restart" path: a freshly constructed service, the
    /// same stand-in this test suite uses for "the app restarted," has empty dictionaries, so the very
    /// first Undo/Redo call against a known document id loads its real, previously persisted history).
    /// </summary>
    private async Task<List<EditTransactionHistoryEntry>> GetHydratedStackAsync(
        Dictionary<Guid, List<EditTransactionHistoryEntry>> primary, Guid documentId, CancellationToken ct)
    {
        if (primary.TryGetValue(documentId, out var existing))
        {
            return existing;
        }

        if (_historyStore is not null)
        {
            var snapshot = await _historyStore.LoadAsync(documentId, ct).ConfigureAwait(false);
            if (snapshot is not null)
            {
                _undoStacks[documentId] = snapshot.UndoStack.ToList();
                _redoStacks[documentId] = snapshot.RedoStack.ToList();
                return primary[documentId];
            }
        }

        var fresh = new List<EditTransactionHistoryEntry>();
        primary[documentId] = fresh;
        return fresh;
    }

    private static List<EditTransactionHistoryEntry> GetOrCreateStack(Dictionary<Guid, List<EditTransactionHistoryEntry>> dict, Guid id)
    {
        if (!dict.TryGetValue(id, out var stack))
        {
            stack = [];
            dict[id] = stack;
        }

        return stack;
    }

    /// <summary>
    /// PHASE-03 Step 022's own command: toggles a single effect's <see cref="EffectInstance.Locked"/>
    /// bit. Deliberately resolves the target effect with <c>enforceLocks: false</c> — see
    /// <see cref="SetEffectLocked"/>'s own doc comment for why the lock toggle itself must not be
    /// gated by the very lock it is setting. Stale revision and out-of-scope-model checks are not
    /// bypassed.
    /// </summary>
    private EditApplyResult ApplySetLocked(SequenceDocument document, SetEffectLocked setLocked)
    {
        var (effectIndex, effect) = ResolveEditableEffect(
            document, setLocked.ExpectedRevision, setLocked.Scope, setLocked.EffectId, enforceLocks: false);

        var updatedEffect = effect with { Locked = setLocked.Locked };
        return CommitReplacement(document, effectIndex, effect, updatedEffect);
    }

    private EditApplyResult ApplyMove(SequenceDocument document, MoveEffect move)
    {
        var (effectIndex, effect) = ResolveEditableEffect(document, move.ExpectedRevision, move.Scope, move.EffectId);

        // Clamp negative times; preserve duration exactly (Step 019 implementation guidance:
        // "clamp negative times and prevent inadvertently changing effect duration").
        var duration = effect.EndTicks - effect.StartTicks;
        var newStart = Math.Max(0, move.NewStartTicks);
        var newEnd = newStart + duration;

        if (newStart < move.Scope.Time.StartTicks || newEnd > move.Scope.Time.EndTicks)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.OutOfScope,
                $"Moving effect '{effect.Id}' to [{newStart}, {newEnd}) would leave the permitted edit scope's time window [{move.Scope.Time.StartTicks}, {move.Scope.Time.EndTicks}); no edit was applied.");
        }

        var movedEffect = effect with { StartTicks = newStart, EndTicks = newEnd };
        return CommitReplacement(document, effectIndex, effect, movedEffect);
    }

    private EditApplyResult ApplyResize(SequenceDocument document, ResizeEffect resize)
    {
        var (effectIndex, effect) = ResolveEditableEffect(document, resize.ExpectedRevision, resize.Scope, resize.EffectId);

        // "Resize independently of playback/frame snap" — StartTicks is never touched by this
        // command; only EndTicks moves, so duration changes exactly by the caller's own requested
        // delta with no implicit clamp/snap (unlike MoveEffect's negative-start clamp, which exists
        // for a different reason: keeping a dragged effect from going off the timeline's own origin).
        var newEnd = resize.NewEndTicks;

        if (newEnd <= effect.StartTicks)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.InvalidDuration,
                $"Resizing effect '{effect.Id}' to end at {newEnd} would leave a zero-length or negative-length effect (start is {effect.StartTicks}); no edit was applied.");
        }

        if (newEnd > resize.Scope.Time.EndTicks)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.OutOfScope,
                $"Resizing effect '{effect.Id}' to end at {newEnd} would leave the permitted edit scope's time window [{resize.Scope.Time.StartTicks}, {resize.Scope.Time.EndTicks}); no edit was applied.");
        }

        var resizedEffect = effect with { EndTicks = newEnd };
        return CommitReplacement(document, effectIndex, effect, resizedEffect);
    }

    private EditApplyResult ApplySetParameters(SequenceDocument document, SetEffectParameters setParameters)
    {
        var (effectIndex, effect) = ResolveEditableEffect(document, setParameters.ExpectedRevision, setParameters.Scope, setParameters.EffectId);

        // Every requested key/value is verified against the effect's own recipe before any is
        // applied — one unsupported entry rejects the whole command, never a partial parameter set
        // (the same "no partial mutation" rule every other gate in this class enforces).
        foreach (var (key, value) in setParameters.ParameterChanges)
        {
            if (!_recipeRegistry.IsSupportedParameterValue(effect.RecipeId, key, value))
            {
                throw new EditCommandRejectedException(
                    EditRejectionReason.UnsupportedParameter,
                    $"Recipe '{effect.RecipeId}' does not support parameter '{key}' = '{value}' on effect '{effect.Id}'; no edit was applied.");
            }
        }

        // Preserve unchanged effect fields: only the requested keys are overwritten; every other
        // existing parameter on the effect is carried forward untouched.
        var mergedParameters = new Dictionary<string, string>(effect.Parameters, StringComparer.Ordinal);
        foreach (var (key, value) in setParameters.ParameterChanges)
        {
            mergedParameters[key] = value;
        }

        var updatedEffect = effect with { Parameters = mergedParameters };
        return CommitReplacement(document, effectIndex, effect, updatedEffect);
    }

    /// <summary>
    /// Shared gates every Step 019/020/022 command runs before touching anything: stale revision,
    /// unknown effect id, out-of-scope model, out-of-scope explicit effect id, then — when
    /// <paramref name="enforceLocks"/> is <see langword="true"/> (every caller except
    /// <see cref="ApplySetLocked"/>) — locked. Every gate runs to completion, in order, before any
    /// replacement document or transaction object is constructed — "no partial mutation" means not
    /// even an intermediate half-valid object is ever built, let alone persisted or observed by the
    /// caller.
    /// </summary>
    private (int Index, EffectInstance Effect) ResolveEditableEffect(
        SequenceDocument document, DocumentRevision expectedRevision, EditScope scope, Guid effectId, bool enforceLocks = true)
    {
        if (expectedRevision.Value != document.Revision)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.StaleRevision,
                $"Expected revision {expectedRevision.Value} does not match the document's current revision {document.Revision}; no edit was applied.");
        }

        var effectIndex = -1;
        for (var i = 0; i < document.Effects.Count; i++)
        {
            if (document.Effects[i].Id == effectId)
            {
                effectIndex = i;
                break;
            }
        }

        if (effectIndex < 0)
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.EffectNotFound,
                $"No effect with id '{effectId}' exists in this document; no edit was applied.");
        }

        var effect = document.Effects[effectIndex];

        if (!scope.Models.Contains(effect.Target))
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.OutOfScope,
                $"Effect '{effect.Id}' targets '{effect.Target.Value}', which is outside the permitted edit scope's model set; no edit was applied.");
        }

        if (scope.EffectIds is not null && !scope.EffectIds.Contains(effectId))
        {
            throw new EditCommandRejectedException(
                EditRejectionReason.OutOfScope,
                $"Effect '{effectId}' is outside the permitted edit scope's explicit effect set; no edit was applied.");
        }

        if (enforceLocks)
        {
            _lockPolicy.AssertEditable(document, scope);
        }

        return (effectIndex, effect);
    }

    private static EditApplyResult CommitReplacement(
        SequenceDocument document, int effectIndex, EffectInstance before, EffectInstance after)
    {
        var effects = document.Effects.ToList();
        effects[effectIndex] = after;
        var newRevision = document.Revision + 1;
        var updatedDocument = document with { Revision = newRevision, Effects = effects };

        var transaction = new EditTransaction(
            Guid.NewGuid(),
            document.Revision,
            newRevision,
            JsonSerializer.Serialize(before),
            JsonSerializer.Serialize(after),
            DateTimeOffset.UtcNow);

        return new EditApplyResult(updatedDocument, transaction);
    }
}
