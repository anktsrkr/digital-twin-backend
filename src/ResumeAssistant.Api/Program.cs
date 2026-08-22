using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using AGUI.Server;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using MongoDB.Driver;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ResumeAssistant.Api.Agent;
using ResumeAssistant.Api.Configuration;
using ResumeAssistant.Api.Extensions;
using ResumeAssistant.Api.Services;
using ResumeAssistant.Api.Telemetry;
using ResumeAssistant.Core.Interfaces;
using ResumeAssistant.Core.Models;
using ResumeAssistant.Core.Services;
using VoyageAI;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel limits (64 KB max body to prevent DOS payload flooding)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 64 * 1024;
});

// 1. Configure strongly typed options
var telemetryOptions = builder.Configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();
var llmOptions = builder.Configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
var followUpLlmOptions = builder.Configuration.GetSection(FollowUpLlmOptions.SectionName).Get<FollowUpLlmOptions>() ?? new FollowUpLlmOptions();
var embeddingOptions = builder.Configuration.GetSection(EmbeddingOptions.SectionName).Get<EmbeddingOptions>() ?? new EmbeddingOptions();
var jinaOptions = builder.Configuration.GetSection(JinaAiOptions.SectionName).Get<JinaAiOptions>() ?? new JinaAiOptions();
var voyageOptions = builder.Configuration.GetSection(VoyageAiOptions.SectionName).Get<VoyageAiOptions>() ?? new VoyageAiOptions();
var mongoOptions = builder.Configuration.GetSection(MongoDbOptions.SectionName).Get<MongoDbOptions>() ?? new MongoDbOptions();
var clerkOptions = builder.Configuration.GetSection(ClerkOptions.SectionName).Get<ClerkOptions>() ?? new ClerkOptions();
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
if (builder.Configuration["CLOUDFLARE_MODEL"] is { } cfModel) llmOptions.Cloud.Model = cfModel;

if (builder.Configuration["FOLLOWUP_LLM_MODE"] is { } fMode) followUpLlmOptions.Mode = fMode;
if (builder.Configuration["FOLLOWUP_LOCAL_LLM_ENDPOINT"] is { } fLocalEndpoint) followUpLlmOptions.Local.Endpoint = fLocalEndpoint;
if (builder.Configuration["FOLLOWUP_LOCAL_LLM_MODEL"] is { } fLocalModel) followUpLlmOptions.Local.Model = fLocalModel;
if (builder.Configuration["FOLLOWUP_CLOUDFLARE_API_TOKEN"] is { } fCfToken) followUpLlmOptions.Cloud.ApiToken = fCfToken;
if (builder.Configuration["FOLLOWUP_CLOUDFLARE_ACCOUNT_ID"] is { } fCfAccount) followUpLlmOptions.Cloud.AccountId = fCfAccount;
if (builder.Configuration["FOLLOWUP_CLOUDFLARE_MODEL"] is { } fCfModel) followUpLlmOptions.Cloud.Model = fCfModel;

if (builder.Configuration["EMBEDDING_PROVIDER"] is { } embProv) embeddingOptions.Provider = embProv;
if (builder.Configuration["JINA_API_KEY"] is { } jKey) jinaOptions.ApiKey = jKey;
if (builder.Configuration["VOYAGE_API_KEY"] is { } vKey) voyageOptions.ApiKey = vKey;

if (builder.Configuration["MONGODB_MODE"] is { } mMode) mongoOptions.Mode = mMode;
if (builder.Configuration["MONGODB_CONNECTION_STRING"] is { } mConn) { if (mongoOptions.IsCloud) mongoOptions.Cloud.ConnectionString = mConn; else mongoOptions.Local.ConnectionString = mConn; }
if (builder.Configuration["MONGODB_DATABASE"] is { } mDb) mongoOptions.DatabaseName = mDb;

