import { Anchor, Badge, Card, Group, Stack, Text, ThemeIcon, Title } from '@mantine/core';
import { IconRepeatOff } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { api } from '../../lib/api';
import { formatDate } from '../../lib/format';
import { keys } from '../../lib/queryKeys';

/**
 * The chronically delayed block on the manager's home screen: responsibilities that keep being missed,
 * not the ones that were missed once. Its own endpoint, so the exception dashboard's own query stays
 * exactly what it was.
 */
export function ChronicDelayCard({ limit = 5 }: { limit?: number }) {
  const { t } = useTranslation();

  const chronic = useQuery({
    queryKey: keys.insights.chronic(limit),
    queryFn: () => api.insights.chronic({ limit }),
  });

  return (
    <Card withBorder padding="md">
      <Group gap="xs" mb="sm">
        <ThemeIcon size="md" radius="md" variant="light" color="red">
          <IconRepeatOff size={16} />
        </ThemeIcon>
        <Title order={5}>{t('insights.chronicTitle')}</Title>
      </Group>

      {chronic.data?.length === 0 && (
        <Text size="sm" c="dimmed">
          {t('insights.chronicEmpty')}
        </Text>
      )}

      <Stack gap="xs">
        {(chronic.data ?? []).map((row) => (
          <Group key={row.responsibilityId} justify="space-between" wrap="nowrap">
            <Stack gap={0}>
              <Anchor component={Link} to={`/insights/compliance/${row.responsibilityId}`} size="sm">
                {row.title}
              </Anchor>
              <Text size="xs" c="dimmed">
                {row.ownerName}
                {row.entityName ? ` · ${row.entityName}` : ''}
                {row.lastMissedPeriodStart ? ` · ${t('insights.lastMissed')} ${formatDate(row.lastMissedPeriodStart)}` : ''}
              </Text>
            </Stack>

            <Badge color="red" variant="light" style={{ whiteSpace: 'nowrap' }}>
              {t('insights.missedOf', { missed: row.missed, evaluated: row.evaluated })}
            </Badge>
          </Group>
        ))}
      </Stack>
    </Card>
  );
}
