using Application.DTOs;
using Application.Exceptions;
using Application.UseCases;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly RegisterUseCase _register;
    private readonly LoginUseCase _login;
    private readonly RefreshUseCase _refresh;
    private readonly LogoutUseCase _logout;
    private readonly JwtOptions _jwtOptions;
    private readonly ChangePasswordUseCase _changePassword;
    private readonly ForgotPasswordUseCase _forgotPassword;
    private readonly ResetPasswordUseCase _resetPassword;

    public AuthController(
    RegisterUseCase register,
    LoginUseCase login,
    RefreshUseCase refresh,
    LogoutUseCase logout,
    ChangePasswordUseCase changePassword,
    ForgotPasswordUseCase forgotPassword,
    ResetPasswordUseCase resetPassword,
    IOptions<JwtOptions> jwtOptions)
    {
        _register = register;
        _login = login;
        _refresh = refresh;
        _logout = logout;
        _changePassword = changePassword;
        _forgotPassword = forgotPassword;
        _resetPassword = resetPassword;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> Register(RegisterRequest request)
    {
        var (response, rawRefreshToken) = await _register.ExecuteAsync(request);
        SetRefreshTokenCookie(rawRefreshToken);
        return Ok(response);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var (response, rawRefreshToken) = await _login.ExecuteAsync(request);
        SetRefreshTokenCookie(rawRefreshToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RefreshResponse>> Refresh()
    {
        var rawToken = Request.Cookies["refreshToken"]
            ?? throw new InvalidRefreshTokenException();

        var (response, newRawToken) = await _refresh.ExecuteAsync(rawToken);
        SetRefreshTokenCookie(newRawToken);
        return Ok(response);
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.FindFirst("userId")?.Value
            ?? throw new InvalidOperationException("userId claim not found.");

        await _changePassword.ExecuteAsync(Guid.Parse(userId), request.CurrentPassword, request.NewPassword);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _forgotPassword.ExecuteAsync(request.Email);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        await _resetPassword.ExecuteAsync(request.Token, request.NewPassword);
        return NoContent();
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        var rawToken = Request.Cookies["refreshToken"];
        if (rawToken != null)
        {
            await _logout.ExecuteAsync(rawToken);
        }

        ClearRefreshTokenCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var userId = User.FindFirst("userId")?.Value;
        var email = User.FindFirst("email")?.Value;

        return Ok(new UserInfoResponse
        {
            UserId = userId!,
            Email = email!
        });
    }

    private void SetRefreshTokenCookie(string rawToken)
    {
        Response.Cookies.Append(
            "refreshToken",
            rawToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth/refresh",
                Expires = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiresInDays)
            }
        );
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Append(
            "refreshToken",
            "",
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth/refresh",
                Expires = DateTime.UtcNow.AddDays(-1)
            }
        );
    }
}