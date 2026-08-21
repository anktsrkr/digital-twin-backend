using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ResumeAssistant.Core.Models;

/// <summary>
/// Persisted conversation thread stored in MongoDB.
/// Supports Microsoft Agent Framework serialized AgentSession roundtripping across browser sessions.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class UserThread
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("user_id")]
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [BsonElement("user_email")]
    [JsonPropertyName("user_email")]
    public string? UserEmail { get; set; }

    [BsonElement("title")]
    [JsonPropertyName("title")]
    public string Title { get; set; } = "New Conversation";

    [BsonElement("messages")]
    [JsonPropertyName("messages")]
    public List<PersistedChatMessage> Messages { get; set; } = [];

    [BsonElement("serialized_session")]
    [JsonPropertyName("serialized_session")]
    public string? SerializedSession { get; set; }

    [BsonElement("metadata")]
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?> Metadata { get; set; } = [];

    [BsonElement("created_at")]
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [BsonElement("last_updated_at")]
    [JsonPropertyName("last_updated_at")]
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Persisted chat message inside a user thread.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class PersistedChatMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("role")]
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user"; // "user", "assistant", "system", "tool"

    [BsonElement("content")]
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("citations")]
    [JsonPropertyName("citations")]
    public List<CitationDto> Citations { get; set; } = [];

    [BsonElement("tool_calls")]
    [JsonPropertyName("tool_calls")]
    public List<PersistedToolCall> ToolCalls { get; set; } = [];

    [BsonElement("timestamp")]
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Record of tool execution within a conversation turn.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class PersistedToolCall
{
    [BsonElement("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("name")]
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [BsonElement("arguments")]
    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }

    [BsonElement("result")]
    [JsonPropertyName("result")]
    public string? Result { get; set; }
}
