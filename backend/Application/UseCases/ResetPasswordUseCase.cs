using Application.Exceptions;
using Application.Interfaces;
using Domain.ValueObjects;

namespace Application.UseCases;

public class ResetPasswordUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetTokenRepository _resetTokens;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordUseCase(
        IUserRepository users,
        IPasswordResetTokenRepository resetTokens,
        IRefreshTokenRepository refreshTokens,
        ITokenService tokenService,
        IPasswordHasher passwordHasher)
    {
        _users = users;
        _resetTokens = resetTokens;
        _refreshTokens = refreshTokens;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task ExecuteAsync(string rawToken, string newPassword)
    {
        var hashedToken = _tokenService.HashToken(rawToken);
        var resetToken = await _resetTokens.GetValidTokenAsync(hashedToken)
            ?? throw new InvalidResetTokenException();

        var user = await _users.GetByIdAsync(resetToken.UserId)
            ?? throw new UserNotFoundException();

        var newHash = new PasswordHash(_passwordHasher.Hash(newPassword));
        user.UpdatePassword(newHash);
        await _users.UpdateAsync(user);

        await _resetTokens.MarkAsUsedAsync(resetToken);

        // リセット成功時も全セッションを無効化
        await _refreshTokens.RevokeAllByUserIdAsync(user.Id);
    }
}