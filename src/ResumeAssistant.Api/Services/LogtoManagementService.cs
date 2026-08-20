using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ResumeAssistant.Api.Configuration;

namespace ResumeAssistant.Api.Services;

public interface ILogtoManagementService
{
    Task<bool> SuspendUserAsync(string userId, string reason, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(string userId, CancellationToken ct = default);
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}

public sealed class LogtoManagementService : ILogtoManagementService
{
    private readonly HttpClient _httpClient;
    private readonly LogtoOptions _logtoOptions;
    private readonly ILogger<LogtoManagementService> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _cachedAccessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public LogtoManagementService(
        HttpClient httpClient,
        LogtoOptions logtoOptions,
        ILogger<LogtoManagementService> logger)
    {
        _httpClient = httpClient;
        _logtoOptions = logtoOptions;
        _logger = logger;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (_cachedAccessToken != null && DateTimeOffset.UtcNow.AddMinutes(2) < _tokenExpiresAt)
        {
            return _cachedAccessToken;
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedAccessToken != null && DateTimeOffset.UtcNow.AddMinutes(2) < _tokenExpiresAt)
            {
                return _cachedAccessToken;
            }

            var m2mAppId = _logtoOptions.GetResolvedM2MAppId();
            var m2mSecret = _logtoOptions.GetResolvedM2MSecret();
            var endpoint = _logtoOptions.GetResolvedEndpoint().TrimEnd('/');
            var resource = _logtoOptions.GetResolvedManagementApiResource();

            if (string.IsNullOrWhiteSpace(m2mAppId) || m2mAppId.StartsWith("YOUR_") ||
                string.IsNullOrWhiteSpace(m2mSecret) || m2mSecret.StartsWith("YOUR_"))
            {
                _logger.LogWarning("Logto M2M credentials are not configured. Cannot obtain Management API token.");
                return null;
            }

            var tokenEndpoint = $"{endpoint}/oidc/token";
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = m2mAppId,
                    ["client_secret"] = m2mSecret,
                    ["resource"] = resource,
                    ["scope"] = "all"
                })
            };

            var response = await _httpClient.SendAsync(tokenRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to acquire Logto Management API token. Status: {StatusCode}, Body: {Body}", response.StatusCode, errorBody);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var node = JsonNode.Parse(json);
            var token = node?["access_token"]?.GetValue<string>();
            var expiresIn = node?["expires_in"]?.GetValue<int>() ?? 3600;

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Logto Management API token response did not contain 'access_token'. Response: {Body}", json);
                return null;
            }

            _cachedAccessToken = token;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            _logger.LogInformation("Successfully acquired Logto Management API access token (expires in {ExpiresIn}s).", expiresIn);

            return _cachedAccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while requesting Logto Management API token.");
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task<bool> SuspendUserAsync(string userId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Cannot suspend user: userId is null or empty.");
            return false;
        }

        var token = await GetAccessTokenAsync(ct);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("Cannot suspend user {UserId}: failed to obtain Logto Management API token.", userId);
            return false;
        }

        try
        {
            var endpoint = _logtoOptions.GetResolvedEndpoint().TrimEnd('/');
            // Logto Management API: PATCH /api/users/{id}/is-suspended or PATCH /api/users/{id}
            var requestUrl = $"{endpoint}/api/users/{Uri.EscapeDataString(userId)}/is-suspended";
            var payload = JsonSerializer.Serialize(new { isSuspended = true });

            var request = new HttpRequestMessage(HttpMethod.Patch, requestUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully suspended Logto user {UserId}. Reason: {Reason}", userId, reason);
                return true;
            }

            // Fallback to PATCH /api/users/{id} if /is-suspended endpoint returns 404
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var fallbackUrl = $"{endpoint}/api/users/{Uri.EscapeDataString(userId)}";
                var fallbackRequest = new HttpRequestMessage(HttpMethod.Patch, fallbackUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                fallbackRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var fallbackResponse = await _httpClient.SendAsync(fallbackRequest, ct);
                if (fallbackResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully suspended Logto user {UserId} via fallback endpoint. Reason: {Reason}", userId, reason);
                    return true;
                }
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to suspend Logto user {UserId}. Status: {StatusCode}, Body: {Body}", userId, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while attempting to suspend Logto user {UserId}.", userId);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        var token = await GetAccessTokenAsync(ct);
        if (string.IsNullOrEmpty(token)) return false;

        try
        {
            var endpoint = _logtoOptions.GetResolvedEndpoint().TrimEnd('/');
            var requestUrl = $"{endpoint}/api/users/{Uri.EscapeDataString(userId)}";

            var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted Logto user {UserId}.", userId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to delete Logto user {UserId}. Status: {StatusCode}, Body: {Body}", userId, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while attempting to delete Logto user {UserId}.", userId);
            return false;
        }
    }
}
