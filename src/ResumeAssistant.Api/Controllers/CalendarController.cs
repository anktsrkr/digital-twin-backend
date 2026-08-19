using Microsoft.AspNetCore.Mvc;
using ResumeAssistant.Api.Services;

namespace ResumeAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CalendarController(ICalComService calComService, ILogger<CalendarController> logger) : ControllerBase
{
    /// <summary>
    /// Returns live available appointment slots from Cal.com
    /// </summary>
    [HttpGet("slots")]
    public async Task<IActionResult> GetSlots(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        [FromQuery] string? timeZone,
        [FromQuery] int? duration,
        CancellationToken ct)
    {
        logger.LogInformation("REST request to fetch Cal.com slots from {Start} to {End} ({TimeZone}, {Duration}m)", start, end, timeZone, duration ?? 30);
        var result = await calComService.GetAvailableSlotsAsync(start, end, timeZone, duration, ct);
        return Ok(result);
    }

    /// <summary>
    /// Creates a meeting booking directly via Cal.com
    /// </summary>
    [HttpPost("book")]
    public async Task<IActionResult> BookSlot([FromBody] BookSlotRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Name and Email are required." });
        }

        var result = await calComService.CreateBookingAsync(
            request.Name,
            request.Email,
            request.StartTimeUtc,
            request.TimeZone,
            request.DurationInMinutes,
            request.Notes,
            ct);

        return result.Success ? Ok(result) : StatusCode(400, result);
    }

    /// <summary>
    /// Returns active Cal.com event types
    /// </summary>
    [HttpGet("event-types")]
    public async Task<IActionResult> GetEventTypes(CancellationToken ct)
    {
        var types = await calComService.GetEventTypesAsync(ct);
        return Ok(types);
    }
}

public sealed record BookSlotRequest(
    string Name,
    string Email,
    DateTime StartTimeUtc,
    string? TimeZone = null,
    int? DurationInMinutes = null,
    string? Notes = null);
