# domain-design.md

# 認証システム Domain 層設計書

Vue + ASP.NET Core（Clean Architecture）認証テンプレート

> **更新履歴:** ValueObjectの `sealed record` 化、コンストラクタでの追加バリデーション、二重Revoke防止ガードを反映

---

## 1. 概要

Domain 層は、認証システムにおける **ビジネスルールの中心** となる層です。  
UI・DB・外部サービスに依存せず、認証に関する本質的なルールのみを保持します。

本システムでは以下の要素を Domain 層に配置します。

- User エンティティ
- RefreshToken エンティティ
- ValueObject（Email, PasswordHash）
- 各エンティティのコンストラクタによる不変条件の検証

Domain 層は **不変条件（Invariant）を守る役割** を持ち、  
アプリケーション全体の整合性を担保します。

---

## 2. Domain 層の責務

### ✔ 認証ロジックの核となるルールを保持する

例：

- Email の形式チェック
- パスワードハッシュの保持（平文を持たない）
- RefreshToken の有効期限管理
- RefreshToken の多重無効化防止

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
- RefreshToken の `ExpiresAt` は生成時点で必ず未来の日時（追加：コンストラクタでガード）

---

## 3. エンティティ設計

### 3.1 User エンティティ

#### 役割

- 認証対象となるユーザーを表す
- Email と PasswordHash を ValueObject として保持
- RefreshToken と関連する（1対多）

#### 実装（更新）

```csharp
public class User
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; }
    public PasswordHash PasswordHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // EF Core 用（Nullable Reference Types 警告を明示的に抑制）
    private User()
    {
        Email = null!;
        PasswordHash = null!;
    }

    public User(Email email, PasswordHash passwordHash)
    {
        Id = Guid.NewGuid();
        Email = email ?? throw new ArgumentNullException(nameof(email));
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePassword(PasswordHash newHash)
    {
        PasswordHash = newHash ?? throw new ArgumentNullException(nameof(newHash));
        UpdatedAt = DateTime.UtcNow;
    }
}
```

**変更点：** コンストラクタおよび `UpdatePassword` に null チェックを追加。ValueObjectは参照型（`record`）のため、Nullable Reference Typesの警告だけでは実行時のnull混入を完全に防げない。明示的な `ArgumentNullException` により、不正な状態のUserが生成されることをDomain層で確実に防ぐ。

#### 不変条件

- Email は必ず正しい形式
- PasswordHash は平文を保持しない
- Id は不変
- Email / PasswordHash は null を許容しない

---

### 3.2 RefreshToken エンティティ

#### 役割

- リフレッシュトークンの発行・ローテーション管理
- 有効期限の判定
- トークンのハッシュ化保存（セキュリティ対策）

#### 実装（更新）

```csharp
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private RefreshToken()
    {
        TokenHash = null!;
    }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash cannot be empty.");

        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("ExpiresAt must be in the future.");

        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsActive => RevokedAt == null && !IsExpired;

    public void Revoke()
    {
        if (RevokedAt != null)
            throw new InvalidOperationException("Token is already revoked.");

        RevokedAt = DateTime.UtcNow;
    }
}
```

**変更点：**

1. コンストラクタで `tokenHash` の空文字/null チェックを追加（設計書の不変条件「TokenHashは平文を保持しない」の実装としても、まず空でないことを保証）
2. コンストラクタで `expiresAt` が未来日時であることを検証（不変条件「ExpiresAtは必ず未来の日時」をコード上で強制）
3. `Revoke()` に**二重無効化防止ガード**を追加。既に `RevokedAt` が設定済みのトークンに対して再度 `Revoke()` を呼ぶと `InvalidOperationException` を投げる。これにより、Infrastructure層の `RevokeAllByUserIdAsync` 実装では「`RevokedAt == null` のトークンのみ」を対象にクエリで絞り込むことで、この例外が発生する余地をなくしている

#### 不変条件

- TokenHash は平文を保持しない（かつ空文字・nullを許容しない）
- ExpiresAt は必ず未来の日時（生成時点で検証）
- RevokedAt が null の場合のみ有効
- 一度無効化されたトークンは再度無効化できない（追加）

---

## 4. ValueObject 設計

### 4.1 Email ValueObject（更新）

