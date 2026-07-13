using Domain.Entities;

namespace Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);
    Task<RefreshToken?> GetValidTokenAsync(string tokenHash);
    Task RevokeAsync(RefreshToken token);
}