import { Button, Group, MultiSelect, Stack, Switch, TextInput } from '@mantine/core';
import { IconDeviceFloppy, IconPlus } from '@tabler/icons-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { WEBHOOK_EVENT_TYPES, type WebhookEventType, type WebhookSubscription } from '../../../api/types';

export type WebhookDraft = { url: string; eventTypes: WebhookEventType[]; active: boolean };

/**
 * Create and edit share one form. The only asymmetry is <c>active</c>, which on an existing subscription is also
 * how an auto-disabled one comes back — re-enabling is a decision somebody makes, never something that happens
 * on its own.
 */
export function WebhookForm({
  existing,
  onSubmit,
  onCancel,
  submitting,
}: {
  existing?: WebhookSubscription;
  onSubmit: (draft: WebhookDraft) => void;
  onCancel: () => void;
  submitting: boolean;
}) {
  const { t } = useTranslation();

  const [url, setUrl] = useState(existing?.url ?? '');
  const [eventTypes, setEventTypes] = useState<WebhookEventType[]>(existing?.eventTypes ?? ['WorkItemMissed']);
  const [active, setActive] = useState(existing?.active ?? true);

  return (
    <Stack>
      <TextInput
        label={t('webhooks.url')}
        description={t('webhooks.urlHint')}
        placeholder="https://example.com/hooks/everdue"
        value={url}
        maxLength={500}
        required
        onChange={(event) => setUrl(event.currentTarget.value)}
      />

      <MultiSelect
        label={t('webhooks.events')}
        description={t('webhooks.eventsHint')}
        data={WEBHOOK_EVENT_TYPES.map((value) => ({ value, label: t(`webhooks.eventTypes.${value}`) }))}
        value={eventTypes}
        onChange={(values) => setEventTypes(values as WebhookEventType[])}
      />

      {existing && (
        <Switch
          checked={active}
          label={t('common.active')}
          description={existing.disabledAt ? t('webhooks.reEnableHint') : undefined}
          onChange={(event) => setActive(event.currentTarget.checked)}
        />
      )}

      <Group justify="flex-end">
        <Button variant="default" onClick={onCancel}>
          {t('common.cancel')}
        </Button>
        <Button
          loading={submitting}
          disabled={url.trim().length === 0 || eventTypes.length === 0}
          leftSection={existing ? <IconDeviceFloppy size={16} /> : <IconPlus size={16} />}
          onClick={() => onSubmit({ url: url.trim(), eventTypes, active })}
        >
          {existing ? t('common.save') : t('common.create')}
        </Button>
      </Group>
    </Stack>
  );
}
