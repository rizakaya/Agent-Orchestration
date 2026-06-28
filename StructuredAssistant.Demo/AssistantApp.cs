using System.Text.Json;

public sealed class AssistantApp(AppSettings settings, IntentParser parser, IntentRouter router)
{
    public async Task RunAsync()
    {
        Console.WriteLine("StructuredAssistant.Demo");
        Console.WriteLine("Structured output + tool calling + basit handoff");
        Console.WriteLine($"Kullanılan model: {settings.Model}");

        while (true)
        {
            Console.Write("İstek yazın (çıkmak için q): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("q", StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                var intent = await parser.ParseAsync(input);

                Console.WriteLine("Structured output:");
                Console.WriteLine(JsonSerializer.Serialize(intent, new JsonSerializerOptions { WriteIndented = true }));

                var result = router.Route(intent);

                Console.WriteLine("İşlem özeti:");
                foreach (var line in result) Console.WriteLine($"- {line}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
            }
        }
    }
}
