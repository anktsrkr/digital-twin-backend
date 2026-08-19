using System.Text.RegularExpressions;
using ResumeAssistant.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ResumeAssistant.Ingestion.Services;

public class FrontmatterMetadata
{
    public string? Title { get; set; }
    public string? Category { get; set; }
    public string? Company { get; set; }
    public string? Role { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? SourceName { get; set; }
    public string? SourceLink { get; set; }
    public List<string>? Technologies { get; set; }
}

public static class MarkdownResumeParser
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly Regex FrontmatterRegex = new(@"^---\r?\n(.*?)\r?\n---\r?\n", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HeaderSplitRegex = new(@"(?=^##\s+)", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Parses a Markdown document with YAML frontmatter and splits it into semantic, header-aware ResumeChunks.
    /// </summary>
    public static List<ResumeChunk> ParseAndChunkFile(string filePath)
    {
        string rawText = File.ReadAllText(filePath);
        var match = FrontmatterRegex.Match(rawText);

        FrontmatterMetadata metadata = new();
        string markdownBody = rawText;

        if (match.Success)
        {
            string yaml = match.Groups[1].Value;
            try
            {
                metadata = YamlDeserializer.Deserialize<FrontmatterMetadata>(yaml) ?? new FrontmatterMetadata();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"   ⚠️ Warning: Failed to parse YAML frontmatter in {Path.GetFileName(filePath)}: {ex.Message}");
                Console.ResetColor();
            }

            markdownBody = rawText.Substring(match.Length).Trim();
        }

        string baseTitle = metadata.Title ?? Path.GetFileNameWithoutExtension(filePath);
        string category = metadata.Category ?? InferCategoryFromPath(filePath);
        string sourceName = metadata.SourceName ?? $"Knowledge Base: {baseTitle}";
        string[] technologies = metadata.Technologies?.ToArray() ?? [];

        var chunks = new List<ResumeChunk>();

        // If the content is short (e.g. <= 1200 characters), keep as a single atomic chunk
        if (markdownBody.Length <= 1200)
        {
            chunks.Add(new ResumeChunk
            {
                Title = baseTitle,
                Category = category,
                Company = metadata.Company,
                Role = metadata.Role,
                StartDate = metadata.StartDate,
                EndDate = metadata.EndDate,
                SourceName = sourceName,
                SourceLink = metadata.SourceLink,
                Technologies = technologies,
                Content = markdownBody
            });
            return chunks;
        }

        // Header-aware semantic splitting for longer case studies and architecture deep dives
        var sections = HeaderSplitRegex.Split(markdownBody)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (sections.Count <= 1)
        {
            // Fallback: split by double newlines if no ## headers exist
            sections = SplitByParagraphs(markdownBody, maxCharsPerChunk: 1000);
        }

        int sectionIndex = 1;
        foreach (var section in sections)
        {
            string sectionTitle = baseTitle;
            var headerMatch = Regex.Match(section, @"^##\s+(.+)$", RegexOptions.Multiline);
            if (headerMatch.Success)
            {
                string headerName = headerMatch.Groups[1].Value.Trim();
                sectionTitle = $"{baseTitle} — {headerName}";
            }
            else if (sections.Count > 1)
            {
                sectionTitle = $"{baseTitle} (Part {sectionIndex})";
            }

            chunks.Add(new ResumeChunk
            {
                Title = sectionTitle,
                Category = category,
                Company = metadata.Company,
                Role = metadata.Role,
                StartDate = metadata.StartDate,
                EndDate = metadata.EndDate,
                SourceName = sourceName,
                SourceLink = metadata.SourceLink,
                Technologies = technologies,
                Content = section
            });

            sectionIndex++;
        }

        return chunks;
    }

    private static string InferCategoryFromPath(string filePath)
    {
        string dir = Path.GetFileName(Path.GetDirectoryName(filePath)) ?? "";
        return dir switch
        {
            "Experience" => "Experience",
            "Architecture" => "Experience",
            "Blogs" => "Projects",
            "Certifications" => "Certifications",
            "Skills" => "Skills",
            "Education" => "Education",
            "About" => "About",
            _ => "About"
        };
    }

    private static List<string> SplitByParagraphs(string text, int maxCharsPerChunk)
    {
        var paragraphs = text.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        var currentChunk = new System.Text.StringBuilder();

        foreach (var p in paragraphs)
        {
            if (currentChunk.Length + p.Length > maxCharsPerChunk && currentChunk.Length > 0)
            {
                result.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }

            if (currentChunk.Length > 0) currentChunk.AppendLine().AppendLine();
            currentChunk.Append(p);
        }

        if (currentChunk.Length > 0)
        {
            result.Add(currentChunk.ToString().Trim());
        }

        return result;
    }
}
