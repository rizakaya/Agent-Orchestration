using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;
using AgentToolGateway.Api.Infrastructure;

namespace AgentToolGateway.Api.Tools;

public sealed class ReadFileTool(WorkspacePathValidator pathValidator)
    : AgentTool<ReadFileInput, ReadFileResult>
{
    public override ToolDefinition Definition { get; } = new()
    {
        Name = "ReadFile",
        Description = "Reads a file from inside the configured workspace root.",
        RiskLevel = ToolRiskLevel.Low,
        AccessType = ToolAccessType.Read,
        RequiresApproval = false,
        TimeoutSeconds = 5
    };

    public override async Task<ReadFileResult> ExecuteTypedAsync(ReadFileInput input, CancellationToken cancellationToken)
    {
        var fullPath = pathValidator.ResolveInsideWorkspace(input.Path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("File was not found inside the workspace.", input.Path);
        }

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        return new ReadFileResult
        {
            FilePath = Path.GetRelativePath(pathValidator.WorkspaceRoot, fullPath),
            Content = content
        };
    }
}

public sealed class ReadFileInput
{
    public required string Path { get; init; }
}

public sealed class ReadFileResult
{
    public required string FilePath { get; init; }
    public required string Content { get; init; }
}
