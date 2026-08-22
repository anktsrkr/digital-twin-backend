namespace ResumeAssistant.Api.Configuration;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>
    /// Telemetry operating mode: "Local" (Docker Compose Grafana LGTM), "Cloud" (Grafana Cloud OTLP), or "None".
    /// </summary>
    public string Mode { get; set; } = "Local";

    public LocalTelemetryOptions Local { get; set; } = new();
    public CloudTelemetryOptions Cloud { get; set; } = new();

    public bool IsLocal => string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase);
    public bool IsCloud => string.Equals(Mode, "Cloud", StringComparison.OrdinalIgnoreCase);
    public bool IsDisabled => string.Equals(Mode, "None", StringComparison.OrdinalIgnoreCase);
}

public sealed class LocalTelemetryOptions
{
    /// <summary>
    /// Local OTLP HTTP Endpoint for Docker Compose Grafana LGTM (default: http://localhost:4318)
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4318";
}

public sealed class CloudTelemetryOptions
{
    /// <summary>
    /// Grafana Cloud OTLP Gateway endpoint, e.g. "https://otlp-gateway-prod-eu-west-0.grafana.net/otlp".
    /// </summary>
    public string OtlpEndpoint { get; set; } = "https://otlp-gateway-prod-eu-west-0.grafana.net/otlp";

    public string? InstanceId { get; set; }
    public string? ApiToken { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(InstanceId) &&
        !InstanceId.StartsWith("YOUR_") &&
        !string.IsNullOrWhiteSpace(ApiToken) &&
        !ApiToken.StartsWith("YOUR_");
}

public sealed class LlmOptions
{
    public const string SectionName = "LLM";

    /// <summary>
    /// LLM provider mode: "Local" (LM Studio / Ollama) or "Cloud" (Cloudflare Workers AI).
    /// </summary>
    public string Mode { get; set; } = "Local";

    public LocalLlmConfig Local { get; set; } = new();
    public CloudLlmConfig Cloud { get; set; } = new();

    public bool IsLocal => string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase);
    public bool IsCloud => string.Equals(Mode, "Cloud", StringComparison.OrdinalIgnoreCase);
}

public sealed class FollowUpLlmOptions
{
    public const string SectionName = "FollowUpLLM";

    /// <summary>
    /// Dedicated LLM provider mode for follow-up suggestions: "Local" (LM Studio / Ollama) or "Cloud" (Cloudflare Workers AI).
    /// </summary>
    public string Mode { get; set; } = "Local";

    public LocalLlmConfig Local { get; set; } = new()
    {
        Endpoint = "http://localhost:1234/v1",
        Model = "lfm2.5-2.6b",
        ApiKey = "lm-studio"
    };

    public CloudLlmConfig Cloud { get; set; } = new()
    {
        Model = "@cf/meta/llama-3.3-70b-instruct"
    };

    public bool IsLocal => string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase);
    public bool IsCloud => string.Equals(Mode, "Cloud", StringComparison.OrdinalIgnoreCase);

    public LlmOptions ToLlmOptions() => new()
    {
        Mode = Mode,
        Local = new LocalLlmConfig
        {
            Endpoint = Local.Endpoint,
            Model = Local.Model,
            ApiKey = Local.ApiKey
        },
        Cloud = new CloudLlmConfig
        {
            AccountId = Cloud.AccountId,
            ApiToken = Cloud.ApiToken,
            Model = Cloud.Model,
            BaseUrl = Cloud.BaseUrl
        }
    };
}

public sealed class LocalLlmConfig
{
    /// <summary>
    /// Local OpenAI-compatible endpoint, e.g. LM Studio ("http://localhost:1234/v1") or Ollama ("http://localhost:11434/v1").
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:1234/v1";

    /// <summary>
    /// Model identifier loaded in LM Studio (e.g. "lfm2.5-2.6b", "llama-3.1-8b-instruct", "qwen2.5-7b-instruct").
    /// </summary>
    public string Model { get; set; } = "lfm2.5-2.6b";

    /// <summary>
    /// API key for local server (LM Studio accepts any string, e.g. "lm-studio").
    /// </summary>
    public string ApiKey { get; set; } = "lm-studio";
}

