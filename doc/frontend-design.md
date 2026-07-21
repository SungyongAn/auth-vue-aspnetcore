# frontend-design.md

# フロントエンド設計書

Vue 3 + TypeScript + Pinia + openapi-typescript-codegen + Element Plus

> **更新履歴:** APIクライアント方式を axios から **fetchベースの自動生成クライアント** に変更。  
> UIライブラリとして **Element Plus** を採用。Silent Refresh・401自動リトライの設計を追加。

---

## 1. 概要

本ドキュメントでは、認証テンプレートのフロントエンド構成を整理します。

本システムは以下の技術を採用しています。

- **Vue 3（Composition API、`<script setup>`）**
- **TypeScript**
- **Pinia（状態管理）**
- **Vite（ビルドツール）**
- **Vue Router 4**
- **Element Plus（UIコンポーネントライブラリ）** + `@element-plus/icons-vue`
- **openapi-typescript-codegen（`--client fetch`）**：OpenAPI から型・API クライアントを自動生成
- **HttpOnly Cookie（リフレッシュトークン）**
- **JWT（アクセストークン、メモリ保持のみ）**

バックエンドの ASP.NET Core（Clean Architecture）と連携し、  
安全な認証フローを実現します。

---

## 2. ディレクトリ構成（実装版）

```
frontend/
  src/
    api/
      generated/          ← openapi-typescript-codegen の出力（fetchクライアント、再生成対象・直接編集不可）
        core/
          OpenAPI.ts       ← BASE / WITH_CREDENTIALS / TOKEN 等の設定
          ApiError.ts      ← エラークラス（status, body 等を持つ）
        models/            ← DTO型（LoginRequest, LoginResponse 等）
        services/
          AuthService.ts   ← エンドポイントごとの呼び出し関数
      withAuthRetry.ts     ← 401検知時に自動でrefresh→リトライする汎用ラッパー関数
    stores/
      auth.ts              ← 認証ストア（Pinia）
    router/
      index.ts             ← ルーティング + 認証ガード
    views/
      LoginView.vue
      RegisterView.vue
      DashboardView.vue
    vite-env.d.ts          ← 環境変数の型定義（ImportMetaEnv拡張）
    main.ts                ← アプリ初期化、OpenAPI設定、Silent Refresh起動
    App.vue
  .env                     ← VITE_API_BASE_URL 等（Git管理対象外）
  .env.example             ← .env のテンプレート（Git管理対象）
  vite.config.ts           ← `@` パスエイリアス設定
  tsconfig.app.json        ← `@` パスエイリアスの型解決設定
  package.json
```

**方針転換の理由:** 当初 axios ベースの手書き `auth-client.ts` を想定していましたが、  
バックエンドDTOとの型同期を自動化し、手動保守コストを下げるため、OpenAPI仕様から自動生成する fetch クライアントに統一しました。

---

## 3. 認証方式（フロント側）

### ✔ アクセストークン（JWT）

- 保存場所：**Piniaストアの `state`（メモリ上）のみ**
- **`localStorage` / `sessionStorage` には一切保存しない**（XSS対策、security.md準拠）
- ページリロードで消える。復元は Silent Refresh（後述）で行う
- API 呼び出し時に Authorization ヘッダーで送信（`OpenAPI.TOKEN` リゾルバ経由で自動付与、手動でヘッダーを付ける必要はない）

### ✔ リフレッシュトークン（Cookie）

- HttpOnly / Secure / SameSite=strict
- JavaScript から参照不可
- `/auth/refresh` を呼び出すと自動で Cookie が送信される（`OpenAPI.CREDENTIALS = "include"` により有効化）

---

## 4. 型生成（openapi-typescript-codegen）

### 4.1 型生成コマンド

```bash
# 1. OpenAPI仕様をファイルに保存（URLを直接指定すると $ref 解決に失敗することがあるため）
curl http://localhost:5000/openapi/v1.json -o openapi.json

# 2. ローカルファイルから型生成
npx openapi-typescript-codegen --input ./openapi.json --output src/api/generated --client fetch
```

### 4.2 生成されるもの

