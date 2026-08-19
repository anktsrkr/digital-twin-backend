using ResumeAssistant.Core.Models;

namespace ResumeAssistant.Core.Interfaces;

/// <summary>
/// Service contract to detect and block disposable / temporary recruiter emails.
/// </summary>
public interface IDisposableEmailValidator
{
    /// <summary>
    /// Checks whether an email address is valid, formatted properly, and not belonging to a disposable email provider.
    /// </summary>
    EmailValidationResult ValidateEmail(string email);

    /// <summary>
    /// Returns true if the domain is known to be a temporary or disposable email provider.
    /// </summary>
    bool IsDisposableDomain(string domain);
}
