using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.ValueObjects;

namespace Application.UseCases;

public class LoginUseCase
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;

    public LoginUseCase(
        IUserRepository users,
        IRefreshTokenRepository tokens,
        ITokenService tokenService,
        IPasswordHasher passwordHasher)
    {
        _users = users;
        _tokens = tokens;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task<(LoginResponse Response, string RawRefreshToken)> ExecuteAsync(LoginRequest request)
    {
        var email = new Email(request.Email);
        var user = await _users.GetByEmailAsync(email);

        if (user == null)
            throw new InvalidCredentialsException();

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash.Value))
            throw new InvalidCredentialsException();

        // 古いリフレッシュトークンを無効化（セッション固定攻撃対策）
        await _tokens.RevokeAllByUserIdAsync(user.Id);

        var accessToken = _tokenService.GenerateAccessToken(user);
        var (rawToken, tokenEntity) = _tokenService.GenerateRefreshToken(user);

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