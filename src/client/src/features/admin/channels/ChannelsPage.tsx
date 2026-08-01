import { Alert, Badge, Card, Group, Loader, Stack, Table, Text, ThemeIcon } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { IconAlertTriangle, IconHeartRateMonitor } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import { NOTIFICATION_CHANNELS } from '../../../api/types';
import { PageHeader } from '../../../components/PageHeader';
import { api } from '../../../lib/api';
import { ChannelForm } from './ChannelForm';
import { keys } from '../../../lib/queryKeys';
import { CHANNEL_ICON } from '../../../theme';

/**
 * A page of its own rather than more rows on tenant settings: each channel has its own form, its own
 * test button and its own failure story, and bundling them would produce one screen nobody can read.
 */
export function ChannelsPage() {
  const { t } = useTranslation();

  const settings = useQuery({ queryKey: keys.channels.all, queryFn: () => api.channels.list() });
  const health = useQuery({ queryKey: keys.channels.health, queryFn: () => api.channels.health(), refetchInterval: 60_000 });

  const failing = (health.data ?? []).filter((h) => h.failedRecently > 0);

  return (
    <>
      <PageHeader title={t('channels.title')} description={t('channels.description')} />

      {failing.length > 0 && (
        <Alert color="red" icon={<IconAlertTriangle size={18} />} mb="md" title={t('channels.deliveryProblem')}>
          <Stack gap={4}>
            {failing.map((h) => (
              <Text key={h.channel} size="sm">
                <strong>{t(`notifications.channels.${h.channel}`)}</strong>: {h.lastError ?? t('channels.unknownError')}
              </Text>
            ))}
          </Stack>
        </Alert>
      )}

      {settings.isLoading && <Loader />}

      <Stack gap="md">
        {NOTIFICATION_CHANNELS.map((channel) => (
          <ChannelForm
            key={channel}
            channel={channel}
            settings={settings.data?.find((s) => s.channel === channel)}
          />
        ))}
      </Stack>

      <Card withBorder padding="md" mt="md">
        <Stack gap="xs">
          <Group gap="xs">
            <ThemeIcon size="sm" radius="sm" variant="light" color="teal">
              <IconHeartRateMonitor size={14} />
            </ThemeIcon>
            <Text fw={600}>{t('channels.health')}</Text>
          </Group>

          <Table highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>{t('channels.channel')}</Table.Th>
                <Table.Th>{t('channels.pending')}</Table.Th>
                <Table.Th>{t('channels.failed24h')}</Table.Th>
                <Table.Th>{t('channels.skipped24h')}</Table.Th>
                <Table.Th>{t('channels.receipts')}</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {(health.data ?? []).map((row) => {
                const ChannelIcon = CHANNEL_ICON[row.channel];

                return (
                  <Table.Tr key={row.channel}>
                    <Table.Td>
                      <Group gap={6} wrap="nowrap">
                        <ChannelIcon size={16} style={{ color: 'var(--mantine-color-dimmed)' }} />
                        {t(`notifications.channels.${row.channel}`)}
                      </Group>
                    </Table.Td>
                    <Table.Td>{row.pending}</Table.Td>
                    <Table.Td>
                      <Text c={row.failedRecently > 0 ? 'red' : undefined} fw={row.failedRecently > 0 ? 600 : undefined}>
                        {row.failedRecently}
                      </Text>
                    </Table.Td>
                    <Table.Td>{row.skippedRecently}</Table.Td>
                    <Table.Td>
                      {/* Said out loud: without a webhook, "sent" means the provider accepted it. */}
                      <Badge size="sm" variant="light" color={row.deliveryReceiptsSupported ? 'teal' : 'gray'}>
                        {row.deliveryReceiptsSupported ? t('channels.receiptsYes') : t('channels.receiptsNo')}
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                );
              })}
            </Table.Tbody>
          </Table>
        </Stack>
      </Card>
    </>
  );
}
