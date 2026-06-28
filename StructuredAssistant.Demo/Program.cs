using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var settings = AppSettings.FromEnvironment();
var app = new AssistantApp(new IntentParser(new OllamaClient(settings)), new IntentRouter(new DemoTools()));
await app.RunAsync();

public sealed record AppSettings(string OllamaUrl, string Model)
{
    public static AppSettings FromEnvironment() => new(
        Environment.GetEnvironmentVariable("OLLAMA_URL")?.TrimEnd('/') ?? "http://localhost:11434",
        Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen2.5:7b");
}

public sealed record AssistantIntent(
    string Intent,
    string Topic,
    int? DurationMinutes,
    string? Date,
    bool NeedsResources,
    bool NeedsCalendar,
    bool RequiresHumanApproval,
    string[] MissingFields);

public sealed class AssistantApp(IntentParser parser, IntentRouter router)
{
    public async Task RunAsync()
    {
        Console.WriteLine("StructuredAssistant.Demo");
        Console.WriteLine("Structured output + tool calling + basit handoff");

        while (true)
        {
            Console.Write("\nİstek yazın (çıkmak için q): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("q", StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                var intent = await parser.ParseAsync(input);

                Console.WriteLine("\nStructured output:");
                Console.WriteLine(JsonSerializer.Serialize(intent, new JsonSerializerOptions { WriteIndented = true }));

                var result = router.Route(intent);

                Console.WriteLine("\nİşlem özeti:");
                foreach (var line in result) Console.WriteLine($"- {line}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nHata: {ex.Message}");
            }
        }
    }
}

public sealed class IntentParser(OllamaClient client)
{
    public async Task<AssistantIntent> ParseAsync(string input)
    {
        var prompt = $$"""
        Sadece geçerli JSON döndür. Markdown veya açıklama yazma.

        Şu tipe uygun JSON üret:
        {
          "Intent": "create_study_plan | unknown",
          "Topic": "string",
          "DurationMinutes": 30,
          "Date": "YYYY-MM-DD veya null",
          "NeedsResources": true,
          "NeedsCalendar": false,
          "RequiresHumanApproval": false,
          "MissingFields": []
        }

        Kurallar:
        - Ders/öğrenme/çalışma isteklerinde Intent "create_study_plan" olsun.
        - Konu yoksa Topic "genel çalışma", MissingFields içine "topic" ekle.
        - Süre yoksa DurationMinutes null, MissingFields içine "durationMinutes" ekle.
        - Kaynak/link/video/doküman istenirse NeedsResources true.
        - Takvim/hatırlatma istenirse NeedsCalendar true.
        - "önce bana sor" veya "onay al" geçerse RequiresHumanApproval true.

        Bugünün tarihi: {{DateTime.Today:yyyy-MM-dd}}
        Kullanıcı isteği: {{input}}
        """;

        var raw = await client.GenerateAsync(prompt);
        var jsonStart = raw.IndexOf('{');
        var jsonEnd = raw.LastIndexOf('}');

        if (jsonStart < 0 || jsonEnd < jsonStart)
            throw new InvalidOperationException($"Model geçerli JSON döndürmedi: {raw}");

        var json = raw[jsonStart..(jsonEnd + 1)];

        return JsonSerializer.Deserialize<AssistantIntent>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Model boş JSON döndürdü.");
    }
}

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
            format = "json",
            options = new { temperature = 0 }
        });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OllamaResponse>();
        return body?.Response ?? throw new InvalidOperationException("Ollama boş yanıt verdi.");
    }

    private sealed record OllamaResponse([property: JsonPropertyName("response")] string Response);
}

public sealed class IntentRouter(DemoTools tools)
{
    public List<string> Route(AssistantIntent intent)
    {
        var summary = new List<string>();

        if (intent.MissingFields.Length > 0)
        {
            var ok = tools.AskHumanApproval($"Eksik alanlar var ({string.Join(", ", intent.MissingFields)}). Devam edilsin mi?");
            if (!ok) return ["Handoff tamamlandı: kullanıcı işlemi durdurdu."];
            summary.Add("Handoff tamamlandı: varsayılan değerlerle devam edildi.");
        }

        if (intent.Intent != "create_study_plan")
            return [$"Desteklenmeyen intent: {intent.Intent}"];

        summary.Add(tools.CreateStudyPlan(intent));

        if (intent.NeedsResources)
            summary.Add(tools.SearchFreeResources(intent.Topic));

        if (intent.NeedsCalendar)
        {
            summary.Add(tools.CreateCalendarDraft(intent));

            if (intent.RequiresHumanApproval &&
                !tools.AskHumanApproval("Bu işlem için onay gerekiyor: takvim taslağı kaydedilsin mi?"))
            {
                summary.Add("Handoff tamamlandı: kullanıcı takvim taslağını reddetti.");
                return summary;
            }
        }

        summary.Add(tools.SaveTask(intent));
        return summary;
    }
}

public sealed class DemoTools
{
    public string CreateStudyPlan(AssistantIntent intent)
    {
        var duration = intent.DurationMinutes ?? 30;
        var date = intent.Date ?? "tarih belirtilmedi";
        return $"CreateStudyPlan çalıştı: {date} için {duration} dakikalık '{intent.Topic}' planı hazırlandı.";
    }

    public string SearchFreeResources(string topic) =>
        $"SearchFreeResources çalıştı: {topic} için Microsoft Learn, freeCodeCamp ve resmi dokümantasyon önerildi.";

    public string CreateCalendarDraft(AssistantIntent intent) =>
        $"CreateCalendarDraft çalıştı: '{intent.Topic}' için takvim taslağı oluşturuldu.";

    public string SaveTask(AssistantIntent intent) =>
        $"SaveTask çalıştı: '{intent.Topic}' görevi demo belleğine kaydedildi.";

    public bool AskHumanApproval(string question)
    {
        Console.Write($"{question} (evet/hayır): ");
        var answer = Console.ReadLine();
        return answer?.Equals("evet", StringComparison.OrdinalIgnoreCase) == true
            || answer?.Equals("e", StringComparison.OrdinalIgnoreCase) == true;
    }
}