if (builder.Configuration["CLERK_ISSUER"] is { } clIssuer) clerkOptions.Issuer = clIssuer;
if (builder.Configuration["CLERK_PUBLISHABLE_KEY"] is { } clPubKey) clerkOptions.PublishableKey = clPubKey;
if (builder.Configuration["CLERK_SECRET_KEY"] is { } clSecKey) clerkOptions.SecretKey = clSecKey;
if (builder.Configuration["CLERK_WEBHOOK_SECRET"] is { } clWhSec) clerkOptions.WebhookSecret = clWhSec;
if (builder.Configuration["CLERK_AUDIENCE"] is { } clAud) clerkOptions.Audience = clAud;

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
builder.Services.AddSingleton(mongoOptions);
builder.Services.AddSingleton(clerkOptions);
builder.Services.AddSingleton(calComOptions);
builder.Services.AddSingleton<IFollowUpAgent, FollowUpAgent>();
builder.Services.AddSingleton<IDailyQuotaService, MongoDbDailyQuotaService>();
builder.Services.AddMemoryCache();
builder.Services.AddAppRateLimiting();
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Validates RS256 JWTs against Clerk's OpenID Discovery & JWKS endpoint
        var authority = clerkOptions.Issuer.TrimEnd('/');
        options.Authority = authority;
        options.RequireHttpsMetadata = authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authority,
            ValidateAudience = !string.IsNullOrWhiteSpace(clerkOptions.Audience),
            ValidAudience = clerkOptions.Audience,
            ValidateLifetime = true
        };

        // Allow token to be passed via query string for WebSocket/SignalR/AG-UI connections
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
builder.Services.AddSingleton<IDisposableEmailValidator, DisposableEmailValidator>();
builder.Services.AddHttpClient<IClerkManagementService, ClerkManagementService>(client =>
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
            origin.EndsWith(".siteasp.net", StringComparison.OrdinalIgnoreCase) ||
            origin.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase) ||
            origin.EndsWith(".netlify.app", StringComparison.OrdinalIgnoreCase) ||
            origin.EndsWith(".snapdeploy.dev", StringComparison.OrdinalIgnoreCase) ||
            origin.EndsWith(".outplane.app", StringComparison.OrdinalIgnoreCase))
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("X-Blocked-Reason")
            .AllowCredentials();
    });
});

// 5. Configure MongoDB Client & Database (Local vs Atlas Cloud)
var activeMongoConn = mongoOptions.GetResolvedConnectionString();
if (!string.IsNullOrWhiteSpace(activeMongoConn))
{
    try
    {
        var settings = MongoClientSettings.FromConnectionString(activeMongoConn);
        settings.MaxConnectionPoolSize = 25;
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(10);
        var mongoClient = new MongoClient(settings);
        var mongoDatabase = mongoClient.GetDatabase(mongoOptions.GetResolvedDatabaseName());
        builder.Services.AddSingleton<IMongoClient>(mongoClient);
        builder.Services.AddSingleton<IMongoDatabase>(mongoDatabase);
    }
    catch
    {
        // Fallback without database if connection string is invalid
    }
}

// 6. Register Embedding Generator (Jina AI 1024-dim default vs Voyage AI)
if (embeddingOptions.IsJina && jinaOptions.IsConfigured)
{
    builder.Services.AddHttpClient("JinaAI", client =>
    {
        var baseUrl = string.IsNullOrWhiteSpace(jinaOptions.BaseUrl)
            ? "https://api.jina.ai/v1/"
            : (jinaOptions.BaseUrl.TrimEnd('/') + "/");
        client.BaseAddress = new Uri(baseUrl);
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
            httpClient: httpClient,
            baseUrl: jinaOptions.BaseUrl);
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

// Register MongoDB RAG Searcher, Chat History Provider & Cal.com Service
builder.Services.AddSingleton<MongoDbRagSearcher>();
builder.Services.AddSingleton<MongoDbChatHistoryProvider>();

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
    var rs = sp.GetRequiredService<MongoDbRagSearcher>();
    var cs = sp.GetRequiredService<ICalComService>();
    var hp = sp.GetRequiredService<MongoDbChatHistoryProvider>();
    var dq = sp.GetRequiredService<IDailyQuotaService>();
    var hca = sp.GetRequiredService<IHttpContextAccessor>();
    var vo = sp.GetRequiredService<VoyageAiOptions>();
    var llmOpt = sp.GetRequiredService<LlmOptions>();

    IChatClient baseClient = LlmChatClientFactory.CreateChatClient(llmOpt, l);
    return DigitalTwinAgentFactory.CreateAgent(baseClient, rs, cs, hp, dq, hca, vo, null, lf);
});

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

