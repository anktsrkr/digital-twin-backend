using System.Text.Json.Serialization;

namespace ResumeAssistant.Core.Models;

public sealed class EmailValidationRequest
{
    [JsonPropertyName("email")]
    public required string Email { get; set; }
}

public sealed class EmailValidationResult
{
    [JsonPropertyName("is_valid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("is_disposable")]
    public bool IsDisposable { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("inferred_company")]
    public string? InferredCompany { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    public static EmailValidationResult Success(string domain, string? inferredCompany = null) => new()
    {
        IsValid = true,
        IsDisposable = false,
        Domain = domain,
        InferredCompany = inferredCompany,
        Message = "Valid corporate or standard email address."
    };

    public static EmailValidationResult DisposableBlocked(string domain) => new()
    {
        IsValid = false,
        IsDisposable = true,
        Domain = domain,
        Message = "Temporary or disposable email addresses are not accepted. Please use your corporate or standard email address."
    };

    public static EmailValidationResult InvalidFormat() => new()
    {
        IsValid = false,
        IsDisposable = false,
        Message = "Please enter a valid email address format (e.g. name@company.com)."
    };
}
