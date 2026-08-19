using System.Text.RegularExpressions;
using ResumeAssistant.Core.Interfaces;
using ResumeAssistant.Core.Models;

namespace ResumeAssistant.Api.Services;

/// <summary>
/// High-performance disposable / temporary email detector with built-in domain blacklist and regex heuristics.
/// </summary>
public sealed class DisposableEmailValidator : IDisposableEmailValidator
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    // Common personal webmail providers (allowed for recruiters who contact candidates from personal accounts)
    private static readonly HashSet<string> StandardProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "outlook.com", "hotmail.com", "live.com", "msn.com",
        "yahoo.com", "yahoo.co.uk", "icloud.com", "me.com", "mac.com", "proton.me", "protonmail.com",
        "zoho.com", "aol.com", "fastmail.com", "gmx.com", "mail.com"
    };

    // Curated high-volume disposable / temporary email domains to block
    private static readonly HashSet<string> DisposableDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "10minutemail.com", "10minutemail.net", "10minutemail.org", "10minmail.com", "20minutemail.com",
        "anonbox.net", "burnermail.io", "crazymailing.com", "dispostable.com", "dropmail.me",
        "emailondeck.com", "fakeinbox.com", "fakemailgenerator.com", "generator.email", "getairmail.com",
        "getnada.com", "guerrillamail.biz", "guerrillamail.com", "guerrillamail.de", "guerrillamail.net",
        "guerrillamail.org", "guerrillamailblock.com", "incognitomail.org", "inboxkitten.com", "maildrop.cc",
        "mailinator.com", "mailinator.net", "mailinator2.com", "mailnesia.com", "mailnull.com",
        "mohmal.com", "mytrashmail.com", "mytemp.email", "nada.ltd", "nada.ltd", "sharklasers.com",
        "spam4.me", "spambox.us", "spamfree24.org", "spamgourmet.com", "temp-mail.org", "tempmail.com",
        "tempmail.net", "tempmailaddress.com", "throwawaymail.com", "trashmail.com", "trashmail.net",
        "trashmail.org", "yopmail.com", "yopmail.fr", "yopmail.net", "zippymail.info", "disposablemail.com",
        "grr.la", "pokemail.net", "tempail.com", "guerrillamail.info", "armyspy.com", "cuvox.de", "dayrep.com",
        "einrot.com", "fleckens.hu", "gustr.com", "jourrapide.com", "rhyta.com", "superrito.com", "teleworm.us",
        "mvrht.com", "binkmail.com", "safetymail.info", "trashmail.ws", "mytempmail.com", "mohmal.im"
    };

    public EmailValidationResult ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return EmailValidationResult.InvalidFormat();
        }

        string trimmed = email.Trim().ToLowerInvariant();
        if (!EmailRegex.IsMatch(trimmed))
        {
            return EmailValidationResult.InvalidFormat();
        }

        string[] parts = trimmed.Split('@');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return EmailValidationResult.InvalidFormat();
        }

        string domain = parts[1];

        if (IsDisposableDomain(domain))
        {
            return EmailValidationResult.DisposableBlocked(domain);
        }

        // Infer company name from domain if it's a corporate email (e.g. google.com -> Google)
        string? inferredCompany = null;
        if (!StandardProviders.Contains(domain))
        {
            string companyPart = domain.Split('.')[0];
            if (!string.IsNullOrWhiteSpace(companyPart))
            {
                inferredCompany = char.ToUpperInvariant(companyPart[0]) + (companyPart.Length > 1 ? companyPart[1..] : "");
            }
        }

        return EmailValidationResult.Success(domain, inferredCompany);
    }

    public bool IsDisposableDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return false;
        string cleanDomain = domain.Trim().ToLowerInvariant();

        if (DisposableDomains.Contains(cleanDomain))
        {
            return true;
        }

        // Subdomain check (e.g. sub.mailinator.com)
        return DisposableDomains.Any(d => cleanDomain.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
    }
}
