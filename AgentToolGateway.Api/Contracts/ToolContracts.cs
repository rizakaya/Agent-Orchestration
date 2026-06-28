using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentToolGateway.Api.Contracts;

public enum ToolRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum ToolAccessType
{
    Read,
    Execute,
    Write,
    External
}

public sealed class ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ToolRiskLevel RiskLevel { get; init; }
    public required ToolAccessType AccessType { get; init; }
    public bool RequiresApproval { get; init; }
    public int TimeoutSeconds { get; init; }
    public bool AuditEnabled { get; init; } = true;
}

public sealed class AgentRunContext
{
    public required string RunId { get; init; }
    public string? ApprovalToken { get; init; }
}

public sealed class ToolCallRequest
{
    public required string RunId { get; init; }
    public required string ToolName { get; init; }
    public JsonElement Input { get; init; }
    public string? ApprovalToken { get; init; }
}

public sealed class ToolExecutionResult
{
    public required string ToolName { get; init; }
    public required bool Success { get; init; }
    public object? Output { get; init; }
    public ToolError? Error { get; init; }

    public static ToolExecutionResult Ok(string toolName, object? output) =>
        new() { ToolName = toolName, Success = true, Output = output };

    public static ToolExecutionResult Fail(string toolName, string code, string message, string? approvalId = null) =>
        new()
        {
            ToolName = toolName,
            Success = false,
            Error = new ToolError { Code = code, Message = message, ApprovalId = approvalId }
        };
}

public sealed class ToolError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? ApprovalId { get; init; }
}

public sealed class ToolAuditLogEntry
{
    public required string RunId { get; init; }
    public required string ToolName { get; init; }
    public required object? Input { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ToolRiskLevel RiskLevel { get; init; }
    public required bool RequiresApproval { get; init; }
    public string? ApprovalId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required long DurationMs { get; init; }
    public required bool Success { get; init; }
    public ToolError? Error { get; init; }
}

public sealed class ApprovalRequest
{
    public required string RunId { get; init; }
    public required string ToolName { get; init; }
    public JsonElement Input { get; init; }
    public string? Reason { get; init; }
}

public sealed class ApprovalRecord
{
    public required string ApprovalId { get; init; }
    public required string Token { get; init; }
    public required string RunId { get; init; }
    public required string ToolName { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed class AgentPromptRequest
{
    public required string RunId { get; init; }
    public required string Prompt { get; init; }
}

public sealed class AgentToolIntent
{
    public required string ToolName { get; init; }
    public JsonElement Input { get; init; }
}
