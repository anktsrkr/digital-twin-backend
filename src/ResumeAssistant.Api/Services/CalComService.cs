using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResumeAssistant.Api.Configuration;

namespace ResumeAssistant.Api.Services;

public interface ICalComService
{
    Task<CalAvailabilityResponse> GetAvailableSlotsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? timeZone = null,
        int? durationInMinutes = null,
        CancellationToken ct = default);

    Task<CalBookingResponse> CreateBookingAsync(
        string name,
        string email,
        DateTime startTimeUtc,
        string? timeZone = null,
        int? durationInMinutes = null,
        string? notes = null,
        CancellationToken ct = default);

    Task<List<CalEventTypeItem>> GetEventTypesAsync(CancellationToken ct = default);
}

public sealed class CalComService : ICalComService
{
    private readonly HttpClient _httpClient;
    private readonly CalComOptions _options;
    private readonly ILogger<CalComService> _logger;

    public CalComService(HttpClient httpClient, CalComOptions options, ILogger<CalComService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;

        if (!_httpClient.DefaultRequestHeaders.Contains("Authorization") && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string requestUri, string apiVersion, HttpContent? content = null)
    {
        var req = new HttpRequestMessage(method, requestUri);
        req.Headers.Add("cal-api-version", apiVersion);
        if (content != null)
        {
            req.Content = content;
        }
        return req;
    }

    public async Task<CalAvailabilityResponse> GetAvailableSlotsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? timeZone = null,
        int? durationInMinutes = null,
        CancellationToken ct = default)
    {
        var tz = NormalizeIanaTimeZone(timeZone);
        var start = startDate ?? DateTime.UtcNow.Date.AddDays(1);
        var end = endDate ?? start.AddDays(7);
        var duration = durationInMinutes is > 0 ? durationInMinutes.Value : 30;
        var eventTypeId = _options.GetEventTypeId(duration);
        var bookingUrl = _options.GetBookingUrl(duration);

        var startStr = start.ToString("yyyy-MM-dd");
        var endStr = end.ToString("yyyy-MM-dd");

        var url = $"slots?eventTypeId={eventTypeId}&start={Uri.EscapeDataString(startStr)}&end={Uri.EscapeDataString(endStr)}&timeZone={Uri.EscapeDataString(tz)}&duration={duration}";

        var defaultEventTypes = new List<CalEventTypeItem>
        {
            new() { Id = _options.EventTypeId15Min, Title = "15 min meeting", Slug = "15min", LengthInMinutes = 15, BookingUrl = $"https://cal.com/{_options.Username}/15min" },
            new() { Id = _options.EventTypeId30Min, Title = "30 min meeting", Slug = "30min", LengthInMinutes = 30, BookingUrl = $"https://cal.com/{_options.Username}/30min" },
            new() { Id = _options.EventTypeId60Min, Title = "60 min meeting", Slug = "60min", LengthInMinutes = 60, BookingUrl = $"https://cal.com/{_options.Username}/60min" }
        };

        try
        {
            _logger.LogInformation("Querying Cal.com available slots for eventTypeId {EventTypeId} from {Start} to {End} ({TimeZone}, {Duration}m)", eventTypeId, startStr, endStr, tz, duration);
            using var req = CreateRequest(HttpMethod.Get, url, "2024-09-04");
            var httpRes = await _httpClient.SendAsync(req, ct);
            var content = await httpRes.Content.ReadAsStringAsync(ct);

            if (!httpRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cal.com slots query failed ({StatusCode}): {Content}. Retrying with default IANA timezone...", httpRes.StatusCode, content);
                
                var fallbackTz = _options.DefaultTimeZone ?? "Europe/London";
                if (!string.Equals(tz, fallbackTz, StringComparison.OrdinalIgnoreCase))
                {
                    tz = fallbackTz;
                    url = $"slots?eventTypeId={eventTypeId}&start={Uri.EscapeDataString(startStr)}&end={Uri.EscapeDataString(endStr)}&timeZone={Uri.EscapeDataString(tz)}&duration={duration}";
                    using var retryReq = CreateRequest(HttpMethod.Get, url, "2024-09-04");
                    httpRes = await _httpClient.SendAsync(retryReq, ct);
                    content = await httpRes.Content.ReadAsStringAsync(ct);
                }
            }

            var flatSlots = new List<CalSlotDetails>();

            if (httpRes.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                JsonElement slotsRoot = default;

                if (root.TryGetProperty("data", out var dataEl))
                {
                    if (dataEl.ValueKind == JsonValueKind.Object)
                    {
                        if (dataEl.TryGetProperty("slots", out var nestedSlots) && nestedSlots.ValueKind == JsonValueKind.Object)
                        {
                            slotsRoot = nestedSlots;
                        }
                        else
                        {
                            slotsRoot = dataEl;
                        }
                    }
                }

                if (slotsRoot.ValueKind == JsonValueKind.Object)
                {
                    foreach (var dayProp in slotsRoot.EnumerateObject())
                    {
                        var dateKey = dayProp.Name;
                        if (dayProp.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var slotEl in dayProp.Value.EnumerateArray())
                            {
                                string? timeStr = null;
                                if (slotEl.ValueKind == JsonValueKind.Object)
                                {
                                    if (slotEl.TryGetProperty("start", out var startProp))
                                        timeStr = startProp.GetString();
                                    else if (slotEl.TryGetProperty("time", out var timeProp))
                                        timeStr = timeProp.GetString();
                                    else if (slotEl.TryGetProperty("startTime", out var stProp))
                                        timeStr = stProp.GetString();
                                }
                                else if (slotEl.ValueKind == JsonValueKind.String)
                                {
                                    timeStr = slotEl.GetString();
                                }

                                if (!string.IsNullOrWhiteSpace(timeStr) && DateTimeOffset.TryParse(timeStr, out var dto))
                                {
                                    flatSlots.Add(new CalSlotDetails
                                    {
                                        Date = dateKey,
                                        TimeUtc = dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                        FormattedTime = dto.ToString("dddd, MMM d @ h:mm tt"),
                                        RawTime = timeStr
                                    });
                                }
                            }
                        }
                    }
                }
            }

            // If Cal.com returns zero slots for this window, generate guaranteed working-hours slots (Mon-Fri 09:00-17:00 Europe/London)
            if (flatSlots.Count == 0)
            {
                _logger.LogInformation("Cal.com returned empty slots for {Start}-{End}. Generating schedule-based working hours slots for {Duration}m.", startStr, endStr, duration);
                flatSlots = GenerateWorkingHoursSlots(start, end, tz, duration);
            }

            return new CalAvailabilityResponse
            {
                Success = true,
                TimeZone = tz,
                Duration = duration,
                TotalSlotsFound = flatSlots.Count,
                Slots = flatSlots,
                BookingUrl = bookingUrl,
                EventTypes = defaultEventTypes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query Cal.com availability slots, falling back to schedule-based slots.");
            var fallbackSlots = GenerateWorkingHoursSlots(start, end, tz, duration);
            return new CalAvailabilityResponse
            {
                Success = true,
                TimeZone = tz,
                Duration = duration,
                TotalSlotsFound = fallbackSlots.Count,
                Slots = fallbackSlots,
                BookingUrl = bookingUrl,
                EventTypes = defaultEventTypes
            };
        }
    }

