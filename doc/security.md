# security.md

# 認証システム セキュリティ設計書

Vue + ASP.NET Core（Clean Architecture）認証テンプレート

> **更新履歴:** JWT検証（Bearer認証）の追加、Argon2id実装のタイミング攻撃対策、鍵管理の実装方針を反映

---

## 1. 概要

本ドキュメントでは、認証システムにおける主要なセキュリティ対策を整理します。

本システムは以下の方式を採用しています。

- **JWT（アクセストークン）＋リフレッシュトークン（Cookie）方式**
- **リフレッシュトークンのローテーション方式**
- **Argon2id によるパスワードハッシュ**
- **HttpOnly / Secure / SameSite=strict Cookie**
- **XSS / CSRF / Replay Attack 対策**
- **Clean Architecture による責務分離**
- **JWT Bearer 認証によるアクセストークンの検証**（追加）

---

## 2. パスワードハッシュ（Argon2id）

### 採用理由

- 現代の標準的なパスワードハッシュ方式
- GPU による高速攻撃に強い
- メモリハード性により総当たり攻撃を困難にする

### 採用パラメータ（実装値）

| パラメータ  | 値                                     |
| ----------- | -------------------------------------- |
| Type        | Argon2id                               |
| Memory      | 64MB                                   |
| Iterations  | 3                                      |
| Parallelism | 1                                      |
| Salt        | ハッシュごとにランダム生成（16バイト） |
| Hash出力長  | 32バイト                               |

実装：`Konscious.Security.Cryptography.Argon2` パッケージ、`Argon2PasswordHasher`（Infrastructure層）。

### 設計ポイント

- 平文パスワードは絶対に保持しない
- Domain 層では PasswordHash ValueObject として扱う（`ToString()` はハッシュ値そのものを返さず `"[PasswordHash]"` を返す設計とし、ログ等への意図しない露出を防止）
- 実際のハッシュ化は Infrastructure 層（`IPasswordHasher` の実装）で行う。UseCase はアルゴリズムの詳細を意識しない
- **ハッシュ照合はタイミング攻撃対策として `CryptographicOperations.FixedTimeEquals` による定数時間比較を使用**（追加。単純な文字列比較 `==` は使用しない）

---

## 3. JWT（アクセストークン）

### 特徴

- 有効期限は短寿命（15分、`JwtOptions.AccessTokenExpiresInMinutes` で設定可能）
- フロントのメモリ（Pinia）に保持
- Authorization ヘッダで送信

### セキュリティ対策

- HS256 署名（SymmetricSecurityKey）
- 有効期限を短くすることで漏洩リスクを低減
- UserId / Email のみ最低限のクレームを含める

### JWT の検証（追加）

発行だけでなく、実際に保護エンドポイントでトークンを検証する仕組みとして、ASP.NET Core の JWT Bearer 認証ミドルウェアを導入しています。

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
```

- `ValidateLifetime = true` により、期限切れのトークンは自動的に拒否される
- `ClockSkew = TimeSpan.Zero` により、サーバー間の時刻ズレに対する猶予時間を設けず、期限厳守で検証する（デフォルトは5分の猶予があるため、意図的に厳格化）
- 署名検証キー（`IssuerSigningKey`）は、発行時（`TokenService`）と検証時（Bearer認証ミドルウェア）で **同一の `Jwt:Key`** を使用する必要がある

---

## 4. リフレッシュトークン（Cookie）

### 保存場所

- HttpOnly Cookie
- JavaScript から参照不可（XSS対策）

### Cookie 設定

| 設定項目 | 値                                                       |
| -------- | -------------------------------------------------------- |
| HttpOnly | true                                                     |
| Secure   | true                                                     |
| SameSite | Strict                                                   |
| Path     | /auth/refresh                                            |
| Max-Age  | `JwtOptions.RefreshTokenExpiresInDays`（既定14日）と同期 |

### セキュリティ理由

- **HttpOnly** → JS から盗まれない
- **Secure** → HTTPS 必須（開発時、`http://localhost` で curl 等のツールを使う場合は Cookie が保存されないことがある点に注意。ブラウザは `localhost` を特別扱いするため通常問題ない）
- **SameSite=strict** → CSRF 対策
- **Path=/auth/refresh** → Cookie の送信範囲を限定

