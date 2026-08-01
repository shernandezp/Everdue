import { Badge, Button, Card, Group, JsonInput, Stack, Switch, Text, ThemeIcon, Title } from '@mantine/core';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { IconDeviceFloppy, IconSend, IconTrash } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ChannelSettings, NotificationChannel } from '../../../api/types';
import { api } from '../../../lib/api';
import { notifyError, notifySaved } from '../../../lib/notify';
import { keys } from '../../../lib/queryKeys';
import { CHANNEL_ICON } from '../../../theme';

/**
 * Each channel owns the shape of its own configuration, so this edits the JSON directly rather than
 * inventing a second schema per provider that would have to be kept in step with the first.
 *
 * Secrets are never rendered back — leaving one blank keeps the stored value, which is why changing
 * a bot's username does not require re-typing its token.
 */
const TEMPLATES: Record<NotificationChannel, string> = {
  Email: JSON.stringify(
    { host: 'smtp.example.com', port: 587, user: '', password: '', from: 'everdue@example.com', fromName: 'Everdue', useStartTls: true },
    null,
    2,
  ),
  Telegram: JSON.stringify({ botToken: '', botUsername: 'everduebot' }, null, 2),
  WhatsApp: JSON.stringify(
    {
      phoneNumberId: '',
      accessToken: '',
      templateLanguage: 'es',
      templates: {
        Assigned: 'everdue_assigned',
        DueToday: 'everdue_due_today',
        Missed: 'everdue_missed',
        Mentioned: 'everdue_mentioned',
        PutOnHold: 'everdue_on_hold',
        Test: 'everdue_test',
      },
    },
    null,
    2,
  ),
};

export function ChannelForm({ channel, settings }: { channel: NotificationChannel; settings?: ChannelSettings }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [configJson, setConfigJson] = useState(settings?.redactedConfigJson ?? TEMPLATES[channel]);

  // A channel with no row yet is being set up, so the switch starts on: nobody fills in a token in
  // order to leave it switched off.
  const [active, setActive] = useState(settings?.redactedConfigJson ? settings.active : true);
  const [touched, setTouched] = useState(false);

  // What is stored, minus its secrets — so changing a bot's username does not mean re-typing every
  // template name. An edit in progress is never overwritten by a refetch.
  useEffect(() => {
    if (touched) return;

    setConfigJson(settings?.redactedConfigJson ?? TEMPLATES[channel]);
    setActive(settings?.redactedConfigJson ? settings.active : true);
  }, [settings?.redactedConfigJson, settings?.active, channel, touched]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: keys.channels.all });

  const save = useMutation({
    mutationFn: () => api.channels.save(channel, configJson, active),
    onSuccess: async () => {
      notifySaved();
      setTouched(false);
      await refresh();
    },
    onError: notifyError,
  });

  const remove = useMutation({
    mutationFn: () => api.channels.remove(channel),
    onSuccess: refresh,
    onError: notifyError,
  });

  const test = useMutation({
    mutationFn: () => api.channels.test(channel),
    onSuccess: (result) =>
      result.sent ? notifySaved(t('channels.testSent')) : notifyError(new Error(result.error ?? t('channels.testFailed'))),
    onError: notifyError,
  });

  const ChannelIcon = CHANNEL_ICON[channel];

  return (
    <Card withBorder padding="md">
      <Stack gap="sm">
        <Group justify="space-between">
          <Group gap="xs">
            <ThemeIcon size="md" radius="md" variant="light" color={settings?.configured ? 'teal' : 'gray'}>
              <ChannelIcon size={16} />
            </ThemeIcon>
            <Title order={5}>{t(`notifications.channels.${channel}`)}</Title>

            {settings?.configured ? (
              <Badge color="teal" variant="light">
                {settings.usingSystemScope ? t('channels.systemScope') : t('channels.configured')}
              </Badge>
            ) : (
              <Badge color="gray" variant="light">
                {t('channels.notConfigured')}
              </Badge>
            )}
          </Group>

          {settings?.summary && (
            <Text size="sm" c="dimmed">
              {settings.summary}
            </Text>
          )}
        </Group>

        {channel === 'WhatsApp' && (
          <Text size="xs" c="dimmed">
            {t('channels.whatsAppHint')}
          </Text>
        )}

        <JsonInput
          label={t('channels.configuration')}
          description={t('channels.secretHint')}
          value={configJson}
          onChange={(value) => {
            setTouched(true);
            setConfigJson(value);
          }}
          autosize
          minRows={6}
          validationError={t('channels.invalidJson')}
          formatOnBlur
        />

        <Switch
          label={t('channels.active')}
          checked={active}
          onChange={(event) => {
            setTouched(true);
            setActive(event.currentTarget.checked);
          }}
        />

        <Group justify="flex-end" gap="xs">
          <Button
            variant="subtle"
            color="red"
            leftSection={<IconTrash size={16} />}
            onClick={() => remove.mutate()}
            loading={remove.isPending}
          >
            {t('channels.clear')}
          </Button>
          <Button
            variant="light"
            color="teal"
            leftSection={<IconSend size={16} />}
            onClick={() => test.mutate()}
            loading={test.isPending}
            disabled={!settings?.configured}
          >
            {t('channels.sendTest')}
          </Button>
          <Button leftSection={<IconDeviceFloppy size={16} />} onClick={() => save.mutate()} loading={save.isPending}>
            {t('common.save')}
          </Button>
        </Group>
      </Stack>
    </Card>
  );
}
