# 認証テンプレート

Vue + ASP.NET Core（Clean Architecture）

JWT / Refresh Token（HttpOnly Cookie） / OpenAPI / TypeScript 型生成

---

## 📌 概要

このプロジェクトは、Vue と ASP.NET Core を用いて構築した、Clean Architecture ベースの認証テンプレートです。

認証機能を安全かつ再利用可能な形で実装することを目的としており、実務で利用されるセキュリティ対策や設計手法を取り入れています。

### 主な特徴

- Vue 3 + TypeScript
- ASP.NET Core 10
- Clean Architecture
- JWT（アクセストークン）
- Refresh Token（HttpOnly Cookie）
- リフレッシュトークンのローテーション
- Argon2id によるパスワードハッシュ
- パスワード変更・パスワードリセット（メール送信）
- OpenAPI 3.1 自動生成
- TypeScript API クライアント自動生成
- Element Plus（UIコンポーネント）
- MySQL 8.0
- Docker / Docker Compose

---

## 🧱 アーキテクチャ

本システムは Clean Architecture を採用しています。

```text
Api
 ↓

Application
 ↓

Domain

Infrastructure
 ├──→ Application
 └──→ Domain
```

### 各層の責務

| 層             | 責務                                |
| -------------- | ----------------------------------- |
| Domain         | エンティティ、ValueObject、不変条件 |
| Application    | UseCase、DTO、Interface             |
| Infrastructure | DB、JWT、メール送信、Repository 実装 |
| Api            | Controller、認証、DI、OpenAPI       |

---

## 📁 ディレクトリ構成

```text
backend/
├── Api/
├── Application/
├── Domain/
└── Infrastructure/

frontend/
└── src/
    ├── api/
    │   ├── generated/
    │   └── withAuthRetry.ts
    ├── stores/
    │   └── auth.ts
    ├── router/
    └── views/
        ├── LoginView.vue
        ├── RegisterView.vue
        ├── DashboardView.vue
        ├── ChangePasswordView.vue
        ├── ForgotPasswordView.vue
        └── ResetPasswordView.vue

doc/
├── architecture.md
├── domain-design.md
├── application-design.md
├── infrastructure-design.md
├── api-design.md
├── db-design.md
├── auth-flow.md
└── security.md

docker-compose.yml
README.md
```

---

## 🔐 認証方式

### アクセストークン（JWT）

- 有効期限：15 分
- 保存場所：Pinia（メモリのみ）
- localStorage / sessionStorage は使用しない
- Authorization ヘッダーで送信

### リフレッシュトークン

- 保存場所：HttpOnly Cookie
- 有効期限：14 日
- Secure / SameSite=Strict
- `/auth/refresh` でローテーション

### トークン保存方式

```text
ブラウザ Cookie
    ↓
生の Refresh Token
    ↓
サーバー側
    ↓
SHA256(refreshToken)
```

データベースにはハッシュ化された値のみを保存します。パスワードリセットトークンも同様の方式でハッシュ化して保存されます。

---

## 🔄 認証フロー

### Register

```text
Register
    ↓
ユーザー作成
    ↓
LoginUseCase 呼び出し
    ↓
AccessToken 発行
    ↓
RefreshToken 発行
    ↓
Cookie 保存
```

### Login

```text
Login
    ↓
既存 RefreshToken を全て無効化
    ↓
AccessToken 発行
    ↓
RefreshToken 発行
    ↓
Cookie 保存
```

### Refresh

```text
Refresh
    ↓
Cookie 読み込み
    ↓
SHA256 ハッシュ化
    ↓
DB 検証
    ↓
旧トークン無効化
    ↓
新トークン発行
```

### Logout

```text
Logout
    ↓
RefreshToken 無効化
    ↓
Cookie 削除
```

### パスワード変更（ログイン中）

```text
ChangePassword
    ↓
現在のパスワード検証
    ↓
新パスワードへ更新
    ↓
全 RefreshToken 無効化
    ↓
再ログインが必要
```

### パスワードリセット（忘却時）

```text
ForgotPassword（メールアドレス送信）
    ↓
PasswordResetToken 発行（有効期限 1 時間）
    ↓
リセットリンクをメール送信

        ↓（メール内リンクをクリック）

ResetPassword（トークン + 新パスワード送信）
    ↓
トークン検証
    ↓
新パスワードへ更新
    ↓
全 RefreshToken 無効化
```

ユーザーが存在しないメールアドレスを指定した場合も、`/auth/forgot-password` は常に同じ成功レスポンスを返します（メールアドレス列挙攻撃対策）。

---

## 🔄 フロントエンド認証制御

### Silent Refresh

アプリ起動時に `/auth/refresh` を自動実行し、Cookie が有効であればログイン状態を復元します。

