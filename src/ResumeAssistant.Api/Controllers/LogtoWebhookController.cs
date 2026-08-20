using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using ResumeAssistant.Api.Configuration;
using ResumeAssistant.Api.Services;
using ResumeAssistant.Core.Interfaces;

namespace ResumeAssistant.Api.Controllers;

[ApiController]
[Route("api/webhooks/logto")]
public sealed class LogtoWebhookController : ControllerBase
{
    private readonly ILogtoManagementService _logtoManagementService;
    private readonly IDisposableEmailValidator _emailValidator;
    private readonly LogtoOptions _logtoOptions;
    private readonly ILogger<LogtoWebhookController> _logger;

    public LogtoWebhookController(
        ILogtoManagementService logtoManagementService,
        IDisposableEmailValidator emailValidator,
        LogtoOptions logtoOptions,
        ILogger<LogtoWebhookController> logger)
    {
        _logtoManagementService = logtoManagementService;
        _emailValidator = emailValidator;
        _logtoOptions = logtoOptions;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook(CancellationToken ct)
    {
        // Read raw request body
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return BadRequest(new { error = "Empty webhook payload." });
        }

        // 1. Verify Logto Webhook HMAC Signature if secret is configured
        var webhookSecret = _logtoOptions.GetResolvedWebhookSecret();
        if (!string.IsNullOrWhiteSpace(webhookSecret) && !webhookSecret.StartsWith("YOUR_"))
        {
            if (!Request.Headers.TryGetValue("logto-signature-sha-256", out var signatureHeader) ||
                !VerifySignature(rawBody, webhookSecret, signatureHeader.ToString()))
            {
                _logger.LogWarning("Unauthorized Logto webhook request: signature mismatch.");
                return Unauthorized(new { error = "Invalid webhook signature." });
            }
        }

        // 2. Parse Webhook Event JSON
        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(rawBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Logto webhook payload as JSON.");
            return BadRequest(new { error = "Invalid JSON payload." });
        }

        if (rootNode is null)
        {
            return Ok(new { received = true });
        }

        var eventName = rootNode["event"]?.GetValue<string>() ?? rootNode["type"]?.GetValue<string>() ?? "Unknown";
        _logger.LogInformation("Received Logto webhook event: {EventName}", eventName);

        // Filter events of interest: User.Created, PostRegister, User.Data.Updated
        if (!eventName.Equals("User.Created", StringComparison.OrdinalIgnoreCase) &&
            !eventName.Equals("PostRegister", StringComparison.OrdinalIgnoreCase) &&
            !eventName.Equals("User.Data.Updated", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { received = true, handled = false, eventName });
        }

        // 3. Extract User ID and Email
        var (userId, email) = ExtractUserCredentials(rootNode);

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogInformation("Logto webhook event {EventName} for user {UserId} contained no email.", eventName, userId);
            return Ok(new { received = true, handled = true, action = "no_email" });
        }

        // 4. Validate Email against Disposable Domain Blacklist
        var validation = _emailValidator.ValidateEmail(email);
        if (validation.IsDisposable)
        {
            _logger.LogWarning("🚫 Logto Webhook detected DISPOSABLE EMAIL [{Email}] for user [{UserId}]. Initiating immediate suspension.", email, userId);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var suspended = await _logtoManagementService.SuspendUserAsync(
                    userId,
                    $"Disposable email domain blocked ({validation.Domain})",
                    ct);

                return Ok(new
                {
                    received = true,
                    handled = true,
                    eventName,
                    email,
                    userId,
                    isDisposable = true,
                    suspended
                });
            }
        }

        return Ok(new
        {
            received = true,
            handled = true,
            eventName,
            email,
            isDisposable = false
        });
    }

    private static (string? userId, string? email) ExtractUserCredentials(JsonNode root)
    {
        string? userId = null;
        string? email = null;

        // Check root-level fields
        userId = root["userId"]?.GetValue<string>() ?? root["id"]?.GetValue<string>();
        email = root["email"]?.GetValue<string>() ?? root["primaryEmail"]?.GetValue<string>();

        // Check "data" sub-object
        if (root["data"] is JsonObject dataObj)
        {
            userId ??= dataObj["id"]?.GetValue<string>() ?? dataObj["userId"]?.GetValue<string>();
            email ??= dataObj["primaryEmail"]?.GetValue<string>() ?? dataObj["email"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(email) && dataObj["emails"] is JsonArray emailsArr && emailsArr.Count > 0)
            {
                email = emailsArr[0]?["address"]?.GetValue<string>() ?? emailsArr[0]?.GetValue<string>();
            }
        }

        // Check "user" sub-object
        if (root["user"] is JsonObject userObj)
        {
            userId ??= userObj["id"]?.GetValue<string>() ?? userObj["userId"]?.GetValue<string>();
            email ??= userObj["primaryEmail"]?.GetValue<string>() ?? userObj["email"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(email) && userObj["emails"] is JsonArray userEmails && userEmails.Count > 0)
            {
                email = userEmails[0]?["address"]?.GetValue<string>() ?? userEmails[0]?.GetValue<string>();
            }
        }

        return (userId, email);
    }

    private static bool VerifySignature(string payload, string secret, string signatureHeader)
    {
        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();

            // Handle potential prefix or formatting in header
            var incomingHex = signatureHeader.Trim().ToLowerInvariant();
            if (incomingHex.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            {
                incomingHex = incomingHex[7..];
            }

            var expectedBytes = Encoding.UTF8.GetBytes(expectedHex);
            var incomingBytes = Encoding.UTF8.GetBytes(incomingHex);

            return CryptographicOperations.FixedTimeEquals(expectedBytes, incomingBytes);
        }
        catch
        {
            return false;
        }
    }
}
