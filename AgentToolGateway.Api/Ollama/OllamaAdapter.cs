using System.Net.Http.Json;
using System.Text.Json;
using AgentToolGateway.Api.Contracts;
using Microsoft.Extensions.Options;

namespace AgentToolGateway.Api.Ollama;

public sealed class OllamaOptions
{
    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "qwen2.5-coder";
}

public sealed class OllamaAdapter(HttpClient httpClient, IOptions<OllamaOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentToolIntent> GetToolIntentAsync(
        string prompt,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        const string systemPrompt = """
You are a tool router. Return only JSON with this shape:
{"toolName":"SearchCode","input":{"query":"example"}}
Choose exactly one available tool. Do not explain.
""";

        var toolCatalog = JsonSerializer.Serialize(tools, JsonOptions);
        var requestPrompt = $"""
{systemPrompt}
Available tools:
{toolCatalog}
User request:
{prompt}
""";

        var response = await httpClient.PostAsJsonAsync(
            "/api/generate",
            new
            {
                model = options.Value.Model,
                stream = false,
                format = "json",
                prompt = requestPrompt
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
            JsonOptions,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(ollamaResponse?.Response))
        {
            throw new InvalidOperationException("Ollama returned an empty response.");
        }

        return JsonSerializer.Deserialize<AgentToolIntent>(ollamaResponse.Response, JsonOptions)
            ?? throw new InvalidOperationException("Ollama response did not match the tool intent schema.");
    }
}

public sealed class OllamaGenerateResponse
{
    public string? Response { get; init; }
}
