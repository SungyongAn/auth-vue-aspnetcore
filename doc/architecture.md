# architecture.md  
# 認証システム アーキテクチャ設計書  
Vue + ASP.NET Core（Clean Architecture）認証テンプレート

> **更新履歴:** 実装フェーズでの変更を反映（プロジェクト命名の簡素化、OpenAPI生成方式の変更）

---

## 1. 概要

本システムは、Vue（TypeScript）と ASP.NET Core（C#）を用いて構築された  
**JWT（アクセストークン）＋リフレッシュトークン（Cookie）方式の認証テンプレート** です。

バックエンドは **Clean Architecture** を採用し、  
認証ロジック・ドメインモデル・ユースケース・インフラ層を明確に分離しています。

また、.NET 10 組み込みの **`Microsoft.AspNetCore.OpenApi`** を用いて API 仕様を自動生成し、  
フロントエンドでは **`openapi-typescript-codegen`（fetchクライアント）** により型と API クライアントを自動生成します。

---

## 2. アーキテクチャ構成（Clean Architecture）

本システムは以下の 4 層で構成されます。

> **命名変更:** 当初 `Auth.Domain` 等のプレフィックス付き命名を想定していましたが、  
> 実装では `Auth.` プレフィックスを外し、`Domain` / `Application` / `Infrastructure` / `Api` としています。  
> namespace もこれに合わせて `Domain.Entities` のように簡潔にしています。

```
Domain            ← ビジネスルール（最内層）
Application       ← ユースケース・DTO・インターフェース
Infrastructure     ← DB・外部サービス・実装
Api                ← HTTP API（Controller）
```

依存関係は以下の通りです。

```
Domain → Application → Api
Domain → Infrastructure → Application → Api
```

**内側の層は外側に依存しない**ことが Clean Architecture の原則です。

---

## 3. 各層の責務

### 3.1 Domain（ドメイン層）
**責務：ビジネスルールの中心。認証ロジックの核。**

- `User` エンティティ
- `RefreshToken` エンティティ（`IsExpired` / `IsActive` 判定、二重 Revoke 防止ガード付き）
- ValueObject（`Email`, `PasswordHash`） — いずれも `sealed record` として実装し、値の等価性と拡張不可を保証
- コンストラクタでの不変条件検証（`ExpiresAt` が未来日時であること等）を徹底

**特徴：**
- DB や ASP.NET Core を知らない
- 認証の本質的なルールのみを保持
- 不変条件（Invariant）を守る役割

---

### 3.2 Application（アプリケーション層）
**責務：ユースケース（認証処理の流れ）を定義する。**

- DTO（`LoginRequest` / `LoginResponse` / `RegisterRequest` / `RefreshResponse` / `UserInfoResponse`）
  - いずれも `record` + `init` プロパティとし、必須項目には `required` 修飾子を付与（OpenAPI スキーマの `required` 判定に必須）
- Interface（`IUserRepository` / `IRefreshTokenRepository` / `ITokenService` / `IPasswordHasher`）
- UseCase（`LoginUseCase` / `RegisterUseCase` / `RefreshUseCase` / `LogoutUseCase`）
- カスタム例外（`InvalidCredentialsException` 等）によるエラーハンドリングの一元化

**特徴：**
- Domain を使って処理を組み立てる
- Repository や TokenService、PasswordHasher の抽象（Interface）を定義
- OpenAPI に出す DTO はここに置く
→ フロントの型生成と同期しやすい

---

### 3.3 Infrastructure（インフラ層）
**責務：外部接続（DB・JWT・Repository の実装）。**

- EF Core の DbContext（MySQL / Pomelo.EntityFrameworkCore.MySql）
- Repository 実装（`UserRepository` / `RefreshTokenRepository`）
- `TokenService`（JWT 発行、リフレッシュトークンの生成・SHA256 ハッシュ化）
- `Argon2PasswordHasher`（`IPasswordHasher` の実装。Konscious.Security.Cryptography.Argon2 使用）
- `JwtOptions`（`IOptions<T>` パターンによる JWT 設定の注入）
- Migration（EF Core）

**特徴：**
- Application の Interface を実装する
- DB や外部サービスの知識を持つ
- Domain や Application に依存するが、逆依存はしない

---

### 3.4 Api（API 層）
**責務：HTTP API の入口。Controller を提供する。**

- `AuthController`（`/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/me`）
- JWT Bearer 認証ミドルウェア（`AddAuthentication().AddJwtBearer()`）
  - `JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear()` により、独自クレーム名（`userId`, `email`）がそのまま保持されるよう設定
- Cookie 設定（HttpOnly / Secure / SameSite=strict）
- グローバル例外ハンドラー（`IExceptionHandler` 実装、`ProblemDetails` 形式でエラーを返却）
- `Microsoft.AspNetCore.OpenApi` によるOpenAPI仕様の自動生成（`/openapi/v1.json`）
  - `AddDocumentTransformer` で `servers` を明示指定（Docker環境でのアドレス誤認識を防止）
