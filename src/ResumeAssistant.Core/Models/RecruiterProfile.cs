using System.Text.Json.Serialization;

namespace ResumeAssistant.Core.Models;

/// <summary>
/// Recruiter profile captured during Magic Link authentication.
/// </summary>
public sealed class RecruiterProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public required string Email { get; set; }

    [JsonPropertyName("domain")]
    public required string Domain { get; set; }

    [JsonPropertyName("company_inferred")]
    public string? CompanyInferred { get; set; }

    [JsonPropertyName("first_login_at")]
    public DateTimeOffset FirstLoginAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("last_active_at")]
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("total_messages")]
    public int TotalMessages { get; set; }
}

/// <summary>
/// Audit trail of recruiter conversation exchanges.
/// </summary>
public sealed class RecruiterConversation
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("recruiter_id")]
    public string? RecruiterId { get; set; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; set; }

    [JsonPropertyName("query")]
    public required string Query { get; set; }

    [JsonPropertyName("response")]
    public required string Response { get; set; }

    [JsonPropertyName("citations")]
    public List<CitationDto> Citations { get; set; } = [];

    [JsonPropertyName("tokens_used")]
    public int TokensUsed { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CitationDto
{
    [JsonPropertyName("source_name")]
    public required string SourceName { get; set; }

    [JsonPropertyName("source_link")]
    public string? SourceLink { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}
