# application-design.md

# 認証システム Application 層設計書

Vue + ASP.NET Core（Clean Architecture）認証テンプレート

> **更新履歴:** リフレッシュトークンの生ハッシュ分離設計、`IPasswordHasher` の追加、カスタム例外、`RevokeAllByUserIdAsync` を反映

---

## 1. 概要

Application 層は、認証システムにおける **ユースケース（UseCase）を実行する層** です。  
Domain 層のビジネスルールを組み合わせ、認証処理の流れを定義します。

また、外部依存（DB・JWT・Cookie・パスワードハッシュ化 など）を抽象化する **インターフェース（Interface）** を提供し、  
Infrastructure 層がそれらを実装することで依存関係の逆転を実現します。

さらに、API に公開する **DTO（Request / Response）** を管理し、  
OpenAPI（`Microsoft.AspNetCore.OpenApi`）とフロントエンドの型生成（openapi-typescript-codegen）の基盤となります。

---

## 2. Application 層の責務

### ✔ 認証処理のユースケースを定義する

- `LoginUseCase`
- `RegisterUseCase`
- `RefreshUseCase`
- `LogoutUseCase`（追加）

### ✔ Domain 層のモデルを組み合わせて処理を実行する

- User エンティティの生成
- RefreshToken のローテーション（生成・無効化・一括無効化）
- PasswordHash の検証（`IPasswordHasher` 経由、Domain層はアルゴリズムを意識しない）

### ✔ 外部依存を抽象化する

- `IUserRepository`
- `IRefreshTokenRepository`
- `ITokenService`
- `IPasswordHasher`（追加：Argon2id 等のハッシュアルゴリズムの実装詳細をInfrastructure層に隠蔽する）
- `IPasswordResetTokenRepository`（追加：パスワードリセットトークンの永続化）
- `IEmailService`（追加：パスワードリセットメール送信の抽象化。実装詳細（SMTP等）はInfrastructure層に隠蔽する）

### ✔ API に公開する DTO を管理する

- `LoginRequest`
- `LoginResponse`
- `RegisterRequest`
- `RefreshResponse`
- `UserInfoResponse`（追加）
- `ChangePasswordRequest`（追加）
- `ForgotPasswordRequest`（追加）
- `ResetPasswordRequest`（追加）

### ✔ エラーを表現するカスタム例外を定義する（追加）

汎用 `Exception` ではなく、Api層のグローバル例外ハンドラーが判別しやすいよう、意味のある例外型を用意する。

- `InvalidCredentialsException`
- `InvalidRefreshTokenException`
- `UserNotFoundException`
- `EmailAlreadyExistsException`
- `InvalidResetTokenException`（追加：パスワードリセットトークンが無効・期限切れ・使用済みの場合）

---

## 3. DTO 設計（Request / Response）

**実装上の注意：** DTOは `record` + `init` プロパティとし、必須フィールドには **`required` 修飾子を明示** する。値型（`int`, `Guid`）は `required` を付けなくてもコンパイルは通るが、`required` を省略すると OpenAPI 生成時にオプション項目として出力されてしまい、フロントエンドの型生成で `?` 付きの型になってしまうため注意。

### 3.1 LoginRequest

```csharp
public record LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}
```

### 3.2 LoginResponse

```csharp
public record LoginResponse
{
    public required string AccessToken { get; init; }
    public required int ExpiresIn { get; init; }
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
}
```

### 3.3 RegisterRequest

```csharp
public record RegisterRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}
```

### 3.4 RefreshResponse

```csharp
public record RefreshResponse
{
    public required string AccessToken { get; init; }
    public required int ExpiresIn { get; init; }
}
```

### 3.5 UserInfoResponse（追加）

`/auth/me` エンドポイント用。JWT検証済みユーザーのクレームから構築する。

```csharp
public record UserInfoResponse
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
}
```

### 3.6 ChangePasswordRequest（追加）

