# db-design.md  
# 認証システム DB 設計書  
Vue + ASP.NET Core（Clean Architecture）認証テンプレート

---

## 1. 概要

本システムは、認証機能に必要なデータを管理するために  
**MySQL 8.0** を採用しています。

DB 設計は Clean Architecture の原則に従い、  
Domain 層のエンティティ（User / RefreshToken）を基盤として構築します。

---

## 2. テーブル一覧

| テーブル名 | 説明 |
|------------|------|
| users | ユーザー情報を管理する |
| refresh_tokens | リフレッシュトークンを管理する |

---

## 3. users テーブル設計

### テーブル定義

```sql
CREATE TABLE users (
    id CHAR(36) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    created_at DATETIME NOT NULL,
    updated_at DATETIME NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_users_email (email)
);
```

### カラム仕様

| カラム名 | 型 | 説明 |
|----------|------|------|
| id | CHAR(36) | User の GUID |
| email | VARCHAR(255) | Email ValueObject |
| password_hash | VARCHAR(255) | PasswordHash ValueObject |
| created_at | DATETIME | 作成日時 |
| updated_at | DATETIME | 更新日時 |

### 制約

- **PRIMARY KEY(id)**  
- **UNIQUE(email)**  
  → Email の重複登録を防ぐ

---

## 4. refresh_tokens テーブル設計

### テーブル定義

```sql
CREATE TABLE refresh_tokens (
    id CHAR(36) NOT NULL,
    user_id CHAR(36) NOT NULL,
    token_hash VARCHAR(255) NOT NULL,
    expires_at DATETIME NOT NULL,
    created_at DATETIME NOT NULL,
    revoked_at DATETIME NULL,
    PRIMARY KEY (id),
    KEY idx_refresh_tokens_user_id (user_id),
    CONSTRAINT fk_refresh_tokens_user
        FOREIGN KEY (user_id) REFERENCES users(id)
        ON DELETE CASCADE
);
```

### カラム仕様

| カラム名 | 型 | 説明 |
|----------|------|------|
| id | CHAR(36) | RefreshToken の GUID |
| user_id | CHAR(36) | 紐づく User の GUID |
| token_hash | VARCHAR(255) | ハッシュ化されたリフレッシュトークン |
| expires_at | DATETIME | 有効期限 |
| created_at | DATETIME | 作成日時 |
| revoked_at | DATETIME NULL | 無効化日時（ローテーション時に設定） |

### 制約

- **PRIMARY KEY(id)**  
- **FOREIGN KEY(user_id)** → users.id  
- **ON DELETE CASCADE**  
  → ユーザー削除時にトークンも削除

### インデックス

| インデックス名 | 対象 | 説明 |
|----------------|------|------|
| idx_refresh_tokens_user_id | user_id | ユーザーごとの検索高速化 |
| token_hash（必要に応じて） | token_hash | ハッシュ検索用 |

---

## 5. ER 図（簡易）

```
+-----------+            +------------------+
|  users    | 1        n | refresh_tokens   |
+-----------+------------+------------------+
| id        |            | id               |
| email     |            | user_id          |
| password  |            | token_hash       |
| created   |            | expires_at       |
| updated   |            | created_at       |
+-----------+            | revoked_at       |
                         +------------------+
```

---

## 6. マイグレーション（EF Core）

### Users

```csharp
builder.Entity<User>(entity =>
{
    entity.ToTable("users");
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.Email).IsUnique();
});
```

### RefreshTokens

```csharp
builder.Entity<RefreshToken>(entity =>
{
    entity.ToTable("refresh_tokens");
    entity.HasKey(e => e.Id);

    entity.HasOne<User>()
        .WithMany()
        .HasForeignKey(e => e.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasIndex(e => e.UserId);
});
```

---

## 7. DB 設計のポイント（認証システム特有）

### ✔ リフレッシュトークンは「ハッシュ化して保存」
→ 生のトークンを DB に保存しない（セキュリティ向上）

### ✔ リフレッシュトークンは「ローテーション方式」
→ 古いトークンは revoked_at を設定して無効化

### ✔ Email は UNIQUE 制約
→ 登録時の重複チェックを DB レベルで保証

### ✔ User 削除時に RefreshToken も削除（CASCADE）
→ データ整合性を保つ

---

## 8. 今後の拡張性

- ロール管理（roles テーブル）  
- MFA（multi_factor_tokens テーブル）  
- パスワードリセット（password_reset_tokens テーブル）  
- メール認証（email_verification_tokens テーブル）  
- 監査ログ（audit_logs テーブル）  

---
