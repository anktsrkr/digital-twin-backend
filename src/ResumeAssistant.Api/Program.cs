using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using AGUI.Samples.Shared;
using AGUI.Server;
using Microsoft.Extensions.AI;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pgvector.Npgsql;
using ResumeAssistant.Api.Agent;
using ResumeAssistant.Api.Configuration;
using ResumeAssistant.Api.Services;
using ResumeAssistant.Api.Telemetry;
using ResumeAssistant.Core.Interfaces;
using ResumeAssistant.Core.Services;
using VoyageAI;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure strongly typed options
var telemetryOptions = builder.Configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();
var llmOptions = builder.Configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
var followUpLlmOptions = builder.Configuration.GetSection(FollowUpLlmOptions.SectionName).Get<FollowUpLlmOptions>() ?? new FollowUpLlmOptions();
var embeddingOptions = builder.Configuration.GetSection(EmbeddingOptions.SectionName).Get<EmbeddingOptions>() ?? new EmbeddingOptions();
var jinaOptions = builder.Configuration.GetSection(JinaAiOptions.SectionName).Get<JinaAiOptions>() ?? new JinaAiOptions();
var voyageOptions = builder.Configuration.GetSection(VoyageAiOptions.SectionName).Get<VoyageAiOptions>() ?? new VoyageAiOptions();
var supabaseOptions = builder.Configuration.GetSection(SupabaseOptions.SectionName).Get<SupabaseOptions>() ?? new SupabaseOptions();
var logtoOptions = builder.Configuration.GetSection(LogtoOptions.SectionName).Get<LogtoOptions>() ?? new LogtoOptions();
var calComOptions = builder.Configuration.GetSection(CalComOptions.SectionName).Get<CalComOptions>() ?? new CalComOptions();

// Allow overriding via environment variables
if (builder.Configuration["TELEMETRY_MODE"] is { } tMode) telemetryOptions.Mode = tMode;
if (builder.Configuration["TELEMETRY_LOCAL_ENDPOINT"] is { } tLocal) telemetryOptions.Local.OtlpEndpoint = tLocal;
if (builder.Configuration["GRAFANA_OTLP_ENDPOINT"] is { } gEndpoint) telemetryOptions.Cloud.OtlpEndpoint = gEndpoint;
if (builder.Configuration["GRAFANA_INSTANCE_ID"] is { } gInstance) telemetryOptions.Cloud.InstanceId = gInstance;
if (builder.Configuration["GRAFANA_API_TOKEN"] is { } gToken) telemetryOptions.Cloud.ApiToken = gToken;

if (builder.Configuration["LLM_MODE"] is { } lMode) llmOptions.Mode = lMode;
if (builder.Configuration["LOCAL_LLM_ENDPOINT"] is { } localEndpoint) llmOptions.Local.Endpoint = localEndpoint;
if (builder.Configuration["LOCAL_LLM_MODEL"] is { } localModel) llmOptions.Local.Model = localModel;
if (builder.Configuration["CLOUDFLARE_API_TOKEN"] is { } cfToken) llmOptions.Cloud.ApiToken = cfToken;
if (builder.Configuration["CLOUDFLARE_ACCOUNT_ID"] is { } cfAccount) llmOptions.Cloud.AccountId = cfAccount;

if (builder.Configuration["FOLLOWUP_LLM_MODE"] is { } fMode) followUpLlmOptions.Mode = fMode;
if (builder.Configuration["FOLLOWUP_LOCAL_LLM_ENDPOINT"] is { } fLocalEndpoint) followUpLlmOptions.Local.Endpoint = fLocalEndpoint;
if (builder.Configuration["FOLLOWUP_LOCAL_LLM_MODEL"] is { } fLocalModel) followUpLlmOptions.Local.Model = fLocalModel;
if (builder.Configuration["FOLLOWUP_CLOUDFLARE_API_TOKEN"] is { } fCfToken) followUpLlmOptions.Cloud.ApiToken = fCfToken;
if (builder.Configuration["FOLLOWUP_CLOUDFLARE_ACCOUNT_ID"] is { } fCfAccount) followUpLlmOptions.Cloud.AccountId = fCfAccount;
if (builder.Configuration["FOLLOWUP_CLOUDFLARE_MODEL"] is { } fCfModel) followUpLlmOptions.Cloud.Model = fCfModel;