```csharp
public record ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; init; }

    [Required]
    public required string NewPassword { get; init; }
}
```

### 3.7 ForgotPasswordRequest（追加）

```csharp
public record ForgotPasswordRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }
}
```

### 3.8 ResetPasswordRequest（追加）

```csharp
public record ResetPasswordRequest
{
    [Required]
    public required string Token { get; init; }

    [Required]
    public required string NewPassword { get; init; }
}
```

いずれもレスポンスボディを持たない（204 No Content）操作のため、対応するResponse DTOは存在しない。

---

## 4. Interface 設計（抽象化）

### 4.1 IUserRepository

```csharp
public interface IUserRepository
{
    Task<User?> GetByEmailAsync(Email email);
    Task<User?> GetByIdAsync(Guid id);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}
```

（設計書からの変更なし）

---

### 4.2 IRefreshTokenRepository（変更）

```csharp
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);
    Task<RefreshToken?> GetValidTokenAsync(string tokenHash);
    Task RevokeAsync(RefreshToken token);
    Task RevokeAllByUserIdAsync(Guid userId); // 追加：ログイン時の一括無効化用
}
```

**変更点：**

- `GetValidTokenAsync(Guid userId)` → **`GetValidTokenAsync(string tokenHash)`** に変更。  
  理由：`/auth/refresh` 呼び出し時、クライアントから渡されるのは Cookie に入った生トークンのみで `UserId` は分からない。ハッシュ化した値をキーに検索する必要があるため。
- `RevokeAllByUserIdAsync(Guid userId)` を追加。ログイン時に該当ユーザーの既存トークンを一括無効化する（セッション固定攻撃対策、security.md 10章）。

---

### 4.3 ITokenService（変更）

```csharp
public interface ITokenService
{
    // アクセストークン生成
    string GenerateAccessToken(User user);

    // 生トークンとハッシュ化トークンのペアを返す（Cookie用 + DB保存用）
    (string RawToken, RefreshToken TokenEntity) GenerateRefreshToken(User user);

    // トークン検索用のハッシュ化（決定的ハッシュ、SHA256）
    string HashToken(string rawToken);
}
```

**変更点：** `GenerateRefreshToken(User user): RefreshToken` という単一の戻り値では、Cookieに設定すべき「生のトークン文字列」を返せない（ハッシュ値は不可逆なため、DB保存用のハッシュから生トークンを復元できない）。そのため、**生トークンとエンティティ（ハッシュ済み）のタプルを返す**設計に変更。また、Refresh時にCookieの生トークンをハッシュ化してDB検索するための `HashToken` を追加。

---

### 4.4 IPasswordHasher（追加）

```csharp
public interface IPasswordHasher
{
    string Hash(string plainPassword);
    bool Verify(string plainPassword, string hash);
}
```

**追加理由：** 当初 UseCase 内で `BCrypt.Net.BCrypt.HashPassword` を直接呼び出していたが、これは以下の2つの問題があった。

1. security.md で採用を定めた **Argon2id** と異なるアルゴリズムを使ってしまっていた
2. ハッシュアルゴリズムの実装詳細が Application 層に漏れてしまい、Clean Architecture の責務分離に反していた

`IPasswordHasher` を Application 層に定義し、実装（`Argon2PasswordHasher`）を Infrastructure 層に置くことで、UseCase はアルゴリズムを意識せず、将来アルゴリズムを変更してもUseCaseを触らずに済む設計とした。

---

### 4.5 IPasswordResetTokenRepository（追加）

```csharp
public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token);
    Task<PasswordResetToken?> GetValidTokenAsync(string tokenHash);
    Task MarkAsUsedAsync(PasswordResetToken token);
}
```

`IRefreshTokenRepository` と同様の設計思想（生トークンはハッシュ化して検索する）を踏襲している。

---

### 4.6 IEmailService（追加）

```csharp
public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetUrl);
}
```

