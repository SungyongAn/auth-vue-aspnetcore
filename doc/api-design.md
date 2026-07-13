# api-design.md  
# 認証システム API 層設計書  
Vue + ASP.NET Core（Clean Architecture）認証テンプレート

---

## 1. 概要

API 層（Auth.Api）は、認証システムの **HTTP インターフェース** を提供する層です。  
Application 層の UseCase を呼び出し、DTO を使ってリクエストとレスポンスを処理します。

また、Swagger（OpenAPI）を用いて API 仕様を自動生成し、  
フロントエンドでは api-typescript-codegen により型と API クライアントを自動生成します。

さらに、リフレッシュトークンは **HttpOnly Cookie** として返却し、  
セキュリティを高めた認証フローを実現します。

---

## 2. API 層の責務

### ✔ Application 層の UseCase を呼び出す  
- LoginUseCase  
- RegisterUseCase  
- RefreshUseCase  

### ✔ DTO を使ってリクエスト・レスポンスを扱う  
- LoginRequest  
- LoginResponse  
- RegisterRequest  
- RefreshResponse  

### ✔ Cookie を設定する（HttpOnly / Secure / SameSite=strict）  
- リフレッシュトークンを Cookie に保存  
- アクセストークンはレスポンスで返却  

### ✔ Swagger（OpenAPI）を生成する  
- フロントの型生成に利用  
- API 仕様書として機能  

### ✔ CORS 設定  
- Vue（http://localhost:5173）からのアクセスを許可  

---

## 3. エンドポイント一覧

| メソッド | パス | 説明 |
|---------|------|------|
| POST | /auth/register | 新規登録（自動ログイン） |
| POST | /auth/login | ログイン |
| POST | /auth/refresh | リフレッシュトークンによるアクセストークン再発行 |
| POST | /auth/logout | ログアウト（Cookie 削除） |

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

#### Response（LoginResponse）

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
- 有効期限：7〜30日  
- Secure: true  
- SameSite: Strict  

---

### 4.2 POST /auth/login  
ログイン処理を行い、アクセストークンとリフレッシュトークンを返します。

#### Request Body（LoginRequest）

```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

#### Response（LoginResponse）

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
- ローテーション方式で新しいトークンを発行  

---

### 4.3 POST /auth/refresh  
Cookie のリフレッシュトークンを使ってアクセストークンを再発行します。

#### Request
Cookie のみ（Body は不要）

#### Response（RefreshResponse）

```json
{
  "accessToken": "xxxxx",
  "expiresIn": 900
}
```

#### Cookie（HttpOnly）
- refreshToken をローテーション  
- 古いトークンは無効化（RevokedAt 設定）  

---

### 4.4 POST /auth/logout  
リフレッシュトークン Cookie を削除します。

#### Response
204 No Content

#### Cookie
- refreshToken を削除（Max-Age=0）

---

## 5. Cookie 設定（セキュリティ）

API 層ではリフレッシュトークンを Cookie に設定します。

### Cookie 設定例

| 設定項目 | 値 |
|---------|----|
| HttpOnly | true |
| Secure | true |
| SameSite | Strict |
| Path | /auth/refresh |
| Max-Age | リフレッシュトークンの有効期限 |

### 理由
- JavaScript から参照できない（XSS 対策）  
- CSRF を防ぐため SameSite=strict  
- HTTPS 必須（Secure=true）  

---

## 6. Swagger（OpenAPI）設定

### 自動生成される仕様
- DTO（Application 層）  
- エンドポイント  
- リクエスト  
- レスポンス  
- スキーマ  

### フロント側の型生成

```bash
npx api-typescript-codegen \
  --input http://localhost:5000/swagger/v1/swagger.json \
  --output src/api/generated
```

---

## 7. CORS 設定

### 許可するオリジン
- http://localhost:5173（Vue）

### 設定例

- AllowCredentials: true  
- AllowHeaders: "*"  
- AllowMethods: "*"  

---

## 8. API 層のメリット

- Application 層と明確に分離されている  
- Cookie と JWT を安全に扱える  
- OpenAPI によりフロントとの同期が容易  
- 認証テンプレートとして再利用しやすい  

---

## 9. 今後の拡張性

- ロールベース認可（Authorize 属性）  
- メール認証（Email Verification）  
- パスワードリセット API  
- MFA（多要素認証）  
- ログ出力（Serilog）  

---

