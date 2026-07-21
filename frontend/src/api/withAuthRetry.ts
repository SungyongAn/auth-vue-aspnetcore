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
        return await fn();
      } catch {
        authStore.clearSession();
      }
    }
    throw error;
  }
}