- DI（依存性注入）
- CORS 設定

**特徴：**
- Application の UseCase を呼び出す
- OpenAPI を自動生成
→ フロントの型生成の基盤となる

---

## 4. ディレクトリ構成（実装版）

```
backend/
Api/
Controllers/
  AuthController.cs
ExceptionHandling/
  GlobalExceptionHandler.cs
Properties/
Program.cs
appsettings.json
Dockerfile

Application/
DTOs/
Exceptions/
Interfaces/
  IUserRepository.cs
  IRefreshTokenRepository.cs
  ITokenService.cs
  IPasswordHasher.cs
UseCases/

Domain/
Entities/
ValueObjects/

Infrastructure/
Data/
  AppDbContext.cs
  Configurations/
    UserConfiguration.cs
    RefreshTokenConfiguration.cs
Migrations/
Repositories/
Services/
  TokenService.cs
  JwtOptions.cs
  Argon2PasswordHasher.cs

Auth.slnx

frontend/
src/
  api/
    generated/        ← openapi-typescript-codegen の自動生成物（fetchクライアント）
    withAuthRetry.ts   ← 401時の自動リフレッシュ＆リトライ
  stores/
    auth.ts            ← Piniaストア（アクセストークンはメモリ保持、localStorage不使用）
  router/
  views/
    LoginView.vue
    RegisterView.vue
    DashboardView.vue
  vite-env.d.ts
Dockerfile
.env / .env.example

doc/
architecture.md
domain-design.md
application-design.md
infrastructure-design.md
api-design.md
db-design.md
auth-flow.md
security.md

docker-compose.yml
.gitignore
README.md
```

---

## 5. 認証方式（アーキテクチャ観点）

### アクセストークン
- 有効期限：短寿命（15分、`JwtOptions.AccessTokenExpiresInMinutes` で設定可能）
- 保存場所：フロントのメモリ（Pinia の state。**localStorage は使用しない**）
- API 呼び出し時に Authorization ヘッダーで送信（`OpenAPI.TOKEN` リゾルバ経由で自動付与）

### リフレッシュトークン
- 有効期限：長寿命（14日、`JwtOptions.RefreshTokenExpiresInDays` で設定可能）
- 保存場所：HttpOnly Cookie（Secure / SameSite=strict / Path=`/auth/refresh`）
- `/auth/refresh` でローテーション
- ログイン時に該当ユーザーの既存トークンを一括無効化（`RevokeAllByUserIdAsync`、セッション固定攻撃対策）

### パスワードハッシュ
- Argon2id（`Konscious.Security.Cryptography.Argon2`）
- Domain 層で `PasswordHash` ValueObject として扱う（`ToString()` はハッシュ値を露出させない設計）
- Infrastructure 層の `Argon2PasswordHasher` で実際のハッシュ化・検証（`FixedTimeEquals` によるタイミング攻撃対策込み）を実装

---

## 6. OpenAPI と型生成（実装版）

### バックエンド（Api）
- .NET 10 組み込みの `Microsoft.AspNetCore.OpenApi` により OpenAPI 3.1 仕様を自動生成
- エンドポイント: `GET /openapi/v1.json`
- レスポンス型をスキーマへ正しく反映させるため、Controller アクションには **`[ProducesResponseType]` 属性を明示** している（`ActionResult<T>` の型推論だけでは反映されないケースがあったため）

### フロントエンド（Vue）
- `openapi-typescript-codegen`（`--client fetch`）により型と API クライアントを自動生成

```bash
curl http://localhost:5000/openapi/v1.json -o openapi.json
npx openapi-typescript-codegen --input ./openapi.json --output src/api/generated --client fetch
```

- 生成物: `models/`（DTO型）, `services/AuthService.ts`（API呼び出し関数）, `core/OpenAPI.ts`（設定オブジェクト）
- `OpenAPI.BASE` / `WITH_CREDENTIALS` / `TOKEN` は `main.ts` でアプリ起動時に初期化

---

## 7. Docker 構成

docker-compose により以下を一括起動：

- `api`（ASP.NET Core、MySQL接続待機のヘルスチェック付き）
- `db`（MySQL 8.0）
- `frontend`（Vue、Nginx配信 or 開発サーバー）※コンテナ化検討中

---

## 8. このアーキテクチャのメリット

- 責務分離が明確で保守性が高い
- 認証ロジックが UI や DB に依存しない
- OpenAPI と型生成でフロントとの同期が容易
- Clean Architecture により企業評価が高い
- 認証テンプレートとして再利用しやすい

---

## 9. 今後の拡張性

- ロール管理（Admin / User）
- メール認証（SendGrid）
- パスワードリセット
- 多要素認証（MFA）
- 他のフロント（React）への横展開
- 他のバックエンド（FastAPI）への横展開

---