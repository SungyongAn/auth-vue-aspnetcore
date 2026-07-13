# architecture.md  
# 認証システム アーキテクチャ設計書  
Vue + ASP.NET Core（Clean Architecture）認証テンプレート

---

## 1. 概要

本システムは、Vue（TypeScript）と ASP.NET Core（C#）を用いて構築された  
**JWT（アクセストークン）＋リフレッシュトークン（Cookie）方式の認証テンプレート** です。

バックエンドは **Clean Architecture** を採用し、  
認証ロジック・ドメインモデル・ユースケース・インフラ層を明確に分離しています。

また、OpenAPI（Swagger）を用いて API 仕様を自動生成し、  
フロントエンドでは **api-typescript-codegen** により型と API クライアントを自動生成します。

---

## 2. アーキテクチャ構成（Clean Architecture）

本システムは以下の 4 層で構成されます。

```
Auth.Domain        ← ビジネスルール（最内層）
Auth.Application   ← ユースケース・DTO・インターフェース
Auth.Infrastructure← DB・外部サービス・実装
Auth.Api           ← HTTP API（Controller）
```

依存関係は以下の通りです。

```
Domain → Application → Api
Domain → Infrastructure → Application → Api
```

**内側の層は外側に依存しない**ことが Clean Architecture の原則です。

---

## 3. 各層の責務

### 3.1 Auth.Domain（ドメイン層）
**責務：ビジネスルールの中心。認証ロジックの核。**

- User エンティティ  
- RefreshToken エンティティ  
- ValueObject（Email, PasswordHash）  
- DomainService（パスワード検証など）

**特徴：**
- DB や ASP.NET Core を知らない  
- 認証の本質的なルールのみを保持  
- 不変条件（Invariant）を守る役割

---

### 3.2 Auth.Application（アプリケーション層）
**責務：ユースケース（認証処理の流れ）を定義する。**

- DTO（LoginRequest / LoginResponse / RegisterRequest）  
- Interface（IUserRepository / ITokenService）  
- UseCase（Login / Register / Refresh）

**特徴：**
- Domain を使って処理を組み立てる  
- Repository や TokenService の抽象（Interface）を定義  
- OpenAPI に出す DTO はここに置く  
→ フロントの型生成と同期しやすい

---

### 3.3 Auth.Infrastructure（インフラ層）
**責務：外部接続（DB・JWT・Repository の実装）。**

- EF Core の DbContext  
- Repository 実装（UserRepository / RefreshTokenRepository）  
- TokenService（JWT 発行）  
- Migration（EF Core）

**特徴：**
- Application の Interface を実装する  
- DB や外部サービスの知識を持つ  
- Domain や Application に依存するが、逆依存はしない

---

### 3.4 Auth.Api（API 層）
**責務：HTTP API の入口。Controller を提供する。**

- AuthController  
- Cookie 設定（HttpOnly / Secure / SameSite=strict）  
- Swagger（OpenAPI）  
- DI（依存性注入）  
- CORS 設定

**特徴：**
- Application の UseCase を呼び出す  
- OpenAPI を自動生成  
→ フロントの型生成の基盤となる

---

## 4. ディレクトリ構成（最終版）

```
backend/
Auth.Api/
Controllers/
Program.cs
appsettings.json
Dockerfile

Auth.Application/
DTOs/
Interfaces/
UseCases/

Auth.Domain/
Entities/
ValueObjects/
DomainServices/

Auth.Infrastructure/
Data/
Repositories/
Services/
Migrations/

frontend/
src/
api/
generated/
stores/
pages/
components/
styles/
Dockerfile

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
README.md
```

---

## 5. 認証方式（アーキテクチャ観点）

### アクセストークン
- 有効期限：短寿命（5〜15分）
- 保存場所：フロントのメモリ（Pinia）
- API 呼び出し時に Authorization ヘッダで送信

### リフレッシュトークン
- 有効期限：長寿命（7〜30日）
- 保存場所：HttpOnly Cookie（Secure / SameSite=strict）
- `/auth/refresh` でローテーション

### パスワードハッシュ
- Argon2id  
- Domain 層で ValueObject として扱う  
- Infrastructure 層で実際のハッシュ化を実装

---

## 6. OpenAPI と型生成

### バックエンド（Auth.Api）
- Swagger により OpenAPI を自動生成  
- DTO（Application 層）を元に仕様が作られる

### フロントエンド（Vue）
- api-typescript-codegen により型と API クライアントを自動生成  
- バックエンドの変更と同期しやすい

---

## 7. Docker 構成

docker-compose により以下を一括起動：

- backend（ASP.NET Core）  
- frontend（Vue）  
- db（MySQL）

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