using System.Text.Json.Serialization;
using VoyageAI;

namespace ResumeAssistant.Core.Models;

/// <summary>
/// Represents a granular chunk of professional resume information stored in Supabase with pgvector.
/// Implements <see cref="IVoyageSearchResultMetadata"/> so citations are automatically surfaced by <see cref="VoyageRagContextProvider"/>.
/// </summary>
public sealed class ResumeChunk : IVoyageSearchResultMetadata
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("category")]
    public required string Category { get; set; } // "Experience", "Skills", "Education", "Projects", "Leadership", "About"

    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("start_date")]
    public string? StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public string? EndDate { get; set; }

    [JsonPropertyName("content")]
    public required string Content { get; set; }

    [JsonPropertyName("source_name")]
    public required string SourceName { get; set; }

    [JsonPropertyName("source_link")]
    public string? SourceLink { get; set; }

    [JsonPropertyName("technologies")]
    public string[] Technologies { get; set; } = [];

    [JsonPropertyName("similarity")]
    public double? Similarity { get; set; }

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
