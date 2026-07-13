using Domain.Entities;

namespace Application.Interfaces;

public interface ITokenService
{
    // アクセストークン生成（従来通り）
    string GenerateAccessToken(User user);

    // 生トークンとハッシュ化トークンを返す（Cookie + DB 保存用）
    (string RawToken, RefreshToken TokenEntity) GenerateRefreshToken(User user);

    // トークン検索用のハッシュ化（決定的ハッシュ、SHA256）
    string HashToken(string rawToken);
}