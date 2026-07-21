# infrastructure-design.md

# 認証システム Infrastructure 層設計書

Vue + ASP.NET Core（Clean Architecture）認証テンプレート

> **更新履歴:** Argon2idハッシュ実装、`IOptions<JwtOptions>` パターン、EF Core Entity構成（OwnsOne）、JWT Bearer認証ミドルウェアを反映

---

## 1. 概要

Infrastructure 層は、認証システムにおける **外部接続（DB・JWT・パスワードハッシュ化・Repository・Migration）を担当する層** です。  
Application 層で定義された Interface を実装し、外部技術（MySQL・EF Core・JWT・Argon2）を扱います。

Clean Architecture の原則に従い、  
**Infrastructure → Application → Domain の依存方向** を守ります。

---

## 2. Infrastructure 層の責務

### ✔ DB（MySQL）との接続

- EF Core の DbContext（`Pomelo.EntityFrameworkCore.MySql` 使用）
- Migration
- テーブル作成（users / refresh_tokens）
- **Entity構成クラス（`IEntityTypeConfiguration<T>`）による ValueObject のマッピング**（追加）

### ✔ Repository の実装

- `IUserRepository` の実装
- `IRefreshTokenRepository` の実装（`RevokeAllByUserIdAsync` 含む）

### ✔ JWT の発行

- `ITokenService` の実装
- アクセストークン生成
- リフレッシュトークン生成（生トークン + ハッシュ化トークンのペアを返却）

### ✔ パスワードハッシュ化（追加）

- `IPasswordHasher` の実装（`Argon2PasswordHasher`）
- Argon2id によるハッシュ生成・検証

### ✔ メール送信（追加）

- `IEmailService` の実装（`SmtpEmailService`）
- パスワードリセットメールの送信（MailKit / SMTP）

### ✔ 外部サービスとの接続

- 今後の拡張（Redis、外部API）にも対応可能

---

## 3. DbContext 設計

### AppDbContext

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
    }
}
```

### 主な責務

- Entity と DB テーブルのマッピング
- Migration の管理
- トランザクション管理

---

## 4. Entity構成（Configurations）※追加

`User` エンティティは `Email` / `PasswordHash` という ValueObject（プリミティブ型でないプロパティ）を持つため、EF Core の `OnModelCreating` で明示的にマッピングを構成する必要がある（構成がないと、アプリ起動時にモデル構築エラーが発生する）。

### 4.1 UserConfiguration

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        // ValueObject を Owned Entity Type として構成
        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(255)
                .IsRequired();

            email.HasIndex(e => e.Value).IsUnique();
        });

        builder.OwnsOne(u => u.PasswordHash, hash =>
        {
            hash.Property(h => h.Value)
                .HasColumnName("password_hash")
                .HasMaxLength(255)
                .IsRequired();
        });

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
    }
}
```

### 4.2 RefreshTokenConfiguration

```csharp
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.RevokedAt).HasColumnName("revoked_at");

        builder.HasIndex(t => t.UserId).HasDatabaseName("idx_refresh_tokens_user_id");
        builder.HasIndex(t => t.TokenHash);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**既知の軽微な不統一：** `UserId` カラムには明示的な `HasColumnName` を指定していないため、他カラム（snake_case）と異なり `UserId`（PascalCase）のままDBに反映されている。動作上の問題はないが、将来的に統一する場合は `builder.Property(t => t.UserId).HasColumnName("user_id");` を追加しマイグレーションを作成する。

### 4.3 PasswordResetTokenConfiguration（追加）

```csharp
public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).HasColumnName("user_id"); // RefreshTokenの反省を踏まえ、最初からsnake_caseで明示
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UsedAt).HasColumnName("used_at");

        builder.HasIndex(t => t.UserId).HasDatabaseName("idx_password_reset_tokens_user_id");
        builder.HasIndex(t => t.TokenHash);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

## 5. Repository 設計

### 5.1 UserRepository

```csharp
public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByEmailAsync(Email email)
        => _db.Users.FirstOrDefaultAsync(u => u.Email.Value == email.Value);

    public Task<User?> GetByIdAsync(Guid id)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public async Task AddAsync(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }
}
```

---

### 5.2 RefreshTokenRepository（変更）

```csharp
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db) => _db = db;

    public Task<RefreshToken?> GetValidTokenAsync(string tokenHash)
    {
        return _db.RefreshTokens
            .Where(t => t.TokenHash == tokenHash
                     && t.RevokedAt == null
                     && t.ExpiresAt > DateTime.UtcNow)
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

    // 追加：ログイン時の一括無効化（セッション固定攻撃対策）
    public async Task RevokeAllByUserIdAsync(Guid userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
            token.Revoke();

        await _db.SaveChangesAsync();
    }
}
```

