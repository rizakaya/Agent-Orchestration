using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;

namespace AgentToolGateway.Api.Tools;

public sealed class CreatePatchDraftTool : AgentTool<CreatePatchDraftInput, CreatePatchDraftResult>
{
    public override ToolDefinition Definition { get; } = new()
    {
        Name = "CreatePatchDraft",
        Description = "Creates a patch draft without changing files.",
        RiskLevel = ToolRiskLevel.Medium,
        AccessType = ToolAccessType.Write,
        RequiresApproval = false,
        TimeoutSeconds = 5
    };

    public override Task<CreatePatchDraftResult> ExecuteTypedAsync(
        CreatePatchDraftInput input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Patch))
        {
            throw new ArgumentException("Patch is required.");
        }

        return Task.FromResult(new CreatePatchDraftResult
        {
            DraftId = $"patch-draft-{Guid.NewGuid():N}",
            Patch = input.Patch,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}

public sealed class CreatePatchDraftInput
{
    public required string Patch { get; init; }
}

public sealed class CreatePatchDraftResult
{
    public required string DraftId { get; init; }
    public required string Patch { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
