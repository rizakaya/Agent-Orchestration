using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;
using AgentToolGateway.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace AgentToolGateway.Api.Tools;

public sealed class RunCommandTool(
    ProcessRunner processRunner,
    IOptions<ToolGatewayOptions> options)
    : AgentTool<RunCommandInput, RunCommandResult>
{
    public override ToolDefinition Definition { get; } = new()
    {
        Name = "RunCommand",
        Description = "Runs an approval-required whitelisted command.",
        RiskLevel = ToolRiskLevel.High,
        AccessType = ToolAccessType.Execute,
        RequiresApproval = true,
        TimeoutSeconds = 30
    };

    public override async Task<RunCommandResult> ExecuteTypedAsync(RunCommandInput input, CancellationToken cancellationToken)
    {
        var command = input.Command.Trim();
        if (!options.Value.AllowedRunCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Command '{command}' is not whitelisted.");
        }

        var timeout = input.TimeoutSeconds ?? Definition.TimeoutSeconds;
        if (timeout <= 0)
        {
            throw new InvalidOperationException("RunCommand requires a positive timeout.");
        }

        var result = await processRunner.RunAsync(command, timeout, cancellationToken);
        return new RunCommandResult
        {
            Command = command,
            ExitCode = result.ExitCode,
            StandardOutput = result.StandardOutput,
            StandardError = result.StandardError,
            TimedOut = result.TimedOut
        };
    }
}

public sealed class RunCommandInput
{
    public required string Command { get; init; }
    public int? TimeoutSeconds { get; init; }
}

public sealed class RunCommandResult
{
    public required string Command { get; init; }
    public int? ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
    public required bool TimedOut { get; init; }
}
