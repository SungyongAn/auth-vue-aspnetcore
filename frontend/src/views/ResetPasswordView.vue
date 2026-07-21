<template>
  <div class="reset-password-container">
    <el-card class="reset-password-card" shadow="always">
      <div class="reset-password-header">
        <el-icon :size="48" color="#409EFF">
          <Lock />
        </el-icon>
        <h2>新しいパスワードの設定</h2>
        <p class="subtitle">新しいパスワードを入力してください</p>
      </div>

      <template v-if="!tokenPresent">
        <el-alert
          title="無効なリンクです"
          type="error"
          show-icon
          :closable="false"
          description="このリンクは無効です。パスワード再設定を再度リクエストしてください。"
        />
        <div class="back-link">
          <router-link to="/forgot-password">パスワード再設定をリクエストする</router-link>
        </div>
      </template>

      <template v-else-if="!completed">
        <el-form
          ref="formRef"
          :model="form"
          :rules="rules"
          @submit.prevent="handleSubmit"
        >
          <el-form-item prop="newPassword">
            <el-input
              v-model="form.newPassword"
              :type="showPassword ? 'text' : 'password'"
              placeholder="新しいパスワード"
              :prefix-icon="Lock"
              :disabled="loading"
              size="large"
            >
              <template #suffix>
                <el-icon class="password-toggle" @click="showPassword = !showPassword">
                  <View v-if="!showPassword" />
                  <Hide v-else />
                </el-icon>
              </template>
            </el-input>
          </el-form-item>

          <el-form-item prop="confirmPassword">
            <el-input
              v-model="form.confirmPassword"
              :type="showPassword ? 'text' : 'password'"
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
              {{ loading ? "設定中..." : "パスワードを設定" }}
            </el-button>
          </el-form-item>
        </el-form>
      </template>

      <template v-else>
        <el-alert
          title="パスワードを再設定しました"
          type="success"
          show-icon
          :closable="false"
          description="新しいパスワードでログインしてください。"
        />
        <div class="back-link">
          <router-link to="/login">ログイン画面へ</router-link>
        </div>
      </template>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import { useRoute } from "vue-router";
import { useAuthStore } from "@/stores/auth";
import { Lock, View, Hide } from "@element-plus/icons-vue";
import type { FormInstance, FormRules } from "element-plus";
import { ApiError } from "@/api/generated";

const route = useRoute();
const authStore = useAuthStore();

const token = computed(() => {
  const value = route.query.token;
  return typeof value === "string" ? value : "";
});
const tokenPresent = computed(() => token.value.length > 0);

const formRef = ref<FormInstance>();
const form = ref({ newPassword: "", confirmPassword: "" });
const showPassword = ref(false);
const loading = ref(false);
const errorMessage = ref("");
const completed = ref(false);

const rules: FormRules = {
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
    await authStore.resetPassword(token.value, form.value.newPassword);
    completed.value = true;
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      if (error.status === 400 || error.status === 401) {
        errorMessage.value =
          "リンクの有効期限が切れているか、無効なリンクです。再度パスワード再設定をリクエストしてください";
      } else {
        errorMessage.value = "パスワードの設定に失敗しました";
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
.reset-password-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, var(--el-color-primary-light-3), var(--el-color-primary-dark-2));
}

.reset-password-card {
  width: 100%;
  max-width: 440px;
  border-radius: 1rem;
  padding: 16px;
}

.reset-password-header {
  text-align: center;
  margin-bottom: 24px;
}

.reset-password-header h2 {
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

.back-link {
  text-align: center;
  margin-top: 16px;
  font-size: 0.85rem;
}
</style>