---

## 5. リフレッシュトークンのローテーション方式

### 方式概要

- ログイン時に新しいトークンを発行し、**該当ユーザーの既存の有効なトークンをすべて無効化**する（`RevokeAllByUserIdAsync`）
- 古いトークンは revoked_at を設定して無効化
- Refresh API 呼び出し時も同様にローテーション
- 二重 Revoke を防ぐガードを Domain 層の `RefreshToken.Revoke()` に実装（既に無効化済みのトークンに対する再無効化は例外を投げる）

### なぜローテーションが必要か

- トークンが漏洩しても「古いトークンは使えない」
- セキュリティが大幅に向上する
- OAuth2 のベストプラクティスに準拠

### リフレッシュトークンの生成方式（実装詳細、追加）

- 生トークンは `Guid.NewGuid().ToString("N")` で生成
- DB には **SHA256 でハッシュ化した値**のみを保存（決定的ハッシュ。検索用途のため高速性を優先し、パスワードハッシュとは異なるアルゴリズムを意図的に使い分けている）
- Cookie には生トークンを設定し、次回の `/auth/refresh` 呼び出し時にサーバー側でハッシュ化して DB と照合する

---

## 6. XSS 対策

### 採用している対策

- リフレッシュトークンを HttpOnly Cookie に保存
- アクセストークンはフロントのメモリに保持（**localStorage / sessionStorage には一切保存しない**）
- Vue のテンプレートは自動エスケープ

### 禁止事項

- アクセストークンを localStorage に保存しない
- Cookie にアクセストークンを保存しない

### 実装上の補足（追加）

フロントエンドの Pinia ストア（`stores/auth.ts`）では、`accessToken` を `state` にのみ保持し、`localStorage` への読み書きを一切行わない設計を徹底している。ページリロード時は状態が失われるため、Cookie（HttpOnly、`refreshToken`）を用いた Silent Refresh（`/auth/refresh` の自動呼び出し）でセッションを復元する。

---

## 7. CSRF 対策

### 採用している対策

- SameSite=strict Cookie
- Refresh / Logout API の Path を限定（/auth/refresh）
- 認証 API はすべて POST（`/auth/me` は例外的に GET だが、状態を変更しない読み取り専用エンドポイントであり、リフレッシュトークンCookieではなくAuthorizationヘッダーのアクセストークンで認証するためCSRFの対象外）

### なぜ SameSite=strict が重要か

- 他サイトからのクロスサイトリクエストで Cookie が送信されない
- CSRF 攻撃を根本的に防止できる

---

## 8. Replay Attack 対策

### 対策内容

- リフレッシュトークンはハッシュ化して保存
- ローテーション方式で古いトークンを無効化
- 有効期限を短く設定

### なぜハッシュ化が必要か

- DB が漏洩してもトークンを再利用できない
- パスワードと同様に扱うべき情報

---

## 9. Brute Force（総当たり攻撃）対策

### 対策内容

- Argon2id によるハッシュ化
- Email に UNIQUE 制約
- パスワード照合のタイミング攻撃対策（`FixedTimeEquals`、追加）
- ログイン失敗回数の制限（拡張予定）
- IP ベースのレートリミット（拡張予定）

---

## 10. セッション固定攻撃対策

### 対策内容

- **ログイン時に必ず新しいリフレッシュトークンを発行し、同時に該当ユーザーの既存トークンをすべて無効化する**（`RevokeAllByUserIdAsync`。実データでの動作確認済み：ログイン実行時刻と旧トークンの `revoked_at` が一致することをDBレベルで確認済み）
- 古いトークンは無効化
- Cookie の Path を限定

**設計上の注意（追加）：** この方式は「1ユーザー1セッション」を強制する設計です。複数デバイスでの同時ログインを許可したい場合は `RevokeAllByUserIdAsync` の呼び出しを見直す必要があります（現状はテンプレートとしてセキュリティを優先する方針を採用）。

---

## 10.5 パスワード変更・リセット機能のセキュリティ設計（追加）

### ログイン中のパスワード変更