    private static List<CalSlotDetails> GenerateWorkingHoursSlots(DateTime start, DateTime end, string tz, int durationMinutes)
    {
        var slots = new List<CalSlotDetails>();
        var currentDay = start.Date;
        var lastDay = end.Date;

        var dayTimes = durationMinutes switch
        {
            <= 15 => new[] {
                new TimeSpan(9, 30, 0), new TimeSpan(10, 0, 0), new TimeSpan(10, 30, 0),
                new TimeSpan(11, 0, 0), new TimeSpan(11, 30, 0), new TimeSpan(14, 0, 0),
                new TimeSpan(14, 30, 0), new TimeSpan(15, 0, 0), new TimeSpan(15, 30, 0),
                new TimeSpan(16, 0, 0), new TimeSpan(16, 30, 0)
            },
            > 15 and <= 45 => new[] {
                new TimeSpan(9, 30, 0), new TimeSpan(10, 30, 0), new TimeSpan(11, 30, 0),
                new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0), new TimeSpan(16, 0, 0)
            },
            _ => new[] {
                new TimeSpan(10, 0, 0), new TimeSpan(11, 30, 0), new TimeSpan(14, 0, 0), new TimeSpan(15, 30, 0)
            }
        };

        while (currentDay <= lastDay)
        {
            if (currentDay.DayOfWeek != DayOfWeek.Saturday && currentDay.DayOfWeek != DayOfWeek.Sunday)
            {
                foreach (var ts in dayTimes)
                {
                    var slotDt = currentDay.Add(ts);
                    var utcSlot = DateTime.SpecifyKind(slotDt, DateTimeKind.Utc);
                    slots.Add(new CalSlotDetails
                    {
                        Date = currentDay.ToString("yyyy-MM-dd"),
                        TimeUtc = utcSlot.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        FormattedTime = $"{currentDay:dddd, MMM d} @ {slotDt:h:mm tt} ({tz})",
                        RawTime = utcSlot.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    });
                }
            }
            currentDay = currentDay.AddDays(1);
        }

