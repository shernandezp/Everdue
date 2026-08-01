import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  Code,
  CopyButton,
  Group,
  Modal,
  Stack,
  Text,
} from '@mantine/core';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { IconAlertTriangle, IconCopy, IconPencil, IconPlus, IconSend, IconTrash, IconX } from '@tabler/icons-react';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { WebhookSubscription } from '../../../api/types';
import { api } from '../../../lib/api';
import { formatDateTime } from '../../../lib/format';
import { notifyError, notifySaved } from '../../../lib/notify';
import { keys } from '../../../lib/queryKeys';
import { WebhookForm, type WebhookDraft } from './WebhookForm';
import { WebhookHealthCard } from './WebhookHealthCard';

/**
 * Where a tenant wants to be told about work. A tab of the settings page — the header above it
 * belongs to Settings.
 *
 * Outbound only, and the screen says so: Everdue makes HTTP calls out, which a home or office router permits, and
 * never needs an inbound endpoint. Delivery is at-least-once, so the note about `webhook-id` being the receiver's
 * idempotency key is on the screen rather than only in the docs.
 */
export function WebhooksPanel() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<WebhookSubscription | null>(null);
  const [secret, setSecret] = useState<string | null>(null);

  const subscriptions = useQuery({ queryKey: keys.webhooks.all, queryFn: () => api.webhooks.list() });
  const health = useQuery({ queryKey: keys.webhooks.health, queryFn: () => api.webhooks.health() });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: keys.webhooks.all });
    await queryClient.invalidateQueries({ queryKey: keys.webhooks.health });
  };

  const create = useMutation({
    mutationFn: (draft: WebhookDraft) => api.webhooks.create({ url: draft.url, eventTypes: draft.eventTypes }),
    onSuccess: async (created) => {
      setCreating(false);
      setSecret(created.secret);
      await refresh();
    },
    onError: notifyError,
  });

  const update = useMutation({
    mutationFn: ({ id, draft }: { id: string; draft: WebhookDraft }) => api.webhooks.update(id, draft),
    onSuccess: async () => {
      notifySaved();
      setEditing(null);
      await refresh();
    },
    onError: notifyError,
  });

  const test = useMutation({
    mutationFn: (id: string) => api.webhooks.test(id),
    onSuccess: async () => {
      notifySaved(t('webhooks.testQueued'));
      await refresh();
    },
    onError: notifyError,
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.webhooks.remove(id),
    onSuccess: async () => {
      notifySaved();
      await refresh();
    },
    onError: notifyError,
  });

  return (
    <Stack>
      <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
        <Text size="sm" c="dimmed" maw={640}>
          {t('webhooks.description')}
        </Text>
        <Button size="xs" leftSection={<IconPlus size={14} />} onClick={() => setCreating(true)}>
          {t('webhooks.add')}
        </Button>
      </Group>

      <WebhookHealthCard health={health.data ?? []} />

      <DataTable
        highlightOnHover
        withTableBorder
        minHeight={200}
        records={subscriptions.data ?? []}
        fetching={subscriptions.isLoading}
        noRecordsText={t('webhooks.none')}
        idAccessor="id"
        columns={[
          {
            accessor: 'url',
            title: t('webhooks.url'),
            render: (row: WebhookSubscription) => (
              <Stack gap={2}>
                <Text size="sm" style={{ wordBreak: 'break-all' }}>
                  {row.url}
                </Text>
                {!row.active && (
                  <Badge size="xs" color="red" variant="light">
                    {t('webhooks.disabled')}
                  </Badge>
                )}
              </Stack>
            ),
          },
          {
            accessor: 'eventTypes',
            title: t('webhooks.events'),
            render: (row: WebhookSubscription) => (
              <Group gap={4}>
                {row.eventTypes.map((type) => (
                  <Badge key={type} size="xs" variant="light">
                    {t(`webhooks.eventTypes.${type}`)}
                  </Badge>
                ))}
              </Group>
            ),
          },
          {
            accessor: 'lastSuccessAt',
            title: t('webhooks.lastSuccess'),
            render: (row: WebhookSubscription) =>
              row.lastSuccessAt ? formatDateTime(row.lastSuccessAt) : t('common.never'),
          },
          {
            accessor: 'actions',
            title: t('common.actions'),
            textAlign: 'right',
            render: (row: WebhookSubscription) => (
              <Group gap={4} justify="flex-end" wrap="nowrap">
                <ActionIcon
                  variant="subtle"
                  aria-label={t('webhooks.test')}
                  disabled={!row.active}
                  onClick={() => test.mutate(row.id)}
                >
                  <IconSend size={16} />
                </ActionIcon>
                <ActionIcon variant="subtle" aria-label={t('common.edit')} onClick={() => setEditing(row)}>
                  <IconPencil size={16} />
                </ActionIcon>
                <ActionIcon
                  variant="subtle"
                  color="red"
                  aria-label={t('common.delete')}
                  onClick={() => remove.mutate(row.id)}
                >
                  <IconTrash size={16} />
                </ActionIcon>
              </Group>
            ),
          },
        ]}
      />

      <Text size="xs" c="dimmed">
        {t('webhooks.deliveryNote')}
      </Text>

      <Modal opened={creating} onClose={() => setCreating(false)} title={t('webhooks.add')}>
        <WebhookForm onSubmit={(draft) => create.mutate(draft)} onCancel={() => setCreating(false)} submitting={create.isPending} />
      </Modal>

      <Modal opened={editing !== null} onClose={() => setEditing(null)} title={t('webhooks.edit')}>
        {editing && (
          <WebhookForm
            existing={editing}
            onSubmit={(draft) => update.mutate({ id: editing.id, draft })}
            onCancel={() => setEditing(null)}
            submitting={update.isPending}
          />
        )}
      </Modal>

      {/* The signing secret, shown once. Everdue keeps only its ciphertext and cannot show it again. */}
      <Modal opened={secret !== null} onClose={() => setSecret(null)} title={t('webhooks.secretTitle')}>
        <Stack>
          <Alert variant="light" color="orange" icon={<IconAlertTriangle size={16} />}>
            <Text size="sm">{t('webhooks.secretShownOnce')}</Text>
          </Alert>

          <Code block style={{ wordBreak: 'break-all' }}>
            {secret}
          </Code>

          <Text size="xs" c="dimmed">
            {t('webhooks.secretVerifyHint')}
          </Text>

          <Group justify="flex-end">
            {secret && (
              <CopyButton value={secret}>
                {({ copied, copy }) => (
                  <Button
                    variant="light"
                    leftSection={<IconCopy size={14} />}
                    color={copied ? 'teal' : undefined}
                    onClick={copy}
                  >
                    {copied ? t('common.copied') : t('common.copy')}
                  </Button>
                )}
              </CopyButton>
            )}
            <Button leftSection={<IconX size={16} />} onClick={() => setSecret(null)}>
              {t('common.close')}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}
