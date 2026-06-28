using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;
using AgentToolGateway.Api.Infrastructure;

namespace AgentToolGateway.Api.Tools;

public sealed class GetGitDiffTool(ProcessRunner processRunner)
    : AgentTool<GetGitDiffInput, GetGitDiffResult>
{
    public override ToolDefinition Definition { get; } = new()
    {
        Name = "GetGitDiff",
        Description = "Returns the current git diff for the workspace.",
        RiskLevel = ToolRiskLevel.Low,
        AccessType = ToolAccessType.Read,
        RequiresApproval = false,
        TimeoutSeconds = 10
    };

    public override async Task<GetGitDiffResult> ExecuteTypedAsync(GetGitDiffInput input, CancellationToken cancellationToken)
    {
        var command = input.StagedOnly ? "git diff --cached" : "git diff";
        var result = await processRunner.RunAsync(command, Definition.TimeoutSeconds, cancellationToken);
        return new GetGitDiffResult
        {
            Diff = result.StandardOutput,
            ExitCode = result.ExitCode,
            Error = result.StandardError,
            GitAvailable = result.ExitCode is not null &&
                !result.StandardError.Contains("not recognized", StringComparison.OrdinalIgnoreCase) &&
                !result.StandardError.Contains("No such file", StringComparison.OrdinalIgnoreCase)
        };
    }
}

public sealed class GetGitDiffInput
{
    public bool StagedOnly { get; init; }
}

public sealed class GetGitDiffResult
{
    public required string Diff { get; init; }
    public int? ExitCode { get; init; }
    public string? Error { get; init; }
    public required bool GitAvailable { get; init; }
}
