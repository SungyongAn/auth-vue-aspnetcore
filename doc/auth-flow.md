# auth-flow.md

# 認証フロー設計書

Vue + ASP.NET Core（Clean Architecture）認証テンプレート

> **更新履歴:** `/auth/me`（保護エンドポイント）の追加、ログイン時の一括トークン無効化、フロントエンドのSilent Refresh／401自動リトライのフローを反映

---

## 1. 概要

本ドキュメントでは、認証システムにおける以下のフローを整理します。

- 新規登録（Register）
- ログイン（Login）
- アクセストークン再発行（Refresh）
- ログアウト（Logout）
- **認証ユーザー情報取得（Me）**（追加）

本システムは以下の方式を採用しています。

- **アクセストークン（JWT）**：短寿命、フロントのメモリに保持
- **リフレッシュトークン（Cookie）**：長寿命、HttpOnly Cookie に保存
- **リフレッシュトークンのローテーション方式**（ログイン時は既存トークンを一括無効化）
- **Cookie は Secure / HttpOnly / SameSite=strict**
- **JWT Bearer 認証による保護エンドポイントの検証**（追加）

---

## 2. 認証フロー全体図（概要、更新）

```
[Register] → [Login処理へ委譲] → [AccessToken発行] → [RefreshToken発行] → [Cookie保存]

[Login] → [既存RefreshTokenを一括無効化] → [AccessToken発行] → [RefreshToken発行] → [Cookie保存]

[AccessToken期限切れ、または保護API呼び出しで401]
        ↓
[Refresh] → [新AccessToken発行] → [RefreshTokenローテーション]

[保護エンドポイント呼び出し（例：/auth/me）]
        ↓
[JWT Bearer 検証] → [Claims からユーザー情報取得] → [Response]

[Logout] → [DB上のRefreshToken無効化] → [Cookie削除]

[ChangePassword（ログイン中）] → [現在のパスワード検証] → [新パスワード更新] → [全RefreshToken無効化]

[ForgotPassword] → [PasswordResetToken発行] → [メール送信]
        ↓（メール内リンククリック）
[ResetPassword] → [トークン検証] → [新パスワード更新] → [全RefreshToken無効化]

[アプリ起動時（フロント）]
        ↓
[Silent Refresh：/auth/refresh を自動呼び出し] → [有効なCookieがあればログイン状態復元]
```

---

## 3. 新規登録（Register）

### フロー概要（更新）

1. フロントが `/auth/register` に Email / Password を送信
2. Application 層で Email 重複チェック
3. `IPasswordHasher.Hash`（Argon2id）で PasswordHash を生成
4. User エンティティを作成
5. DB に保存
6. **`LoginUseCase` を実際に呼び出し、自動ログイン**（ここでログイン時の既存トークン無効化ロジックも適用される）
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

### エラーケース

- Email重複 → `409 Conflict`（`EmailAlreadyExistsException`）

---

## 4. ログイン（Login、更新）

### フロー概要

1. フロントが `/auth/login` に Email / Password を送信
2. UserRepository でユーザー取得
3. `IPasswordHasher.Verify` でパスワードを検証（定数時間比較によりタイミング攻撃対策済み）
4. **該当ユーザーの既存の有効なリフレッシュトークンをすべて無効化**（`RevokeAllByUserIdAsync`、セッション固定攻撃対策）
5. TokenService が AccessToken を生成
6. TokenService が RefreshToken を生成（生トークン＋ハッシュ化エンティティのペア）
7. RefreshTokenRepository に保存
8. AccessToken をレスポンスで返す
9. RefreshToken（生トークン）を Cookie に設定（HttpOnly）

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

### エラーケース

- ユーザー不在、またはパスワード不一致 → `401 Unauthorized`（`InvalidCredentialsException`。ユーザーの存在有無を区別せず同一メッセージを返し、ユーザー列挙攻撃を防止）

### 動作確認済みの挙動（追加）

