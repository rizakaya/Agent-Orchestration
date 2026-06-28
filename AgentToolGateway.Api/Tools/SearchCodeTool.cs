using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;
using AgentToolGateway.Api.Infrastructure;

namespace AgentToolGateway.Api.Tools;

public sealed class SearchCodeTool(WorkspacePathValidator pathValidator)
    : AgentTool<SearchCodeInput, SearchCodeResult>
{
    public override ToolDefinition Definition { get; } = new()
    {
        Name = "SearchCode",
        Description = "Searches text files in the workspace and returns matching snippets.",
        RiskLevel = ToolRiskLevel.Low,
        AccessType = ToolAccessType.Read,
        RequiresApproval = false,
        TimeoutSeconds = 10
    };

    public override async Task<SearchCodeResult> ExecuteTypedAsync(
        SearchCodeInput input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Query))
        {
            throw new ArgumentException("Query is required.");
        }

        var root = pathValidator.ResolveInsideWorkspace(input.Path);
        var files = File.Exists(root)
            ? new[] { root }
            : Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(IsSearchableFile);

        var matches = new List<SearchCodeMatch>();
        var maxResults = Math.Clamp(input.MaxResults, 1, 100);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!pathValidator.IsInsideWorkspace(file))
            {
                continue;
            }

            var lineNumber = 0;
            await foreach (var line in File.ReadLinesAsync(file, cancellationToken))
            {
                lineNumber++;
                if (line.Contains(input.Query, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new SearchCodeMatch
                    {
                        FilePath = Path.GetRelativePath(pathValidator.WorkspaceRoot, file),
                        LineNumber = lineNumber,
                        Snippet = line.Trim()
                    });
                }

                if (matches.Count >= maxResults)
                {
                    return new SearchCodeResult { Matches = matches };
                }
            }
        }

        return new SearchCodeResult { Matches = matches };
    }

    private static bool IsSearchableFile(string file)
    {
        var directory = Path.GetDirectoryName(file) ?? string.Empty;
        if (directory.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = Path.GetExtension(file);
        return extension is ".cs" or ".json" or ".md" or ".txt" or ".csproj" or ".sln" or ".props" or ".targets";
    }
}

public sealed class SearchCodeInput
{
    public required string Query { get; init; }
    public string? Path { get; init; }
    public int MaxResults { get; init; } = 20;
}

public sealed class SearchCodeResult
{
    public required List<SearchCodeMatch> Matches { get; init; }
}

public sealed class SearchCodeMatch
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Snippet { get; init; }
}