var app = builder.Build();

app.UseCors("AllowFrontend");
app.UseRateLimiter();

// Middleware to bridge CopilotKit runtime envelope protocol with AG-UI endpoint, sanitize message history, and serve info discovery anonymously
// CRITICAL: MUST execute before UseAuthentication & UseAuthorization so CopilotKit info discovery never returns 401 Unauthorized.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    // 1. Direct discovery endpoints (GET/POST /agentic_chat/info, /info)
    if (path.Equals("/agentic_chat/info", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/info", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(runtimeInfo);
        return;
    }

    // 2. CopilotKit envelope protocol on /agentic_chat
    if (path.StartsWithSegments("/agentic_chat") && HttpMethods.IsPost(context.Request.Method))
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

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    runtime = ".NET 10",
    service = "ResumeAssistant.Api",
    authProvider = "Clerk Authentication (RS256 Session JWT)",
    clerkIssuer = clerkOptions.Issuer,
    clerkConfigured = clerkOptions.IsConfigured,
    llmMode = llmOptions.Mode,
    llmProvider = llmOptions.IsLocal ? $"LM Studio ({llmOptions.Local.Model})" : $"Cloudflare Workers AI ({llmOptions.Cloud.Model})",
    llmEndpoint = llmOptions.IsLocal ? llmOptions.Local.Endpoint : llmOptions.Cloud.GetResolvedBaseUrl(),
    followupLlmMode = followUpLlmOptions.Mode,
    followupLlmProvider = followUpLlmOptions.IsLocal ? $"LM Studio ({followUpLlmOptions.Local.Model})" : $"Cloudflare Workers AI ({followUpLlmOptions.Cloud.Model})",
    embeddingProvider = embeddingOptions.IsJina && jinaOptions.IsConfigured ? $"Jina AI ({jinaOptions.Model})" : $"Voyage AI ({voyageOptions.EmbeddingModel})",
    mongoDbMode = mongoOptions.Mode,
    mongoDbDatabase = mongoOptions.GetResolvedDatabaseName(),
    threadPersistence = "MongoDB user_threads (Microsoft Agent Framework)",
    calComConfigured = calComOptions.IsConfigured,
    calComUser = calComOptions.Username,
    calComEventTypeId = calComOptions.EventTypeId,
    telemetryMode = telemetryOptions.Mode,
    telemetryEndpoint = targetOtlpBase ?? "None",
    timestamp = DateTimeOffset.UtcNow
})).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AnonPolicy);

app.MapGet("/", () => Results.Ok(new
{
    status = "healthy",
    service = "ResumeAssistant.Api",
    version = "1.0.0",
    timestamp = DateTime.UtcNow
})).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AnonPolicy);

app.MapGet("/agentic_chat/info", () => Results.Ok(runtimeInfo)).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AnonPolicy);
app.MapPost("/agentic_chat/info", () => Results.Ok(runtimeInfo)).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AnonPolicy);
app.MapGet("/info", () => Results.Ok(runtimeInfo)).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AnonPolicy);
app.MapPost("/info", () => Results.Ok(runtimeInfo)).AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.AnonPolicy);

var chatClient = app.Services.GetRequiredService<IChatClient>();
var agent = chatClient.AsAIAgent();

// Map the AG-UI agent streaming endpoint with native ASP.NET Core Authorization & Rate Limiting
app.MapAGUIServer("/agentic_chat", agent).RequireAuthorization().RequireRateLimiting(RateLimitingExtensions.ChatPolicy);

app.MapControllers();

app.Run();

public partial class Program;