実装時の動作確認により、Register直後に発行されたトークンが、その後のLogin実行時刻と同時刻で `revoked_at` が設定され無効化されることをDBレベルで確認済み。

---

## 5. アクセストークン再発行（Refresh、更新）

### フロー概要

1. フロントが `/auth/refresh` を呼び出す（Cookie のみ送信）
2. Cookieの生トークンを `ITokenService.HashToken` でハッシュ化
3. RefreshTokenRepository が `GetValidTokenAsync(hash)` で検証（`RevokedAt == null && ExpiresAt > UtcNow` をSQLレベルで絞り込み）
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

### エラーケース

- Cookie不在、トークン無効・期限切れ → `401 Unauthorized`（`InvalidRefreshTokenException`）
- トークンに紐づくユーザーが存在しない → `404 Not Found`（`UserNotFoundException`。通常発生しないが、ユーザー削除後に古いトークンが使われた場合等を想定）

---

## 6. ログアウト（Logout、更新）

### フロー概要

1. フロントが `/auth/logout` を呼び出す
2. Cookie の生トークンをハッシュ化して検索
3. 見つかった場合はDB上のトークンを無効化（見つからない場合も冪等に成功扱い）
4. Cookie の refreshToken を削除（空値＋過去日時で上書き）
5. レスポンスは `204 No Content`

### フロント側の挙動（追加）

`AuthService.postAuthLogout()` の呼び出しが失敗した場合でも、`try/finally` によりローカルのセッション状態（Piniaストア）は必ずクリアされる設計。ネットワークエラー等でAPIが失敗しても、フロント側では確実にログアウト状態になる。

---

## 7. 認証ユーザー情報取得（Me）※新規追加

保護エンドポイントの動作確認、およびテンプレートとしてのサンプル実装を兼ねて追加。

### フロー概要

1. フロントが `Authorization: Bearer <accessToken>` ヘッダー付きで `/auth/me` を呼び出す
2. JWT Bearer 認証ミドルウェアが署名・有効期限を検証
3. 検証成功時、`User.FindFirst("userId")` / `User.FindFirst("email")` でクレームから情報を取得
4. UserInfoResponse を返す

### 出力（UserInfoResponse）

```json
{
  "userId": "guid",
  "email": "user@example.com"
}
```

### エラーケース

- トークン不正・期限切れ・未送信 → `401 Unauthorized`（`WWW-Authenticate: Bearer` ヘッダー付き、JWT Bearer認証ミドルウェアが自動的に返却）

### 実装上の注意点（重要）

ASP.NET Core は標準クレーム名（`email` 等）を独自のURI形式クレーム型に自動マッピングする挙動があるため、`JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear()` を `Program.cs` で呼び出し、発行時のクレーム名（`userId`, `email`）がそのまま取得できるようにしている。

---

## 7.5 パスワード変更（Change Password）※新規追加

### フロー概要

1. フロントが `Authorization: Bearer <accessToken>` 付きで `/auth/change-password` を呼び出す
2. JWT Bearer 認証で本人確認
3. `IPasswordHasher.Verify` で現在のパスワードを検証
4. 新しいパスワードをハッシュ化して更新
5. **該当ユーザーの全リフレッシュトークンを無効化**
6. フロント側も明示的にセッションをクリアし、再ログインを促す

### 入力（ChangePasswordRequest）

```json
{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewPassword456!"
}
```

### 出力

`204 No Content`

### エラーケース

- 現在のパスワードが不一致 → `401 Unauthorized`（`InvalidCredentialsException`）

---

## 7.6 パスワードリセット（Forgot / Reset Password）※新規追加

### フロー概要（2段階）

**Step 1: リセットリクエスト**

1. フロントが `/auth/forgot-password` にメールアドレスを送信
2. ユーザーが存在すれば、`PasswordResetToken`（有効期限1時間）を生成しDBに保存
3. リセットリンク付きメールを送信（`IEmailService` 経由）
4. **ユーザーが存在してもしなくても同じ成功レスポンスを返す**（メールアドレス列挙攻撃対策）

