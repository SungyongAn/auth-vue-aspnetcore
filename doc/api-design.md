# api-design.md

# 認証システム API 層設計書

Vue + ASP.NET Core（Clean Architecture）認証テンプレート

> **更新履歴:** `/auth/me` エンドポイントの追加、OpenAPI生成方式の変更、エラーレスポンス形式の明記を反映

---

## 1. 概要

API 層（Api）は、認証システムの **HTTP インターフェース** を提供する層です。  
Application 層の UseCase を呼び出し、DTO を使ってリクエストとレスポンスを処理します。

.NET 10 組み込みの `Microsoft.AspNetCore.OpenApi` を用いて API 仕様を自動生成し、  
フロントエンドでは `openapi-typescript-codegen`（fetchクライアント）により型と API クライアントを自動生成します。

また、リフレッシュトークンは **HttpOnly Cookie** として返却し、  
セキュリティを高めた認証フローを実現します。JWT の検証には ASP.NET Core の JWT Bearer 認証ミドルウェアを使用します。

---

## 2. API 層の責務

### ✔ Application 層の UseCase を呼び出す

- `LoginUseCase` / `RegisterUseCase` / `RefreshUseCase` / `LogoutUseCase`

### ✔ DTO を使ってリクエスト・レスポンスを扱う

- `LoginRequest` / `LoginResponse` / `RegisterRequest` / `RefreshResponse` / `UserInfoResponse`

### ✔ Cookie を設定する（HttpOnly / Secure / SameSite=strict）

- リフレッシュトークンを Cookie に保存
- アクセストークンはレスポンスで返却
- Cookie の有効期限は `JwtOptions.RefreshTokenExpiresInDays` と同期（Controller に `IOptions<JwtOptions>` を注入）

### ✔ JWT を検証する（Authorize保護エンドポイント用）

- `AddAuthentication().AddJwtBearer()` によりトークンの署名・有効期限を検証
- 検証キーは `TokenService` の発行時と同一の `Jwt:Key` を使用

### ✔ OpenAPI 仕様を生成する

- フロントの型生成に利用
- API 仕様書として機能
- エンドポイント: `GET /openapi/v1.json`（Development環境のみ有効）

### ✔ CORS 設定

- Vue（`http://localhost:5173`）からのアクセスを許可

### ✔ グローバル例外ハンドリング

- `IExceptionHandler` の実装（`GlobalExceptionHandler`）により、Application層の例外を一元的に HTTP ステータスコードへマッピング
- レスポンスは `ProblemDetails`（RFC 9457）形式で統一

---

## 3. エンドポイント一覧

| メソッド | パス                  | 認証要否             | 説明                                                             |
| -------- | --------------------- | -------------------- | ---------------------------------------------------------------- |
| POST     | /auth/register        | 不要                 | 新規登録（自動ログイン）                                         |
| POST     | /auth/login           | 不要                 | ログイン                                                         |
| POST     | /auth/refresh         | 不要（Cookie必須）   | リフレッシュトークンによるアクセストークン再発行                 |
| POST     | /auth/logout          | 不要（Cookie必須）   | ログアウト（Cookie 削除、DBトークン無効化）                      |
| GET      | /auth/me              | **必須**（Bearer）   | 現在の認証ユーザー情報を取得（動作確認・テンプレート用サンプル） |
| POST     | /auth/change-password | **必須**（Bearer）   | ログイン中ユーザーのパスワード変更（追加）                       |
| POST     | /auth/forgot-password | 不要                 | パスワードリセットメールの送信依頼（追加）                       |
| POST     | /auth/reset-password  | 不要（トークン必須） | リセットトークンを用いた新パスワードの設定（追加）               |

---

## 4. エンドポイント詳細

---

### 4.1 POST /auth/register

新規ユーザー登録を行い、登録後に自動ログインします。

#### Request Body（RegisterRequest）

