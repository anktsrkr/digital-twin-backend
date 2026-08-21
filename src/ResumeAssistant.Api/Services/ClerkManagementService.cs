using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ResumeAssistant.Api.Configuration;

namespace ResumeAssistant.Api.Services;

public interface IClerkManagementService
{
    Task<bool> BanUserAsync(string userId, string? reason = null, CancellationToken ct = default);
    Task<bool> UnbanUserAsync(string userId, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(string userId, CancellationToken ct = default);
}

public sealed class ClerkManagementService : IClerkManagementService
{
    private readonly HttpClient _httpClient;
    private readonly ClerkOptions _clerkOptions;
    private readonly ILogger<ClerkManagementService> _logger;

    public ClerkManagementService(
        HttpClient httpClient,
        ClerkOptions clerkOptions,
        ILogger<ClerkManagementService> logger)
    {
        _httpClient = httpClient;
        _clerkOptions = clerkOptions;
        _logger = logger;
    }

    private void SetAuthorizationHeader(HttpRequestMessage request)
    {
        var secret = _clerkOptions.SecretKey;
        if (!string.IsNullOrWhiteSpace(secret) && !secret.StartsWith("YOUR_"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        }
    }

    public async Task<bool> BanUserAsync(string userId, string? reason = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Cannot ban user: userId is null or empty.");
            return false;
        }

        try
        {
            var requestUrl = $"https://api.clerk.com/v1/users/{Uri.EscapeDataString(userId)}/ban";
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            SetAuthorizationHeader(request);

            var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully banned Clerk user {UserId}. Reason: {Reason}", userId, reason ?? "N/A");
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to ban Clerk user {UserId}. Status: {StatusCode}, Body: {Body}", userId, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while attempting to ban Clerk user {UserId}.", userId);
            return false;
        }
    }

    public async Task<bool> UnbanUserAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        try
        {
            var requestUrl = $"https://api.clerk.com/v1/users/{Uri.EscapeDataString(userId)}/unban";
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            SetAuthorizationHeader(request);

            var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully unbanned Clerk user {UserId}.", userId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to unban Clerk user {UserId}. Status: {StatusCode}, Body: {Body}", userId, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while attempting to unban Clerk user {UserId}.", userId);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        try
        {
            var requestUrl = $"https://api.clerk.com/v1/users/{Uri.EscapeDataString(userId)}";
            using var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl);
            SetAuthorizationHeader(request);

            var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted Clerk user {UserId}.", userId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to delete Clerk user {UserId}. Status: {StatusCode}, Body: {Body}", userId, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while attempting to delete Clerk user {UserId}.", userId);
            return false;
        }
    }
}