- API クライアント（**fetchベース**）
- Request / Response の型（`models/`）
- DTO の型
- エンドポイント関数（`services/AuthService.ts`）
- エラークラス（`ApiError`：`status` / `body` / `url` 等を持つ）

### 4.3 レスポンス型が正しく生成されるための前提条件（重要）

バックエンドの Controller アクションが `IActionResult` のままだと、レスポンスの型情報が OpenAPI 仕様に反映されず、生成される型が `CancelablePromise<any>` になってしまいます。**`ActionResult<T>` への変更に加え、`[ProducesResponseType(typeof(T), StatusCodes.Status200OK)]` 属性の明示が必要**です（api-design.md 参照）。

### 4.4 メリット

- バックエンドと型が自動同期
- DTO の変更が即時反映
- 手書き API クライアントが不要
- 型安全なフロント開発が可能

---

## 5. OpenAPI クライアントの初期化（main.ts）

axios の `auth-client.ts` ラッパーに代わり、生成された `OpenAPI` 設定オブジェクトをアプリ起動時に初期化します。

```ts
import { createApp } from "vue";
import { createPinia } from "pinia";
import ElementPlus from "element-plus";
import "element-plus/dist/index.css";
import App from "./App.vue";
import router from "./router";
import { OpenAPI } from "./api/generated";
import { useAuthStore } from "@/stores/auth";

const app = createApp(App);
const pinia = createPinia();

app.use(pinia);
app.use(router);
app.use(ElementPlus);

// ── OpenAPI クライアントの初期化（Pinia登録後に行うこと） ──────────────
OpenAPI.BASE = import.meta.env.VITE_API_BASE_URL;
OpenAPI.WITH_CREDENTIALS = true; // HttpOnly Cookie(refreshToken)の送受信を許可
OpenAPI.CREDENTIALS = "include";

OpenAPI.TOKEN = async () => {
  const authStore = useAuthStore();
  return authStore.accessToken ?? "";
};

// ── Silent Refresh（リロード時のセッション復元） ──────────────
async function initializeAuth(): Promise<void> {
  const authStore = useAuthStore();
  try {
    await authStore.refresh();
  } catch {
    // refreshToken Cookieが無い、または無効 → 未ログイン状態のまま（想定内）
  }
}

// mount前に初期化を待つことで、Routerガードの誤判定を防ぐ
initializeAuth().finally(() => {
  app.mount("#app");
});
```

**設計上の注意点:**

- `OpenAPI.TOKEN` の設定は `useAuthStore()` を内部で呼ぶため、**必ず `app.use(pinia)` の後**に実行すること（順序を誤ると `getActivePinia() was called but there was no active Pinia` エラーになる）
- `initializeAuth()` の完了を `app.mount()` より前に待つことで、初回描画時点で認証状態が確定した状態にし、Routerガードの誤判定を防ぐ
- 初回訪問時（`refreshToken` Cookie が存在しない）は `/auth/refresh` が `401` を返すが、これは想定内の挙動としてコンソールに表示される（catchで正しくハンドリングされ、アプリの動作には影響しない）

### 環境変数（`.env` / `vite-env.d.ts`）

```
# .env.example
VITE_API_BASE_URL=http://localhost:5000
```

```ts
// vite-env.d.ts
/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
```

Viteは環境変数を**ビルド時に埋め込む**ため、Dockerイメージ化する際はビルド時に `VITE_API_BASE_URL` を渡す必要がある点に注意。

---

## 6. Pinia 認証ストア（stores/auth.ts）

### 6.1 状態

```ts
state: () => ({
  accessToken: null as string | null,
  user: null as { id: string; email: string } | null,
});
```

`user` は `any` にせず、明確な型（`AuthUser`）を定義する。

### 6.2 アクション

