# db-design.md

# 認証システム DB 設計書

Vue + ASP.NET Core（Clean Architecture）認証テンプレート

> **更新履歴:** 実装上のカラム命名の既知の差異、Docker環境での接続情報管理方針を反映（テーブル定義そのものに変更なし）

---

## 1. 概要

本システムは、認証機能に必要なデータを管理するために  
**MySQL 8.0** を採用しています（Docker Compose で `mysql:8.0` イメージを使用）。

DB 設計は Clean Architecture の原則に従い、  
Domain 層のエンティティ（User / RefreshToken）を基盤として構築します。  
実際のテーブル定義は、Infrastructure 層の `IEntityTypeConfiguration<T>`（`UserConfiguration` / `RefreshTokenConfiguration`）を通じて EF Core Migration により生成されます。

---

## 2. テーブル一覧

| テーブル名              | 説明                                                 |
| ----------------------- | ---------------------------------------------------- |
| users                   | ユーザー情報を管理する                               |
| refresh_tokens          | リフレッシュトークンを管理する                       |
| password_reset_tokens   | パスワードリセットトークンを管理する（追加）         |
| \_\_EFMigrationsHistory | EF Core が自動生成するマイグレーション管理用テーブル |

---

## 3. users テーブル設計

### テーブル定義（実装版）

```sql
CREATE TABLE users (
    Id CHAR(36) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE KEY IX_users_email (email)
) CHARACTER SET=utf8mb4;
```

### カラム仕様

| カラム名      | 型           | 説明                                                                                                             |
| ------------- | ------------ | ---------------------------------------------------------------------------------------------------------------- |
| Id            | CHAR(36)     | User の GUID。EF Core が GUID 文字列に対して `ascii_general_ci` 照合順序を自動付与（比較・インデックスの最適化） |
| email         | VARCHAR(255) | Email ValueObject（`OwnsOne` でマッピング）                                                                      |
| password_hash | VARCHAR(255) | PasswordHash ValueObject（`OwnsOne` でマッピング。Argon2idハッシュを `salt.hash` 形式で保存）                    |
| created_at    | DATETIME(6)  | 作成日時                                                                                                         |
| updated_at    | DATETIME(6)  | 更新日時                                                                                                         |

### 制約

- **PRIMARY KEY(Id)**
- **UNIQUE(email)**（インデックス名: `IX_users_email`）
  → Email の重複登録を防ぐ

---

## 4. refresh_tokens テーブル設計

### テーブル定義（実装版）

```sql
CREATE TABLE refresh_tokens (
    Id CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    token_hash VARCHAR(255) NOT NULL,
    expires_at DATETIME(6) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    revoked_at DATETIME(6) NULL,
    PRIMARY KEY (Id),
    KEY idx_refresh_tokens_user_id (UserId),
    KEY IX_refresh_tokens_token_hash (token_hash),
    CONSTRAINT FK_refresh_tokens_users_UserId
        FOREIGN KEY (UserId) REFERENCES users(Id)
        ON DELETE CASCADE
) CHARACTER SET=utf8mb4;
```

> **既知の命名不統一：** `UserId` カラムのみ、他のカラム（snake_case）と異なり **PascalCase** のまま実装されています。これは `RefreshTokenConfiguration` で `UserId` に対する明示的な `HasColumnName("user_id")` 指定を行わなかったためです。動作に支障はありませんが、一貫性の観点では改善余地があります。統一する場合は以下の対応が必要です。
>
> ```csharp
> builder.Property(t => t.UserId).HasColumnName("user_id");
> ```
>
> この変更には新しいマイグレーションの追加が必要です（既存データがある場合はカラムリネームの移行が発生します）。

### カラム仕様

| カラム名   | 型               | 説明                                                                 |
| ---------- | ---------------- | -------------------------------------------------------------------- |
| Id         | CHAR(36)         | RefreshToken の GUID                                                 |
| UserId     | CHAR(36)         | 紐づく User の GUID（命名は上記参照）                                |
| token_hash | VARCHAR(255)     | SHA256でハッシュ化されたリフレッシュトークン（16進数文字列）         |
| expires_at | DATETIME(6)      | 有効期限                                                             |
| created_at | DATETIME(6)      | 作成日時                                                             |
| revoked_at | DATETIME(6) NULL | 無効化日時（ローテーション時、またはログイン時の一括無効化時に設定） |

### 制約

- **PRIMARY KEY(Id)**
- **FOREIGN KEY(UserId)** → users.Id
- **ON DELETE CASCADE**
  → ユーザー削除時にトークンも削除

### インデックス

| インデックス名               | 対象       | 説明                                                          |
| ---------------------------- | ---------- | ------------------------------------------------------------- |
| idx_refresh_tokens_user_id   | UserId     | ユーザーごとの検索高速化（`RevokeAllByUserIdAsync` 等で使用） |
| IX_refresh_tokens_token_hash | token_hash | `GetValidTokenAsync(string tokenHash)` によるハッシュ検索用   |

---

## 4.5 password_reset_tokens テーブル設計（追加）

### テーブル定義（実装版）

```sql
CREATE TABLE password_reset_tokens (
    Id CHAR(36) NOT NULL,
    user_id CHAR(36) NOT NULL,
    token_hash VARCHAR(255) NOT NULL,
    expires_at DATETIME(6) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    used_at DATETIME(6) NULL,
    PRIMARY KEY (Id),
    KEY idx_password_reset_tokens_user_id (user_id),
    KEY IX_password_reset_tokens_token_hash (token_hash),
    CONSTRAINT FK_password_reset_tokens_users_user_id
        FOREIGN KEY (user_id) REFERENCES users(Id)
        ON DELETE CASCADE
) CHARACTER SET=utf8mb4;
```

