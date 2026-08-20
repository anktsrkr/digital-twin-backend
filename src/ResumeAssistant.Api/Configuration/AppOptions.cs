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
    public string Model { get; set; } = "jina-embeddings-v3";
    public int Dimensions { get; set; } = 1024;
    public string RerankModel { get; set; } = "jina-reranker-v2-base-multilingual";
    public int TopK { get; set; } = 4;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.StartsWith("YOUR_");
}

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>
    /// Operating mode: "Local" (Docker Compose pgvector + GoTrue) or "Cloud" (Hosted Supabase Project).
    /// </summary>
    public string Mode { get; set; } = "Local";

    public LocalSupabaseOptions Local { get; set; } = new();
    public CloudSupabaseOptions Cloud { get; set; } = new();

    public bool IsLocal => string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase);
    public bool IsCloud => string.Equals(Mode, "Cloud", StringComparison.OrdinalIgnoreCase);

    public string GetResolvedUrl() => IsCloud ? (Cloud.Url ?? "https://your-project.supabase.co") : Local.Url;
    public string? GetResolvedAnonKey() => IsCloud ? Cloud.AnonKey : Local.AnonKey;
    public string GetResolvedConnectionString() => IsCloud ? (Cloud.ConnectionString ?? "") : Local.ConnectionString;
}

public sealed class LocalSupabaseOptions
{
    public string Url { get; set; } = "http://localhost:9999";
    public string AnonKey { get; set; } = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.local-anon-token";
    public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Database=resume_assistant;Username=postgres;Password=postgres;SSL Mode=Disable;Trust Server Certificate=true";
}

public sealed class CloudSupabaseOptions
{
    public string? Url { get; set; } = "https://your-project.supabase.co";
    public string? AnonKey { get; set; } = "your-anon-key";
    public string? ConnectionString { get; set; } = "Host=aws-0-us-east-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.your-ref;Password=your-password;SSL Mode=Require;Trust Server Certificate=true";
}

public sealed class LogtoOptions
{
    public const string SectionName = "Logto";

    /// <summary>
    /// Operating mode: "Local" (Docker Compose Logto / Inbucket) or "Cloud" (Logto Cloud tenant).
    /// </summary>
    public string Mode { get; set; } = "Cloud";

    public LocalLogtoOptions Local { get; set; } = new();
    public CloudLogtoOptions Cloud { get; set; } = new();

    public bool IsLocal => string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase);
    public bool IsCloud => string.Equals(Mode, "Cloud", StringComparison.OrdinalIgnoreCase);

    public string GetResolvedEndpoint() => IsCloud ? Cloud.Endpoint : Local.Endpoint;
    public string GetResolvedAppId() => IsCloud ? Cloud.AppId : Local.AppId;
    public string GetResolvedM2MAppId() => IsCloud ? Cloud.M2MAppId : Local.M2MAppId;
    public string GetResolvedM2MSecret() => IsCloud ? Cloud.M2MAppSecret : Local.M2MAppSecret;
    public string GetResolvedApiResource() => IsCloud ? Cloud.ApiResource : Local.ApiResource;
    public string GetResolvedMagicLinkBaseUrl() => IsCloud ? Cloud.MagicLinkBaseUrl : Local.MagicLinkBaseUrl;
    public string? GetResolvedWebhookSecret() => IsCloud ? Cloud.WebhookSecret : Local.WebhookSecret;
    public string GetResolvedManagementApiResource() => $"{GetResolvedEndpoint().TrimEnd('/')}/api";
}

public sealed class LocalLogtoOptions
{
    public string Endpoint { get; set; } = "http://localhost:3001";
    public string AppId { get; set; } = "local_spa_app_id";
    public string M2MAppId { get; set; } = "local_m2m_app_id";
    public string M2MAppSecret { get; set; } = "local_m2m_app_secret";
    public string ApiResource { get; set; } = "https://api.resumetwin.local";
    public string MagicLinkBaseUrl { get; set; } = "http://localhost:5173";
    public string? WebhookSecret { get; set; }
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 2500;
}

public sealed class CloudLogtoOptions
{
    public string Endpoint { get; set; } = "https://tenant.logto.app";
    public string AppId { get; set; } = "YOUR_LOGTO_SPA_APP_ID";
    public string M2MAppId { get; set; } = "YOUR_LOGTO_M2M_APP_ID";
    public string M2MAppSecret { get; set; } = "YOUR_LOGTO_M2M_SECRET";
    public string ApiResource { get; set; } = "https://api.resumetwin.local";
    public string MagicLinkBaseUrl { get; set; } = "http://localhost:5173";
    public string? WebhookSecret { get; set; }

    public bool IsM2MConfigured =>
        !string.IsNullOrWhiteSpace(M2MAppId) &&
        !M2MAppId.StartsWith("YOUR_") &&
        !string.IsNullOrWhiteSpace(M2MAppSecret) &&
        !M2MAppSecret.StartsWith("YOUR_");
}
