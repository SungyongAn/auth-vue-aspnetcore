using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db)
    {
        _db = db;
    }

    // ハッシュ化されたトークンで検索する
    public Task<RefreshToken?> GetValidTokenAsync(string tokenHash)
    {
        return _db.RefreshTokens
            .Where(t =>
                t.TokenHash == tokenHash &&
                t.RevokedAt == null &&
                t.ExpiresAt > DateTime.UtcNow
            )
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(RefreshToken token)
    {
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();
    }

    public async Task RevokeAsync(RefreshToken token)
    {
        token.Revoke();
        await _db.SaveChangesAsync();
    }
}