**Step 2: リセット実行**

1. ユーザーがメール内のリンク（`{Frontend:BaseUrl}/reset-password?token=xxxxx`）をクリック
2. フロントの `ResetPasswordView` が新パスワードの入力を受け付け、`/auth/reset-password` を呼び出す
3. トークンの有効性を検証（未使用・期限内であること）
4. 新しいパスワードをハッシュ化して更新
5. トークンを使用済みにする
6. **該当ユーザーの全リフレッシュトークンを無効化**

### 入力（ForgotPasswordRequest）

```json
{
  "email": "user@example.com"
}
```

### 入力（ResetPasswordRequest）

```json
{
  "token": "xxxxx",
  "newPassword": "NewPassword456!"
}
```

### 出力

いずれも `204 No Content`

### エラーケース

- リセットトークンが無効・期限切れ・使用済み → `401 Unauthorized`（`InvalidResetTokenException`。理由は区別せず統一メッセージ）

### フロント側の画面遷移

- `/forgot-password`：メールアドレス入力画面。送信後は成功・失敗に関わらず同じ「メールを送信しました」画面を表示
- `/reset-password?token=xxxxx`：新パスワード入力画面。URLに `token` クエリパラメータが無い場合は「無効なリンクです」を表示し、`/forgot-password` への導線を出す

---

## 8. Cookie 設定（セキュリティ）

| 設定項目 | 値                                                       |
| -------- | -------------------------------------------------------- |
| HttpOnly | true                                                     |
| Secure   | true                                                     |
| SameSite | Strict                                                   |
| Path     | /auth/refresh                                            |
| Max-Age  | `JwtOptions.RefreshTokenExpiresInDays`（既定14日）と同期 |

### 理由

- JavaScript から参照不可（XSS対策）
- CSRF対策として SameSite=strict
- HTTPS 必須（Secure=true）

---

## 9. フロントエンド側のフロー（追加）

### ✔ アプリ起動時（Silent Refresh）

1. `main.ts` の `initializeAuth()` が `app.mount()` より前に `/auth/refresh` を呼び出す
2. 有効な `refreshToken` Cookie があれば AccessToken を復元し、ログイン状態で画面表示
3. 無効・不在の場合は `401` となるが正常系としてキャッチし、未ログイン状態で画面表示
4. 初回訪問時は必ず401が発生するが、これは仕様として許容している

### ✔ 保護API呼び出し時の401自動リトライ

1. `withAuthRetry()` でラップされた呼び出しが `401`（`ApiError`）を検知
2. 自動的に `/auth/refresh` を呼び出す
3. 成功すれば元のリクエストを1回だけリトライ
4. 失敗すればセッションをクリアし、Routerガードにより `/login` へ誘導される

---

## 10. 認証フローのポイント（重要）

### ✔ アクセストークンは短寿命

→ セキュリティ向上
→ フロントのメモリに保持（Pinia、**localStorageは使用しない**）

### ✔ リフレッシュトークンは Cookie に保存

→ HttpOnly で安全
→ XSS で盗まれない

### ✔ ローテーション方式

→ 毎回新しいトークンを発行
→ 古いトークンは無効化
→ **ログイン時は該当ユーザーの全トークンを無効化**（セッション固定攻撃対策の強化）
→ セキュリティが大幅に向上

### ✔ Domain / Application / Infrastructure / Api の責務分離

→ Clean Architecture に完全準拠
→ テストしやすい
→ 再利用しやすい

### ✔ エラーレスポンスの統一

→ グローバル例外ハンドラーにより `ProblemDetails` 形式で統一
→ フロント側は `ApiError.status` による分岐でハンドリングを一本化

---

## 11. 今後の拡張性

- ロール管理（Admin / User）
- MFA（多要素認証）
- パスワードリセット
- メール認証（Email Verification）
- アカウントロック
- セッション管理（Redis）
- ログイン試行回数制限・IPレートリミット

---
