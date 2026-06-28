public sealed record AppSettings(string OllamaUrl, string Model)
{
    public static AppSettings FromEnvironment() => new(
        Environment.GetEnvironmentVariable("OLLAMA_URL")?.TrimEnd('/') ?? "http://localhost:11434",
        Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3.6:latest");
}