**実装ポイント：**

- `GetValidTokenAsync` は SQL レベルで `RevokedAt == null && ExpiresAt > UtcNow` を絞り込む（Entityの計算プロパティ `IsActive` はLINQ式内でSQL変換できないため使用しない）
- `RevokeAllByUserIdAsync` は複数トークンをメモリ上でループして `Revoke()` した後、最後に1回だけ `SaveChangesAsync()` を呼ぶ（DB書き込みを1回にまとめ効率化）

---

### 5.3 PasswordResetTokenRepository（追加）

```csharp
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
```

`RefreshTokenRepository` と同じ設計パターン（SQLレベルでの有効性フィルタリング）を踏襲している。

---

## 6. パスワードハッシュサービス（Argon2PasswordHasher）※追加

### IPasswordHasher の実装

security.md 2章の推奨パラメータ（Memory 64MB, Iterations 3, Parallelism 1）に準拠。`Konscious.Security.Cryptography.Argon2` パッケージを使用。

```csharp
public class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemorySizeKb = 65536; // 64MB
    private const int Iterations = 3;
    private const int Parallelism = 1;

    public string Hash(string plainPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(plainPassword, salt);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string plainPassword, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = ComputeHash(plainPassword, salt);

        // タイミング攻撃対策：定数時間比較
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] ComputeHash(string plainPassword, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(plainPassword))
        {
            Salt = salt,
            MemorySize = MemorySizeKb,
            Iterations = Iterations,
            DegreeOfParallelism = Parallelism
        };
        return argon2.GetBytes(HashSize);
    }
}
```

### 設計ポイント

- salt はハッシュごとにランダム生成し、`salt.hash` の形式で1つの文字列として保存（DBスキーマの変更は不要）
- `CryptographicOperations.FixedTimeEquals` によりタイミング攻撃を防止（単純な文字列比較 `==` は使わない）

---

## 7. JWT 発行サービス（TokenService）※変更

### ITokenService の実装

```csharp
public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("userId", user.Id.ToString()),
                new Claim("email", user.Email.Value)
            },
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenExpiresInMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string RawToken, RefreshToken TokenEntity) GenerateRefreshToken(User user)
    {
        var rawToken = Guid.NewGuid().ToString("N");
        var hashed = HashToken(rawToken);

        var entity = new RefreshToken(
            user.Id,
            hashed,
            DateTime.UtcNow.AddDays(_options.RefreshTokenExpiresInDays)
        );

        return (rawToken, entity);
    }

    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
```

**変更点：**

- コンストラクタでの `string jwtKey` 直接注入から **`IOptions<JwtOptions>` パターン**に変更。DIコンテナでの登録・`appsettings.json` からの設定読み込みが自然になる
- `GenerateRefreshToken` が生トークンとエンティティのペアを返す設計（4章参照）
- `HashToken` を追加。パスワードのArgon2id（非決定的・低速・総当たり耐性重視）とは異なり、トークン検索用はSHA256（決定的・高速・完全一致検索）を使う。用途に応じてハッシュアルゴリズムを使い分けている

### JwtOptions（設定クラス）

```csharp
public class JwtOptions
{
    public required string Key { get; set; }
    public int AccessTokenExpiresInMinutes { get; set; } = 15;
    public int RefreshTokenExpiresInDays { get; set; } = 14;
}
```

`appsettings.json` の `Jwt` セクションにバインドする。`Jwt:Key` は本番相当の値を `dotnet user-secrets`（開発時）や環境変数（Docker/本番）で管理し、リポジトリにコミットしない。

---

## 7.5 メール送信サービス（SmtpEmailService）※追加

### IEmailService の実装

`MailKit` パッケージを使用したSMTPベースの実装。

```csharp
public class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _options;

    public SmtpEmailService(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetUrl)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "パスワードリセットのご案内";

        message.Body = new TextPart("plain")
        {
            Text = $"パスワードをリセットするには、以下のリンクをクリックしてください。\n" +
                   $"このリンクは1時間有効です。\n\n{resetUrl}"
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_options.Username, _options.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
```

### SmtpOptions（設定クラス）

```csharp
public class SmtpOptions
{
    public required string Host { get; set; }
    public int Port { get; set; } = 587;
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string FromAddress { get; set; }
    public string FromName { get; set; } = "Auth System";
}
```

### FrontendOptions（設定クラス）

パスワードリセットメールに含めるリンクURLの起点を保持する。

```csharp
public class FrontendOptions
{
    public required string BaseUrl { get; set; }
}
```

### 環境ごとの設定値の注入方法（重要）

