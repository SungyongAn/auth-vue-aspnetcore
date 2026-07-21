<template>
  <div class="change-password-container">
    <el-card class="change-password-card" shadow="always">
      <div class="change-password-header">
        <el-icon :size="48" color="#409EFF">
          <Lock />
        </el-icon>
        <h2>パスワード変更</h2>
        <p class="subtitle">現在のパスワードと新しいパスワードを入力してください</p>
      </div>

      <template v-if="!completed">
        <el-form
          ref="formRef"
          :model="form"
          :rules="rules"
          @submit.prevent="handleSubmit"
        >
          <el-form-item prop="currentPassword">
            <el-input
              v-model="form.currentPassword"
              :type="showCurrentPassword ? 'text' : 'password'"
              placeholder="現在のパスワード"
              :prefix-icon="Lock"
              :disabled="loading"
              size="large"
            >
              <template #suffix>
                <el-icon class="password-toggle" @click="showCurrentPassword = !showCurrentPassword">
                  <View v-if="!showCurrentPassword" />
                  <Hide v-else />
                </el-icon>
              </template>
            </el-input>
          </el-form-item>

          <el-form-item prop="newPassword">
            <el-input
              v-model="form.newPassword"
              :type="showNewPassword ? 'text' : 'password'"
              placeholder="新しいパスワード"
              :prefix-icon="Lock"
              :disabled="loading"
              size="large"
            >
              <template #suffix>
                <el-icon class="password-toggle" @click="showNewPassword = !showNewPassword">
                  <View v-if="!showNewPassword" />
                  <Hide v-else />
                </el-icon>
              </template>
            </el-input>
          </el-form-item>

          <el-form-item prop="confirmPassword">
            <el-input
              v-model="form.confirmPassword"
              :type="showNewPassword ? 'text' : 'password'"
              placeholder="新しいパスワード（確認）"
              :prefix-icon="Lock"
              :disabled="loading"
              size="large"
            />
          </el-form-item>

          <el-alert
            v-if="errorMessage"
            :title="errorMessage"
            type="error"
            show-icon
            :closable="false"
            class="mb-4"
          />

          <el-form-item>
            <el-button
              type="primary"
              native-type="submit"
              :loading="loading"
              size="large"
              class="submit-button"
            >
              {{ loading ? "変更中..." : "パスワードを変更" }}
            </el-button>
          </el-form-item>

          <div class="cancel-link">
            <router-link to="/dashboard">キャンセルして戻る</router-link>
          </div>
        </el-form>
      </template>

      <template v-else>
        <el-alert
          title="パスワードを変更しました"
          type="success"
          show-icon
          :closable="false"
          description="セキュリティのため、再度ログインしてください。"
        />
        <div class="back-link">
          <router-link to="/login">ログイン画面へ</router-link>
        </div>
      </template>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useAuthStore } from "@/stores/auth";
import { Lock, View, Hide } from "@element-plus/icons-vue";
import type { FormInstance, FormRules } from "element-plus";
import { ApiError } from "@/api/generated";

const authStore = useAuthStore();

const formRef = ref<FormInstance>();
const form = ref({
  currentPassword: "",
  newPassword: "",
  confirmPassword: "",
});
const showCurrentPassword = ref(false);
const showNewPassword = ref(false);
const loading = ref(false);
const errorMessage = ref("");
const completed = ref(false);

const rules: FormRules = {
  currentPassword: [
    { required: true, message: "現在のパスワードを入力してください", trigger: "blur" },
  ],
  newPassword: [
    { required: true, message: "新しいパスワードを入力してください", trigger: "blur" },
  ],
  confirmPassword: [
    { required: true, message: "確認用パスワードを入力してください", trigger: "blur" },
    {
      validator: (_rule: unknown, value: string, callback: (error?: Error) => void) => {
        if (value !== form.value.newPassword) {
          callback(new Error("パスワードが一致しません"));
        } else {
          callback();
        }
      },
      trigger: "blur",
    },
  ],
};

const handleSubmit = async () => {
  if (!formRef.value) return;

  const valid = await formRef.value.validate().catch(() => false);
  if (!valid) return;

  loading.value = true;
  errorMessage.value = "";

  try {
    await authStore.changePassword(form.value.currentPassword, form.value.newPassword);
    completed.value = true;
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      if (error.status === 401) {
        errorMessage.value = "現在のパスワードが正しくありません";
      } else {
        errorMessage.value = "パスワードの変更に失敗しました";
      }
    } else {
      errorMessage.value = "サーバーに接続できません。しばらく経ってから再度お試しください";
    }
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
.change-password-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--el-color-primary-light-9);
}

.change-password-card {
  width: 100%;
  max-width: 440px;
  border-radius: 1rem;
  padding: 16px;
}

.change-password-header {
  text-align: center;
  margin-bottom: 24px;
}

.change-password-header h2 {
  margin: 12px 0 4px;
  font-size: 1.3rem;
  color: var(--el-text-color-primary);
}

.subtitle {
  color: var(--el-text-color-regular);
  font-size: 0.85rem;
  margin: 0;
}

.submit-button {
  width: 100%;
}

.password-toggle {
  cursor: pointer;
  color: var(--el-text-color-regular);
}

.password-toggle:hover {
  color: var(--el-color-primary);
}

.mb-4 {
  margin-bottom: 16px;
}

.cancel-link,
.back-link {
  text-align: center;
  margin-top: 12px;
  font-size: 0.85rem;
}
</style>