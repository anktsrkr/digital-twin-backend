using System;
using System.IO;
using System.Linq;
using ResumeAssistant.Ingestion.Services;
using Xunit;
using Xunit.Abstractions;

namespace ResumeAssistant.Tests;

public class ChunkQualityTests
{
    private readonly ITestOutputHelper _output;

    public ChunkQualityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InspectAllMarkdownChunksForQuality()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "src", "ResumeAssistant.Ingestion", "Data")))
        {
            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }
        string dataDir = Path.Combine(dir!, "src", "ResumeAssistant.Ingestion", "Data");

        var files = Directory.GetFiles(dataDir, "*.md", SearchOption.AllDirectories)
            .Where(f => f.Contains("Architecture") || f.Contains("Experience") || f.Contains("Projects") || f.Contains("Skills"))
            .OrderBy(f => f);

        int totalChunks = 0;
        int shortChunks = 0;

        foreach (var file in files)
        {
            var chunks = MarkdownResumeParser.ParseAndChunkFile(file);
            totalChunks += chunks.Count;
            _output.WriteLine($"\n========================================================");
            _output.WriteLine($"FILE: {Path.GetFileName(file)} ({chunks.Count} chunks)");
            _output.WriteLine($"========================================================");
            for (int i = 0; i < chunks.Count; i++)
            {
                var c = chunks[i];
                int charCount = c.Content.Length;
                _output.WriteLine($"  [{i + 1:D2}] ({charCount,4} chars) | {c.Title}");
            }
        }

        _output.WriteLine($"\nTotal Chunks Analyzed: {totalChunks}");
        _output.WriteLine($"Short / Fragment Chunks (<200 chars): {shortChunks}");
    }
}
