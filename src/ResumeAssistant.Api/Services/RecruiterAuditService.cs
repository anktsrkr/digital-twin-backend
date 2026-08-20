using Microsoft.Extensions.Logging;
using Npgsql;
using ResumeAssistant.Core.Models;

namespace ResumeAssistant.Api.Services;

public sealed class RecruiterAuditService
{
    private readonly NpgsqlDataSource? _dataSource;
    private readonly ILogger<RecruiterAuditService> _logger;

    public RecruiterAuditService(NpgsqlDataSource? dataSource, ILogger<RecruiterAuditService> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task LogInteractionAsync(
        string sessionId,
        string query,
        string response,
        List<CitationDto> citations,
        string? recruiterId = null,
        int tokensUsed = 0,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Recruiter query logged [{SessionId}]: '{Query}' (Citations: {Count})",
            sessionId, query, citations.Count);

        if (_dataSource is null || string.IsNullOrWhiteSpace(recruiterId))
        {
            return;
        }

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            const string sql = @"
                INSERT INTO public.recruiter_conversations 
                (recruiter_id, session_id, query, response, citations, tokens_used, created_at)
                VALUES 
                (@recruiter_id, @session_id, @query, @response, @citations::jsonb, @tokens_used, NOW());

                UPDATE public.recruiter_profiles 
                SET last_active_at = NOW(), total_messages = total_messages + 1
                WHERE id = @recruiter_id;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("recruiter_id", recruiterId);
            cmd.Parameters.AddWithValue("session_id", sessionId);
            cmd.Parameters.AddWithValue("query", query);
            cmd.Parameters.AddWithValue("response", response);
            cmd.Parameters.AddWithValue("citations", System.Text.Json.JsonSerializer.Serialize(citations));
            cmd.Parameters.AddWithValue("tokens_used", tokensUsed);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist recruiter interaction audit log to Supabase.");
        }
    }
}