**設計判断：** メールの文面・件名の組み立てもInfrastructure層（実装側）に持たせる。Application層は「パスワードリセットメールを送ってほしい」という意図だけを表現し、SMTPやテンプレートの詳細を知らない。

---

## 5. UseCase 設計

### 5.1 LoginUseCase（変更）

#### 役割

- Email でユーザーを検索
- `IPasswordHasher` によるパスワード検証
- **ログイン時、該当ユーザーの既存有効トークンを `RevokeAllByUserIdAsync` で一括無効化**（追加）
- アクセストークン発行
- リフレッシュトークン発行（生トークン・エンティティのペア）
- 新しいリフレッシュトークンを保存

#### 入力

- LoginRequest

#### 出力

- `(LoginResponse Response, string RawRefreshToken)`（Api層でCookie設定するため生トークンも返す）

#### エラー

- ユーザーが存在しない、またはパスワード不一致 → `InvalidCredentialsException`

---

### 5.2 RegisterUseCase（変更）

#### 役割

- Email の重複チェック
- `IPasswordHasher.Hash` によるパスワードハッシュ生成
- User エンティティの作成
- DB に保存
- **`LoginUseCase` を実際に呼び出して自動ログイン処理を委譲**（設計書通りの動作を実装。以前はログイン処理を重複実装していたが、`LoginUseCase` の呼び出しに統一しコード重複を解消）

#### 入力

- RegisterRequest

#### 出力

- `(LoginResponse Response, string RawRefreshToken)`

#### エラー

- Email重複 → `EmailAlreadyExistsException`

---

### 5.3 RefreshUseCase（変更）

#### 役割

- Cookie から受け取った生トークンを `ITokenService.HashToken` でハッシュ化
- `IRefreshTokenRepository.GetValidTokenAsync(hash)` で検索・有効性確認
- 古いトークンを無効化
- 新しいトークンを発行
- AccessToken を再発行

#### 入力

- 生のリフレッシュトークン文字列（Cookieから取得、Api層で渡す）

#### 出力

- `(RefreshResponse Response, string RawRefreshToken)`

#### エラー

- トークンが無効・期限切れ・見つからない → `InvalidRefreshTokenException`
- 紐づくユーザーが存在しない → `UserNotFoundException`

---

### 5.4 LogoutUseCase（追加）

#### 役割

- Cookie から受け取った生トークンをハッシュ化して検索
- 見つかった場合、該当トークンを無効化（`RevokeAsync`）
- 見つからない場合は何もしない（冪等性を保つ）

#### 入力

- 生のリフレッシュトークン文字列

#### 出力

- なし（成功時は204 No Contentに対応）

---

### 5.5 ChangePasswordUseCase（追加）

#### 役割

- ログイン中ユーザー（JWTのuserIdクレームから特定）の現在のパスワードを検証
- 新しいパスワードをハッシュ化して更新
- **成功時、該当ユーザーの全リフレッシュトークンを無効化**（`RevokeAllByUserIdAsync`。パスワード変更後は既存の全セッションを終了させ、再ログインを要求するセキュリティ方針）

#### 入力

- userId（JWTクレームから取得、Api層で渡す）
- currentPassword
- newPassword

#### 出力

- なし（204 No Content）

#### エラー

- ユーザーが存在しない → `UserNotFoundException`
- 現在のパスワードが不一致 → `InvalidCredentialsException`

---

### 5.6 ForgotPasswordUseCase（追加）

#### 役割

- Email でユーザーを検索
- **ユーザーが存在しない場合も同じ成功応答とする**（メールアドレス列挙攻撃対策。存在確認の可否を外部から判別できないようにする）
- `PasswordResetToken` を生成（生トークン＋ハッシュ化エンティティのペア。`ITokenService.HashToken` を再利用）
- DB に保存（有効期限：1時間）
- `IEmailService` を通じてリセットリンク付きメールを送信

#### 入力

- email
- resetUrlBase（DI経由で `FrontendOptions.BaseUrl` を注入。フロントエンドのリセットページの起点URL）