| アクション     | 説明                                                                                                      |
| -------------- | --------------------------------------------------------------------------------------------------------- |
| `login`        | `/auth/login` を呼び出し、アクセストークン・ユーザー情報をstateに保存                                     |
| `register`     | `/auth/register` を呼び出し、自動ログイン                                                                 |
| `refresh`      | `/auth/refresh` を呼び出し、アクセストークンを再発行。失敗時は例外をスローし `clearSession()` を実行      |
| `fetchMe`      | `/auth/me` を呼び出し、現在のユーザー情報を取得（`withAuthRetry` でラップ）                               |
| `logout`       | `/auth/logout` を呼び出した上で、ローカルの状態をクリア（`try/finally`によりAPI失敗時もクリアは必ず実行） |
| `clearSession` | `accessToken` / `user` を `null` にリセット                                                               |

### 6.3 実装例

```ts
import { defineStore } from "pinia";
import { AuthService } from "@/api/generated";
import type { LoginResponse } from "@/api/generated";
import { withAuthRetry } from "@/api/withAuthRetry";

interface AuthUser {
  id: string;
  email: string;
}

export const useAuthStore = defineStore("auth", {
  state: () => ({
    accessToken: null as string | null,
    user: null as AuthUser | null,
  }),

  getters: {
    isAuthenticated: (state) => !!state.accessToken,
  },

  actions: {
    setSession(response: LoginResponse) {
      this.accessToken = response.accessToken;
      this.user = { id: response.userId, email: response.email };
    },

    async login(email: string, password: string) {
      const response = await AuthService.postAuthLogin({ email, password });
      this.setSession(response);
    },

    async register(email: string, password: string) {
      const response = await AuthService.postAuthRegister({ email, password });
      this.setSession(response);
    },

    async refresh() {
      try {
        const response = await AuthService.postAuthRefresh();
        this.accessToken = response.accessToken;
      } catch {
        this.clearSession();
        throw new Error("Refresh failed");
      }
    },

    async fetchMe() {
      const me = await withAuthRetry(() => AuthService.getAuthMe());
      this.user = { id: me.userId, email: me.email };
    },

    async logout() {
      try {
        await AuthService.postAuthLogout();
      } finally {
        this.clearSession();
      }
    },

    clearSession() {
      this.accessToken = null;
      this.user = null;
    },
  },
});
```

**重要：`localStorage` は一切使用しない。** 旧設計にあった `localStorage.setItem("accessToken", ...)` は security.md の方針（XSS対策）に反するため廃止。

---

## 7. 401自動リトライ（withAuthRetry）※新規

axios の interceptor に代わる仕組みとして、認証が必要なAPI呼び出しをラップする汎用関数を用意する。

```ts
// src/api/withAuthRetry.ts
import { useAuthStore } from "@/stores/auth";
import { ApiError } from "./generated";

export async function withAuthRetry<T>(fn: () => Promise<T>): Promise<T> {
  try {
    return await fn();
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      const authStore = useAuthStore();
      try {
        await authStore.refresh();
        return await fn(); // リトライ
      } catch {
        authStore.clearSession();
      }
    }
    throw error;
  }
}
```

使用例：

```ts
const me = await withAuthRetry(() => AuthService.getAuthMe());
```

認証必須の新規エンドポイントを追加する場合は、必ずこの関数でラップすることで自動リフレッシュが機能する。

---

## 8. 認証フロー（フロント側）

### ✔ Login

1. `/auth/login` を呼び出す
2. AccessToken を Pinia に保存
3. RefreshToken は Cookie に保存（HttpOnly、ブラウザが自動管理）

### ✔ Register

1. `/auth/register` を呼び出す
2. 自動ログイン
3. AccessToken を Pinia に保存
4. RefreshToken は Cookie に保存

### ✔ Silent Refresh（リロード時、新規）

1. アプリ起動時（`main.ts`）に自動的に `/auth/refresh` を呼び出す
2. Cookie に有効な RefreshToken があれば、新しい AccessToken を取得してログイン状態を復元
3. 無効・不在の場合は 401 となるが、これは正常系としてキャッチしログイン画面へ

### ✔ Refresh（401検知時、新規）

1. 保護APIの呼び出しが `401` を返した場合、`withAuthRetry` が自動的に `/auth/refresh` を呼び出す
2. 成功すれば元のリクエストを自動的にリトライ
3. 失敗すればセッションをクリアしログイン画面へ誘導（Routerガードによる）