if (builder.Configuration["EMBEDDING_PROVIDER"] is { } embProv) embeddingOptions.Provider = embProv;
if (builder.Configuration["JINA_API_KEY"] is { } jKey) jinaOptions.ApiKey = jKey;
if (builder.Configuration["VOYAGE_API_KEY"] is { } vKey) voyageOptions.ApiKey = vKey;

if (builder.Configuration["SUPABASE_MODE"] is { } sMode) supabaseOptions.Mode = sMode;
if (builder.Configuration["SUPABASE_URL"] is { } sUrl) { if (supabaseOptions.IsCloud) supabaseOptions.Cloud.Url = sUrl; else supabaseOptions.Local.Url = sUrl; }
if (builder.Configuration["SUPABASE_ANON_KEY"] is { } sKey) { if (supabaseOptions.IsCloud) supabaseOptions.Cloud.AnonKey = sKey; else supabaseOptions.Local.AnonKey = sKey; }
if (builder.Configuration["SUPABASE_DB_CONNECTION_STRING"] is { } sConn) { if (supabaseOptions.IsCloud) supabaseOptions.Cloud.ConnectionString = sConn; else supabaseOptions.Local.ConnectionString = sConn; }

if (builder.Configuration["LOGTO_MODE"] is { } lgMode) logtoOptions.Mode = lgMode;
if (builder.Configuration["LOGTO_ENDPOINT"] is { } lgEndpoint) { if (logtoOptions.IsCloud) logtoOptions.Cloud.Endpoint = lgEndpoint; else logtoOptions.Local.Endpoint = lgEndpoint; }
if (builder.Configuration["LOGTO_APP_ID"] is { } lgAppId) { if (logtoOptions.IsCloud) logtoOptions.Cloud.AppId = lgAppId; else logtoOptions.Local.AppId = lgAppId; }
if (builder.Configuration["LOGTO_M2M_APP_ID"] is { } lgM2mId) { if (logtoOptions.IsCloud) logtoOptions.Cloud.M2MAppId = lgM2mId; else logtoOptions.Local.M2MAppId = lgM2mId; }
if (builder.Configuration["LOGTO_M2M_SECRET"] is { } lgM2mSec) { if (logtoOptions.IsCloud) logtoOptions.Cloud.M2MAppSecret = lgM2mSec; else logtoOptions.Local.M2MAppSecret = lgM2mSec; }
if (builder.Configuration["LOGTO_API_RESOURCE"] is { } lgRes) { if (logtoOptions.IsCloud) logtoOptions.Cloud.ApiResource = lgRes; else logtoOptions.Local.ApiResource = lgRes; }
if (builder.Configuration["LOGTO_MAGIC_LINK_BASE_URL"] is { } lgMagicUrl) { if (logtoOptions.IsCloud) logtoOptions.Cloud.MagicLinkBaseUrl = lgMagicUrl; else logtoOptions.Local.MagicLinkBaseUrl = lgMagicUrl; }
if (builder.Configuration["LOGTO_WEBHOOK_SECRET"] is { } lgWebhookSec) { if (logtoOptions.IsCloud) logtoOptions.Cloud.WebhookSecret = lgWebhookSec; else logtoOptions.Local.WebhookSecret = lgWebhookSec; }

if (builder.Configuration["CALCOM_API_KEY"] is { } calApiKey) calComOptions.ApiKey = calApiKey;
if (builder.Configuration["CALCOM_EVENT_TYPE_ID"] is { } calEventId && int.TryParse(calEventId, out var parsedEventId)) calComOptions.EventTypeId = parsedEventId;
if (builder.Configuration["CALCOM_USERNAME"] is { } calUsername) calComOptions.Username = calUsername;
if (builder.Configuration["CALCOM_DEFAULT_TIMEZONE"] is { } calTz) calComOptions.DefaultTimeZone = calTz;

builder.Services.AddSingleton(telemetryOptions);
builder.Services.AddSingleton(llmOptions);
builder.Services.AddSingleton(followUpLlmOptions);
builder.Services.AddSingleton(embeddingOptions);
builder.Services.AddSingleton(jinaOptions);
builder.Services.AddSingleton(voyageOptions);
builder.Services.AddSingleton(supabaseOptions);
builder.Services.AddSingleton(logtoOptions);
builder.Services.AddSingleton(calComOptions);
builder.Services.AddSingleton<IFollowUpAgent, FollowUpAgent>();
builder.Services.AddHttpClient();

