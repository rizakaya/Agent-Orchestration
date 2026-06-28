using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;
using AgentToolGateway.Api.Infrastructure;

namespace AgentToolGateway.Api.Tools;

public sealed class ApplyPatchTool(WorkspacePathValidator pathValidator)
    : AgentTool<ApplyPatchInput, ApplyPatchResult>
{
    public override ToolDefinition Definition { get; } = new()
    {
        Name = "ApplyPatch",
        Description = "Approval-required patch application mock. V1 validates intent but does not mutate files.",
        RiskLevel = ToolRiskLevel.High,
        AccessType = ToolAccessType.Write,
        RequiresApproval = true,
        TimeoutSeconds = 10
    };

    public override Task<ApplyPatchResult> ExecuteTypedAsync(ApplyPatchInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Patch))
        {
            throw new ArgumentException("Patch is required.");
        }

        if (!string.IsNullOrWhiteSpace(input.TargetPath))
        {
            pathValidator.ResolveInsideWorkspace(input.TargetPath);
        }

        return Task.FromResult(new ApplyPatchResult
        {
            Applied = false,
            DryRun = true,
            Message = "Patch was approved by policy but not applied because v1 runs ApplyPatch as a dry-run mock."
        });
    }
}

public sealed class ApplyPatchInput
{
    public required string Patch { get; init; }
    public string? TargetPath { get; init; }
}

public sealed class ApplyPatchResult
{
    public required bool Applied { get; init; }
    public required bool DryRun { get; init; }
    public required string Message { get; init; }
}