| 実行環境                      | 設定方法                                                                                 |
| ----------------------------- | ---------------------------------------------------------------------------------------- |
| ローカル（`dotnet run`）      | `dotnet user-secrets set "Smtp:Host" "..."`                                              |
| Docker（`docker compose up`） | `docker-compose.yml` の `environment`（`Smtp__Host: ${SMTP_HOST}` のように `.env` 経由） |

**重要な注意点：** `dotnet user-secrets` はローカル実行時のみ有効で、**Dockerコンテナには反映されない**。Docker環境でSMTP機能を使う場合は、必ず `docker-compose.yml` の `environment` セクションと、ルートの `.env` ファイルの両方に設定を追加する必要がある（`Jwt:Key` や DB接続文字列と同様の注意点）。

### Gmail利用時の注意（動作確認時の実例）

Gmailは2022年以降、通常のアカウントパスワードでのSMTP認証を許可していない。2段階認証を有効にした上で「アプリパスワード」（16桁）を発行し、`Smtp:Password` にはスペースを除去した値を設定する必要がある。認証情報が誤っていると `MailKit.Security.AuthenticationException: 535 5.7.8 Username and Password not accepted` が発生する（実装時に実際に遭遇し、アプリパスワードの発行で解決済み）。

---

## 8. JWT Bearer 認証ミドルウェア（追加）

`TokenService` がトークンを**発行**する一方、実際に保護エンドポイント（`/auth/me` 等）でトークンを**検証**するための仕組みが別途必要。ASP.NET Core の JWT Bearer 認証ミドルウェアを Api 層の `Program.cs` で設定する（詳細は architecture.md / api-design.md 参照）。

```csharp
JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear(); // クレーム名の自動マッピングを無効化

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
```

**重要な注意点：**

- `IssuerSigningKey` には `TokenService` の発行時と**同一の `Jwt:Key`** を使うこと（一致していないと正規発行トークンでも署名検証エラーになる）
- `JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear()` を呼ばないと、ASP.NET Core が `email` 等の標準クレーム名を独自のURI形式クレーム型に自動マッピングしてしまい、`User.FindFirst("email")` で取得できなくなる（既知のハマりポイント）

---

## 9. Migration 設計（EF Core）

### テーブル：Users

```sql
Users
- Id (char(36))
- Email (varchar(255))       -- ValueObject を OwnsOne でマッピング
- PasswordHash (varchar(255)) -- ValueObject を OwnsOne でマッピング
- CreatedAt (datetime)
- UpdatedAt (datetime)
```

### テーブル：RefreshTokens

```sql
RefreshTokens
- Id (char(36))
- UserId (char(36))
- TokenHash (varchar(255))
- ExpiresAt (datetime)
- CreatedAt (datetime)
- RevokedAt (datetime nullable)
```

### テーブル：PasswordResetTokens（追加）

```sql
PasswordResetTokens
- Id (char(36))
- UserId (char(36))        -- user_id としてsnake_caseで実装（RefreshTokensの命名不統一を踏まえ、最初からsnake_caseで統一）
- TokenHash (varchar(255))
- ExpiresAt (datetime)
- CreatedAt (datetime)
- UsedAt (datetime nullable)
```

### インデックス

- Users.Email（Unique）
- RefreshTokens.UserId
- RefreshTokens.TokenHash
- PasswordResetTokens.UserId（追加）
- PasswordResetTokens.TokenHash（追加）

### マイグレーションコマンド

```bash
dotnet ef migrations add InitialCreate --project Infrastructure --startup-project Api
dotnet ef migrations add AddPasswordResetTokens --project Infrastructure --startup-project Api
dotnet ef database update --project Infrastructure --startup-project Api
```

**前提パッケージ：** `Microsoft.EntityFrameworkCore.Design`（Api層に必要）、`dotnet-ef` CLIツール（グローバルインストール、`dotnet tool install --global dotnet-ef`）。

---

## 10. Cookie 設定（Api 層と連携）

Infrastructure 層では Cookie を直接扱わないが、`TokenService` が生成した RefreshToken（生トークン）を Api 層で Cookie に設定する。有効期限は `JwtOptions.RefreshTokenExpiresInDays` と同期させる（ハードコードしない）。

---

## 11. Infrastructure 層のメリット

- 外部技術（DB・JWT・ハッシュアルゴリズム）を Application 層から分離できる
- テストが容易（Interface によりモック可能）
- Clean Architecture の依存方向を守れる
- 認証テンプレートとして再利用しやすい
- ハッシュアルゴリズムやDBプロバイダの変更が、この層の実装差し替えだけで完結する

---

## 12. 今後の拡張性

- Redis によるセッション管理
- メール送信（SendGrid）
- ログ出力（Serilog）
- 外部 API 連携
- 監査ログ（Audit Log）

Infrastructure 層の設計が正しいほど、これらの拡張が容易になります。

---
