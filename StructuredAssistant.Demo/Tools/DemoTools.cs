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
