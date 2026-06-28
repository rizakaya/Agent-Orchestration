using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;
using AgentToolGateway.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace AgentToolGateway.Api.Tools;

public sealed class RunTestsTool(
    ProcessRunner processRunner,
    IOptions<ToolGatewayOptions> options)
    : AgentTool<RunTestsInput, RunTestsResult>
{
    public override ToolDefinition Definition { get; } = new()
    {
        Name = "RunTests",
        Description = "Runs a whitelisted test command with a mandatory timeout.",
        RiskLevel = ToolRiskLevel.Medium,
        AccessType = ToolAccessType.Execute,
        RequiresApproval = false,
        TimeoutSeconds = 60
    };

    public override async Task<RunTestsResult> ExecuteTypedAsync(RunTestsInput input, CancellationToken cancellationToken)
    {
        var command = string.IsNullOrWhiteSpace(input.Command) ? "dotnet test" : input.Command.Trim();
        if (!options.Value.AllowedTestCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Test command '{command}' is not whitelisted.");
        }

        var timeout = input.TimeoutSeconds ?? Definition.TimeoutSeconds;
        if (timeout <= 0)
        {
            throw new InvalidOperationException("RunTests requires a positive timeout.");
        }

        var result = await processRunner.RunAsync(command, timeout, cancellationToken);
        return new RunTestsResult
        {
            Command = command,
            ExitCode = result.ExitCode,
            StandardOutput = result.StandardOutput,
            StandardError = result.StandardError,
            TimedOut = result.TimedOut
        };
    }
}

public sealed class RunTestsInput
{
    public string? Command { get; init; }
    public int? TimeoutSeconds { get; init; }
}

public sealed class RunTestsResult
{
    public required string Command { get; init; }
    public int? ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
    public required bool TimedOut { get; init; }
}
