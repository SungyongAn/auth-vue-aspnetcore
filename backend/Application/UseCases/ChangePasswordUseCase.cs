using Application.Exceptions;
using Application.Interfaces;

namespace Application.UseCases;

public class ChangePasswordUseCase
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IPasswordHasher _passwordHasher;

    public ChangePasswordUseCase(
        IUserRepository users,
        IRefreshTokenRepository tokens,
        IPasswordHasher passwordHasher)
    {
        _users = users;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
    }

    public async Task ExecuteAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new UserNotFoundException();

        if (!_passwordHasher.Verify(currentPassword, user.PasswordHash.Value))
            throw new InvalidCredentialsException();

        var newHash = new Domain.ValueObjects.PasswordHash(_passwordHasher.Hash(newPassword));
        user.UpdatePassword(newHash);
        await _users.UpdateAsync(user);

        // パスワード変更成功時、全セッションを無効化(セキュリティ上の推奨挙動)
        await _tokens.RevokeAllByUserIdAsync(user.Id);
    }
}