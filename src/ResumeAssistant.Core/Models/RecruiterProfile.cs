using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ResumeAssistant.Core.Models;

/// <summary>
/// Recruiter profile captured during Magic Link authentication.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class RecruiterProfile
{
    [BsonId]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("email")]
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    [BsonElement("domain")]
    [JsonPropertyName("domain")]
    public required string Domain { get; set; }

    [BsonElement("company_inferred")]
    [JsonPropertyName("company_inferred")]
    public string? CompanyInferred { get; set; }

    [BsonElement("first_login_at")]
    [JsonPropertyName("first_login_at")]
    public DateTimeOffset FirstLoginAt { get; set; } = DateTimeOffset.UtcNow;

    [BsonElement("last_active_at")]
    [JsonPropertyName("last_active_at")]
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;

    [BsonElement("total_messages")]
    [JsonPropertyName("total_messages")]
    public int TotalMessages { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class CitationDto
{
    [BsonElement("source_name")]
    [JsonPropertyName("source_name")]
    public required string SourceName { get; set; }

    [BsonElement("source_link")]
    [JsonPropertyName("source_link")]
    public string? SourceLink { get; set; }

    [BsonElement("title")]
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [BsonElement("category")]
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [BsonElement("company")]
    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [BsonElement("role")]
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [BsonElement("period")]
    [JsonPropertyName("period")]
    public string? Period { get; set; }

    [BsonElement("content")]
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [BsonElement("technologies")]
    [JsonPropertyName("technologies")]
    public string[]? Technologies { get; set; }

    [BsonElement("similarity")]
    [JsonPropertyName("similarity")]
    public double? Similarity { get; set; }
}
