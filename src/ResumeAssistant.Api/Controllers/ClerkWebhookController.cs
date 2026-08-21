using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using ResumeAssistant.Api.Configuration;
using ResumeAssistant.Api.Services;
using ResumeAssistant.Core.Interfaces;

namespace ResumeAssistant.Api.Controllers;

[ApiController]
[Route("api/webhooks/clerk")]
public sealed class ClerkWebhookController : ControllerBase
{
    private readonly IClerkManagementService _clerkManagementService;
    private readonly IDisposableEmailValidator _emailValidator;
    private readonly ClerkOptions _clerkOptions;
    private readonly ILogger<ClerkWebhookController> _logger;

    public ClerkWebhookController(
        IClerkManagementService clerkManagementService,
        IDisposableEmailValidator emailValidator,
        ClerkOptions clerkOptions,
        ILogger<ClerkWebhookController> logger)
    {
        _clerkManagementService = clerkManagementService;
        _emailValidator = emailValidator;
        _clerkOptions = clerkOptions;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return BadRequest(new { error = "Empty webhook payload." });
        }

        // 1. Verify Svix Webhook Signature if secret configured
        var webhookSecret = _clerkOptions.WebhookSecret;
        if (!string.IsNullOrWhiteSpace(webhookSecret) && !webhookSecret.StartsWith("YOUR_"))
        {
            if (!Request.Headers.TryGetValue("svix-id", out var svixId) ||
                !Request.Headers.TryGetValue("svix-timestamp", out var svixTimestamp) ||
                !Request.Headers.TryGetValue("svix-signature", out var svixSignature) ||
                !VerifySvixSignature(rawBody, webhookSecret, svixId.ToString(), svixTimestamp.ToString(), svixSignature.ToString()))
            {
                _logger.LogWarning("Unauthorized Clerk webhook request: signature verification failed.");
                return Unauthorized(new { error = "Invalid webhook signature." });
            }
        }

        // 2. Parse Webhook JSON
        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(rawBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Clerk webhook payload as JSON.");
            return BadRequest(new { error = "Invalid JSON payload." });
        }

        if (rootNode is null)
        {
            return Ok(new { received = true });
        }

        var eventType = rootNode["type"]?.GetValue<string>() ?? "Unknown";
        _logger.LogInformation("Received Clerk webhook event: {EventType}", eventType);

        if (!eventType.Equals("user.created", StringComparison.OrdinalIgnoreCase) &&
            !eventType.Equals("user.updated", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { received = true, handled = false, eventType });
        }

        // 3. Extract User ID and Email
        var (userId, email) = ExtractUserCredentials(rootNode);

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogInformation("Clerk webhook event {EventType} for user {UserId} contained no email.", eventType, userId);
            return Ok(new { received = true, handled = true, action = "no_email" });
        }

        // 4. Validate Email against Disposable Domain Blacklist
        var validation = _emailValidator.ValidateEmail(email);
        if (validation.IsDisposable)
        {
            _logger.LogWarning("🚫 Clerk Webhook detected DISPOSABLE EMAIL [{Email}] for user [{UserId}]. Initiating immediate ban.", email, userId);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var banned = await _clerkManagementService.BanUserAsync(
                    userId,
                    $"Disposable email domain blocked ({validation.Domain})",
                    ct);

                return Ok(new
                {
                    received = true,
                    handled = true,
                    eventType,
                    email,
                    userId,
                    isDisposable = true,
                    banned
                });
            }
        }

        return Ok(new
        {
            received = true,
            handled = true,
            eventType,
            email,
            isDisposable = false
        });
    }

    private static (string? userId, string? email) ExtractUserCredentials(JsonNode root)
    {
        string? userId = null;
        string? email = null;

        var dataObj = root["data"] as JsonObject;
        if (dataObj is not null)
        {
            userId = dataObj["id"]?.GetValue<string>();

            if (dataObj["email_addresses"] is JsonArray emailsArr && emailsArr.Count > 0)
            {
                var primaryId = dataObj["primary_email_address_id"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(primaryId))
                {
                    foreach (var item in emailsArr)
                    {
                        if (item is JsonObject emailObj &&
                            emailObj["id"]?.GetValue<string>() == primaryId)
                        {
                            email = emailObj["email_address"]?.GetValue<string>();
                            break;
                        }
                    }
                }

                email ??= emailsArr[0]?["email_address"]?.GetValue<string>();
            }
        }

        return (userId, email);
    }

    private static bool VerifySvixSignature(string payload, string secret, string msgId, string timestamp, string signatureHeader)
    {
        try
        {
            // Svix secret usually starts with "whsec_"
            var keySecret = secret.StartsWith("whsec_") ? secret[6..] : secret;
            var keyBytes = Convert.FromBase64String(keySecret);

            var toSign = $"{msgId}.{timestamp}.{payload}";
            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign));
            var expectedSignature = $"v1,{Convert.ToBase64String(hash)}";

            var signatures = signatureHeader.Split(' ');
            foreach (var sig in signatures)
            {
                if (sig.Equals(expectedSignature, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // Fallback for non-standard test secrets
            return false;
        }
    }
}