### ✔ Logout

1. `/auth/logout` を呼び出す（Cookieの削除とDB上のトークン無効化）
2. API呼び出しの成否に関わらず、`finally` でPiniaの状態を初期化
3. ログイン画面へ遷移

---

## 9. Vue コンポーネント構成

### views/

- `LoginView.vue`：Element Plus の `el-form` によるバリデーション付きフォーム
- `RegisterView.vue`：確認用パスワード入力のカスタムバリデーションあり
- `DashboardView.vue`：認証後ページ。ログアウトボタンは非同期処理の完了を待ってから画面遷移する

### エラーハンドリングの実装パターン（新規、重要）

生成された `ApiError` クラスを用いて、fetchベースのエラー形式に対応する。axios の `error.response.status` ではなく **`error.status`** で判定する点に注意。

```ts
import { ApiError } from "@/api/generated";

try {
  await authStore.login(form.value.email, form.value.password);
  router.push("/dashboard");
} catch (error: unknown) {
  if (error instanceof ApiError) {
    const status = error.status;
    if (status === 401) {
      errorMessage.value = "メールアドレスまたはパスワードが正しくありません";
    } else if (status === 400) {
      errorMessage.value = "入力内容に誤りがあります";
    } else {
      errorMessage.value = "ログインに失敗しました";
    }
  } else {
    errorMessage.value =
      "サーバーに接続できません。しばらく経ってから再度お試しください";
  }
}
```

### 設計ポイント

- ページは画面構成
- コンポーネントは UI 部品
- 認証ロジックは Pinia に集約（Fat Store / Thin Component）
- エラーメッセージの日本語化は View 層の責務とし、ステータスコードによる分岐で対応

---

## 10. 認証ガード（Router）

```ts
import { createRouter, createWebHistory } from "vue-router";
import { useAuthStore } from "@/stores/auth";
import LoginView from "@/views/LoginView.vue";
import RegisterView from "@/views/RegisterView.vue";
import DashboardView from "@/views/DashboardView.vue";

const routes = [
  { path: "/", redirect: "/dashboard" },
  { path: "/login", name: "login", component: LoginView },
  { path: "/register", name: "register", component: RegisterView },
  {
    path: "/dashboard",
    name: "dashboard",
    component: DashboardView,
    meta: { requiresAuth: true },
  },
  { path: "/:pathMatch(.*)*", redirect: "/" }, // catch-all
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach((to) => {
  const auth = useAuthStore();
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: "login" };
  }
});

export default router;
```

**旧設計からの変更点:**

- `next()` コールバック形式ではなく、Vue Router 4 推奨の戻り値ベースの書き方に変更
- ガード内で `loadFromStorage()`（localStorage復元）は呼ばない。Silent Refreshは `main.ts` で起動時に一度だけ実行する設計に統一し、ガード内での重複実行・不要なAPIコールを避けている
- ルートパス `/` へのリダイレクト、およびcatch-allルートを追加（未定義パスへのアクセス対応）

---

## 11. セキュリティ対策（フロント側）

### ✔ アクセストークンはメモリに保存

→ `localStorage` に保存しない（XSS対策）。ページリロードで失われるが、Silent Refreshで復元する設計

### ✔ Cookie は HttpOnly

→ JS から参照不可

### ✔ Refresh / Logout API は POST

→ CSRF 対策

### ✔ SameSite=strict

→ 他サイトから Cookie が送信されない

### ✔ エラーメッセージの詳細を過度に露出しない

→ バックエンドのグローバル例外ハンドラーが500エラー時に詳細を隠蔽する設計と対応させ、フロント側もステータスコードベースで安全なメッセージのみ表示

---

## 12. 今後の拡張性

- ロール管理（Admin / User）
- MFA（多要素認証）
- メール認証（Email Verification）
- パスワードリセット
- WebAuthn（パスキー）
- `src/auth/` ディレクトリへの認証基盤の切り出し（他プロジェクトへの移植性向上）
- Vue Router の認可ガード強化（ロールベース）
- フロントエンドのコンテナ化（Nginx配信によるDocker化）

---
