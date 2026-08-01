import { Alert, Button, Card, Center, Group, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconAlertTriangle, IconLogin } from '@tabler/icons-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { BrandMark } from '../../components/BrandMark';
import { ColorSchemeToggle } from '../../components/ColorSchemeToggle';
import { GoogleSignInButton } from './GoogleSignInButton';
import { LegalNotice } from '../../components/LegalNotice';
import { LanguageToggle } from './LanguageToggle';
import { useSession } from './session';
import { ApiError } from '../../lib/http';
import { routes } from '../../lib/routes';

export function LoginPage() {
  const { t } = useTranslation();
  const { signIn } = useSession();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Every external refusal looks the same here: which account exists, and which is deactivated, are
  // not things an unauthenticated visitor gets to learn.
  const externalError = params.get('error') ? t('auth.externalFailed') : null;

  const form = useForm({
    initialValues: { email: '', password: '' },
    validate: {
      email: (value) => (value.trim().length === 0 ? t('common.required') : null),
      password: (value) => (value.length === 0 ? t('common.required') : null),
    },
  });

  const submit = form.onSubmit(async ({ email, password }) => {
    setBusy(true);
    setError(null);

    try {
      const user = await signIn(email.trim(), password);
      navigate(user.mustChangePassword ? routes.changePassword : routes.board, { replace: true });
    } catch (e) {
      // The server answers identically for a wrong password and an unknown account; so does this.
      setError(e instanceof ApiError && e.problem.detail?.includes('locked') ? t('auth.lockedOut') : t('auth.invalid'));
    } finally {
      setBusy(false);
    }
  });

  return (
    <Center h="100vh" px="md">
      <Card withBorder shadow="sm" padding="xl" w="100%" maw={400}>
        <form onSubmit={submit}>
          <Stack gap="md">
            {/* The mark, at the one screen somebody may see before they are anybody here. */}
            <Group gap="sm" wrap="nowrap">
              <BrandMark size={40} />
              <Stack gap={2}>
                <Title order={3} style={{ letterSpacing: '-0.01em' }}>
                  {/* The heading stays a heading; only its paint changes. */}
                  <Text span inherit variant="gradient" gradient={{ from: 'everdue.6', to: 'teal.7', deg: 135 }}>
                    {t('common.appName')}
                  </Text>
                </Title>
                <Text size="sm" c="dimmed">
                  {t('auth.signInTitle')}
                </Text>
              </Stack>
            </Group>

            {(error ?? externalError) && (
              <Alert color="red" icon={<IconAlertTriangle size={16} />}>
                {error ?? externalError}
              </Alert>
            )}

            <TextInput
              label={t('auth.email')}
              type="email"
              autoComplete="username"
              autoFocus
              {...form.getInputProps('email')}
            />
            <PasswordInput label={t('auth.password')} autoComplete="current-password" {...form.getInputProps('password')} />

            <Button type="submit" loading={busy} fullWidth leftSection={<IconLogin size={16} />}>
              {t('auth.signIn')}
            </Button>

            {/* Password login always stays: it is the rescue path if the Google client ever breaks. */}
            <GoogleSignInButton />

            <Center>
              <LanguageToggle />
            </Center>
            <ColorSchemeToggle />

            {/*
              The AGPL's notice belongs in the interactive interface, and for somebody who never signs in this is the
              only screen they see.
            */}
            <LegalNotice />
          </Stack>
        </form>
      </Card>
    </Center>
  );
}
