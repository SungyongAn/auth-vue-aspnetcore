using Domain.Entities;

namespace Application.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token);
    Task<PasswordResetToken?> GetValidTokenAsync(string tokenHash);
    Task MarkAsUsedAsync(PasswordResetToken token);
}