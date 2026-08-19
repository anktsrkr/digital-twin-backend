using Microsoft.AspNetCore.Mvc;
using ResumeAssistant.Api.Agent;

namespace ResumeAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class FollowUpController : ControllerBase
{
    private readonly IFollowUpAgent _followUpAgent;
    private readonly ILogger<FollowUpController> _logger;

    public FollowUpController(
        IFollowUpAgent followUpAgent,
        ILogger<FollowUpController> logger)
    {
        _followUpAgent = followUpAgent;
        _logger = logger;
    }

    /// <summary>
    /// Generates up to 5 actionable pills for recruiters (including guaranteed Download Resume and Book a Call)
    /// based on the recent conversation history using the independent Follow-Up LLM agent.
    /// </summary>
    [HttpPost("suggestions")]
    public async Task<ActionResult<FollowUpResponse>> GetSuggestions(
        [FromBody] FollowUpRequest request,
        CancellationToken cancellationToken)
    {
        var messages = request?.Messages ?? [];
        _logger.LogInformation("Generating follow-up pills for conversation with {Count} messages", messages.Count);

        var result = await _followUpAgent.GenerateFollowUpPillsAsync(messages, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Fallback alias endpoint for POST /api/followup
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<FollowUpResponse>> PostFollowUp(
        [FromBody] FollowUpRequest request,
        CancellationToken cancellationToken)
    {
        return await GetSuggestions(request, cancellationToken);
    }

    /// <summary>
    /// Returns default initial 5 actionable pills for new sessions.
    /// </summary>
    [HttpGet("default")]
    public async Task<ActionResult<FollowUpResponse>> GetDefaults(CancellationToken cancellationToken)
    {
        var result = await _followUpAgent.GenerateFollowUpPillsAsync([], cancellationToken);
        return Ok(result);
    }
}
