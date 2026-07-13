# infrastructure-design.md  
# 認証システム Infrastructure 層設計書  
Vue + ASP.NET Core（Clean Architecture）認証テンプレート

---

## 1. 概要

Infrastructure 層は、認証システムにおける **外部接続（DB・JWT・Repository・Migration）を担当する層** です。  
Application 層で定義された Interface を実装し、外部技術（MySQL・EF Core・JWT）を扱います。

Clean Architecture の原則に従い、  
**Infrastructure → Application → Domain の依存方向** を守ります。

---

## 2. Infrastructure 層の責務

### ✔ DB（MySQL）との接続  
- EF Core の DbContext  
- Migration  
- テーブル作成（users / refresh_tokens）

### ✔ Repository の実装  
- IUserRepository の実装  
- IRefreshTokenRepository の実装  

### ✔ JWT の発行  
- ITokenService の実装  
- アクセストークン生成  
- リフレッシュトークン生成（ハッシュ化）

### ✔ 外部サービスとの接続  
- 今後の拡張（メール送信、Redis、外部API）にも対応可能

---

## 3. DbContext 設計

### AppDbContext

```csharp
AppDbContext
- DbSet<User> Users
- DbSet<RefreshToken> RefreshTokens
```

### 主な責務
- Entity と DB テーブルのマッピング  
- Migration の管理  
- トランザクション管理  

---

## 4. Repository 設計

Infrastructure 層では Application 層の Interface を実装します。

---

### 4.1 UserRepository

```csharp
UserRepository : IUserRepository
- Task<User?> GetByEmailAsync(Email email)
- Task<User?> GetByIdAsync(Guid id)
- Task AddAsync(User user)
- Task UpdateAsync(User user)
```

#### 実装ポイント
- Email ValueObject を string に変換して検索  
- PasswordHash は平文を保持しない  
- UpdatedAt を自動更新  

---

### 4.2 RefreshTokenRepository

```csharp
RefreshTokenRepository : IRefreshTokenRepository
- Task AddAsync(RefreshToken token)
- Task<RefreshToken?> GetValidTokenAsync(Guid userId)
- Task RevokeAsync(RefreshToken token)
```

#### 実装ポイント
- TokenHash をハッシュ化して保存  
- ExpiresAt と RevokedAt をチェックして有効判定  
- ローテーション方式に対応  

---

## 5. JWT 発行サービス（TokenService）

### ITokenService の実装

```csharp
TokenService : ITokenService
- string GenerateAccessToken(User user)
- RefreshToken GenerateRefreshToken(User user)
```

---

### 5.1 アクセストークン生成

#### 使用技術
- System.IdentityModel.Tokens.Jwt  
- SymmetricSecurityKey  
- SigningCredentials  

#### 設計ポイント
- 有効期限は短寿命（5〜15分）  
- UserId と Email をクレームに含める  
- HS256 で署名する  

---

### 5.2 リフレッシュトークン生成

#### 設計ポイント
- ランダム文字列を生成  
- ハッシュ化して DB に保存  
- 有効期限は長寿命（7〜30日）  
- Cookie（HttpOnly）で返す（Api 層で設定）

---

## 6. Migration 設計（EF Core）

### テーブル：Users

```sql
Users
- Id (char(36))
- Email (varchar(255))
- PasswordHash (varchar(255))
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

### インデックス
- Users.Email（Unique）  
- RefreshTokens.UserId  
- RefreshTokens.TokenHash  

---

## 7. Cookie 設定（Api 層と連携）

Infrastructure 層では Cookie を直接扱いませんが、  
TokenService が生成した RefreshToken を Api 層で Cookie に設定します。

### Cookie 設定例（Api 層）

- HttpOnly: true  
- Secure: true  
- SameSite: Strict  
- Path: /auth/refresh  
- Max-Age: リフレッシュトークンの有効期限と同期  

---

## 8. Infrastructure 層のメリット

- 外部技術（DB・JWT）を Application 層から分離できる  
- テストが容易（Interface によりモック可能）  
- Clean Architecture の依存方向を守れる  
- 認証テンプレートとして再利用しやすい  

---

## 9. 今後の拡張性

- Redis によるセッション管理  
- メール送信（SendGrid）  
- ログ出力（Serilog）  
- 外部 API 連携  
- 監査ログ（Audit Log）  

Infrastructure 層の設計が正しいほど、これらの拡張が容易になります。

---
