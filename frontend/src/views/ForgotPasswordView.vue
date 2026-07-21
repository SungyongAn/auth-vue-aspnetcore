<template>
  <div class="forgot-password-container">
    <el-card class="forgot-password-card" shadow="always">
      <div class="forgot-password-header">
        <el-icon :size="48" color="#409EFF">
          <Message />
        </el-icon>
        <h2>パスワードをお忘れですか?</h2>
        <p class="subtitle">
          登録済みのメールアドレスを入力してください。<br />
          パスワード再設定用のリンクをお送りします。
        </p>
      </div>

      <template v-if="!submitted">
        <el-form
          ref="formRef"
          :model="form"
          :rules="rules"
          @submit.prevent="handleSubmit"
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
              {{ loading ? "送信中..." : "リセットリンクを送信" }}
            </el-button>
          </el-form-item>
        </el-form>
      </template>

      <template v-else>
        <el-alert
          title="メールを送信しました"
          type="success"
          show-icon
          :closable="false"
          description="入力されたメールアドレス宛にパスワード再設定用のリンクを送信しました。メールが届かない場合は、メールアドレスが正しいかご確認ください。"
        />
      </template>

      <div class="back-link">
        <router-link to="/login">ログイン画面に戻る</router-link>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useAuthStore } from "@/stores/auth";
import { Message } from "@element-plus/icons-vue";
import type { FormInstance, FormRules } from "element-plus";
import { ApiError } from "@/api/generated";

const authStore = useAuthStore();

const formRef = ref<FormInstance>();
const form = ref({ email: "" });
const loading = ref(false);
const errorMessage = ref("");
const submitted = ref(false);

const rules: FormRules = {
  email: [
    { required: true, message: "メールアドレスを入力してください", trigger: "blur" },
    { type: "email", message: "正しいメールアドレスを入力してください", trigger: "blur" },
  ],
};

const handleSubmit = async () => {
  if (!formRef.value) return;

  const valid = await formRef.value.validate().catch(() => false);
  if (!valid) return;

  loading.value = true;
  errorMessage.value = "";

  try {
    await authStore.forgotPassword(form.value.email);
    // 成功・失敗にかかわらず、常に同じ完了画面を表示する(メールアドレス列挙攻撃対策)
    submitted.value = true;
  } catch (error: unknown) {
    if (error instanceof ApiError) {
      errorMessage.value = "送信に失敗しました。しばらく経ってから再度お試しください";
    } else {
      errorMessage.value = "サーバーに接続できません。しばらく経ってから再度お試しください";
    }
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
.forgot-password-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, var(--el-color-primary-light-3), var(--el-color-primary-dark-2));
}

.forgot-password-card {
  width: 100%;
  max-width: 440px;
  border-radius: 1rem;
  padding: 16px;
}

.forgot-password-header {
  text-align: center;
  margin-bottom: 24px;
}

.forgot-password-header h2 {
  margin: 12px 0 8px;
  font-size: 1.3rem;
  color: var(--el-text-color-primary);
}

.subtitle {
  color: var(--el-text-color-regular);
  font-size: 0.85rem;
  line-height: 1.5;
  margin: 0;
}

.submit-button {
  width: 100%;
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