public sealed class CloudLlmConfig
{
    public string? AccountId { get; set; } = "YOUR_CLOUDFLARE_ACCOUNT_ID";
    public string? ApiToken { get; set; } = "YOUR_CLOUDFLARE_API_TOKEN";
    public string Model { get; set; } = "@cf/meta/llama-3.3-70b-instruct";
    public string? BaseUrl { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiToken) &&
        !ApiToken.StartsWith("YOUR_") &&
        !string.IsNullOrWhiteSpace(AccountId) &&
        !AccountId.StartsWith("YOUR_");

    public string GetResolvedBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl)) return BaseUrl;
        return $"https://api.cloudflare.com/client/v4/accounts/{AccountId}/ai/v1";
    }
}

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    /// <summary>
    /// Active embedding provider: "VoyageAI" or "JinaAI". Defaults to "JinaAI" if Jina API key is configured.
    /// </summary>
    public string Provider { get; set; } = "JinaAI";

    public bool IsJina => string.Equals(Provider, "JinaAI", StringComparison.OrdinalIgnoreCase);
    public bool IsVoyage => string.Equals(Provider, "VoyageAI", StringComparison.OrdinalIgnoreCase);
}

public sealed class VoyageAiOptions
{
    public const string SectionName = "VoyageAI";

    public string? ApiKey { get; set; }
    public string EmbeddingModel { get; set; } = "voyage-3-lite";
    public string RerankModel { get; set; } = "rerank-2";
    public int TopK { get; set; } = 4;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.StartsWith("YOUR_");
}

public sealed class JinaAiOptions
{
    public const string SectionName = "JinaAI";

    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string Model { get; set; } = "jina-embeddings-v3";
    public int Dimensions { get; set; } = 1024;
    public string RerankModel { get; set; } = "jina-reranker-v2-base-multilingual";
    public int TopK { get; set; } = 4;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.StartsWith("YOUR_");
}

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDB";

    /// <summary>
    /// Operating mode: "Local" (Docker Compose MongoDB) or "Cloud" (MongoDB Atlas).
    /// </summary>
    public string Mode { get; set; } = "Local";

    public LocalMongoDbOptions Local { get; set; } = new();
    public CloudMongoDbOptions Cloud { get; set; } = new();
    public string DatabaseName { get; set; } = "resume_assistant";

    public bool IsLocal => string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase);
    public bool IsCloud => string.Equals(Mode, "Cloud", StringComparison.OrdinalIgnoreCase);

    public string GetResolvedConnectionString() => IsCloud ? (Cloud.ConnectionString ?? "") : Local.ConnectionString;
    public string GetResolvedDatabaseName() => !string.IsNullOrWhiteSpace(DatabaseName) ? DatabaseName : "resume_assistant";
}

public sealed class LocalMongoDbOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
}

public sealed class CloudMongoDbOptions
{
    public string? ConnectionString { get; set; } = "mongodb+srv://<username>:<password>@cluster.mongodb.net/?retryWrites=true&w=majority";
}

public sealed class ClerkOptions
{
    public const string SectionName = "Clerk";

    /// <summary>
    /// Clerk Frontend API / Issuer URL: e.g., https://<instance>.clerk.accounts.dev or custom domain.
    /// </summary>
    public string Issuer { get; set; } = "https://humble-finch-6302.clerk.accounts.dev";

    /// <summary>
    /// Clerk Publishable Key: pk_test_... or pk_live_...
    /// </summary>
    public string PublishableKey { get; set; } = "";

    /// <summary>
    /// Clerk Backend Secret Key: sk_test_... or sk_live_...
    /// </summary>
    public string SecretKey { get; set; } = "";

    /// <summary>
    /// Clerk Webhook Signing Secret: whsec_...
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// Optional Audience for JWT validation if customized in Clerk JWT template.
    /// </summary>
    public string? Audience { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Issuer) &&
        !Issuer.StartsWith("YOUR_") &&
        !string.IsNullOrWhiteSpace(SecretKey) &&
        !SecretKey.StartsWith("YOUR_");
}
