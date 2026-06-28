namespace AgentToolGateway.Api.Infrastructure;

public sealed class ToolGatewayOptions
{
    public string WorkspaceRoot { get; init; } = "..";
    public string AuditLogPath { get; init; } = "audit/tool-calls.jsonl";
    public string[] AllowedTestCommands { get; init; } = ["dotnet test"];
    public string[] AllowedRunCommands { get; init; } = ["dotnet --info", "dotnet test"];
}
