using System.ComponentModel;
using System.Text.Json.Serialization;
using ResumeAssistant.Api.Services;

namespace ResumeAssistant.Api.Agent;

public static class DigitalTwinTools
{
    [Description("Displays an interactive meeting scheduler card allowing the recruiter to browse all available meeting formats (15m intro, 30m screening, 60m system design) or open the Cal.com scheduling portal directly.")]
    public static ScheduleMeetingCardResponse ShowScheduleInterviewCard(
        [Description("The preferred interview topic or duration: e.g. '15 min Quick Catch-up', 'AI Architecture Discussion (30 min)', 'Principal Engineer Screening', or 'System Design (60 min)'")]
        string interviewType = "AI Architecture Discussion")
    {
        return new ScheduleMeetingCardResponse
        {
            CardType = "ScheduleInterviewCard",
            BookingUrl = "https://cal.com/ankitsarkar",
            AvailableDurations = ["15 min intro", "30 min screening", "60 min system design"],
            Headline = "Schedule a Call / Technical Discussion with Ankit Sarkar",
            RecommendedTopic = interviewType
        };
    }

    [Description("Provides a direct download card for Ankit Sarkar's official 2-page PDF resume, LinkedIn profile, GitHub repositories, and portfolio.")]
    public static DownloadResumeCardResponse ShowDownloadResumeCard()
    {
        return new DownloadResumeCardResponse
        {
            CardType = "DownloadResumeCard",
            PdfUrl = "/resume.pdf",
            FileName = "Ankit_Sarkar_AI_Solutions_Architect_Resume.pdf",
            GitHubUrl = "https://github.com/anktsrkr",
            LinkedInUrl = "https://linkedin.com/in/sarkaran",
            BlogUrl = "https://anktsrkr.github.io"
        };
    }
}

public sealed class DigitalTwinCalendarTools(ICalComService calComService)
{
    [Description("Fetches real-time available interview and meeting slots directly from Ankit Sarkar's Cal.com calendar. Call ONLY when the user asks about Ankit's open availability, when he is free, or wants to check open dates and times. Do NOT call this tool for questions about who booked slots, existing appointments, or attendee names (attendee data is confidential).")]
    public async Task<CalAvailabilityResponse> GetAvailableInterviewSlots(
        [Description("Desired meeting duration in minutes (10 for quick catch-up, 15 for intro, 30 for screening, 45 for deep dive, 60 for system design). Defaults to 30.")]
        int durationInMinutes = 30,
        [Description("Optional start date to search from. Defaults to tomorrow if omitted.")]
        DateTime? startDate = null,
        [Description("Optional end date to search up to. Defaults to 7 days from start date if omitted.")]
        DateTime? endDate = null,
        [Description("Target time zone string (e.g. 'Europe/London', 'America/New_York', 'UTC'). Defaults to 'Europe/London'.")]
        string? timeZone = null)
    {
        return await calComService.GetAvailableSlotsAsync(startDate, endDate, timeZone, durationInMinutes);
    }

    [Description("Directly books a confirmed interview with Ankit Sarkar at the requested duration via Cal.com and dispatches a Google Meet calendar invite once attendee details are provided.")]
    public async Task<CalBookingResponse> BookInterviewSlot(
        [Description("Full name of the recruiter, hiring manager, or attendee.")]
        string recruiterName,
        [Description("Work email address of the attendee where the Google Meet calendar invite will be sent.")]
        string recruiterEmail,
        [Description("Chosen slot start time in UTC (e.g. '2026-09-04T08:00:00Z').")]
        DateTime slotStartTimeUtc,
        [Description("Optional meeting duration in minutes if applicable.")]
        int? durationInMinutes = null,
        [Description("Attendee time zone (e.g. 'Europe/London', 'America/New_York'). Defaults to 'Europe/London'.")]
        string? timeZone = null,
        [Description("Brief notes or interview context (e.g. '10-min catch-up on Principal AI role at XYZ Corp').")]
        string? notes = null)
    {
        return await calComService.CreateBookingAsync(
            recruiterName,
            recruiterEmail,
            slotStartTimeUtc,
            timeZone,
            durationInMinutes,
            notes);
    }
}

