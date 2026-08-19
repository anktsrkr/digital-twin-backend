using Microsoft.AspNetCore.Mvc;
using ResumeAssistant.Core.Interfaces;
using ResumeAssistant.Core.Models;

namespace ResumeAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IDisposableEmailValidator _emailValidator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IDisposableEmailValidator emailValidator, ILogger<AuthController> logger)
    {
        _emailValidator = emailValidator;
        _logger = logger;
    }

    /// <summary>
    /// Validates an email address and rejects disposable / temporary domains before Magic Link dispatch.
    /// </summary>
    [HttpPost("validate-email")]
    public ActionResult<EmailValidationResult> ValidateEmail([FromBody] EmailValidationRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(EmailValidationResult.InvalidFormat());
        }

        var result = _emailValidator.ValidateEmail(request.Email);
        _logger.LogInformation("Email validation result for '{Email}': Valid={IsValid}, Disposable={IsDisposable}",
            request.Email, result.IsValid, result.IsDisposable);

        return Ok(result);
    }
}
