using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly AppDbContext _db;

    public PasswordResetTokenRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(PasswordResetToken token)
    {
        _db.PasswordResetTokens.Add(token);
        await _db.SaveChangesAsync();
    }

    public Task<PasswordResetToken?> GetValidTokenAsync(string tokenHash)
    {
        return _db.PasswordResetTokens
            .Where(t => t.TokenHash == tokenHash
                     && t.UsedAt == null
                     && t.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync();
    }

    public async Task MarkAsUsedAsync(PasswordResetToken token)
    {
        token.MarkAsUsed();
        await _db.SaveChangesAsync();
    }
}