// 2. Configure OpenTelemetry (Local Docker vs Cloud Mode)
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: ResumeAssistantTelemetry.ServiceName, serviceVersion: "1.0.0")
    .AddAttributes(new Dictionary<string, object>
    {
        ["service.name"] = ResumeAssistantTelemetry.ServiceName,
        ["deployment.environment"] = builder.Environment.EnvironmentName,
        ["service.instance.id"] = Environment.MachineName
    });

var openTelemetryBuilder = builder.Services.AddOpenTelemetry();

string? targetOtlpBase = null;
string? authHeader = null;

if (telemetryOptions.IsCloud && telemetryOptions.Cloud.IsConfigured)
{
    targetOtlpBase = telemetryOptions.Cloud.OtlpEndpoint.TrimEnd('/');
    authHeader = $"Authorization=Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{telemetryOptions.Cloud.InstanceId}:{telemetryOptions.Cloud.ApiToken}"))}";
}
else if (telemetryOptions.IsLocal)
{
    targetOtlpBase = telemetryOptions.Local.OtlpEndpoint.TrimEnd('/');
}

if (!telemetryOptions.IsDisabled && !string.IsNullOrWhiteSpace(targetOtlpBase))
{
    // Setup Tracing (Tempo / APM)
    openTelemetryBuilder.WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(resourceBuilder)
            .AddSource(ResumeAssistantTelemetry.ActivitySourceName)
            .AddSource("Microsoft.Extensions.AI.*")
            .AddSource("Microsoft.Agents.AI.*")
            .AddSource("VoyageAI.*")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri($"{targetOtlpBase}/v1/traces");
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                if (!string.IsNullOrWhiteSpace(authHeader))
                {
                    options.Headers = authHeader;
                }
            });
    });

    // Setup Metrics (Mimir / Prometheus)
    openTelemetryBuilder.WithMetrics(metrics =>
    {
        metrics
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(ResumeAssistantTelemetry.MeterName)
            .AddMeter("Microsoft.Extensions.AI.*")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri($"{targetOtlpBase}/v1/metrics");
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                if (!string.IsNullOrWhiteSpace(authHeader))
                {
                    options.Headers = authHeader;
                }
            });
    });

    // Setup Structured Logging (Loki)
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.SetResourceBuilder(resourceBuilder);
        logging.IncludeScopes = true;
        logging.IncludeFormattedMessage = true;
        logging.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri($"{targetOtlpBase}/v1/logs");
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                options.Headers = authHeader;
            }
        });
    });
}

// 3. Add Core Services & AG-UI
builder.Services.AddControllers();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Validates RS256 JWTs directly against Logto's OpenID Discovery & JWKS endpoint
        options.Authority = $"{logtoOptions.GetResolvedEndpoint().TrimEnd('/')}/oidc";
        options.Audience = logtoOptions.GetResolvedApiResource();
        options.RequireHttpsMetadata = logtoOptions.IsCloud; // Local Logto might be HTTP
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{logtoOptions.GetResolvedEndpoint().TrimEnd('/')}/oidc",
            ValidateAudience = true,
            ValidAudience = logtoOptions.GetResolvedApiResource(),
            ValidateLifetime = true
        };

        // Allow token to be passed via query string for WebSocket/SignalR connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/agentic_chat"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<IDisposableEmailValidator>();
                var email = context.Principal?.FindFirst("email")?.Value ??
                            context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                if (!string.IsNullOrEmpty(email))
                {
                    var result = validator.ValidateEmail(email);
                    if (result.IsDisposable)
                    {
                        context.HttpContext.Response.Headers["X-Blocked-Reason"] = "DisposableEmail";
                        context.Fail("Disposable email addresses are not permitted.");
                    }
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Add(DigitalTwinJsonSerializerContext.Default);
    options.SerializerOptions.TypeInfoResolverChain.Add(new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver());
});
builder.Services.AddAGUI();
builder.Services.AddSingleton<IDisposableEmailValidator, DisposableEmailValidator>();
builder.Services.AddHttpClient<ILogtoManagementService, LogtoManagementService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

