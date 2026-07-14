// src/api/axios.ts
import axios from "axios";

const api = axios.create({
  baseURL: "http://localhost:5000/api", // ASP.NET Core の API URL に合わせて変更
  withCredentials: true,                // Cookie (refresh token) を送るため必須
});

// アクセストークンを保持（Pinia と連携する予定）
let accessToken: string | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

// リクエスト前に Authorization ヘッダーを付与
api.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

// 401 が返ってきたら自動で refresh を呼ぶ
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    // 401 かつ retry していない場合
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      try {
        const refreshResponse = await api.post("/auth/refresh");
        const newAccessToken = refreshResponse.data.accessToken;

        setAccessToken(newAccessToken);

        // 再度リクエストを実行
        originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
        return api(originalRequest);
      } catch (refreshError) {
        // refresh も失敗 → ログイン画面へ
        setAccessToken(null);
        window.location.href = "/login";
      }
    }

    return Promise.reject(error);
  }
);

export default api;
