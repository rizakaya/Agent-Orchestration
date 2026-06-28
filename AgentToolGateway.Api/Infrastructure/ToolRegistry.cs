using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;

namespace AgentToolGateway.Api.Infrastructure;

public sealed class ToolRegistry(IEnumerable<IAgentTool> tools) : IToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _tools = tools.ToDictionary(
        tool => tool.Definition.Name,
        StringComparer.OrdinalIgnoreCase);

    public ToolDefinition GetDefinition(string toolName) => GetTool(toolName).Definition;

    public IAgentTool GetTool(string toolName)
    {
        if (_tools.TryGetValue(toolName, out var tool))
        {
            return tool;
        }

        throw new KeyNotFoundException($"Tool '{toolName}' is not registered.");
    }

    public IReadOnlyList<ToolDefinition> GetAvailableTools() =>
        _tools.Values.Select(tool => tool.Definition).OrderBy(definition => definition.Name).ToList();
}