```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

#### Response（LoginResponse） `200 OK`

```json
{
  "accessToken": "xxxxx",
  "expiresIn": 900,
  "userId": "guid",
  "email": "user@example.com"
}
```

#### Cookie（HttpOnly）

- refreshToken
- 有効期限：14日（設定可能）
- Secure: true
- SameSite: Strict
- Path: /auth/refresh

---

### 4.2 POST /auth/login

ログイン処理を行い、アクセストークンとリフレッシュトークンを返します。  
**ログイン成功時、該当ユーザーの既存の有効なリフレッシュトークンをすべて無効化**してから新しいトークンを発行します（セッション固定攻撃対策）。

#### Request Body（LoginRequest）

```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

#### Response（LoginResponse） `200 OK`

```json
{
  "accessToken": "xxxxx",
  "expiresIn": 900,
  "userId": "guid",
  "email": "user@example.com"
}
```

#### Cookie（HttpOnly）

- refreshToken
- ローテーション方式で新しいトークンを発行（過去のトークンは `revoked_at` を設定して無効化）

#### エラーレスポンス例 `401 Unauthorized`

```json
{
  "title": "Invalid credentials",
  "status": 401,
  "detail": "Invalid credentials."
}
```

---

### 4.3 POST /auth/refresh

Cookie のリフレッシュトークンを使ってアクセストークンを再発行します。

#### Request

Cookie のみ（Body は不要）。Cookie が存在しない、または無効な場合は `InvalidRefreshTokenException` により `401` を返却。

#### Response（RefreshResponse） `200 OK`

```json
{
  "accessToken": "xxxxx",
  "expiresIn": 900
}
```

#### Cookie（HttpOnly）

- refreshToken をローテーション
- 古いトークンは無効化（`revoked_at` 設定）

---

### 4.4 POST /auth/logout

リフレッシュトークン Cookie を削除し、DB上のトークンも無効化します。

#### Response

`204 No Content`

#### Cookie

- refreshToken を削除（Max-Age=0 相当、過去日時を設定）

---

### 4.5 GET /auth/me（追加）

現在ログイン中のユーザー情報を取得します。JWT Bearer認証の動作確認、および「保護されたエンドポイント」のテンプレートサンプルとして用意しています。

#### Request

Header: `Authorization: Bearer <accessToken>`

#### Response（UserInfoResponse） `200 OK`

```json
{
  "userId": "guid",
  "email": "user@example.com"
}
```

#### エラーレスポンス `401 Unauthorized`

トークンが無い、期限切れ、または署名が不正な場合。

---

### 4.6 POST /auth/change-password（追加）

ログイン中ユーザーが自身のパスワードを変更します。成功時、**該当ユーザーの全リフレッシュトークンを無効化**します（既存の全セッションが終了するため、フロント側は再ログインを促す必要があります）。

#### Request

Header: `Authorization: Bearer <accessToken>`

Body（ChangePasswordRequest）

```json
{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewPassword456!"
}
```

#### Response

`204 No Content`

#### エラーレスポンス

- `401 Unauthorized`：現在のパスワードが一致しない、またはトークンが無効(`InvalidCredentialsException`)

---

### 4.7 POST /auth/forgot-password（追加）

パスワードリセット用のメールを送信します。**メールアドレスの存在有無に関わらず、常に同じレスポンスを返します**（メールアドレス列挙攻撃対策）。

#### Request Body（ForgotPasswordRequest）

```json
{
  "email": "user@example.com"
}
```

#### Response

`204 No Content`（ユーザーが存在しない場合も同様。内部的にはメール送信をスキップするのみ）

#### 発行されるリセットリンクの例

```
{Frontend:BaseUrl}/reset-password?token=xxxxx
```

有効期限は1時間。

---

### 4.8 POST /auth/reset-password（追加）

メールで送られたトークンを使い、新しいパスワードを設定します。成功時、**該当ユーザーの全リフレッシュトークンを無効化**します。

#### Request Body（ResetPasswordRequest）

```json
{
  "token": "xxxxx",
  "newPassword": "NewPassword456!"
}
```

#### Response

`204 No Content`

#### エラーレスポンス

- `400 Bad Request` / `401 Unauthorized`：トークンが無効・期限切れ・使用済みの場合(`InvalidResetTokenException`)。具体的な失敗理由は攻撃者への手がかりとなるため区別せず、フロント側でも汎用的なメッセージ（「リンクの有効期限が切れているか、無効なリンクです」）として表示する。

