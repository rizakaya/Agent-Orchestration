public sealed record AssistantIntent(
    string Intent,
    string Topic,
    int? DurationMinutes,
    string? Date,
    bool NeedsResources,
    bool NeedsCalendar,
    bool RequiresHumanApproval,
    string[] MissingFields);
