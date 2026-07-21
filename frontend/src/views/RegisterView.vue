<template>
  <div class="register-container">
    <el-card class="register-card" shadow="always">
      <div class="register-header">
        <el-icon :size="48" color="#67C23A">
          <Management />
        </el-icon>
        <h2>新規登録</h2>
        <p class="subtitle">メールアドレスとパスワードを入力してください</p>
      </div>

      <el-form
        ref="formRef"
        :model="form"
        :rules="rules"
        @submit.prevent="handleRegister"
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

        <el-form-item prop="confirmPassword">
          <el-input
            v-model="form.confirmPassword"
            :type="showPassword ? 'text' : 'password'"
            placeholder="パスワード（確認）"
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
            type="success"
            native-type="submit"
            :loading="loading"
            size="large"
            class="register-button"
          >
            {{ loading ? "登録中..." : "登録" }}
          </el-button>
        </el-form-item>
      </el-form>
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
const form = ref({
  email: "",
  password: "",
  confirmPassword: "",
});
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
  confirmPassword: [
    {
      required: true,
      message: "確認用パスワードを入力してください",
      trigger: "blur",
    },
    {
      validator: (
        _rule: unknown,
        value: string,
        callback: (error?: Error) => void,
      ) => {
        if (value !== form.value.password) {
          callback(new Error("パスワードが一致しません"));
        } else {
          callback();
        }
      },
      trigger: "blur",
    },
  ],
};

const handleRegister = async () => {
  if (!formRef.value) return;

  const valid = await formRef.value.validate().catch(() => false);
  if (!valid) return;

  loading.value = true;
  errorMessage.value = "";

  try {
    await authStore.register(form.value.email, form.value.password);
    router.push("/dashboard");
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      const status = error.status;

      if (status === 409) {
        errorMessage.value = "このメールアドレスは既に登録されています";
      } else if (status === 400) {
        errorMessage.value = "入力内容に誤りがあります";
      } else {
        errorMessage.value = "登録に失敗しました";
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
.register-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(
    135deg,
    var(--el-color-success-light-3),
    var(--el-color-success-dark-2)
  );
}

.register-card {
  width: 100%;
  max-width: 440px;
  border-radius: 1rem;
  padding: 16px;
}

.register-header {
  text-align: center;
  margin-bottom: 32px;
}

.register-header h2 {
  margin: 12px 0 4px;
  font-size: 1.4rem;
  color: var(--el-text-color-primary);
}

.subtitle {
  color: var(--el-text-color-regular);
  font-size: 0.9rem;
  margin: 0;
}

.register-button {
  width: 100%;
}

.password-toggle {
  cursor: pointer;
  color: var(--el-text-color-regular);
}

.password-toggle:hover {
  color: var(--el-color-success);
}

.mb-4 {
  margin-bottom: 16px;
}
</style>
