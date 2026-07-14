<template>
  <div class="dashboard-container">
    <el-card class="dashboard-card" shadow="always">
      <div class="dashboard-header">
        <el-icon :size="48" color="#409EFF">
          <Management />
        </el-icon>
        <h2>ダッシュボード</h2>
        <p class="subtitle">ログインに成功しました</p>
      </div>

      <div class="dashboard-content">
        <p>ようこそ、{{ userEmail }} さん。</p>

        <el-button type="danger" @click="handleLogout"> ログアウト </el-button>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRouter } from "vue-router";
import { useAuthStore } from "@/stores/auth";
import { Management } from "@element-plus/icons-vue";

const router = useRouter();
const authStore = useAuthStore();

// ユーザー情報（メールアドレス）を表示
const userEmail = computed(() => authStore.user?.email ?? "ユーザー");

// ログアウト処理
const handleLogout = () => {
  authStore.logout();
  router.push("/login");
};
</script>

<style scoped>
.dashboard-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--el-color-primary-light-9);
}

.dashboard-card {
  width: 100%;
  max-width: 600px;
  border-radius: 1rem;
  padding: 24px;
}

.dashboard-header {
  text-align: center;
  margin-bottom: 24px;
}

.dashboard-header h2 {
  margin: 12px 0 4px;
  font-size: 1.6rem;
  color: var(--el-text-color-primary);
}

.subtitle {
  color: var(--el-text-color-regular);
  font-size: 0.9rem;
  margin: 0;
}

.dashboard-content {
  text-align: center;
  margin-top: 16px;
}

.dashboard-content p {
  margin-bottom: 16px;
  font-size: 1.1rem;
}
</style>