#### 出力

- なし（204 No Content。ユーザー存在有無に関わらず常に成功扱い）

#### 重要な実装上の注意

リセットURLの組み立ては `$"{resetUrlBase}/reset-password?token={rawToken}"` のように、**フロントエンドのルーティングパス（`/reset-password`）を明示的に含める**必要がある。`resetUrlBase` のみをリンクにしてしまうと、パスが欠落しリンクが機能しない不具合になる（実装時に一度この誤りが発生し、修正済み）。

---

### 5.7 ResetPasswordUseCase（追加）

#### 役割

- 受け取った生トークンをハッシュ化し、`IPasswordResetTokenRepository.GetValidTokenAsync` で検証（有効期限内・未使用であることを確認）
- 新しいパスワードをハッシュ化して更新
- リセットトークンを使用済みにする（`MarkAsUsedAsync`）
- **成功時、該当ユーザーの全リフレッシュトークンを無効化**（ChangePasswordUseCaseと同様の方針）

#### 入力

- 生のリセットトークン文字列
- newPassword

#### 出力

- なし（204 No Content）

#### エラー

- トークンが無効・期限切れ・使用済み → `InvalidResetTokenException`（追加のカスタム例外）
- トークンに紐づくユーザーが存在しない → `UserNotFoundException`

---

## 6. 認証フロー（Application 層視点、更新）

### ✔ Login

1. UserRepository でユーザー取得
2. `IPasswordHasher.Verify` でパスワードを検証
3. **`RevokeAllByUserIdAsync` で既存トークンを一括無効化**
4. TokenService で AccessToken 発行
5. TokenService で RefreshToken 発行（生トークン・エンティティのペア）
6. RefreshTokenRepository に保存
7. LoginResponse と生トークンを返す

### ✔ Register

1. Email 重複チェック
2. `IPasswordHasher.Hash` でパスワードハッシュを生成
3. User エンティティ作成
4. UserRepository に保存
5. **LoginUseCase を呼び出し自動ログイン**

### ✔ Refresh

1. Cookie の生トークンを取得（Api層から渡される）
2. `HashToken` でハッシュ化
3. RefreshTokenRepository で検索・有効性確認
4. 古いトークンを無効化
5. 新しいトークンを発行
6. AccessToken を再発行

### ✔ Logout（追加）

1. Cookie の生トークンをハッシュ化して検索
2. 見つかれば無効化
3. Api層でCookieを削除

### ✔ ChangePassword（追加）

1. userId（JWTクレーム）でユーザー取得
2. `IPasswordHasher.Verify` で現在のパスワードを検証
3. 新しいパスワードをハッシュ化して更新
4. **`RevokeAllByUserIdAsync` で全セッションを無効化**

### ✔ ForgotPassword（追加）

1. Email でユーザー検索（存在しなくても成功扱い、列挙攻撃対策）
2. PasswordResetToken を生成（有効期限1時間）
3. DB に保存
4. リセットリンク付きメールを送信

### ✔ ResetPassword（追加）

1. トークンをハッシュ化して検索・有効性確認
2. 新しいパスワードをハッシュ化して更新
3. トークンを使用済みにする
4. **`RevokeAllByUserIdAsync` で全セッションを無効化**

---

## 7. Application 層のメリット

- Domain 層のルールを安全に利用できる
- 外部依存（DB、JWT、ハッシュアルゴリズム）を抽象化しテストが容易
- API とフロントの型生成と同期しやすい
- Clean Architecture の中心となる層
- 認証テンプレートとして再利用しやすい
- カスタム例外により、Api層でのHTTPステータスコードへの変換が一元化・簡潔になる

---

## 8. 今後の拡張性

- ロール管理（Admin / User）
- パスワードリセット
- メール認証
- MFA（多要素認証）
- アカウントロック

Application 層の設計が正しいほど、これらの拡張が容易になります。

---
