using System.Net.Http.Json;
using System.Text.Json.Serialization;

public sealed class OllamaClient(AppSettings settings)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };

    public async Task<string> GenerateAsync(string prompt)
    {
        var response = await Http.PostAsJsonAsync($"{settings.OllamaUrl}/api/generate", new
        {
            model = settings.Model,
            prompt,
            stream = false,
            think = false,
            format = "json",
            options = new { temperature = 0 }
        });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OllamaResponse>();
        return body?.Response ?? body?.Thinking ?? throw new InvalidOperationException("Ollama boş yanıt verdi.");
    }

    private sealed record OllamaResponse(
        [property: JsonPropertyName("response")] string? Response,
        [property: JsonPropertyName("thinking")] string? Thinking);
}
