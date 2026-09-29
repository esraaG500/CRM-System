using Crm.Application.Abstractions;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Crm.Api.Controllers.V1;

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResultResponse(string AccessToken, int ExpiresIn, CurrentUserDto User);

[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(IAuthService auth, ICurrentUser currentUser, IWebHostEnvironment environment) : ApiControllerBase
{
    private const string RefreshCookie = "crm_refresh";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResultResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Unauthorized401("Enter your email and password.");
        }

        var outcome = await auth.LoginAsync(request.Email.Trim(), request.Password, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Complete(outcome);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultResponse>> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var token) || string.IsNullOrEmpty(token))
        {
            return Unauthorized401("Your session has expired. Please sign in again.");
        }

        var outcome = await auth.RefreshAsync(token, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Complete(outcome);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookie, out var token) && !string.IsNullOrEmpty(token))
        {
            await auth.LogoutAsync(token, ct);
        }

        Response.Cookies.Delete(RefreshCookie, CookieOptions(DateTimeOffset.UnixEpoch));
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct)
    {
        var user = await auth.GetCurrentUserAsync(currentUser.RequiredUserId, ct);
        return user is null ? NotFound() : Ok(user);
    }

    private ActionResult<AuthResultResponse> Complete(AuthOutcome outcome)
    {
        if (!outcome.Succeeded)
        {
            Response.Cookies.Delete(RefreshCookie, CookieOptions(DateTimeOffset.UnixEpoch));
            return outcome.Failure switch
            {
                AuthFailure.LockedOut => Unauthorized401("Too many failed attempts. Try again in 15 minutes.", "auth.locked"),
                AuthFailure.Inactive => Unauthorized401("This account is deactivated.", "auth.inactive"),
                AuthFailure.InvalidRefreshToken => Unauthorized401("Your session has expired. Please sign in again.", "auth.session-expired"),
                _ => Unauthorized401("Incorrect email or password.", "auth.invalid-credentials"),
            };
        }

        var tokens = outcome.Tokens!;
        Response.Cookies.Append(RefreshCookie, tokens.RefreshToken, CookieOptions(tokens.RefreshTokenExpiresAt));
        return Ok(new AuthResultResponse(tokens.AccessToken, tokens.ExpiresIn, outcome.User!));
    }

    private CookieOptions CookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() || Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth",
        Expires = expires,
    };

    private ObjectResult Unauthorized401(string detail, string code = "auth.required")
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "Unauthorized", Detail = detail };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status401Unauthorized, ContentTypes = { "application/problem+json" } };
    }
}
