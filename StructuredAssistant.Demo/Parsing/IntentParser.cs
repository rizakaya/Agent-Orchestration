using System.Text.Json;

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
        var json = ExtractJson(raw);

        var intent = JsonSerializer.Deserialize<AssistantIntent>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Model boş JSON döndürdü.");

        return intent with { MissingFields = intent.MissingFields ?? [] };
    }

    private static string ExtractJson(string raw)
    {
        var jsonStart = raw.IndexOf('{');
        var jsonEnd = raw.LastIndexOf('}');

        if (jsonStart < 0 || jsonEnd < jsonStart)
            throw new InvalidOperationException($"Model geçerli JSON döndürmedi: {raw}");

        return raw[jsonStart..(jsonEnd + 1)];
    }
}
