namespace ResumeAssistant.Api.Configuration;

public sealed class CalComOptions
{
    public const string SectionName = "CalCom";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.cal.com/v2";

    public string ApiVersion { get; set; } = "2024-08-13";

    public int EventTypeId { get; set; } = 6703935;

    public string Username { get; set; } = "anktsrkr";

    public string DefaultTimeZone { get; set; } = "Europe/London";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
