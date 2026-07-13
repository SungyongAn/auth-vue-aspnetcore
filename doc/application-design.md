# application-design.md  
# 認証システム Application 層設計書  
Vue + ASP.NET Core（Clean Architecture）認証テンプレート

---

## 1. 概要

Application 層は、認証システムにおける **ユースケース（UseCase）を実行する層** です。  
Domain 層のビジネスルールを組み合わせ、認証処理の流れを定義します。

また、外部依存（DB・JWT・Cookie など）を抽象化する **インターフェース（Interface）** を提供し、  
Infrastructure 層がそれらを実装することで依存関係の逆転を実現します。

さらに、API に公開する **DTO（Request / Response）** を管理し、  
OpenAPI（Swagger）とフロントエンドの型生成（api-typescript-codegen）の基盤となります。

---

## 2. Application 層の責務

### ✔ 認証処理のユースケースを定義する  
- LoginUseCase  
- RegisterUseCase  
- RefreshUseCase  

### ✔ Domain 層のモデルを組み合わせて処理を実行する  
- User エンティティの生成  
- RefreshToken のローテーション  
- PasswordHash の検証  

### ✔ 外部依存を抽象化する  
- IUserRepository  
- IRefreshTokenRepository  
- ITokenService  

### ✔ API に公開する DTO を管理する  
- LoginRequest  
- LoginResponse  
- RegisterRequest  
- RefreshResponse  

---

## 3. DTO 設計（Request / Response）

### 3.1 LoginRequest

```csharp
LoginRequest
- Email (string)
- Password (string)
```

### 3.2 LoginResponse

```csharp
LoginResponse
- AccessToken (string)
- ExpiresIn (int)
- UserId (Guid)
- Email (string)
```

### 3.3 RegisterRequest

```csharp
RegisterRequest
- Email (string)
- Password (string)
```

### 3.4 RefreshResponse

```csharp
RefreshResponse
- AccessToken (string)
- ExpiresIn (int)
```

---

## 4. Interface 設計（抽象化）

### 4.1 IUserRepository

```csharp
IUserRepository
- Task<User?> GetByEmailAsync(Email email)
- Task<User?> GetByIdAsync(Guid id)
- Task AddAsync(User user)
- Task UpdateAsync(User user)
```

### 4.2 IRefreshTokenRepository

```csharp
IRefreshTokenRepository
- Task AddAsync(RefreshToken token)
- Task<RefreshToken?> GetValidTokenAsync(Guid userId)
- Task RevokeAsync(RefreshToken token)
```

### 4.3 ITokenService

```csharp
ITokenService
- string GenerateAccessToken(User user)
- RefreshToken GenerateRefreshToken(User user)
```

---

## 5. UseCase 設計

### 5.1 LoginUseCase

#### 役割
- Email でユーザーを検索  
- パスワード検証  
- アクセストークン発行  
- リフレッシュトークン発行  
- 古いリフレッシュトークンを無効化  
- 新しいリフレッシュトークンを保存  

#### 入力
- LoginRequest

#### 出力
- LoginResponse

---

### 5.2 RegisterUseCase

#### 役割
- Email の重複チェック  
- PasswordHash の生成  
- User エンティティの作成  
- DB に保存  

#### 入力
- RegisterRequest

#### 出力
- LoginResponse（登録後に自動ログイン）

---

### 5.3 RefreshUseCase

#### 役割
- Cookie のリフレッシュトークンを検証  
- 有効期限チェック  
- 古いトークンを無効化  
- 新しいトークンを発行  
- AccessToken を再発行  

#### 入力
- Cookie（HttpOnly）

#### 出力
- RefreshResponse

---

## 6. 認証フロー（Application 層視点）

### ✔ Login  
1. UserRepository でユーザー取得  
2. PasswordHash を検証  
3. TokenService で AccessToken 発行  
4. TokenService で RefreshToken 発行  
5. RefreshTokenRepository に保存  
6. LoginResponse を返す  

### ✔ Register  
1. Email 重複チェック  
2. PasswordHash を生成  
3. User エンティティ作成  
4. UserRepository に保存  
5. LoginUseCase を呼び出し自動ログイン  

### ✔ Refresh  
1. Cookie の RefreshToken を取得  
2. RefreshTokenRepository で検証  
3. 古いトークンを無効化  
4. 新しいトークンを発行  
5. AccessToken を再発行  

---

## 7. Application 層のメリット

- Domain 層のルールを安全に利用できる  
- 外部依存を抽象化しテストが容易  
- API とフロントの型生成と同期しやすい  
- Clean Architecture の中心となる層  
- 認証テンプレートとして再利用しやすい  

---

## 8. 今後の拡張性

- ロール管理（Admin / User）  
- パスワードリセット  
- メール認証  
- MFA（多要素認証）  
- アカウントロック  

Application 層の設計が正しいほど、これらの拡張が容易になります。

---
