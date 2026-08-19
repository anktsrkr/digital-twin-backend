using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pgvector;
using ResumeAssistant.Core.Models;
using ResumeAssistant.Core.Services;
using ResumeAssistant.Ingestion.Services;
using VoyageAI;

namespace ResumeAssistant.Ingestion;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("==================================================================");
        Console.WriteLine(" Resume Digital Twin - Markdown Knowledge Ingestion & Vector Tool ");
        Console.WriteLine("==================================================================\n");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        string embeddingProvider = configuration["EMBEDDING_PROVIDER"] ?? configuration["Embedding:Provider"] ?? "JinaAI";
        string? jinaApiKey = configuration["JINA_API_KEY"] ?? configuration["JinaAI:ApiKey"];
        string jinaModel = configuration["JinaAI:Model"] ?? "jina-embeddings-v3";
        string? voyageApiKey = configuration["VOYAGE_API_KEY"] ?? configuration["VoyageAI:ApiKey"];
        string voyageModel = configuration["VoyageAI:Model"] ?? "voyage-3-lite";
        string supabaseMode = configuration["SUPABASE_MODE"] ?? configuration["Supabase:Mode"] ?? "Local";
        string? connectionString = configuration["SUPABASE_DB_CONNECTION_STRING"]
            ?? configuration["Supabase:ConnectionString"]
            ?? (string.Equals(supabaseMode, "Cloud", StringComparison.OrdinalIgnoreCase)
                ? configuration["Supabase:Cloud:ConnectionString"]
                : configuration["Supabase:Local:ConnectionString"]);

        bool isJinaConfigured = !string.IsNullOrWhiteSpace(jinaApiKey) && !jinaApiKey.StartsWith("YOUR_");
        bool isVoyageConfigured = !string.IsNullOrWhiteSpace(voyageApiKey) && !voyageApiKey.StartsWith("YOUR_");

        bool useJina = string.Equals(embeddingProvider, "JinaAI", StringComparison.OrdinalIgnoreCase) ? isJinaConfigured : (!isVoyageConfigured && isJinaConfigured);

        string activeProvider = useJina ? $"Jina AI ({jinaModel})" : $"Voyage AI ({voyageModel})";
        Console.WriteLine($"⚙️  Embedding Provider: {activeProvider}");
        Console.WriteLine($"⚙️  Supabase Database Target: {supabaseMode}");

        if (!isJinaConfigured && !isVoyageConfigured)
        {
            Console.WriteLine("⚠️  WARNING: No embedding API key configured (neither JINA_API_KEY nor VOYAGE_API_KEY).");
            Console.WriteLine("   Running in dry-run validation mode (validating Markdown chunks & frontmatter)...\n");
        }

        // 1. Discover and load all Markdown documents from Data/ directory
        string dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dataDir) || Directory.GetFiles(dataDir, "*.md", SearchOption.AllDirectories).Length == 0)
        {
            string localData = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            if (Directory.Exists(localData) && Directory.GetFiles(localData, "*.md", SearchOption.AllDirectories).Length > 0)
            {
                dataDir = localData;
            }
            else
            {
                string projectData = Path.Combine(Directory.GetCurrentDirectory(), "src", "ResumeAssistant.Ingestion", "Data");
                if (Directory.Exists(projectData) && Directory.GetFiles(projectData, "*.md", SearchOption.AllDirectories).Length > 0)
                {
                    dataDir = projectData;
                }
            }
        }

        var chunks = new List<ResumeChunk>();

        if (Directory.Exists(dataDir))
        {
            var mdFiles = Directory.GetFiles(dataDir, "*.md", SearchOption.AllDirectories);
            if (mdFiles.Length > 0)
            {
                Console.WriteLine($"📂 Found {mdFiles.Length} Markdown documents in {dataDir}:");
                foreach (var file in mdFiles)
                {
                    string relativePath = Path.GetRelativePath(dataDir, file);
                    var fileChunks = MarkdownResumeParser.ParseAndChunkFile(file);
                    chunks.AddRange(fileChunks);
                    Console.WriteLine($"   📄 [{relativePath}] -> generated {fileChunks.Count} chunk(s)");
                }
            }
        }

        // Fallback: If no markdown files found, check for legacy resume-data.json
        if (chunks.Count == 0)
        {
            string jsonPath = Path.Combine(dataDir, "resume-data.json");
            if (File.Exists(jsonPath))
            {
                Console.WriteLine($"📄 Loading fallback legacy {Path.GetFileName(jsonPath)}...");
                string jsonContent = await File.ReadAllTextAsync(jsonPath);
                using var doc = JsonDocument.Parse(jsonContent);
                var chunksElement = doc.RootElement.GetProperty("chunks");
                foreach (var el in chunksElement.EnumerateArray())
                {
                    var techList = new List<string>();
                    if (el.TryGetProperty("technologies", out var techEl) && techEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var t in techEl.EnumerateArray())
                        {
                            if (t.GetString() is { } str) techList.Add(str);
                        }
                    }

                    chunks.Add(new ResumeChunk
                    {
                        Title = el.GetProperty("title").GetString() ?? "Untitled",
                        Category = el.GetProperty("category").GetString() ?? "General",
                        Company = el.TryGetProperty("company", out var comp) && comp.ValueKind == JsonValueKind.String ? comp.GetString() : null,
                        Role = el.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String ? role.GetString() : null,
                        StartDate = el.TryGetProperty("startDate", out var sd) && sd.ValueKind == JsonValueKind.String ? sd.GetString() : null,
                        EndDate = el.TryGetProperty("endDate", out var ed) && ed.ValueKind == JsonValueKind.String ? ed.GetString() : null,
                        Content = el.GetProperty("content").GetString() ?? "",
                        SourceName = el.GetProperty("sourceName").GetString() ?? "Resume",
                        SourceLink = el.TryGetProperty("sourceLink", out var sl) && sl.ValueKind == JsonValueKind.String ? sl.GetString() : null,
                        Technologies = techList.ToArray()
                    });
                }
            }
        }

        if (chunks.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error: No Markdown (.md) or JSON resume data found in: {dataDir}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"\n📊 Total Chunks to Ingest: {chunks.Count}\n");
        for (int i = 0; i < chunks.Count; i++)
        {
            Console.WriteLine($"   [{i + 1:D2}/{chunks.Count:D2}] ({chunks[i].Category}) {chunks[i].Title}");
        }
        Console.WriteLine();

        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;
        if (useJina)
        {
            embeddingGenerator = new JinaEmbeddingGenerator(
                apiKey: jinaApiKey!,
                model: jinaModel,
                defaultTask: "retrieval.passage",
                dimensions: 1024);
        }
        else if (isVoyageConfigured)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddVoyageAI(o => o.ApiKey = voyageApiKey!);
            services.AddVoyageEmbeddingGenerator(o =>
            {
                o.Model = voyageModel;
                o.InputType = VoyageAI.Models.InputType.Document;
            });
            var sp = services.BuildServiceProvider();
            embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Dry-run validation successful! All Markdown files, YAML frontmatter, and semantic chunks parsed perfectly.");
            Console.WriteLine("   To seed live embeddings to Supabase pgvector, provide valid API credentials and re-run.\n");
            Console.ResetColor();
            return;
        }

        // Test embedding generation for the first chunk to verify API connectivity
        Console.WriteLine($"🔮 Testing {activeProvider} embedding generation for 1024 dimensions...");
        try
        {
            var testEmbedding = await embeddingGenerator.GenerateAsync([chunks[0].ToContextString()]);
            var dim = testEmbedding[0].Vector.Length;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"   ✓ Generated test embedding successfully (Vector dimension: {dim})!\n");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error communicating with Embedding API: {ex.Message}");
            Console.ResetColor();
            return;
        }

        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.StartsWith("Host=aws-0-us-east-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.your-ref"))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("ℹ️  Embeddings API is verified and working perfectly!");
            Console.WriteLine("   To write to pgvector, set SUPABASE_DB_CONNECTION_STRING with your active database credentials.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine("💾 Writing chunks with vector embeddings to Supabase `resume_chunks` table...");

        // Initialize Npgsql DataSource with pgvector mapping
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        await using var dataSource = dataSourceBuilder.Build();
        await using var conn = await dataSource.OpenConnectionAsync();

        // Clean reseed: remove previous chunks so re-runs don't create stale duplicate vectors
        await using (var truncateCmd = new NpgsqlCommand("TRUNCATE TABLE public.resume_chunks;", conn))
        {
            await truncateCmd.ExecuteNonQueryAsync();
            Console.WriteLine("   ✓ Cleared existing records in `resume_chunks` for a clean re-seed.");
        }

        const string sql = @"
            INSERT INTO public.resume_chunks 
            (title, category, company, role, start_date, end_date, content, source_name, source_link, technologies, embedding, updated_at)
            VALUES 
            (@title, @category, @company, @role, @start_date, @end_date, @content, @source_name, @source_link, @technologies, @embedding, NOW())";

        int successCount = 0;
        foreach (var chunk in chunks)
        {
            string contextText = chunk.ToContextString();
            var embeddings = await embeddingGenerator.GenerateAsync([contextText]);
            var embeddingVector = embeddings[0].Vector;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("title", chunk.Title);
            cmd.Parameters.AddWithValue("category", chunk.Category);
            cmd.Parameters.AddWithValue("company", (object?)chunk.Company ?? DBNull.Value);
            cmd.Parameters.AddWithValue("role", (object?)chunk.Role ?? DBNull.Value);
            cmd.Parameters.AddWithValue("start_date", (object?)chunk.StartDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("end_date", (object?)chunk.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("content", chunk.Content);
            cmd.Parameters.AddWithValue("source_name", chunk.SourceName);
            cmd.Parameters.AddWithValue("source_link", (object?)chunk.SourceLink ?? DBNull.Value);
            cmd.Parameters.AddWithValue("technologies", chunk.Technologies);
            cmd.Parameters.AddWithValue("embedding", new Vector(embeddingVector.ToArray()));

            await cmd.ExecuteNonQueryAsync();
            successCount++;
            Console.WriteLine($"   ✓ Seeded: {chunk.Title}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n🎉 Success! Seeded {successCount} chunks with 1024-dim {activeProvider} embeddings into Supabase pgvector.\n");
        Console.ResetColor();
    }
}
