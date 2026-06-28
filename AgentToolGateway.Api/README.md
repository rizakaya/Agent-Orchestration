# Agent Tool Gateway v1

A separate ASP.NET Core Minimal API for governed agent tool execution. It adds typed tool contracts, risk metadata, approval checks, timeouts, JSONL audit logging, and a minimal Ollama/Qwen tool-intent adapter.

## Run

```powershell
dotnet run --project .\AgentToolGateway.Api\AgentToolGateway.Api.csproj
```

## Endpoints

- `GET /api/tools` returns registered tool metadata.
- `POST /api/tool-calls` executes one governed tool call.
- `POST /api/approvals` creates an approval token for high-risk tools.
- `GET /api/approvals/{approvalId}` returns approval details.
- `POST /api/agent-runs` asks Ollama/Qwen for one structured tool intent, then sends it through the gateway pipeline.

## Audit

Audit records are written as JSONL to `AgentToolGateway.Api/audit/tool-calls.jsonl`. The audit folder ignores generated logs by default.

## Example Tool Call

```json
{
  "runId": "agent-run-001",
  "toolName": "SearchCode",
  "input": {
    "query": "ToolDefinition",
    "maxResults": 20
  }
}
```

## Approval Flow

Calling `ApplyPatch` or `RunCommand` without `approvalToken` returns an `ApprovalRequired` error and an `approvalId`. Create or fetch an approval, then retry with the returned token.