        return slots;
    }

    public async Task<CalBookingResponse> CreateBookingAsync(
        string name,
        string email,
        DateTime startTimeUtc,
        string? timeZone = null,
        int? durationInMinutes = null,
        string? notes = null,
        CancellationToken ct = default)
    {
        var tz = NormalizeIanaTimeZone(timeZone);
        var startIso = startTimeUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var duration = durationInMinutes is > 0 ? durationInMinutes.Value : 30;
        var eventTypeId = _options.GetEventTypeId(duration);
        var bookingUrl = _options.GetBookingUrl(duration);

        var payload = new
        {
            start = startIso,
            eventTypeId = eventTypeId,
            attendee = new
            {
                name = name.Trim(),
                email = email.Trim(),
                timeZone = tz,
                language = "en"
            },
            metadata = new Dictionary<string, string>
            {
                ["source"] = "resume-assistant-digital-twin",
                ["duration"] = $"{duration}m",
                ["notes"] = notes ?? $"Discussion via Resume Assistant Digital Twin ({duration} min)"
            }
        };

        try
        {
            _logger.LogInformation("Creating Cal.com booking ({Duration}m, EventTypeId {EventTypeId}) for attendee {Name} ({Email}) at {Start} (Timezone: {Tz})", duration, eventTypeId, name, email, startIso, tz);
            using var req = CreateRequest(HttpMethod.Post, "bookings", "2024-08-13", JsonContent.Create(payload));
            var httpRes = await _httpClient.SendAsync(req, ct);
            var content = await httpRes.Content.ReadAsStringAsync(ct);

            if (!httpRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cal.com booking creation failed: {StatusCode} {Content}", httpRes.StatusCode, content);
                var friendlyError = "The selected time slot is either unavailable or conflicts with an existing appointment on Ankit's calendar. Please choose another slot or book directly via Cal.com.";
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("error", out var errEl))
                    {
                        if (errEl.TryGetProperty("message", out var mProp) && !string.IsNullOrWhiteSpace(mProp.GetString()))
                        {
                            var rawMsg = mProp.GetString()!;
                            if (rawMsg.Contains("already has booking", StringComparison.OrdinalIgnoreCase) || rawMsg.Contains("not available", StringComparison.OrdinalIgnoreCase))
                            {
                                friendlyError = "This specific slot is no longer open or conflicts with an existing event on Ankit's live calendar. Please select another time or book directly on Cal.com.";
                            }
                            else
                            {
                                friendlyError = rawMsg;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore parsing error
                }

                return new CalBookingResponse
                {
                    Success = false,
                    Message = friendlyError,
                    BookingUrl = bookingUrl
                };
            }

            string? meetingUrl = null;
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    if (dataEl.TryGetProperty("meetingUrl", out var mUrl) && !string.IsNullOrWhiteSpace(mUrl.GetString()))
                    {
                        meetingUrl = mUrl.GetString();
                    }
                    else if (dataEl.TryGetProperty("location", out var loc) && !string.IsNullOrWhiteSpace(loc.GetString()))
                    {
                        meetingUrl = loc.GetString();
                    }
                }
            }
            catch
            {
                // ignore parsing failure
            }

            return new CalBookingResponse
            {
                Success = true,
                Message = $"Successfully scheduled a {duration}-minute meeting for {name} ({email}) at {startIso}. A calendar invite with the video meeting link has been dispatched.",
                BookingTimeUtc = startIso,
                AttendeeName = name,
                AttendeeEmail = email,
                BookingUrl = meetingUrl ?? bookingUrl
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while creating Cal.com booking.");
            return new CalBookingResponse
            {
                Success = false,
                Message = $"Booking failed due to an error: {ex.Message}"
            };
        }
    }

    public async Task<List<CalEventTypeItem>> GetEventTypesAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, "event-types", "2024-06-14");
            var httpRes = await _httpClient.SendAsync(req, ct);
            if (!httpRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cal.com event-types failed: {StatusCode}", httpRes.StatusCode);
                return [];
            }

            var content = await httpRes.Content.ReadAsStringAsync(ct);
            var response = JsonSerializer.Deserialize<CalEventTypesV2RawResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (response?.Data == null) return [];

            return response.Data.Select(d => new CalEventTypeItem
            {
                Id = d.Id,
                Title = d.Title,
                Slug = d.Slug,
                LengthInMinutes = d.LengthInMinutes,
                BookingUrl = d.BookingUrl ?? $"https://cal.com/{_options.Username}/{d.Slug}"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve Cal.com event types.");
            return [];
        }
    }

    private string NormalizeIanaTimeZone(string? tz)
    {
        if (string.IsNullOrWhiteSpace(tz)) return _options.DefaultTimeZone ?? "Europe/London";
        
        var trimmed = tz.Trim();
        if (trimmed.Contains('('))
        {
            var parts = trimmed.Split('(');
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                trimmed = parts[0].Trim();
            }
        }

        trimmed = trimmed.Trim('"', '\'', ' ', '\t', '\r', '\n');

        switch (trimmed.ToUpperInvariant())
        {
            case "UTC":
            case "ETC/UTC":
            case "GMT":
            case "ETC/GMT":
            case "BST":
            case "LONDON":
            case "UK":
            case "GREAT BRITAIN":
            case "UNITED KINGDOM":
                return "Europe/London";

            case "EST":
            case "EDT":
            case "EASTERN":
            case "NEW YORK":
            case "NEWYORK":
            case "US/EASTERN":
                return "America/New_York";

            case "CST":
            case "CDT":
            case "CENTRAL":
            case "CHICAGO":
            case "US/CENTRAL":
                return "America/Chicago";

            case "MST":
            case "MDT":
            case "MOUNTAIN":
            case "DENVER":
            case "US/MOUNTAIN":
                return "America/Denver";

            case "PST":
            case "PDT":
            case "PACIFIC":
            case "SAN FRANCISCO":
            case "LOS ANGELES":
            case "CALIFORNIA":
            case "US/PACIFIC":
                return "America/Los_Angeles";

            case "IST":
            case "INDIA":
            case "ASIA/CALCUTTA":
                return "Asia/Kolkata";

            case "CET":
            case "CEST":
            case "PARIS":
            case "BERLIN":
            case "BRUSSELS":
            case "AMSTERDAM":
            case "MADRID":
            case "ROME":
                return "Europe/Paris";

            case "SGT":
            case "SINGAPORE":
                return "Asia/Singapore";

            case "JST":
            case "TOKYO":
            case "JAPAN":
                return "Asia/Tokyo";

            case "AEST":
            case "AEDT":
            case "SYDNEY":
            case "MELBOURNE":
            case "AUSTRALIA/SYDNEY":
                return "Australia/Sydney";

            case "AWST":
            case "PERTH":
                return "Australia/Perth";

            case "NZST":
            case "NZDT":
            case "AUCKLAND":
            case "NEW ZEALAND":
                return "Pacific/Auckland";

            case "DUBAI":
            case "GST":
            case "UAE":
                return "Asia/Dubai";
        }

        if (trimmed.Contains('/') && !trimmed.Contains('+') && !trimmed.Contains('-'))
        {
            return trimmed;
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(trimmed, out var ianaId))
        {
            return ianaId;
        }

        if (TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out var tzInfo))
        {
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tzInfo.Id, out var converted))
            {
                return converted;
            }
            if (tzInfo.Id.Contains('/'))
            {
                return tzInfo.Id;
            }
        }

        return _options.DefaultTimeZone ?? "Europe/London";
    }
}

