using Application.DTOs;
using Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly RegisterUseCase _register;
    private readonly LoginUseCase _login;
    private readonly RefreshUseCase _refresh;
    private readonly LogoutUseCase _logout;

    public AuthController(
        RegisterUseCase register,
        LoginUseCase login,
        RefreshUseCase refresh,
        LogoutUseCase logout)
    {
        _register = register;
        _login = login;
        _refresh = refresh;
        _logout = logout;
    }

    // -----------------------------
    // Register
    // -----------------------------
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var (response, rawRefreshToken) = await _register.ExecuteAsync(request);

        // Cookie に rawRefreshToken を保存
        Response.Cookies.Append(
            "refreshToken",
            rawRefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth/refresh",
                Expires = DateTime.UtcNow.AddDays(14)
            }
        );

        return Ok(response);
    }

    // -----------------------------
    // Login
    // -----------------------------
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var (response, rawRefreshToken) = await _login.ExecuteAsync(request);

        Response.Cookies.Append(
            "refreshToken",
            rawRefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth/refresh",
                Expires = DateTime.UtcNow.AddDays(14)
            }
        );

        return Ok(response);
    }

    // -----------------------------
    // Refresh
    // -----------------------------
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var rawToken = Request.Cookies["refreshToken"];
        if (rawToken == null)
            return Unauthorized("Refresh token missing.");

        var (response, newRawToken) = await _refresh.ExecuteAsync(rawToken);

        // 新しい refreshToken を Cookie に保存（ローテーション）
        Response.Cookies.Append(
            "refreshToken",
            newRawToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth/refresh",
                Expires = DateTime.UtcNow.AddDays(14)
            }
        );

        return Ok(response);
    }

    // -----------------------------
    // Logout
    // -----------------------------
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rawToken = Request.Cookies["refreshToken"];

        if (rawToken != null)
        {
            await _logout.ExecuteAsync(rawToken);
        }

        // Cookie 削除
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

        return NoContent();
    }
}