---

## 5. Cookie 設定（セキュリティ）

API 層ではリフレッシュトークンを Cookie に設定します。

### Cookie 設定例

| 設定項目 | 値                                                       |
| -------- | -------------------------------------------------------- |
| HttpOnly | true                                                     |
| Secure   | true                                                     |
| SameSite | Strict                                                   |
| Path     | /auth/refresh                                            |
| Max-Age  | `JwtOptions.RefreshTokenExpiresInDays`（既定14日）と同期 |

### 理由

- JavaScript から参照できない（XSS 対策）
- CSRF を防ぐため SameSite=strict
- HTTPS 必須（Secure=true）

### 開発環境での注意

`Secure=true` の Cookie は HTTPS 接続でのみブラウザに保存されます。`curl` 等のツールで `http://localhost` に対して動作確認する場合、ブラウザと異なり Cookie が保存されないことがあるため、`/auth/refresh` 等の動作確認はブラウザ経由（`localhost` は多くのブラウザで secure originとして特別扱いされる）で行うことを推奨します。

---

## 6. エラーレスポンス形式（追加）

グローバル例外ハンドラーにより、すべてのエラーは [RFC 9457 ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457) 形式で統一されます。

```json
{
  "title": "エラーの種類",
  "status": 401,
  "detail": "詳細メッセージ"
}
```

### 例外とステータスコードのマッピング

| 例外                                          | ステータス                                                          |
| --------------------------------------------- | ------------------------------------------------------------------- |
| `InvalidCredentialsException`                 | 401                                                                 |
| `InvalidRefreshTokenException`                | 401                                                                 |
| `UserNotFoundException`                       | 404                                                                 |
| `EmailAlreadyExistsException`                 | 409                                                                 |
| `InvalidResetTokenException`（追加）          | 401                                                                 |
| `ArgumentException`（Domain層の不変条件違反） | 400                                                                 |
| その他未処理の例外                            | 500（詳細メッセージはクライアントに非公開、サーバーログにのみ記録） |

---

## 7. Swagger（OpenAPI）設定

### 採用技術

Swashbuckle ではなく、**.NET 10 組み込みの `Microsoft.AspNetCore.OpenApi`** を採用しています。

### 自動生成される仕様

- DTO（Application 層）
- エンドポイント
- リクエスト
- レスポンス（`[ProducesResponseType]` 属性を明示することでスキーマへ正しく反映）
- スキーマ

### サーバーURLの明示指定

Docker環境ではKestrelのリッスンアドレス（`http://[::]:8080`等）がそのままOpenAPI仕様の`servers`に出力されてしまうため、`AddDocumentTransformer`で明示的に外部向けURLを指定しています。

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Servers = new List<OpenApiServer>
        {
            new() { Url = "http://localhost:5000" }
        };
        return Task.CompletedTask;
    });
});
```

### フロント側の型生成

```bash
curl http://localhost:5000/openapi/v1.json -o openapi.json
npx openapi-typescript-codegen --input ./openapi.json --output src/api/generated --client fetch
```

---

## 8. CORS 設定

### 許可するオリジン

- http://localhost:5173（Vue）

### 設定例

- AllowCredentials: true（Cookie送信のため必須）
- AllowHeaders: 任意ヘッダーを許可
- AllowMethods: 任意メソッドを許可

**注意:** `AllowCredentials(true)` を使う場合、`AllowAnyOrigin()` とは併用できません（ブラウザの仕様上拒否されます）。オリジンは `WithOrigins(...)` で明示的に指定する必要があります。

---

## 9. API 層のメリット

- Application 層と明確に分離されている
- Cookie と JWT を安全に扱える
- OpenAPI によりフロントとの同期が容易
- 認証テンプレートとして再利用しやすい
- エラーレスポンスの形式が統一されており、フロント側のハンドリングが一本化できる

---

## 10. 今後の拡張性

- ロールベース認可（Authorize 属性のロール指定）
- メール認証（Email Verification）
- パスワードリセット API
- MFA（多要素認証）
- ログ出力（Serilog）

---
