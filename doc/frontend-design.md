# frontend-design.md  
# フロントエンド設計書  
Vue 3 + TypeScript + Pinia + api-typescript-codegen

---

## 1. 概要

本ドキュメントでは、認証テンプレートのフロントエンド構成を整理します。

本システムは以下の技術を採用しています。

- **Vue 3（Composition API）**
- **TypeScript**
- **Pinia（状態管理）**
- **Vite（ビルドツール）**
- **api-typescript-codegen（OpenAPI から型生成）**
- **HttpOnly Cookie（リフレッシュトークン）**
- **JWT（アクセストークン）**

バックエンドの ASP.NET Core（Clean Architecture）と連携し、  
安全な認証フローを実現します。

---

## 2. ディレクトリ構成

```
frontend/
  src/
    api/
      generated/        ← api-typescript-codegen の出力
    stores/
      auth.ts           ← 認証ストア（Pinia）
    pages/
      Login.vue
      Register.vue
      Dashboard.vue
    components/
      LoginForm.vue
      RegisterForm.vue
    utils/
      auth-client.ts    ← API クライアントのラッパー
    styles/
      *.scss
  vite.config.ts
  package.json
```

---

## 3. 認証方式（フロント側）

### ✔ アクセストークン（JWT）
- 保存場所：**メモリ（Pinia）**
- localStorage / sessionStorage に保存しない（XSS対策）
- API 呼び出し時に Authorization ヘッダで送信

### ✔ リフレッシュトークン（Cookie）
- HttpOnly / Secure / SameSite=strict
- JavaScript から参照不可
- `/auth/refresh` を呼び出すと自動で Cookie が送信される

---

## 4. 型生成（api-typescript-codegen）

### 4.1 型生成コマンド

```bash
npx api-typescript-codegen \
  --input http://localhost:5000/swagger/v1/swagger.json \
  --output src/api/generated
```

### 4.2 生成されるもの

- API クライアント（fetch ベース）
- Request / Response の型
- DTO の型
- エンドポイント関数

### 4.3 メリット

- バックエンドと型が自動同期  
- DTO の変更が即時反映  
- 手書き API クライアントが不要  
- 型安全なフロント開発が可能

---

## 5. API クライアントラッパー（auth-client.ts）

生成された API クライアントをそのまま使うと冗長になるため、  
薄いラッパーを作成して使いやすくします。

### 例：アクセストークン付与

```ts
import { AuthApi } from '@/api/generated';

export const authClient = (accessToken?: string) => {
  return new AuthApi({
    baseUrl: 'http://localhost:5000',
    headers: accessToken
      ? { Authorization: `Bearer ${accessToken}` }
      : {},
    credentials: 'include', // Cookie を送信
  });
};
```

---

## 6. Pinia 認証ストア（auth.ts）

### 6.1 状態

```ts
state: () => ({
  accessToken: null as string | null,
  expiresIn: null as number | null,
  user: null as { id: string; email: string } | null,
})
```

### 6.2 アクション

| アクション | 説明 |
|-----------|------|
| login | /auth/login を呼び出し、アクセストークンを保存 |
| register | /auth/register を呼び出し、自動ログイン |
| refresh | /auth/refresh を呼び出し、アクセストークンを再発行 |
| logout | /auth/logout を呼び出し、Cookie を削除 |

### 6.3 login の例

```ts
async login(email: string, password: string) {
  const api = authClient();
  const res = await api.authLogin({ email, password });

  this.accessToken = res.accessToken;
  this.expiresIn = res.expiresIn;
  this.user = { id: res.userId, email: res.email };
}
```

---

## 7. 認証フロー（フロント側）

### ✔ Login
1. `/auth/login` を呼び出す  
2. AccessToken を Pinia に保存  
3. RefreshToken は Cookie に保存（HttpOnly）

### ✔ Register
1. `/auth/register` を呼び出す  
2. 自動ログイン  
3. AccessToken を Pinia に保存  
4. RefreshToken は Cookie に保存

### ✔ Refresh
1. `/auth/refresh` を呼び出す（Cookie が自動送信）  
2. 新しい AccessToken を Pinia に保存  
3. RefreshToken は Cookie でローテーション

### ✔ Logout
1. `/auth/logout` を呼び出す  
2. Cookie の RefreshToken を削除  
3. Pinia の状態を初期化

---

## 8. Vue コンポーネント構成

### pages/
- Login.vue  
- Register.vue  
- Dashboard.vue  

### components/
- LoginForm.vue  
- RegisterForm.vue  

### 設計ポイント
- ページは画面構成  
- コンポーネントは UI 部品  
- 認証ロジックは Pinia に集約（Fat Store / Thin Component）

---

## 9. 認証ガード（Router）

### 例：ログイン必須ページ

```ts
router.beforeEach(async (to, from, next) => {
  const auth = useAuthStore();

  if (to.meta.requiresAuth && !auth.accessToken) {
    return next('/login');
  }

  next();
});
```

---

## 10. セキュリティ対策（フロント側）

### ✔ アクセストークンはメモリに保存  
→ localStorage に保存しない（XSS対策）

### ✔ Cookie は HttpOnly  
→ JS から参照不可

### ✔ Refresh API は POST  
→ CSRF 対策

### ✔ SameSite=strict  
→ 他サイトから Cookie が送信されない

---

## 11. 今後の拡張性

- ロール管理（Admin / User）
- MFA（多要素認証）
- メール認証（Email Verification）
- パスワードリセット
- WebAuthn（パスキー）
- Vue Router の認可ガード強化
- Axios 版 API クライアント（必要なら）

---
