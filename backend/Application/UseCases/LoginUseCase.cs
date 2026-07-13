using Application.DTOs;
using Application.Interfaces;
using Domain.ValueObjects;

namespace Application.UseCases;

public class LoginUseCase
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly ITokenService _tokenService;

    public LoginUseCase(
        IUserRepository users,
        IRefreshTokenRepository tokens,
        ITokenService tokenService)
    {
        _users = users;
        _tokens = tokens;
        _tokenService = tokenService;
    }

    public async Task<(LoginResponse Response, string RawRefreshToken)> ExecuteAsync(LoginRequest request)
    {
        var email = new Email(request.Email);
        var user = await _users.GetByEmailAsync(email);

        if (user == null)
            throw new Exception("Invalid credentials.");

        // パスワード検証
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash.Value))
            throw new Exception("Invalid credentials.");

        // アクセストークン生成
        var accessToken = _tokenService.GenerateAccessToken(user);

        // リフレッシュトークン生成（raw + entity）
        var (rawToken, tokenEntity) = _tokenService.GenerateRefreshToken(user);

        // DB に保存
        await _tokens.AddAsync(tokenEntity);

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            ExpiresIn = 900,
            UserId = user.Id,
            Email = user.Email.Value
        };

        return (response, rawToken);
    }
}
