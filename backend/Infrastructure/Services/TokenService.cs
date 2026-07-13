using Application.Interfaces;
using Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly string _jwtKey;

    public TokenService(string jwtKey)
    {
        _jwtKey = jwtKey;
    }

    // アクセストークン生成（従来通り）
    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("userId", user.Id.ToString()),
                new Claim("email", user.Email.Value)
            },
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // 生トークンとハッシュ化トークンを返す
    public (string RawToken, RefreshToken TokenEntity) GenerateRefreshToken(User user)
    {
        // 生のトークン（Cookie に保存する）
        var rawToken = Guid.NewGuid().ToString("N");

        // DB に保存するのは検索可能なハッシュ化トークン（SHA256）
        var hashed = HashToken(rawToken);

        var entity = new RefreshToken(
            user.Id,
            hashed,
            DateTime.UtcNow.AddDays(14)
        );

        return (rawToken, entity);
    }

    // トークン検索用のハッシュ化（決定的ハッシュ）
    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}