// 4. Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            origin.StartsWith("http://localhost:") ||
            origin.StartsWith("https://localhost:") ||
            origin.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase) ||
            origin.EndsWith(".snapdeploy.dev", StringComparison.OrdinalIgnoreCase) ||
            origin.EndsWith(".outplane.app", StringComparison.OrdinalIgnoreCase))
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("X-Blocked-Reason")
            .AllowCredentials();
    });
});

// 5. Configure Supabase PostgreSQL DataSource (Local vs Cloud)
var activeConnectionString = supabaseOptions.GetResolvedConnectionString();
if (!string.IsNullOrWhiteSpace(activeConnectionString))
{
    try
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(activeConnectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();
        builder.Services.AddSingleton(dataSource);
    }
    catch
    {
        // Fallback without database if connection string is invalid
    }
}

// 6. Register Embedding Generator (Jina AI vs Voyage AI)
if (embeddingOptions.IsJina && jinaOptions.IsConfigured)
{
    builder.Services.AddHttpClient("JinaAI", client =>
    {
        client.BaseAddress = new Uri("https://api.jina.ai/v1/");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
    {
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = factory.CreateClient("JinaAI");
        return new JinaEmbeddingGenerator(
            apiKey: jinaOptions.ApiKey!,
            model: jinaOptions.Model,
            defaultTask: "retrieval.query",
            dimensions: jinaOptions.Dimensions,
            httpClient: httpClient);
    });
}
else
{
    builder.Services.AddVoyageAI(o =>
    {
        if (!string.IsNullOrWhiteSpace(voyageOptions.ApiKey) && !voyageOptions.ApiKey.StartsWith("YOUR_"))
        {
            o.ApiKey = voyageOptions.ApiKey;
        }
        else
        {
            o.ApiKey = "pa-placeholder-fallback-key";
        }
    });

    builder.Services.AddVoyageEmbeddingGenerator(o =>
    {
        o.Model = voyageOptions.EmbeddingModel;
        o.InputType = VoyageAI.Models.InputType.Query;
    });
}

// Register RAG Searcher, Audit Service & Cal.com Service
builder.Services.AddSingleton<SupabaseRagSearcher>();
builder.Services.AddSingleton<RecruiterAuditService>();

builder.Services.AddHttpClient<ICalComService, CalComService>(client =>
{
    client.BaseAddress = new Uri(calComOptions.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// 7. Register Digital Twin Agent IChatClient
builder.Services.AddChatClient(sp =>
{
    var l = sp.GetRequiredService<ILogger<Program>>();
    var lf = sp.GetRequiredService<ILoggerFactory>();
    var rs = sp.GetRequiredService<SupabaseRagSearcher>();
    var cs = sp.GetRequiredService<ICalComService>();
    var vo = sp.GetRequiredService<VoyageAiOptions>();
    var llmOpt = sp.GetRequiredService<LlmOptions>();

    IChatClient baseClient = LlmChatClientFactory.CreateChatClient(llmOpt, l);
    return DigitalTwinAgentFactory.CreateAgent(baseClient, rs, cs, vo, null, lf);
});

var app = builder.Build();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    runtime = ".NET 10",
    service = "ResumeAssistant.Api",
    authProvider = "Logto Magic Link (Passwordless One-Time Token)",
    logtoMode = logtoOptions.Mode,
    logtoEndpoint = logtoOptions.GetResolvedEndpoint(),
    logtoAppId = logtoOptions.GetResolvedAppId(),
    logtoM2MConfigured = logtoOptions.IsCloud ? logtoOptions.Cloud.IsM2MConfigured : true,
    llmMode = llmOptions.Mode,
    llmProvider = llmOptions.IsLocal ? $"LM Studio ({llmOptions.Local.Model})" : $"Cloudflare Workers AI ({llmOptions.Cloud.Model})",
    llmEndpoint = llmOptions.IsLocal ? llmOptions.Local.Endpoint : llmOptions.Cloud.GetResolvedBaseUrl(),
    followupLlmMode = followUpLlmOptions.Mode,
    followupLlmProvider = followUpLlmOptions.IsLocal ? $"LM Studio ({followUpLlmOptions.Local.Model})" : $"Cloudflare Workers AI ({followUpLlmOptions.Cloud.Model})",
    embeddingProvider = embeddingOptions.IsJina && jinaOptions.IsConfigured ? $"Jina AI ({jinaOptions.Model})" : $"Voyage AI ({voyageOptions.EmbeddingModel})",
    supabaseMode = supabaseOptions.Mode,
    supabaseUrl = supabaseOptions.GetResolvedUrl(),
    calComConfigured = calComOptions.IsConfigured,
    calComUser = calComOptions.Username,
    calComEventTypeId = calComOptions.EventTypeId,
    telemetryMode = telemetryOptions.Mode,
    telemetryEndpoint = targetOtlpBase ?? "None",
    timestamp = DateTimeOffset.UtcNow
}));

// Map CopilotKit runtime info discovery endpoints
var runtimeInfo = new
{
    version = "1.0.0",
    actions = Array.Empty<object>(),
    agents = new Dictionary<string, object>
    {
        ["default"] = new
        {
            name = "default",
            description = "Ankit Sarkar's Digital Twin Agent (Microsoft Agent Framework)"
        },
        ["agentic_chat"] = new
        {
            name = "agentic_chat",
            description = "Ankit Sarkar's Digital Twin Agent (Microsoft Agent Framework)"
        },
        ["followup_agent"] = new
        {
            name = "followup_agent",
            description = "Recruiter Follow-up & Actionable Suggestions Agent (Independent LLM)"
        }
    }
};

// Helper to sanitize incoming AG-UI message arrays by stripping non-conversational reasoning/activity scratchpad roles
static string SanitizeAguiRunInputJson(string rawJson)
{
    try
    {
        var node = JsonNode.Parse(rawJson);
        if (node is JsonObject obj && obj.TryGetPropertyValue("messages", out var messagesNode) && messagesNode is JsonArray arr)
        {
            for (int i = arr.Count - 1; i >= 0; i--)
            {
                var item = arr[i];
                if (item is JsonObject msgObj && msgObj.TryGetPropertyValue("role", out var roleVal))
                {
                    var roleStr = roleVal?.GetValue<string>();
                    if (string.Equals(roleStr, "reasoning", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(roleStr, "activity", StringComparison.OrdinalIgnoreCase))
                    {
                        arr.RemoveAt(i);
                    }
                }
            }
            return node.ToJsonString();
        }
    }
    catch
    {
        // fallback to original if parsing fails
    }
    return rawJson;
}

// Middleware to bridge CopilotKit runtime envelope protocol with AG-UI endpoint and sanitize message history
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/agentic_chat") && HttpMethods.IsPost(context.Request.Method))
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("method", out var methodProp))
                {
                    var method = methodProp.GetString();
                    if (string.Equals(method, "info", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(runtimeInfo);
                        return;
                    }

                    if (string.Equals(method, "agent/run", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("body", out var bodyProp))
                    {
                        var unwrappedJson = bodyProp.GetRawText();
                        unwrappedJson = SanitizeAguiRunInputJson(unwrappedJson);
                        var bytes = Encoding.UTF8.GetBytes(unwrappedJson);
                        context.Request.Body = new MemoryStream(bytes);
                        context.Request.ContentLength = bytes.Length;
                    }
                }
                else
                {
                    var sanitized = SanitizeAguiRunInputJson(body);
                    if (!string.Equals(sanitized, body, StringComparison.Ordinal))
                    {
                        var bytes = Encoding.UTF8.GetBytes(sanitized);
                        context.Request.Body = new MemoryStream(bytes);
                        context.Request.ContentLength = bytes.Length;
                    }
                }
            }
            catch
            {
                context.Request.Body.Position = 0;
            }
        }
    }

    await next();
});

app.MapGet("/agentic_chat/info", () => Results.Ok(runtimeInfo)).AllowAnonymous();
app.MapPost("/agentic_chat/info", () => Results.Ok(runtimeInfo)).AllowAnonymous();
app.MapGet("/info", () => Results.Ok(runtimeInfo)).AllowAnonymous();
app.MapPost("/info", () => Results.Ok(runtimeInfo)).AllowAnonymous();

// Map the AG-UI agent streaming endpoint with native ASP.NET Core Authorization
app.MapAGUI("/agentic_chat").RequireAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
