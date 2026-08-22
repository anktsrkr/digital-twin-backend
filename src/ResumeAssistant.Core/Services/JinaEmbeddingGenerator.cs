using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace ResumeAssistant.Core.Services;

/// <summary>
/// Jina AI Embeddings v3 generator implementing <see cref="IEmbeddingGenerator{TKey, TEmbedding}"/>.
/// Produces 1024-dimensional dense vectors with support for asymmetric tasks (retrieval.passage and retrieval.query).
/// </summary>
public sealed class JinaEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly string _model;
    private readonly string _defaultTask;
    private readonly int _dimensions;

    public const string DefaultBaseUrl = "https://api.jina.ai/v1/";

    public JinaEmbeddingGenerator(
        string apiKey,
        string model = "jina-embeddings-v3",
        string defaultTask = "retrieval.passage",
        int dimensions = 1024,
        HttpClient? httpClient = null,
        string? baseUrl = null)
    {
        _model = string.IsNullOrWhiteSpace(model) ? "jina-embeddings-v3" : model;
        _defaultTask = string.IsNullOrWhiteSpace(defaultTask) ? "retrieval.passage" : defaultTask;
        _dimensions = dimensions > 0 ? dimensions : 1024;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _disposeHttpClient = false;
        }
        else
        {
            var resolvedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? DefaultBaseUrl
                : (baseUrl.TrimEnd('/') + "/");

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(resolvedBaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _disposeHttpClient = true;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }
    }

    public EmbeddingGeneratorMetadata Metadata => new("JinaAI", new Uri("https://api.jina.ai"), _model, _dimensions);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var inputList = values.ToList();
        if (inputList.Count == 0)
        {
            return new GeneratedEmbeddings<Embedding<float>>([]);
        }

        // Determine task type (retrieval.query vs retrieval.passage)
        string task = _defaultTask;
        if (options?.AdditionalProperties?.TryGetValue("Task", out var customTask) == true && customTask is not null)
        {
            task = customTask.ToString()!;
        }
        else if (options?.AdditionalProperties?.TryGetValue("InputType", out var inputType) == true && inputType is not null)
        {
            var it = inputType.ToString();
            task = it?.Equals("Query", StringComparison.OrdinalIgnoreCase) == true
                ? "retrieval.query"
                : "retrieval.passage";
        }

        var requestPayload = new JinaEmbeddingRequest
        {
            Model = _model,
            Task = task,
            Dimensions = _dimensions,
            Input = inputList
        };

        var response = await _httpClient.PostAsJsonAsync("embeddings", requestPayload, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Jina AI Embeddings API call failed with status {(int)response.StatusCode} ({response.ReasonPhrase}): {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<JinaEmbeddingResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result?.Data is null || result.Data.Count == 0)
        {
            throw new InvalidOperationException("Jina AI Embeddings API returned an empty or invalid response.");
        }

        var embeddings = result.Data
            .OrderBy(d => d.Index)
            .Select(d => new Embedding<float>(d.Embedding))
            .ToList();

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    private sealed class JinaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "jina-embeddings-v3";

        [JsonPropertyName("task")]
        public string Task { get; set; } = "retrieval.passage";

        [JsonPropertyName("dimensions")]
        public int Dimensions { get; set; } = 1024;

        [JsonPropertyName("input")]
        public List<string> Input { get; set; } = [];
    }

    private sealed class JinaEmbeddingResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("data")]
        public List<JinaEmbeddingItem> Data { get; set; } = [];
    }

    private sealed class JinaEmbeddingItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
