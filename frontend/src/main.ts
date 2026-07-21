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

// ── OpenAPI クライアントの初期化(Pinia登録後に行う) ──────────────
OpenAPI.BASE = import.meta.env.VITE_API_BASE_URL;
OpenAPI.WITH_CREDENTIALS = true;
OpenAPI.CREDENTIALS = "include";

OpenAPI.TOKEN = async () => {
  const authStore = useAuthStore();
  return authStore.accessToken ?? "";
};

// ── Silent Refresh(リロード時のセッション復元) ──────────────
async function initializeAuth() {
  const authStore = useAuthStore();
  try {
    await authStore.refresh();
  } catch {
    // refreshToken Cookieが無い、または無効 → 未ログイン状態のまま
  }
}

initializeAuth().finally(() => {
  app.mount("#app");
});
