using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.UseCases;

public class RegisterUseCase
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly ITokenService _tokenService;

    public RegisterUseCase(
        IUserRepository users,
        IRefreshTokenRepository tokens,
        ITokenService tokenService)
    {
        _users = users;
        _tokens = tokens;
        _tokenService = tokenService;
    }

    public async Task<(LoginResponse Response, string RawRefreshToken)> ExecuteAsync(RegisterRequest request)
    {
        var email = new Email(request.Email);

        // Email 重複チェック
        var existing = await _users.GetByEmailAsync(email);
        if (existing != null)
            throw new Exception("Email already registered.");

        // パスワードハッシュ生成
        var passwordHash = new PasswordHash(
            BCrypt.Net.BCrypt.HashPassword(request.Password)
        );

        // User エンティティ作成
        var user = new User(email, passwordHash);
        await _users.AddAsync(user);

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
