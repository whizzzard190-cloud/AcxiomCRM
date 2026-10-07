using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid payload", Status = StatusCodes.Status400BadRequest });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
        {
            await _auditService.LogSecurityEventAsync("LoginFailure", null, "Failure", $"API: {request.Email}");
            return Unauthorized(new ProblemDetails { Title = "Invalid credentials.", Status = StatusCodes.Status401Unauthorized });
        }

        var result = await _signInManager.PasswordSignInAsync(user.UserName ?? user.Email!, request.Password, false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await _auditService.LogSecurityEventAsync("Login", user.Id, "Success", "API login successful.");
            var roles = await _userManager.GetRolesAsync(user);

            // Never return password, hash, token or sensitive fields
            return Ok(new AuthResponseDto
            {
                Email = user.Email ?? "",
                FullName = user.FullName,
                Role = roles.FirstOrDefault() ?? "User",
                Message = "Authentication successful."
            });
        }

        if (result.IsLockedOut)
        {
            await _auditService.LogSecurityEventAsync("Lockout", user.Id, "Failure", "API lockout triggered.");
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Account locked due to multiple failed login attempts.",
                Status = StatusCodes.Status403Forbidden
            });
        }

        await _auditService.LogSecurityEventAsync("LoginFailure", user.Id, "Failure", "API invalid password.");
        return Unauthorized(new ProblemDetails { Title = "Invalid credentials.", Status = StatusCodes.Status401Unauthorized });
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { Message = "Logged out successfully." });
    }
}
