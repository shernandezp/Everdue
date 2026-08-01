import { Alert, Card, Group, Loader, Select, Stack, Switch, Text, ThemeIcon, Title } from '@mantine/core';
import { IconBell, IconInfoCircle } from '@tabler/icons-react';
import { useTranslation } from 'react-i18next';
import type { NotificationChannel } from '../../api/types';
import { NOTIFICATION_TYPES } from '../../api/types';
import { useNotificationPreferences, useSavePreferences } from './hooks';
import { TelegramLinkCard } from './TelegramLinkCard';

/**
 * How this person wants to be told. The channel list only offers what the installation has actually
 * configured, so nobody can select a channel and then wonder why nothing arrives.
 */
export function NotificationPreferencesCard() {
  const { t } = useTranslation();
  const preferences = useNotificationPreferences();
  const save = useSavePreferences();

  if (preferences.isLoading) return <Loader />;
  if (!preferences.data) return null;

  const current = preferences.data;
  const types = current.types ?? {};

  const setChannel = (value: string | null) =>
    save.mutate({ channel: (value as NotificationChannel | null) ?? null, types });

  const setType = (type: string, enabled: boolean) =>
    save.mutate({ channel: current.channel ?? null, types: { ...types, [type]: enabled } });

  const available = current.availableChannels ?? [];

  return (
    <Stack gap="md">
      <Card withBorder padding="md">
        <Stack gap="sm">
          <Group gap="xs">
            <ThemeIcon size="md" radius="md" variant="light" color="everdue">
              <IconBell size={16} />
            </ThemeIcon>
            <Title order={5}>{t('notifications.preferences')}</Title>
          </Group>

          <Text size="sm" c="dimmed">
            {t('notifications.preferencesHint')}
          </Text>

          <Select
            label={t('notifications.channel')}
            description={t('notifications.channelHint')}
            data={[
              { value: '', label: t('notifications.inAppOnly') },
              ...available.map((channel) => ({ value: channel, label: t(`notifications.channels.${channel}`) })),
            ]}
            value={current.channel ?? ''}
            onChange={(value) => setChannel(value === '' ? null : value)}
            allowDeselect={false}
            disabled={save.isPending}
            w={280}
          />

          {available.length === 0 && (
            <Alert color="gray" icon={<IconInfoCircle size={16} />}>
              {t('notifications.noChannelsConfigured')}
            </Alert>
          )}

          <Stack gap={6} mt="xs">
            <Text size="sm" fw={500}>
              {t('notifications.tellMeAbout')}
            </Text>

            {NOTIFICATION_TYPES.map((type) => (
              <Switch
                key={type}
                label={t(`notifications.type.${type}`)}
                description={t(`notifications.typeHint.${type}`)}
                checked={types[type] ?? true}
                onChange={(event) => setType(type, event.currentTarget.checked)}
                disabled={save.isPending}
              />
            ))}
          </Stack>

          {current.whatsAppPhoneE164 && (
            <Group gap="xs">
              <Text size="xs" c="dimmed">
                {t('notifications.whatsAppNumber')}:
              </Text>
              <Text size="xs">{current.whatsAppPhoneE164}</Text>
            </Group>
          )}
        </Stack>
      </Card>

      <TelegramLinkCard linked={current.telegramLinked} />
    </Stack>
  );
}
