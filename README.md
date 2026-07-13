# 認証テンプレート  
Vue + ASP.NET Core（Clean Architecture）  
JWT / Refresh Token（HttpOnly Cookie） / OpenAPI / 型生成

---

## 📌 概要

このプロジェクトは、以下の技術を組み合わせた **実務レベルの認証テンプレート** です。

- **Vue 3（TypeScript）**
- **ASP.NET Core（C#）**
- **Clean Architecture**
- **JWT（アクセストークン）**
- **リフレッシュトークン（HttpOnly Cookie）**
- **OpenAPI（Swagger）**
- **api-typescript-codegen による型生成**
- **MySQL 8.0**
- **Docker / docker-compose**

認証機能を安全に実装するためのベストプラクティスをすべて盛り込んでいます。

---

## 🧱 アーキテクチャ構成

```
backend/
  Api/
  Application/
  Domain/
  Infrastructure/

frontend/
  src/
    api/generated/
    stores/
    pages/
    components/

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
```

---

## 🔐 認証方式

### アクセストークン（JWT）
- 有効期限：短寿命（5〜15分）
- 保存場所：フロントのメモリ（Pinia）
- Authorization ヘッダで送信

### リフレッシュトークン（Cookie）
- HttpOnly / Secure / SameSite=strict
- 有効期限：長寿命（7〜30日）
- `/auth/refresh` でローテーション方式

---

## 📚 ドキュメント一覧（doc/）

| ファイル名 | 内容 |
|------------|------|
| architecture.md | Clean Architecture 全体設計 |
| domain-design.md | Domain 層（User / RefreshToken / ValueObject） |
| application-design.md | UseCase / DTO / Interface |
| infrastructure-design.md | Repository / TokenService / DbContext |
| api-design.md | API 仕様（OpenAPI / Cookie / JWT） |
| db-design.md | DB 設計（users / refresh_tokens） |
| auth-flow.md | 認証フロー（Register / Login / Refresh / Logout） |
| security.md | セキュリティ仕様（XSS / CSRF / Argon2id） |

---

## 🚀 起動方法（Docker）

### 1. ビルド & 起動

```bash
docker-compose up --build
```

### 2. アクセス

| サービス | URL |
|----------|------|
| フロント（Vue） | http://localhost:5173 |
| バックエンド（ASP.NET Core） | http://localhost:5000 |
| Swagger（OpenAPI） | http://localhost:5000/swagger |

---

## 🧪 テスト

- Domain 層は外部依存がないためユニットテストが容易  
- Application 層は Interface によりモック可能  
- Infrastructure 層は Repository / TokenService のテストが可能  

---

## 🔧 使用技術

### Backend
- ASP.NET Core 8
- Clean Architecture
- EF Core
- MySQL 8.0
- JWT（System.IdentityModel.Tokens.Jwt）
- Argon2id（パスワードハッシュ）

### Frontend
- Vue 3
- TypeScript
- Pinia
- Vite
- api-typescript-codegen（型生成）

### DevOps
- Docker / docker-compose

---

## 🛡 セキュリティ対策

- Argon2id によるパスワードハッシュ  
- HttpOnly / Secure / SameSite=strict Cookie  
- リフレッシュトークンのローテーション方式  
- JWT の短寿命化  
- XSS / CSRF / Replay Attack 対策  
- Clean Architecture による責務分離  

---

## 📈 今後の拡張

- ロール管理（Admin / User）
- MFA（多要素認証）
- メール認証（Email Verification）
- パスワードリセット
- WebAuthn（パスキー）
- Redis セッション管理
- Audit Log（監査ログ）

---

## 📝 ライセンス

MIT License

---

