using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;

namespace Application.UseCases;

public class RefreshUseCase
{
    private readonly IRefreshTokenRepository _tokens;
    private readonly IUserRepository _users;
    private readonly ITokenService _tokenService;

    public RefreshUseCase(
        IRefreshTokenRepository tokens,
        IUserRepository users,
        ITokenService tokenService)
    {
        _tokens = tokens;
        _users = users;
        _tokenService = tokenService;
    }

    public async Task<(RefreshResponse Response, string RawRefreshToken)> ExecuteAsync(string rawRefreshToken)
    {
        var hashed = _tokenService.HashToken(rawRefreshToken);

        var token = await _tokens.GetValidTokenAsync(hashed);
        if (token == null || !token.IsActive)
            throw new InvalidRefreshTokenException();

        var user = await _users.GetByIdAsync(token.UserId);
        if (user == null)
            throw new UserNotFoundException();

        await _tokens.RevokeAsync(token);

        var (newRawToken, newTokenEntity) = _tokenService.GenerateRefreshToken(user);
        await _tokens.AddAsync(newTokenEntity);

        var newAccessToken = _tokenService.GenerateAccessToken(user);

        var response = new RefreshResponse
        {
            AccessToken = newAccessToken,
            ExpiresIn = 900
        };

        return (response, newRawToken);
    }
}