using AgentToolGateway.Api.Contracts;

namespace AgentToolGateway.Api.Abstractions;

public interface IAgentTool
{
    ToolDefinition Definition { get; }
    Type InputType { get; }
    Task<object?> ExecuteAsync(object input, CancellationToken cancellationToken);
}

public interface IAgentTool<TInput, TOutput> : IAgentTool
{
    Task<TOutput> ExecuteTypedAsync(TInput input, CancellationToken cancellationToken);
}

public abstract class AgentTool<TInput, TOutput> : IAgentTool<TInput, TOutput>
{
    public abstract ToolDefinition Definition { get; }
    public Type InputType => typeof(TInput);

    public abstract Task<TOutput> ExecuteTypedAsync(TInput input, CancellationToken cancellationToken);

    public async Task<object?> ExecuteAsync(object input, CancellationToken cancellationToken)
    {
        if (input is not TInput typedInput)
        {
            throw new ArgumentException($"Expected input type {typeof(TInput).Name}.", nameof(input));
        }

        return await ExecuteTypedAsync(typedInput, cancellationToken);
    }
}

public interface IToolRegistry
{
    ToolDefinition GetDefinition(string toolName);
    IAgentTool GetTool(string toolName);
    IReadOnlyList<ToolDefinition> GetAvailableTools();
}

public interface IToolExecutor
{
    Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        object input,
        AgentRunContext context,
        CancellationToken cancellationToken);
}

public interface IToolPolicyEvaluator
{
    Task<PolicyEvaluationResult> EvaluateAsync(
        ToolDefinition definition,
        AgentRunContext context,
        CancellationToken cancellationToken);
}

public sealed class PolicyEvaluationResult
{
    public required bool Allowed { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ApprovalId { get; init; }
}

public interface IToolAuditStore
{
    Task WriteAsync(ToolAuditLogEntry entry, CancellationToken cancellationToken);
}

public interface IApprovalStore
{
    Task<ApprovalRecord> CreateAsync(ApprovalRequest request, CancellationToken cancellationToken);
    Task<ApprovalRecord?> GetAsync(string approvalId, CancellationToken cancellationToken);
    Task<bool> ValidateAsync(string toolName, string? token, CancellationToken cancellationToken);
}
