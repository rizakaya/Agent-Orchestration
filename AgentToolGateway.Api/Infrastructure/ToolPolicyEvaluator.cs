using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;

namespace AgentToolGateway.Api.Infrastructure;

public sealed class ToolPolicyEvaluator(IApprovalStore approvalStore) : IToolPolicyEvaluator
{
    public async Task<PolicyEvaluationResult> EvaluateAsync(
        ToolDefinition definition,
        AgentRunContext context,
        CancellationToken cancellationToken)
    {
        var approvalRequired = definition.RequiresApproval ||
            definition.RiskLevel is ToolRiskLevel.High or ToolRiskLevel.Critical;

        if (!approvalRequired)
        {
            return new PolicyEvaluationResult { Allowed = true };
        }

        if (await approvalStore.ValidateAsync(definition.Name, context.ApprovalToken, cancellationToken))
        {
            return new PolicyEvaluationResult { Allowed = true };
        }

        var approval = await approvalStore.CreateAsync(
            new ApprovalRequest
            {
                RunId = context.RunId,
                ToolName = definition.Name,
                Input = default,
                Reason = "High-risk tool requires explicit approval."
            },
            cancellationToken);

        return new PolicyEvaluationResult
        {
            Allowed = false,
            ErrorCode = "ApprovalRequired",
            ErrorMessage = $"Tool '{definition.Name}' requires approval.",
            ApprovalId = approval.ApprovalId
        };
    }
}
