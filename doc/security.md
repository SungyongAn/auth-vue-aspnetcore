# security.md  
# 認証システム セキュリティ設計書  
Vue + ASP.NET Core（Clean Architecture）認証テンプレート

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

---

## 2. パスワードハッシュ（Argon2id）

### 採用理由
- 現代の標準的なパスワードハッシュ方式  
- GPU による高速攻撃に強い  
- メモリハード性により総当たり攻撃を困難にする  

### 推奨パラメータ（例）

| パラメータ | 値 |
|------------|----|
| Type | Argon2id |
| Memory | 64MB |
| Iterations | 3 |
| Parallelism | 1 |

### 設計ポイント
- 平文パスワードは絶対に保持しない  
- Domain 層では PasswordHash ValueObject として扱う  
- 実際のハッシュ化は Infrastructure 層で実装する  

---

## 3. JWT（アクセストークン）

### 特徴
- 有効期限は短寿命（5〜15分）  
- フロントのメモリ（Pinia）に保持  
- Authorization ヘッダで送信  

### セキュリティ対策
- HS256 署名（SymmetricSecurityKey）  
- 有効期限を短くすることで漏洩リスクを低減  
- UserId / Email のみ最低限のクレームを含める  

---

## 4. リフレッシュトークン（Cookie）

### 保存場所
- HttpOnly Cookie  
- JavaScript から参照不可（XSS対策）

### Cookie 設定

| 設定項目 | 値 |
|---------|----|
| HttpOnly | true |
| Secure | true |
| SameSite | Strict |
| Path | /auth/refresh |
| Max-Age | リフレッシュトークンの有効期限 |

### セキュリティ理由
- **HttpOnly** → JS から盗まれない  
- **Secure** → HTTPS 必須  
- **SameSite=strict** → CSRF 対策  
- **Path=/auth/refresh** → Cookie の送信範囲を限定  

---

## 5. リフレッシュトークンのローテーション方式

### 方式概要
- ログイン時に新しいトークンを発行  
- 古いトークンは revoked_at を設定して無効化  
- Refresh API 呼び出し時も同様にローテーション  

### なぜローテーションが必要か
- トークンが漏洩しても「古いトークンは使えない」  
- セキュリティが大幅に向上する  
- OAuth2 のベストプラクティスに準拠  

---

## 6. XSS 対策

### 採用している対策
- リフレッシュトークンを HttpOnly Cookie に保存  
- アクセストークンはフロントのメモリに保持（localStorage に保存しない）  
- Vue のテンプレートは自動エスケープ  

### 禁止事項
- アクセストークンを localStorage に保存しない  
- Cookie にアクセストークンを保存しない  

---

## 7. CSRF 対策

### 採用している対策
- SameSite=strict Cookie  
- Refresh API の Path を限定（/auth/refresh）  
- 認証 API はすべて POST  

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
- ログイン失敗回数の制限（拡張予定）  
- IP ベースのレートリミット（拡張予定）  

---

## 10. セッション固定攻撃対策

### 対策内容
- ログイン時に必ず新しいリフレッシュトークンを発行  
- 古いトークンは無効化  
- Cookie の Path を限定  

---

## 11. Clean Architecture によるセキュリティ向上

### メリット
- Domain 層が不変条件を保証  
- Application 層が認証フローを統制  
- Infrastructure 層が外部技術を隔離  
- Api 層が Cookie と JWT を安全に扱う  

### 結果
- セキュリティロジックが分散せず一貫性が保たれる  
- テストが容易  
- 拡張が容易  

---

## 12. 今後の拡張性

- MFA（多要素認証）  
- パスワードリセット  
- メール認証（Email Verification）  
- ログイン試行回数制限  
- IP レートリミット  
- WebAuthn（パスキー）  
- 監査ログ（Audit Log）  

---

