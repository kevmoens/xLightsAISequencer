using System.Text.Json;
using System.Text.Json.Serialization;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// JSON-file-backed <see cref="IEditProposalAuditStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) -- the identical pattern
/// <see cref="JsonSelectionContextStore"/> and <see cref="XlightsAI.Studio.Editing.JsonEditTransactionHistoryStore"/>
/// already use. Deliberately synchronous (no <c>CancellationToken</c>/<c>Task</c>) -- the only two call
/// sites, <see cref="EditProposalDryRunService.Preview"/>/<see cref="MotifPropagationDryRunService.Preview"/>,
/// are themselves synchronous (PHASE-11 Step 076/077's own already-accepted signatures), so an async store
/// would force those sync methods to block on a task anyway; the accept-path call sites
/// (<see cref="EditProposalApprovalService.AcceptAsync"/>/<see cref="MotifPropagationApprovalService.AcceptAsync"/>)
/// are already async and simply call this synchronous API inline, exactly as a fast local file write
/// warrants.
/// </summary>
public sealed class JsonEditProposalAuditStore : IEditProposalAuditStore
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private readonly string _directory;

    public JsonEditProposalAuditStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid proposalId) => Path.Combine(_directory, $"proposal-audit-{proposalId:N}.json");

    public void Save(EditProposalAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(record.ProposalId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(record, Options);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public EditProposalAuditRecord? Load(Guid proposalId)
    {
        var path = GetPath(proposalId);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        try
        {
            return JsonSerializer.Deserialize<EditProposalAuditRecord>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new EditProposalAuditCorruptException(path, ex);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
