<template>
  <div class="login-container">
    <el-card class="login-card" shadow="always">
      <div class="login-header">
        <el-icon :size="48" color="#409EFF">
          <Management />
        </el-icon>
        <h2>開発管理システム</h2>
        <p class="subtitle">メールアドレスとパスワードでログイン</p>
      </div>

      <el-form
        ref="formRef"
        :model="form"
        :rules="rules"
        @submit.prevent="handleLogin"
      >
        <el-form-item prop="email">
          <el-input
            v-model="form.email"
            type="email"
            placeholder="メールアドレス"
            :prefix-icon="Message"
            :disabled="loading"
            size="large"
          />
        </el-form-item>

        <el-form-item prop="password">
          <el-input
            v-model="form.password"
            :type="showPassword ? 'text' : 'password'"
            placeholder="パスワード"
            :prefix-icon="Lock"
            :disabled="loading"
            size="large"
          >
            <template #suffix>
              <el-icon
                class="password-toggle"
                @click="showPassword = !showPassword"
              >
                <View v-if="!showPassword" />
                <Hide v-else />
              </el-icon>
            </template>
          </el-input>
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
            class="login-button"
          >
            {{ loading ? "ログイン中..." : "ログイン" }}
          </el-button>
        </el-form-item>
      </el-form>
      <div class="forgot-password-link">
        <router-link to="/forgot-password"
          >パスワードをお忘れですか？</router-link
        >
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import { useAuthStore } from "@/stores/auth";
import { Message, Lock, View, Hide, Management } from "@element-plus/icons-vue";
import type { FormInstance, FormRules } from "element-plus";
import { ApiError } from "@/api/generated";

const router = useRouter();
const authStore = useAuthStore();

const formRef = ref<FormInstance>();
const form = ref({ email: "", password: "" });
const showPassword = ref(false);
const loading = ref(false);
const errorMessage = ref("");

const rules: FormRules = {
  email: [
    {
      required: true,
      message: "メールアドレスを入力してください",
      trigger: "blur",
    },
    {
      type: "email",
      message: "正しいメールアドレスを入力してください",
      trigger: "blur",
    },
  ],
  password: [
    {
      required: true,
      message: "パスワードを入力してください",
      trigger: "blur",
    },
  ],
};

const handleLogin = async () => {
  if (!formRef.value) return;

  const valid = await formRef.value.validate().catch(() => false);
  if (!valid) return;

  loading.value = true;
  errorMessage.value = "";

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
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
.login-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(
    135deg,
    var(--el-color-primary-light-3),
    var(--el-color-primary-dark-2)
  );
}

.login-card {
  width: 100%;
  max-width: 440px;
  border-radius: 1rem;
  padding: 16px;
}

.login-header {
  text-align: center;
  margin-bottom: 32px;
}

.login-header h2 {
  margin: 12px 0 4px;
  font-size: 1.4rem;
  color: var(--el-text-color-primary);
}

.subtitle {
  color: var(--el-text-color-regular);
  font-size: 0.9rem;
  margin: 0;
}

.login-button {
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

css.forgot-password-link {
  text-align: center;
  margin-top: 8px;
  font-size: 0.85rem;
}
</style>
