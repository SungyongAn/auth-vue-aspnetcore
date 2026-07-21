using Application.Interfaces;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.UseCases;

public class ForgotPasswordUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetTokenRepository _resetTokens;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly string _resetUrlBase;

    public ForgotPasswordUseCase(
        IUserRepository users,
        IPasswordResetTokenRepository resetTokens,
        ITokenService tokenService,
        IEmailService emailService,
        string resetUrlBase)
    {
        _users = users;
        _resetTokens = resetTokens;
        _tokenService = tokenService;
        _emailService = emailService;
        _resetUrlBase = resetUrlBase;
    }


    public async Task ExecuteAsync(string email)
    {
        var user = await _users.GetByEmailAsync(new Email(email));

        if (user == null) return;

        var rawToken = Guid.NewGuid().ToString("N");
        var hashedToken = _tokenService.HashToken(rawToken);

        var resetToken = new PasswordResetToken(
            user.Id,
            hashedToken,
            DateTime.UtcNow.AddHours(1)
        );

        await _resetTokens.AddAsync(resetToken);

        var resetUrl = $"{_resetUrlBase}/reset-password?token={rawToken}"; // 修正:/reset-password を追加
        await _emailService.SendPasswordResetEmailAsync(user.Email.Value, resetUrl);
    }
}