#### 役割

- Email の形式チェック
- 不正な Email を Domain 層で排除する
- User エンティティの不変条件を守る

#### 実装

```csharp
public sealed record Email
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled);

    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !EmailRegex.IsMatch(value))
            throw new ArgumentException("Invalid email format.");

        Value = value;
    }

    public override string ToString() => Value;
}
```

**変更点：**

- `class` → **`sealed record`** に変更。ValueObjectは値そのものを表すため、`record` にすることで自動的に値の等価性（`Equals`/`GetHashCode`）が実装される。`sealed` を付けることで継承を禁止し、`record` の等価性比較が型情報も含めて厳密に機能するようにしている
- 単純な `Contains("@")` チェックから、正規表現による現実的なフォーマットチェックに強化。RFC完全準拠の正規表現は過剰品質になるため、「`@`が1つ、ドメイン部にドットがある」程度の実用的なチェックに留めている。最終的な保証は将来のメール確認リンク機能（今後の拡張）で行う想定
- `ToString()` は `Value` をそのまま返す（メールアドレスは表示・ログ出力しても実害が小さいため、`PasswordHash` とは異なる扱い）

#### 不変条件

- 実用的なメール形式であること（`@`とドメイン部のドットを含む）
- null / 空文字を許容しない

---

### 4.2 PasswordHash ValueObject（更新）

#### 役割

- パスワードのハッシュ値を保持
- 平文パスワードを保持しない
- ハッシュ化ロジックは Infrastructure 層に委譲する

#### 実装

```csharp
public sealed record PasswordHash
{
    public string Value { get; }

    public PasswordHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Password hash cannot be empty.");

        Value = value;
    }

    public override string ToString() => "[PasswordHash]";
}
```

**変更点：**

- `class` → **`sealed record`** に変更（Emailと同様の理由）
- **`ToString()` は実際のハッシュ値を返さず、固定文字列 `"[PasswordHash]"` を返す**。ハッシュ化されているとはいえ、ログ出力・例外メッセージ・デバッガのwatch表示・文字列補間（`$"{user}"`）経由で意図せず値が露出するリスクを避けるための設計判断。`Email` とは非対称な扱いだが、「見せてよい値」と「見せるべきでない値」を型レベルで区別する意図がある

#### 不変条件

- 平文パスワードを保持しない
- null / 空文字を許容しない
- 文字列化しても実際のハッシュ値を露出しない（追加）

---

## 5. ドメインルール（認証システム特有）

### ✔ Email は ValueObject（sealed record）で管理する

→ 不正な Email を早期に排除できる。値としての等価性も保証される

### ✔ PasswordHash は ValueObject（sealed record）で管理する

→ 平文パスワードを保持しない。`ToString()` によるハッシュ値の露出も防ぐ

### ✔ RefreshToken は期限切れ判定を持つ

```csharp
bool IsExpired => DateTime.UtcNow > ExpiresAt;
bool IsActive => RevokedAt == null && !IsExpired;
```

### ✔ RefreshToken はローテーション方式

- 新しいトークンを発行
- 古いトークンは RevokedAt を設定（**二重設定は例外で防止**）
- セキュリティ向上

### ✔ User は RefreshToken を複数持つ

→ ローテーション方式に対応

### ✔ 不正な状態のインスタンスをコンストラクタで確実に防ぐ（追加の設計方針）

Domain層全体を通じて、「一度生成されたインスタンスは常に有効な状態である」ことをコンストラクタでのバリデーションによって保証する方針を徹底している（`User`, `RefreshToken`, `Email`, `PasswordHash` すべてに共通）。

---

## 6. Domain 層のメリット

- 認証ロジックが UI や DB に依存しない
- テストが容易（モックで検証可能）
- Clean Architecture の原則に完全準拠
- 認証テンプレートとして再利用しやすい
- セキュリティ要件をコードで担保できる
- 不正な状態のオブジェクトが生成される余地がなく、バグの早期発見につながる

---

## 7. 今後の拡張性

- ロール管理（Admin / User）
- MFA（多要素認証）
- パスワードリセット
- アカウントロック
- メール認証（Email Verification）

Domain 層の設計が正しいほど、これらの拡張が容易になります。

---
