using System.Diagnostics;
using System.Text.Json;
using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;

namespace AgentToolGateway.Api.Infrastructure;

public sealed class ToolExecutor(
    IToolRegistry registry,
    IToolPolicyEvaluator policyEvaluator,
    IToolAuditStore auditStore) : IToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        object input,
        AgentRunContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        ToolDefinition? definition = null;
        object? typedInput = null;
        ToolExecutionResult result;
        string? approvalId = null;

        try
        {
            var tool = registry.GetTool(toolName);
            definition = tool.Definition;
            typedInput = DeserializeInput(input, tool.InputType);

            var policy = await policyEvaluator.EvaluateAsync(definition, context, cancellationToken);
            approvalId = policy.ApprovalId;
            if (!policy.Allowed)
            {
                result = ToolExecutionResult.Fail(
                    definition.Name,
                    policy.ErrorCode ?? "PolicyDenied",
                    policy.ErrorMessage ?? "Tool execution denied by policy.",
                    policy.ApprovalId);
            }
            else
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, definition.TimeoutSeconds)));

                var output = await tool.ExecuteAsync(typedInput, timeoutCts.Token);
                result = ToolExecutionResult.Ok(definition.Name, output);
            }
        }
        catch (KeyNotFoundException ex)
        {
            result = ToolExecutionResult.Fail(toolName, "UnknownTool", ex.Message);
        }
        catch (JsonException ex)
        {
            result = ToolExecutionResult.Fail(toolName, "InvalidInput", ex.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = ToolExecutionResult.Fail(toolName, "Timeout", "Tool execution timed out.");
        }
        catch (Exception ex)
        {
            result = ToolExecutionResult.Fail(toolName, "ToolExecutionFailed", ex.Message);
        }

        stopwatch.Stop();

        if (definition?.AuditEnabled ?? true)
        {
            await auditStore.WriteAsync(
                new ToolAuditLogEntry
                {
                    RunId = context.RunId,
                    ToolName = definition?.Name ?? toolName,
                    Input = typedInput ?? input,
                    RiskLevel = definition?.RiskLevel ?? ToolRiskLevel.Critical,
                    RequiresApproval = definition?.RequiresApproval ?? true,
                    ApprovalId = approvalId ?? result.Error?.ApprovalId,
                    StartedAt = startedAt,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Success = result.Success,
                    Error = result.Error
                },
                CancellationToken.None);
        }

        return result;
    }

    private static object DeserializeInput(object input, Type inputType)
    {
        if (input.GetType() == inputType)
        {
            return input;
        }

        if (input is JsonElement jsonElement)
        {
            return jsonElement.Deserialize(inputType, JsonOptions)
                ?? throw new JsonException("Input deserialized to null.");
        }

        var json = JsonSerializer.Serialize(input, JsonOptions);
        return JsonSerializer.Deserialize(json, inputType, JsonOptions)
            ?? throw new JsonException("Input deserialized to null.");
    }
}
