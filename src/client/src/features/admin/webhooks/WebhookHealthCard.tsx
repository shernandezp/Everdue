import { Alert, List, Text } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import type { WebhookHealth } from '../../../api/types';

/**
 * Shown only when something is actually wrong. A permanent "all good" banner is furniture nobody reads, which is
 * exactly why the one that appears means something.
 */
export function WebhookHealthCard({ health }: { health: WebhookHealth[] }) {
  const { t } = useTranslation();

  const troubled = health.filter((row) => row.failed24h > 0 || !row.active);

  if (troubled.length === 0) return null;

  return (
    <Alert variant="light" color="orange" icon={<IconAlertTriangle size={16} />} title={t('webhooks.healthTitle')}>
      <List size="sm" spacing={4}>
        {troubled.map((row) => (
          <List.Item key={row.subscriptionId}>
            <Text size="sm" span fw={500}>
              {row.url}
            </Text>{' '}
            <Text size="sm" span>
              {row.active
                ? t('webhooks.failedRecently', { count: row.failed24h })
                : t('webhooks.disabledBecauseOfFailures')}
            </Text>
            {row.lastError && (
              <Text size="xs" c="dimmed">
                {row.lastError}
              </Text>
            )}
          </List.Item>
        ))}
      </List>
    </Alert>
  );
}
