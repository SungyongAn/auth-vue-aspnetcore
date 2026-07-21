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

    // 追加：ログイン中ユーザーのパスワード変更
    async changePassword(currentPassword: string, newPassword: string) {
      await withAuthRetry(() =>
        AuthService.postAuthChangePassword({
          currentPassword,
          newPassword,
        }),
      );
      // パスワード変更成功時、バックエンド側で全セッションが無効化されるため、
      // 現在のリフレッシュトークンCookieも無効になっている。フロント側も明示的にログアウト状態にする。
      this.clearSession();
    },

    // 追加：パスワードリセットメールの送信依頼
    async forgotPassword(email: string) {
      await AuthService.postAuthForgotPassword({ email });
    },

    // 追加：トークンを使った新パスワードの設定
    async resetPassword(token: string, newPassword: string) {
      await AuthService.postAuthResetPassword({ token, newPassword });
    },

    clearSession() {
      this.accessToken = null;
      this.user = null;
    },
  },
});
