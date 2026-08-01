import { Alert, Button, Card, Center, Group, PasswordInput, Stack, Text, ThemeIcon, Title } from '@mantine/core';
import { IconAlertTriangle, IconLock, IconShieldLock } from '@tabler/icons-react';
import { useForm } from '@mantine/form';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { api } from '../../lib/api';
import { describe, notifySaved } from '../../lib/notify';
import { routes } from '../../lib/routes';
import { useSession } from './session';

export function ChangePasswordPage({ forced }: { forced: boolean }) {
  const { t } = useTranslation();
  const { refresh } = useSession();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const form = useForm({
    initialValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
    validate: {
      currentPassword: (value) => (value.length === 0 ? t('common.required') : null),
      newPassword: (value, values) => {
        if (value.length < 10) return t('common.required');
        // The server rejects this too; catching it here saves a round trip and says so in place.
        if (value === values.currentPassword) return t('auth.passwordMustDiffer');
        return null;
      },
      confirmPassword: (value, values) => (value !== values.newPassword ? t('auth.passwordsDoNotMatch') : null),
    },
  });

  const submit = form.onSubmit(async ({ currentPassword, newPassword }) => {
    setBusy(true);
    setError(null);

    try {
      await api.auth.changePassword(currentPassword, newPassword);
      await refresh();
      notifySaved(t('auth.passwordChanged'));
      navigate(routes.board, { replace: true });
    } catch (e) {
      setError(describe(e));
    } finally {
      setBusy(false);
    }
  });

  return (
    <Center h="100vh" px="md">
      <Card withBorder shadow="sm" padding="xl" w="100%" maw={420}>
        <form onSubmit={submit}>
          <Stack gap="md">
            <Group gap="sm" wrap="nowrap">
              <ThemeIcon size={38} radius="md" variant="light" color="everdue">
                <IconShieldLock size={22} />
              </ThemeIcon>
              <Stack gap={4}>
                <Title order={3}>{t('auth.changePasswordTitle')}</Title>
                {forced && (
                  <Text size="sm" c="dimmed">
                    {t('auth.changePasswordHint')}
                  </Text>
                )}
              </Stack>
            </Group>

            {error && (
              <Alert color="red" icon={<IconAlertTriangle size={16} />}>
                {error}
              </Alert>
            )}

            <PasswordInput label={t('auth.currentPassword')} autoComplete="current-password" {...form.getInputProps('currentPassword')} />
            <PasswordInput label={t('auth.newPassword')} autoComplete="new-password" {...form.getInputProps('newPassword')} />
            <PasswordInput label={t('auth.confirmPassword')} autoComplete="new-password" {...form.getInputProps('confirmPassword')} />

            <Button type="submit" loading={busy} fullWidth leftSection={<IconLock size={16} />}>
              {t('common.save')}
            </Button>
          </Stack>
        </form>
      </Card>
    </Center>
  );
}
