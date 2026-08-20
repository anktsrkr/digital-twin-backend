using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResumeAssistant.Core.Interfaces;

namespace ResumeAssistant.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IDisposableEmailValidator _emailValidator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IDisposableEmailValidator emailValidator,
        ILogger<AuthController> logger)
    {
        _emailValidator = emailValidator;
        _logger = logger;
    }

    [HttpGet("validate-email")]
    public IActionResult ValidateEmailGet([FromQuery] string? email)
    {
        return ValidateInternal(email);
    }

    [HttpPost("validate-email")]
    public IActionResult ValidateEmailPost([FromBody] ValidateEmailRequest? request)
    {
        return ValidateInternal(request?.Email);
    }

    private IActionResult ValidateInternal(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new
            {
                isAllowed = false,
                isValid = false,
                isDisposable = false,
                errorMessage = "Email address is required."
            });
        }

        var result = _emailValidator.ValidateEmail(email);

        if (result.IsDisposable)
        {
            _logger.LogWarning("Disposable email detected by validator: {Email} (Domain: {Domain})", email, result.Domain);
        }

        return Ok(new
        {
            isAllowed = result.IsValid && !result.IsDisposable,
            isValid = result.IsValid,
            isDisposable = result.IsDisposable,
            domain = result.Domain,
            company = result.InferredCompany,
            errorMessage = result.Message
        });
    }
}

public sealed record ValidateEmailRequest(string? Email);
