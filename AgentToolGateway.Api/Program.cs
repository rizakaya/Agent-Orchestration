using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;
using AgentToolGateway.Api.Infrastructure;
using AgentToolGateway.Api.Ollama;
using AgentToolGateway.Api.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ToolGatewayOptions>(builder.Configuration.GetSection("ToolGateway"));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));

builder.Services.AddSingleton<WorkspacePathValidator>();
builder.Services.AddSingleton<ProcessRunner>();
builder.Services.AddSingleton<IApprovalStore, InMemoryApprovalStore>();
builder.Services.AddSingleton<IToolAuditStore, JsonlToolAuditStore>();
builder.Services.AddSingleton<IToolPolicyEvaluator, ToolPolicyEvaluator>();
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
builder.Services.AddSingleton<IToolExecutor, ToolExecutor>();

builder.Services.AddSingleton<IAgentTool, SearchCodeTool>();
builder.Services.AddSingleton<IAgentTool, ReadFileTool>();
builder.Services.AddSingleton<IAgentTool, GetGitDiffTool>();
builder.Services.AddSingleton<IAgentTool, RunTestsTool>();
builder.Services.AddSingleton<IAgentTool, CreatePatchDraftTool>();
builder.Services.AddSingleton<IAgentTool, ApplyPatchTool>();
builder.Services.AddSingleton<IAgentTool, RunCommandTool>();

builder.Services.AddHttpClient<OllamaAdapter>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

var app = builder.Build();

app.MapGet("/api/tools", (IToolRegistry registry) => Results.Ok(registry.GetAvailableTools()));

app.MapPost("/api/tool-calls", async (
    ToolCallRequest request,
    IToolExecutor executor,
    CancellationToken cancellationToken) =>
{
    var result = await executor.ExecuteAsync(
        request.ToolName,
        request.Input,
        new AgentRunContext
        {
            RunId = request.RunId,
            ApprovalToken = request.ApprovalToken
        },
        cancellationToken);

    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/api/approvals", async (
    ApprovalRequest request,
    IApprovalStore approvalStore,
    CancellationToken cancellationToken) =>
{
    var approval = await approvalStore.CreateAsync(request, cancellationToken);
    return Results.Created($"/api/approvals/{approval.ApprovalId}", approval);
});

app.MapGet("/api/approvals/{approvalId}", async (
    string approvalId,
    IApprovalStore approvalStore,
    CancellationToken cancellationToken) =>
{
    var approval = await approvalStore.GetAsync(approvalId, cancellationToken);
    return approval is null ? Results.NotFound() : Results.Ok(approval);
});

app.MapPost("/api/agent-runs", async (
    AgentPromptRequest request,
    OllamaAdapter ollama,
    IToolRegistry registry,
    IToolExecutor executor,
    CancellationToken cancellationToken) =>
{
    var intent = await ollama.GetToolIntentAsync(request.Prompt, registry.GetAvailableTools(), cancellationToken);
    var result = await executor.ExecuteAsync(
        intent.ToolName,
        intent.Input,
        new AgentRunContext { RunId = request.RunId },
        cancellationToken);

    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

app.Run();