### 401 自動リトライ

API が 401 を返した場合、

1. `/auth/refresh`
2. AccessToken 再取得
3. 元のリクエストを再実行

を自動で行います（`withAuthRetry` 関数として汎用化）。

---

## 📚 ドキュメント

| ファイル                 | 内容                      |
| ------------------------ | ------------------------- |
| architecture.md          | システム全体設計          |
| domain-design.md         | Domain 層                 |
| application-design.md    | UseCase / DTO / Interface |
| infrastructure-design.md | Repository / TokenService / メール送信 |
| api-design.md            | API 設計                  |
| db-design.md             | DB 設計                   |
| auth-flow.md             | 認証フロー                |
| security.md              | セキュリティ設計          |

---

## ⚙️ OpenAPI と型生成

### OpenAPI 生成

ASP.NET Core の `Microsoft.AspNetCore.OpenApi` により OpenAPI 3.1 を自動生成します。

```bash
GET /openapi/v1.json
```

### TypeScript クライアント生成

```bash
curl http://localhost:5000/openapi/v1.json -o openapi.json

npx openapi-typescript-codegen \
  --input ./openapi.json \
  --output src/api/generated \
  --client fetch
```

生成されるもの：

- models/
- services/
- core/OpenAPI.ts

---

## 🚀 起動方法

### 環境変数の準備

ルートディレクトリに `.env` を作成し、以下を設定してください（`.env.example` を参照）。

```bash
JWT_KEY=<ランダムな長い文字列>
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=<送信元メールアドレス>
SMTP_PASSWORD=<SMTPパスワード、Gmailの場合はアプリパスワード>
SMTP_FROM_ADDRESS=<送信元メールアドレス>
```

パスワードリセット機能を使わない場合、SMTP関連の値は空でも起動可能ですが、`/auth/forgot-password` を呼び出した際にメール送信でエラーになります。

### バックエンド・DB

```bash
docker compose up --build
```

### フロントエンド

```bash
cd frontend

npm install

npm run dev
```

### アクセス先

| サービス    | URL                                    |
| ----------- | --------------------------------------- |
| Frontend    | http://localhost:5173                   |
| Backend API | http://localhost:5000                   |
| OpenAPI     | http://localhost:5000/openapi/v1.json   |

---

## 🧪 テスト容易性

### Domain

- 外部依存なし
- 純粋なユニットテストが可能

### Application

- Interface をモック可能（`IEmailService` も含む）

### Infrastructure

- Repository
- TokenService
- PasswordHasher
- EmailService

を個別にテスト可能です。

---

## 🔧 使用技術

### Backend

- ASP.NET Core 10
- EF Core
- MySQL 8.0
- Clean Architecture
- JWT Bearer Authentication
- Argon2id
- MailKit（SMTPメール送信）
- Microsoft.AspNetCore.OpenApi

### Frontend

- Vue 3
- TypeScript
- Pinia
- Vite
- Vue Router
- Element Plus
- openapi-typescript-codegen

### DevOps

- Docker
- Docker Compose

---

## 🛡 セキュリティ対策

- Argon2id によるパスワードハッシュ
- `CryptographicOperations.FixedTimeEquals`
- JWT の短寿命化
- HttpOnly Cookie
- SameSite=Strict
- Refresh Token ローテーション
- SHA256 によるトークン保存
- XSS 対策
- CSRF 対策
- Replay Attack 対策
- セッション固定攻撃対策
- メールアドレス列挙攻撃対策（パスワードリセット時）
- パスワードリセットトークンの短寿命化（1時間）・使用済みトークンの再利用防止

---

## ⚠️ 制限事項

このテンプレートはセキュリティを優先し、**1 ユーザー 1 セッション** の構成を採用しています。

そのため、

- PC でログイン
- スマートフォンでログイン

した場合、先にログインしていたセッションは無効化されます。同様に、パスワード変更・パスワードリセットを行った場合も、既存の全セッションが無効化されます。

複数デバイスでの同時ログインを許可する場合は、`RevokeAllByUserIdAsync()` の設計を変更する必要があります。

また、パスワードリセットメールの送信には個人のGmailアカウント（SMTP）を使用しており、開発・検証用途を想定しています。本番運用する場合は、SendGrid や AWS SES 等のメール配信サービスに `IEmailService` の実装を差し替えることを推奨します。

---

## 📈 今後の拡張

- ロール管理
- メール認証（アカウント登録時の確認メール）
- MFA
- Redis セッション管理
- WebAuthn（パスキー）
- Audit Log
- BFF パターンへの移行
- フロントエンドのコンテナ化（Nginx配信）
- 本番向けメール配信サービス（SendGrid等）への切り替え

---

## 📝 ライセンス

MIT License