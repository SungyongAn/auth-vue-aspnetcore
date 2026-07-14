using Application.DTOs;
using Application.Exceptions;
using Application.UseCases;
using Infrastructure.Services;
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

    public AuthController(
        RegisterUseCase register,
        LoginUseCase login,
        RefreshUseCase refresh,
        LogoutUseCase logout,
        IOptions<JwtOptions> jwtOptions)
    {
        _register = register;
        _login = login;
        _refresh = refresh;
        _logout = logout;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var (response, rawRefreshToken) = await _register.ExecuteAsync(request);
        SetRefreshTokenCookie(rawRefreshToken);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var (response, rawRefreshToken) = await _login.ExecuteAsync(request);
        SetRefreshTokenCookie(rawRefreshToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var rawToken = Request.Cookies["refreshToken"]
            ?? throw new InvalidRefreshTokenException();

        var (response, newRawToken) = await _refresh.ExecuteAsync(rawToken);
        SetRefreshTokenCookie(newRawToken);
        return Ok(response);
    }


    [HttpPost("logout")]
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