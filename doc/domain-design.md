# domain-design.md  
# 認証システム Domain 層設計書  
Vue + ASP.NET Core（Clean Architecture）認証テンプレート

---

## 1. 概要

Domain 層は、認証システムにおける **ビジネスルールの中心** となる層です。  
UI・DB・外部サービスに依存せず、認証に関する本質的なルールのみを保持します。

本システムでは以下の要素を Domain 層に配置します。

- User エンティティ  
- RefreshToken エンティティ  
- ValueObject（Email, PasswordHash）  
- DomainService（パスワード検証など）

Domain 層は **不変条件（Invariant）を守る役割** を持ち、  
アプリケーション全体の整合性を担保します。

---

## 2. Domain 層の責務

### ✔ 認証ロジックの核となるルールを保持する  
例：  
- Email の形式チェック  
- パスワードハッシュの保持  
- RefreshToken の有効期限管理  
- User の状態管理（Active / Locked など）

### ✔ 外部技術に依存しない  
- DB（EF Core）  
- HTTP（Controller）  
- JWT  
- Cookie  
- Vue  
などの技術は一切知らない。

### ✔ 不変条件（Invariant）を守る  
例：  
- Email は必ず正しい形式である  
- PasswordHash は平文を保持しない  
- RefreshToken は期限切れかどうか判定できる  

---

## 3. エンティティ設計

### 3.1 User エンティティ

#### 役割
- 認証対象となるユーザーを表す  
- Email と PasswordHash を ValueObject として保持  
- RefreshToken と関連する（1対多）

#### 推奨フィールド

```csharp
User
- Id (Guid)
- Email (Email ValueObject)
- PasswordHash (PasswordHash ValueObject)
- CreatedAt (DateTime)
- UpdatedAt (DateTime)
```

#### 不変条件
- Email は必ず正しい形式  
- PasswordHash は平文を保持しない  
- Id は不変

---

### 3.2 RefreshToken エンティティ

#### 役割
- リフレッシュトークンの発行・ローテーション管理  
- 有効期限の判定  
- トークンのハッシュ化保存（セキュリティ対策）

#### 推奨フィールド

```csharp
RefreshToken

- Id (Guid)

- UserId (Guid)

- TokenHash (string)

- ExpiresAt (DateTime)

- CreatedAt (DateTime)

- RevokedAt (DateTime?)
```


#### 不変条件
- TokenHash は平文を保持しない  
- ExpiresAt は必ず未来の日時  
- RevokedAt が null の場合のみ有効

---

## 4. ValueObject 設計

### 4.1 Email ValueObject

#### 役割
- Email の形式チェック  
- 不正な Email を Domain 層で排除する  
- User エンティティの不変条件を守る

#### 不変条件
- RFC に準拠した形式であること  
- null / 空文字を許容しない  

---

### 4.2 PasswordHash ValueObject

#### 役割
- パスワードのハッシュ値を保持  
- 平文パスワードを保持しない  
- ハッシュ化ロジックは Infrastructure 層に委譲する

#### 不変条件
- 平文パスワードを保持しない  
- null / 空文字を許容しない  

---

## 5. DomainService 設計

### 5.1 PasswordService（例）

#### 役割
- パスワードの検証（ハッシュとの比較）  
- Argon2id のパラメータを Domain 層で保持  
- 実際のハッシュ化は Infrastructure 層で実装

#### 不変条件
- 平文パスワードを返さない  
- ハッシュ化ロジックは外部に依存しない（抽象化）

---

## 6. ドメインルール（認証システム特有）

### ✔ Email は ValueObject で管理する  
→ 不正な Email を早期に排除できる

### ✔ PasswordHash は ValueObject で管理する  
→ 平文パスワードを保持しない

### ✔ RefreshToken は期限切れ判定を持つ  
例：

```csharp
bool IsExpired => DateTime.UtcNow > ExpiresAt;
```


### ✔ RefreshToken はローテーション方式  
- 新しいトークンを発行  
- 古いトークンは RevokedAt を設定  
- セキュリティ向上

### ✔ User は RefreshToken を複数持つ  
→ ローテーション方式に対応

---

## 7. Domain 層のメリット

- 認証ロジックが UI や DB に依存しない  
- テストが容易（モックで検証可能）  
- Clean Architecture の原則に完全準拠  
- 認証テンプレートとして再利用しやすい  
- セキュリティ要件をコードで担保できる  

---

## 8. 今後の拡張性

- ロール管理（Admin / User）  
- MFA（多要素認証）  
- パスワードリセット  
- アカウントロック  
- メール認証（Email Verification）  

Domain 層の設計が正しいほど、これらの拡張が容易になります。

---