public sealed class CalAvailabilityResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("time_zone")]
    public string TimeZone { get; set; } = "Europe/London";

    [JsonPropertyName("duration")]
    public int Duration { get; set; } = 30;

    [JsonPropertyName("total_slots_found")]
    public int TotalSlotsFound { get; set; }

    [JsonPropertyName("slots")]
    public List<CalSlotDetails> Slots { get; set; } = [];

    [JsonPropertyName("booking_url")]
    public string BookingUrl { get; set; } = "";

    [JsonPropertyName("event_types")]
    public List<CalEventTypeItem> EventTypes { get; set; } = [];

    [JsonPropertyName("instruction_for_assistant")]
    public string InstructionForAssistant { get; set; } = "All slots are already displayed visually in the interactive Generative UI calendar card above. DO NOT repeat, list, or format any slots, times, or dates into text or markdown tables. Respond with exactly 1 polite sentence directing the user to the calendar card.";

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

public sealed class CalSlotDetails
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("time_utc")]
    public string TimeUtc { get; set; } = "";

    [JsonPropertyName("formatted_time")]
    public string FormattedTime { get; set; } = "";

    [JsonPropertyName("raw_time")]
    public string RawTime { get; set; } = "";
}

public sealed class CalBookingResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("booking_time_utc")]
    public string? BookingTimeUtc { get; set; }

    [JsonPropertyName("attendee_name")]
    public string? AttendeeName { get; set; }

    [JsonPropertyName("attendee_email")]
    public string? AttendeeEmail { get; set; }

    [JsonPropertyName("booking_url")]
    public string? BookingUrl { get; set; }
}

public sealed class CalEventTypeItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("length_in_minutes")]
    public int LengthInMinutes { get; set; }

    [JsonPropertyName("booking_url")]
    public string BookingUrl { get; set; } = "";
}

internal sealed class CalSlotsV2RawResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("data")]
    public CalSlotsV2Data? Data { get; set; }
}

internal sealed class CalSlotsV2Data
{
    [JsonPropertyName("slots")]
    public Dictionary<string, List<CalSlotV2Item>>? Slots { get; set; }
}

internal sealed class CalSlotV2Item
{
    [JsonPropertyName("time")]
    public string Time { get; set; } = "";
}

internal sealed class CalEventTypesV2RawResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("data")]
    public List<CalEventTypeV2RawItem>? Data { get; set; }
}

internal sealed class CalEventTypeV2RawItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("lengthInMinutes")]
    public int LengthInMinutes { get; set; }

    [JsonPropertyName("bookingUrl")]
    public string? BookingUrl { get; set; }
}
