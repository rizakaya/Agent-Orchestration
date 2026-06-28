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
