using System.Collections.Concurrent;
using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;

namespace AgentToolGateway.Api.Infrastructure;

public sealed class InMemoryApprovalStore : IApprovalStore
{
    private readonly ConcurrentDictionary<string, ApprovalRecord> _records = new();

    public Task<ApprovalRecord> CreateAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        var approvalId = $"approval-{Guid.NewGuid():N}";
        var record = new ApprovalRecord
        {
            ApprovalId = approvalId,
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('='),
            RunId = request.RunId,
            ToolName = request.ToolName,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        };

        _records[approvalId] = record;
        return Task.FromResult(record);
    }

    public Task<ApprovalRecord?> GetAsync(string approvalId, CancellationToken cancellationToken)
    {
        _records.TryGetValue(approvalId, out var record);
        return Task.FromResult(record);
    }

    public Task<bool> ValidateAsync(string toolName, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(false);
        }

        var valid = _records.Values.Any(record =>
            record.ToolName.Equals(toolName, StringComparison.OrdinalIgnoreCase) &&
            record.Token == token &&
            record.ExpiresAt > DateTimeOffset.UtcNow);

        return Task.FromResult(valid);
    }
}