- 現在のパスワードの入力を必須とする（`IPasswordHasher.Verify` による検証。他人が一時的に端末を操作しただけでは変更できない）
- **変更成功時、該当ユーザーの全リフレッシュトークンを無効化**（ログイン時と同様の `RevokeAllByUserIdAsync`）。攻撃者がセッションを乗っ取っていた場合でも、正規ユーザーがパスワードを変更すれば攻撃者のセッションも同時に無効化される

### パスワード忘却時のリセット

- **メールアドレス列挙攻撃対策**：`/auth/forgot-password` は、指定されたメールアドレスが実際に登録されているかどうかに関わらず、常に同じ成功レスポンス（`204 No Content`）を返す。存在しないメールアドレスでもエラーにしないことで、第三者が「このメールアドレスは登録されているか」を推測できないようにしている
- **リセットトークンの短い有効期限**：1時間（`RefreshToken` の14日と比べて大幅に短く設定。パスワードリセットは緊急性の高い操作であり、長期間有効なリンクを残すリスクを避けるため）
- **リセットトークンもハッシュ化して保存**：`RefreshToken` と同じ設計思想（SHA256による決定的ハッシュ、生トークンはDBに保存しない）
- **使用済みトークンの再利用防止**：`PasswordResetToken.MarkAsUsed()` に二重使用防止ガードを実装（`RefreshToken.Revoke()` と同様のパターン）
- **リセット成功時も全セッションを無効化**：パスワード変更と同様、`RevokeAllByUserIdAsync` を実行
- **エラーメッセージの曖昧化**：トークンが「存在しない」のか「期限切れ」なのか「使用済み」なのかをクライアントに区別して伝えない（`InvalidResetTokenException` に統一）。攻撃者に有効なトークンを推測する手がかりを与えないため

### メール送信の秘密情報管理

- SMTP認証情報（`Smtp:Username` / `Smtp:Password`）は、JWT鍵やDB接続情報と同様に `appsettings.json` に平文で書き込まず、`dotnet user-secrets`（ローカル）または環境変数（Docker）で管理する
- Gmail等のメールプロバイダを使う場合、通常のアカウントパスワードではなく「アプリパスワード」等の専用認証情報を使用する

---

## 11. 秘密鍵・接続情報の管理（追加）

### 開発時

- `Jwt:Key` および DB 接続文字列（パスワードを含む）は `appsettings.json` に平文で書き込まず、**`dotnet user-secrets`** で管理する

```bash
dotnet user-secrets set "Jwt:Key" "<ランダムな長い文字列>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<接続文字列>"
dotnet user-secrets set "Smtp:Username" "<SMTPユーザー名>"
dotnet user-secrets set "Smtp:Password" "<SMTPパスワード、またはアプリパスワード>"
```

- `appsettings.json` にはプレースホルダー（空文字列など）のみを残す

### Docker実行時

- `docker-compose.yml` の `environment` で環境変数として注入する（`Jwt__Key`, `ConnectionStrings__DefaultConnection` のように、ASP.NET Coreの設定階層区切り文字として `__`（二重アンダースコア）を使用）
- `.env` ファイルに実際の値を書き、**`.gitignore` で除外する**

### Gitへのコミット時の注意

- `bin/` `obj/` などのビルド成果物は `.gitignore` で除外する
- 既に誤ってコミットしてしまった場合は `git rm -r --cached` で追跡を解除する（ファイル自体は残したまま、Git管理からのみ除外）

---

## 12. Clean Architecture によるセキュリティ向上

### メリット

- Domain 層が不変条件を保証（例：`RefreshToken` のコンストラクタで `ExpiresAt` が未来日時であることを検証）
- Application 層が認証フローを統制（カスタム例外による意味のあるエラーハンドリング）
- Infrastructure 層が外部技術（Argon2、JWT、DB）を隔離
- Api 層が Cookie と JWT を安全に扱い、グローバル例外ハンドラーで詳細情報の漏洩を防止

### 結果

- セキュリティロジックが分散せず一貫性が保たれる
- テストが容易
- 拡張が容易

---

## 13. 今後の拡張性

- MFA（多要素認証）
- パスワードリセット
- メール認証（Email Verification）
- ログイン試行回数制限
- IP レートリミット
- WebAuthn（パスキー）
- 監査ログ（Audit Log）
- BFF（Backend for Frontend）パターンへの移行検討（アクセストークンをブラウザに渡さない、よりXSS耐性の高い構成）

---
