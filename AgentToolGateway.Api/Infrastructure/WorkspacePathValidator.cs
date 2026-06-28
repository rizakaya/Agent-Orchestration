using Microsoft.Extensions.Options;

namespace AgentToolGateway.Api.Infrastructure;

public sealed class WorkspacePathValidator(IOptions<ToolGatewayOptions> options, IWebHostEnvironment environment)
{
    private readonly string _workspaceRoot = Path.GetFullPath(Path.Combine(
        environment.ContentRootPath,
        options.Value.WorkspaceRoot));

    public string WorkspaceRoot => _workspaceRoot;

    public string ResolveInsideWorkspace(string? relativeOrAbsolutePath)
    {
        var requestedPath = string.IsNullOrWhiteSpace(relativeOrAbsolutePath)
            ? _workspaceRoot
            : relativeOrAbsolutePath;

        var fullPath = Path.IsPathRooted(requestedPath)
            ? Path.GetFullPath(requestedPath)
            : Path.GetFullPath(Path.Combine(_workspaceRoot, requestedPath));

        if (!IsInsideWorkspace(fullPath))
        {
            throw new InvalidOperationException("Path is outside the configured workspace root.");
        }

        return fullPath;
    }

    public bool IsInsideWorkspace(string fullPath)
    {
        var normalizedRoot = _workspaceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(fullPath);

        return normalizedPath.Equals(_workspaceRoot, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
