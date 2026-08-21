namespace ResumeAssistant.Api.Configuration;

public sealed class CalComOptions
{
    public const string SectionName = "CalCom";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.cal.com/v2";

    public string ApiVersion { get; set; } = "2024-08-13";

    public int EventTypeId { get; set; } = 6740666;

    public int EventTypeId15Min { get; set; } = 6740664;

    public int EventTypeId30Min { get; set; } = 6740666;

    public int EventTypeId60Min { get; set; } = 6752977;

    public string Username { get; set; } = "ankitsarkar";

    public string DefaultTimeZone { get; set; } = "Europe/London";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public int GetEventTypeId(int? durationInMinutes)
    {
        return durationInMinutes switch
        {
            <= 15 => EventTypeId15Min > 0 ? EventTypeId15Min : 6740664,
            > 15 and <= 45 => EventTypeId30Min > 0 ? EventTypeId30Min : 6740666,
            > 45 => EventTypeId60Min > 0 ? EventTypeId60Min : 6752977,
            _ => EventTypeId > 0 ? EventTypeId : 6740666
        };
    }

    public string GetEventSlug(int? durationInMinutes)
    {
        return durationInMinutes switch
        {
            <= 15 => "15min",
            > 15 and <= 45 => "30min",
            > 45 => "60min",
            _ => "30min"
        };
    }

    public string GetBookingUrl(int? durationInMinutes)
    {
        var slug = GetEventSlug(durationInMinutes);
        return $"https://cal.com/{Username}/{slug}";
    }
}