**命名の一貫性について：** `refresh_tokens.UserId` で発生していたPascalCase残留の反省を踏まえ、`password_reset_tokens` では `Configuration` クラスで `user_id` を明示的に指定し、最初からsnake_caseで統一している。

### カラム仕様

| カラム名   | 型               | 説明                                     |
| ---------- | ---------------- | ---------------------------------------- |
| Id         | CHAR(36)         | PasswordResetToken の GUID               |
| user_id    | CHAR(36)         | 紐づく User の GUID                      |
| token_hash | VARCHAR(255)     | SHA256でハッシュ化されたリセットトークン |
| expires_at | DATETIME(6)      | 有効期限（発行から1時間）                |
| created_at | DATETIME(6)      | 作成日時                                 |
| used_at    | DATETIME(6) NULL | 使用日時（未使用の場合はNULL）           |

### 制約

- **PRIMARY KEY(Id)**
- **FOREIGN KEY(user_id)** → users.Id、**ON DELETE CASCADE**

### インデックス

| インデックス名                      | 対象       | 説明                                                        |
| ----------------------------------- | ---------- | ----------------------------------------------------------- |
| idx_password_reset_tokens_user_id   | user_id    | ユーザーごとの検索用                                        |
| IX_password_reset_tokens_token_hash | token_hash | `GetValidTokenAsync(string tokenHash)` によるハッシュ検索用 |

---

## 5. ER 図（簡易）

```
+-----------+            +------------------+
|  users    | 1        n | refresh_tokens   |
+-----------+------------+------------------+
| Id        |            | Id               |
| email     |            | UserId           |
| password_ |            | token_hash       |
|  hash     |            | expires_at       |
| created_at|            | created_at       |
| updated_at|            | revoked_at       |
+-----+-----+            +------------------+
      |
      | 1
      |
      n
+---------------------------+
| password_reset_tokens     |
+----------------------------+
| Id                         |
| user_id                    |
| token_hash                 |
| expires_at                 |
| created_at                 |
| used_at                    |
+----------------------------+
```

---

## 6. マイグレーション（EF Core、実装版）

Entity と DB のマッピングは、専用の Configuration クラス（`IEntityTypeConfiguration<T>`）として `Infrastructure/Data/Configurations/` に実装しています（infrastructure-design.md 4章 参照）。

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value).HasColumnName("email").HasMaxLength(255).IsRequired();
            email.HasIndex(e => e.Value).IsUnique();
        });

        builder.OwnsOne(u => u.PasswordHash, hash =>
        {
            hash.Property(h => h.Value).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        });

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
    }
}
```

```csharp
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();
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

**マイグレーション適用コマンド：**

```bash
dotnet ef migrations add InitialCreate --project Infrastructure --startup-project Api
dotnet ef database update --project Infrastructure --startup-project Api
```

---

## 7. Docker環境での接続情報（追加）

開発環境では Docker Compose により MySQL コンテナ（`db` サービス）を起動する。接続文字列は実行環境によって異なる値を使う必要がある点に注意。

| 実行環境                         | 接続元                                    | Server             | Port                                                                        |
| -------------------------------- | ----------------------------------------- | ------------------ | --------------------------------------------------------------------------- |
| ローカル（`dotnet ef` 等）       | ホストマシン → Dockerポートマッピング経由 | `localhost`        | ホスト公開ポート（例：3307、ローカルMySQLとの競合を避けて変更する場合あり） |
| Dockerコンテナ内（`api` → `db`） | コンテナ間ネットワーク                    | `db`（サービス名） | 3306（コンテナ内部ポート）                                                  |

認証情報（ユーザー名・パスワード）は `docker-compose.yml` の環境変数（`MYSQL_USER` / `MYSQL_PASSWORD` / `MYSQL_DATABASE`）で設定し、アプリ側は `ConnectionStrings__DefaultConnection`（Docker実行時）または `dotnet user-secrets`（ローカル実行時）で注入する。**`appsettings.json` に平文で接続情報を書き込まない**（security.md の鍵管理方針に準拠）。

---

## 8. DB 設計のポイント（認証システム特有）

### ✔ リフレッシュトークンは「ハッシュ化して保存」

→ 生のトークンを DB に保存しない（SHA256、セキュリティ向上）

### ✔ リフレッシュトークンは「ローテーション方式」

→ 古いトークンは revoked_at を設定して無効化。ログイン時は該当ユーザーの全トークンを一括無効化

### ✔ Email は UNIQUE 制約

→ 登録時の重複チェックを DB レベルで保証

### ✔ User 削除時に RefreshToken も削除（CASCADE）

→ データ整合性を保つ

### ✔ ValueObject は Owned Entity Type としてマッピング

→ Domain層の `Email` / `PasswordHash` を、DB上はフラットなカラムとして扱いつつ、アプリケーションコード上は型安全なValueObjectとして扱える

---

## 9. 今後の拡張性

- ロール管理（roles テーブル）
- MFA（multi_factor_tokens テーブル）
- パスワードリセット（password_reset_tokens テーブル）
- メール認証（email_verification_tokens テーブル）
- 監査ログ（audit_logs テーブル）
- `refresh_tokens.UserId` カラムの `user_id` への統一（既知の軽微な改善項目）

---