public sealed class DigitalTwinKnowledgeTools(MongoDbRagSearcher ragSearcher)
{
    [Description("Searches Ankit Sarkar's verified resume, architecture case studies, technical achievements, and work history. Call this whenever answering specific questions about Ankit's past roles, technical architectures, certifications, or project specifics. Do NOT call for availability, scheduling, or booking requests.")]
    public async Task<KnowledgeSearchResponse> SearchResumeKnowledgeBase(
        [Description("Search query or keywords relating to Ankit's experience, technologies, or projects (e.g. 'ASDA eCommerce picking resilience', 'SpiceDB ReBAC architecture', 'Azure certifications').")]
        string query,
        CancellationToken cancellationToken = default)
    {
        var results = await ragSearcher.SearchAsync(query, cancellationToken);
        var citations = results.Take(5).Select(r => new KnowledgeCitationItem
        {
            Title = r.Record?.Title ?? "Resume Entry",
            Category = r.Record?.Category ?? "Experience",
            Company = r.Record?.Company,
            Role = r.Record?.Role,
            Period = (!string.IsNullOrEmpty(r.Record?.StartDate) || !string.IsNullOrEmpty(r.Record?.EndDate))
                ? $"{r.Record?.StartDate} - {r.Record?.EndDate ?? "Present"}"
                : null,
            SourceName = r.Record?.SourceName ?? "Resume",
            SourceLink = r.Record?.SourceLink,
            Technologies = r.Record?.Technologies ?? [],
            Content = r.Record?.Content ?? r.Text,
            Similarity = r.Record?.Score ?? r.Record?.Similarity
        }).ToList();

        var sb = new System.Text.StringBuilder();
        if (citations.Count > 0)
        {
            foreach (var c in citations)
            {
                sb.AppendLine($"[Source: {c.SourceName} | Link: {c.SourceLink ?? "#"}]");
                sb.AppendLine(c.Content);
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("No verified case study chunks passed the 0.65 relevance threshold for this query. Answer from your foundational architectural principles as Ankit Sarkar without generating fictitious case citations.");
        }

        return new KnowledgeSearchResponse
        {
            Query = query,
            TotalResults = citations.Count,
            Citations = citations,
            FormattedContext = sb.ToString()
        };
    }
}

public sealed class KnowledgeCitationItem
{
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = "Experience";

    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("period")]
    public string? Period { get; set; }

    [JsonPropertyName("source_name")]
    public required string SourceName { get; set; }

    [JsonPropertyName("source_link")]
    public string? SourceLink { get; set; }

    [JsonPropertyName("technologies")]
    public string[] Technologies { get; set; } = [];

    [JsonPropertyName("content")]
    public required string Content { get; set; }

    [JsonPropertyName("similarity")]
    public double? Similarity { get; set; }
}

public sealed class KnowledgeSearchResponse
{
    [JsonPropertyName("query")]
    public required string Query { get; set; }

    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }

    [JsonPropertyName("citations")]
    public List<KnowledgeCitationItem> Citations { get; set; } = [];

    [JsonPropertyName("formatted_context")]
    public string FormattedContext { get; set; } = string.Empty;
}

public sealed class ScheduleMeetingCardResponse
{
    [JsonPropertyName("card_type")]
    public required string CardType { get; set; }

    [JsonPropertyName("booking_url")]
    public required string BookingUrl { get; set; }

    [JsonPropertyName("available_durations")]
    public string[] AvailableDurations { get; set; } = [];

    [JsonPropertyName("headline")]
    public required string Headline { get; set; }

    [JsonPropertyName("recommended_topic")]
    public string? RecommendedTopic { get; set; }
}

public sealed class DownloadResumeCardResponse
{
    [JsonPropertyName("card_type")]
    public required string CardType { get; set; }

    [JsonPropertyName("pdf_url")]
    public required string PdfUrl { get; set; }

    [JsonPropertyName("file_name")]
    public required string FileName { get; set; }

    [JsonPropertyName("github_url")]
    public string? GitHubUrl { get; set; }

    [JsonPropertyName("linkedin_url")]
    public string? LinkedInUrl { get; set; }

    [JsonPropertyName("blog_url")]
    public string? BlogUrl { get; set; }
}

public sealed class FollowUpMessageItem
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class FollowUpRequest
{
    [JsonPropertyName("messages")]
    public List<FollowUpMessageItem> Messages { get; set; } = [];

    [JsonPropertyName("turn_count")]
    public int? TurnCount { get; set; }

    [JsonPropertyName("max_daily_limit")]
    public int? MaxDailyLimit { get; set; }
}

public sealed class FollowUpPillItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("label")]
    public required string Label { get; set; }

    [JsonPropertyName("action_type")]
    public required string ActionType { get; set; } // "download_resume", "book_call", "ask_question"

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "sparkles";

    [JsonPropertyName("prompt")]
    public required string Prompt { get; set; }
}

public sealed class FollowUpResponse
{
    [JsonPropertyName("pills")]
    public List<FollowUpPillItem> Pills { get; set; } = [];
}

[JsonSerializable(typeof(KnowledgeSearchResponse))]
[JsonSerializable(typeof(KnowledgeCitationItem))]
[JsonSerializable(typeof(List<KnowledgeCitationItem>))]
[JsonSerializable(typeof(ScheduleMeetingCardResponse))]
[JsonSerializable(typeof(DownloadResumeCardResponse))]
[JsonSerializable(typeof(CalAvailabilityResponse))]
[JsonSerializable(typeof(CalBookingResponse))]
[JsonSerializable(typeof(CalSlotDetails))]
[JsonSerializable(typeof(List<CalSlotDetails>))]
[JsonSerializable(typeof(CalEventTypeItem))]
[JsonSerializable(typeof(List<CalEventTypeItem>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(FollowUpRequest))]
[JsonSerializable(typeof(FollowUpResponse))]
[JsonSerializable(typeof(FollowUpPillItem))]
[JsonSerializable(typeof(List<FollowUpPillItem>))]
[JsonSerializable(typeof(FollowUpMessageItem))]
[JsonSerializable(typeof(List<FollowUpMessageItem>))]
public sealed partial class DigitalTwinJsonSerializerContext : JsonSerializerContext;
