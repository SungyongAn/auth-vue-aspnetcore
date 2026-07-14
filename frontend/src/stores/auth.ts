// src/stores/auth.ts
import { defineStore } from "pinia";
import api, { setAccessToken } from "../api/axios";

export const useAuthStore = defineStore("auth", {
  state: () => ({
    accessToken: null as string | null,
    user: null as any,
  }),

  getters: {
    isAuthenticated: (state) => !!state.accessToken,
  },

  actions: {
    async login(email: string, password: string) {
      const res = await api.post("/auth/login", { email, password });

      this.accessToken = res.data.accessToken;
      this.user = res.data.user;

      setAccessToken(this.accessToken);
      localStorage.setItem("accessToken", this.accessToken);
    },

    async register(email: string, password: string) {
      const res = await api.post("/auth/register", { email, password });

      this.accessToken = res.data.accessToken;
      this.user = res.data.user;

      setAccessToken(this.accessToken);
      localStorage.setItem("accessToken", this.accessToken);
    },

    async refresh() {
      try {
        const res = await api.post("/auth/refresh");

        this.accessToken = res.data.accessToken;
        setAccessToken(this.accessToken);
        localStorage.setItem("accessToken", this.accessToken);
      } catch {
        this.logout();
      }
    },

    logout() {
      this.accessToken = null;
      this.user = null;

      setAccessToken(null);
      localStorage.removeItem("accessToken");
    },

    loadFromStorage() {
      const token = localStorage.getItem("accessToken");
      if (token) {
        this.accessToken = token;
        setAccessToken(token);
      }
    },
  },
});
