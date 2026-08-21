using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using VoyageAI;

namespace ResumeAssistant.Core.Models;

/// <summary>
/// Represents a granular chunk of professional resume information stored in MongoDB.
/// Contains 1024-dimensional Jina AI vector embeddings for semantic vector search.
/// Implements <see cref="IVoyageSearchResultMetadata"/> so citations are automatically surfaced by <see cref="VoyageRagContextProvider"/>.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ResumeChunk : IVoyageSearchResultMetadata
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonElement("title")]
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [BsonElement("category")]
    [JsonPropertyName("category")]
    public required string Category { get; set; } // "Experience", "Skills", "Education", "Projects", "Leadership", "About"

    [BsonElement("company")]
    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [BsonElement("role")]
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [BsonElement("start_date")]
    [JsonPropertyName("start_date")]
    public string? StartDate { get; set; }

    [BsonElement("end_date")]
    [JsonPropertyName("end_date")]
    public string? EndDate { get; set; }

    [BsonElement("content")]
    [JsonPropertyName("content")]
    public required string Content { get; set; }

    [BsonElement("source_name")]
    [JsonPropertyName("source_name")]
    public required string SourceName { get; set; }

    [BsonElement("source_link")]
    [JsonPropertyName("source_link")]
    public string? SourceLink { get; set; }

    [BsonElement("technologies")]
    [JsonPropertyName("technologies")]
    public string[] Technologies { get; set; } = [];

    [BsonElement("embedding")]
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = [];

    [BsonElement("similarity")]
    [BsonIgnoreIfNull]
    [JsonPropertyName("similarity")]
    public double? Similarity { get; set; }

    [BsonElement("score")]
    [BsonIgnoreIfNull]
    [JsonPropertyName("score")]
    public double? Score { get; set; }

    [BsonElement("updated_at")]
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // IVoyageSearchResultMetadata implementation
    string IVoyageSearchResultMetadata.SourceName => SourceName;
    string IVoyageSearchResultMetadata.SourceLink => SourceLink ?? $"#section-{Category.ToLowerInvariant()}";

    /// <summary>
    /// Formats the chunk into a comprehensive, high-context markdown snippet for RAG ranking and LLM context.
    /// </summary>
    public string ToContextString()
    {
        var metaParts = new List<string>();
        if (!string.IsNullOrEmpty(Company)) metaParts.Add($"Company: {Company}");
        if (!string.IsNullOrEmpty(Role)) metaParts.Add($"Role: {Role}");
        if (!string.IsNullOrEmpty(StartDate) || !string.IsNullOrEmpty(EndDate))
            metaParts.Add($"Period: {StartDate ?? ""} - {EndDate ?? "Present"}");
        if (Technologies.Length > 0)
            metaParts.Add($"Tech: {string.Join(", ", Technologies)}");

        string metaHeader = metaParts.Count > 0 ? $" [{string.Join(" | ", metaParts)}]" : "";
        return $"### {Title} ({Category}){metaHeader}\n{Content}\nSource: {SourceName} (Link: {SourceLink ?? "N/A"})";
    }
}
