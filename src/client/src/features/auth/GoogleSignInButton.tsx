import { Button, Divider, Stack } from '@mantine/core';
import { IconBrandGoogle } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '../../lib/api';
import { routes } from '../../lib/routes';
import { keys } from '../../lib/queryKeys';

/**
 * Rendered only where it can work. The flow is a full navigation rather than a fetch: it leaves the
 * origin, and a redirect chain is not something an XHR can follow to Google and back.
 */
export function GoogleSignInButton({ returnUrl = routes.board }: { returnUrl?: string }) {
  const { t } = useTranslation();

  const providers = useQuery({ queryKey: keys.session.authProviders, queryFn: () => api.auth.providers() });

  if (!providers.data?.google) return null;

  return (
    <Stack gap="sm">
      <Divider label={t('auth.or')} labelPosition="center" />

      <Button
        variant="default"
        leftSection={<IconBrandGoogle size={18} />}
        onClick={() => {
          window.location.href = `/api/v1/auth/external/google/start?returnUrl=${encodeURIComponent(returnUrl)}`;
        }}
        fullWidth
      >
        {t('auth.signInWithGoogle')}
      </Button>
    </Stack>
  );
}
