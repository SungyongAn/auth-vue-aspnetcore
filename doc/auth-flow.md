# auth-flow.md  
# 認証フロー設計書  
Vue + ASP.NET Core（Clean Architecture）認証テンプレート

---

## 1. 概要

本ドキュメントでは、認証システムにおける以下のフローを整理します。

- 新規登録（Register）
- ログイン（Login）
- アクセストークン再発行（Refresh）
- ログアウト（Logout）

本システムは以下の方式を採用しています。

- **アクセストークン（JWT）**：短寿命、フロントのメモリに保持  
- **リフレッシュトークン（Cookie）**：長寿命、HttpOnly Cookie に保存  
- **リフレッシュトークンのローテーション方式**  
- **Cookie は Secure / HttpOnly / SameSite=strict**

---

## 2. 認証フロー全体図（概要）

```
[Register] → [Login] → [AccessToken発行] → [RefreshToken発行] → [Cookie保存]

[AccessToken期限切れ]
        ↓
[Refresh] → [新AccessToken発行] → [RefreshTokenローテーション]

[Logout] → [Cookie削除]
```

---

## 3. 新規登録（Register）

### フロー概要

1. フロントが `/auth/register` に Email / Password を送信  
2. Application 層で Email 重複チェック  
3. PasswordHash ValueObject を生成  
4. User エンティティを作成  
5. DB に保存  
6. LoginUseCase を呼び出し、自動ログイン  
7. AccessToken をレスポンスで返す  
8. RefreshToken を Cookie に設定（HttpOnly）

### 入力（RegisterRequest）

```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

### 出力（LoginResponse）

```json
{
  "accessToken": "xxxxx",
  "expiresIn": 900,
  "userId": "guid",
  "email": "user@example.com"
}
```

---

## 4. ログイン（Login）

### フロー概要

1. フロントが `/auth/login` に Email / Password を送信  
2. UserRepository でユーザー取得  
3. PasswordHash を検証  
4. TokenService が AccessToken を生成  
5. TokenService が RefreshToken を生成  
6. RefreshTokenRepository に保存  
7. AccessToken をレスポンスで返す  
8. RefreshToken を Cookie に設定（HttpOnly）

### 入力（LoginRequest）

```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

### 出力（LoginResponse）

```json
{
  "accessToken": "xxxxx",
  "expiresIn": 900,
  "userId": "guid",
  "email": "user@example.com"
}
```

---

## 5. アクセストークン再発行（Refresh）

### フロー概要

1. フロントが `/auth/refresh` を呼び出す（Cookie のみ送信）  
2. RefreshTokenRepository が Cookie のトークンを検証  
3. 有効期限チェック  
4. 古いトークンを無効化（revoked_at 設定）  
5. 新しい RefreshToken を生成  
6. DB に保存  
7. 新しい AccessToken を発行  
8. 新しい RefreshToken を Cookie に設定（ローテーション）

### 入力
- Cookie の refreshToken（HttpOnly）

### 出力（RefreshResponse）

```json
{
  "accessToken": "xxxxx",
  "expiresIn": 900
}
```

---

## 6. ログアウト（Logout）

### フロー概要

1. フロントが `/auth/logout` を呼び出す  
2. Cookie の refreshToken を削除（Max-Age=0）  
3. DB のトークンは必要に応じて無効化  
4. レスポンスは 204 No Content

---

## 7. Cookie 設定（セキュリティ）

| 設定項目 | 値 |
|---------|----|
| HttpOnly | true |
| Secure | true |
| SameSite | Strict |
| Path | /auth/refresh |
| Max-Age | リフレッシュトークンの有効期限 |

### 理由
- JavaScript から参照不可（XSS対策）  
- CSRF対策として SameSite=strict  
- HTTPS 必須（Secure=true）  

---

## 8. 認証フローのポイント（重要）

### ✔ アクセストークンは短寿命  
→ セキュリティ向上  
→ フロントのメモリに保持（Pinia）

### ✔ リフレッシュトークンは Cookie に保存  
→ HttpOnly で安全  
→ XSS で盗まれない

### ✔ ローテーション方式  
→ 毎回新しいトークンを発行  
→ 古いトークンは無効化  
→ セキュリティが大幅に向上

### ✔ Domain / Application / Infrastructure / Api の責務分離  
→ Clean Architecture に完全準拠  
→ テストしやすい  
→ 再利用しやすい

---

## 9. 今後の拡張性

- ロール管理（Admin / User）  
- MFA（多要素認証）  
- パスワードリセット  
- メール認証（Email Verification）  
- アカウントロック  
- セッション管理（Redis）  

---
