import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  Code,
  CopyButton,
  Group,
  Modal,
  Select,
  Stack,
  Text,
  TextInput,
} from '@mantine/core';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { IconAlertTriangle, IconCopy, IconInfoCircle, IconPlus, IconTrash, IconX } from '@tabler/icons-react';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { API_KEY_SCOPES, type ApiKey, type ApiKeyScope } from '../../../api/types';
import { PageHeader } from '../../../components/PageHeader';
import { api } from '../../../lib/api';
import { formatDateTime } from '../../../lib/format';
import { notifyError, notifySaved } from '../../../lib/notify';
import { keys } from '../../../lib/queryKeys';

/**
 * Keys for the public API.
 *
 * The page carries two facts that would otherwise be discovered the hard way: the token is shown exactly once,
 * and a key cannot reach user, settings, channel, import or key-management endpoints whatever its actor's role
 * is. The second is what makes a key in a script's environment variable a contained thing.
 */
export function ApiKeysPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [creating, setCreating] = useState(false);

  const apiKeys = useQuery({
    queryKey: keys.apiKeys.all,
    queryFn: () => api.apiKeys.list({ includeRevoked: true }),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: keys.apiKeys.all });

  const revoke = useMutation({
    mutationFn: (id: string) => api.apiKeys.revoke(id),
    onSuccess: () => {
      notifySaved();
      return refresh();
    },
    onError: notifyError,
  });

  return (
    <Stack>
      <PageHeader
        title={t('apiKeys.title')}
        description={t('apiKeys.description')}
        actions={
          <Button size="xs" leftSection={<IconPlus size={14} />} onClick={() => setCreating(true)}>
            {t('apiKeys.create')}
          </Button>
        }
      />

      <Alert variant="light" color="blue" icon={<IconInfoCircle size={16} />}>
        <Text size="sm">{t('apiKeys.scopeNote')}</Text>
      </Alert>

      <DataTable
        highlightOnHover
        withTableBorder
        minHeight={200}
        records={apiKeys.data ?? []}
        fetching={apiKeys.isLoading}
        noRecordsText={t('apiKeys.none')}
        idAccessor="id"
        rowBackgroundColor={(row: ApiKey) => (row.revokedAt ? 'var(--mantine-color-red-light)' : undefined)}
        columns={[
          { accessor: 'name', title: t('common.name') },
          {
            accessor: 'keyPrefix',
            title: t('apiKeys.prefix'),
            render: (row: ApiKey) => <Code>{row.keyPrefix}…</Code>,
          },
          {
            accessor: 'scope',
            title: t('apiKeys.scope'),
            render: (row: ApiKey) => (
              <Badge variant="light" color={row.scope === 'ReadWrite' ? 'orange' : 'gray'}>
                {t(`apiKeys.scopes.${row.scope}`)}
              </Badge>
            ),
          },
          { accessor: 'actorDisplayName', title: t('apiKeys.actor') },
          {
            accessor: 'lastUsedAt',
            title: t('apiKeys.lastUsed'),
            render: (row: ApiKey) => (row.lastUsedAt ? formatDateTime(row.lastUsedAt) : t('common.never')),
          },
          {
            accessor: 'expiresAt',
            title: t('apiKeys.expires'),
            render: (row: ApiKey) => (row.expiresAt ? formatDateTime(row.expiresAt) : '—'),
          },
          {
            accessor: 'actions',
            title: t('common.actions'),
            textAlign: 'right',
            render: (row: ApiKey) =>
              row.revokedAt ? (
                <Text size="xs" c="dimmed">
                  {t('apiKeys.revokedOn', { when: formatDateTime(row.revokedAt) })}
                </Text>
              ) : (
                <ActionIcon
                  variant="subtle"
                  color="red"
                  aria-label={t('apiKeys.revoke')}
                  onClick={() => revoke.mutate(row.id)}
                >
                  <IconTrash size={16} />
                </ActionIcon>
              ),
          },
        ]}
      />

      <Text size="xs" c="dimmed">
        {t('apiKeys.actorNote')}
      </Text>

      <CreateApiKeyModal opened={creating} onClose={() => setCreating(false)} onCreated={refresh} />
    </Stack>
  );
}

function CreateApiKeyModal({
  opened,
  onClose,
  onCreated,
}: {
  opened: boolean;
  onClose: () => void;
  onCreated: () => Promise<unknown>;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState('');
  const [scope, setScope] = useState<ApiKeyScope>('ReadOnly');
  const [token, setToken] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => api.apiKeys.create({ name: name.trim(), scope }),
    onSuccess: async (created) => {
      setToken(created.token);
      await onCreated();
    },
    onError: notifyError,
  });

  const close = () => {
    setName('');
    setScope('ReadOnly');
    setToken(null);
    onClose();
  };

  return (
    <Modal opened={opened} onClose={close} title={t('apiKeys.create')}>
      {token ? (
        <Stack>
          {/* The only time this string exists outside the server. Everdue stores a hash and cannot show it again. */}
          <Alert variant="light" color="orange" icon={<IconAlertTriangle size={16} />}>
            <Text size="sm">{t('apiKeys.shownOnce')}</Text>
          </Alert>

          <Code block style={{ wordBreak: 'break-all' }}>
            {token}
          </Code>

          <Group justify="space-between">
            <CopyButton value={token}>
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

            <Button leftSection={<IconX size={16} />} onClick={close}>
              {t('common.close')}
            </Button>
          </Group>

          <Text size="xs" c="dimmed">
            {t('apiKeys.headerHint')}
          </Text>
        </Stack>
      ) : (
        <Stack>
          <TextInput
            label={t('common.name')}
            description={t('apiKeys.nameHint')}
            value={name}
            maxLength={100}
            required
            onChange={(event) => setName(event.currentTarget.value)}
          />

          <Select
            label={t('apiKeys.scope')}
            data={API_KEY_SCOPES.map((value) => ({ value, label: t(`apiKeys.scopes.${value}`) }))}
            value={scope}
            allowDeselect={false}
            onChange={(value) => setScope((value ?? 'ReadOnly') as ApiKeyScope)}
          />

          <Group justify="flex-end">
            <Button variant="default" onClick={close}>
              {t('common.cancel')}
            </Button>
            <Button
              loading={create.isPending}
              disabled={name.trim().length === 0}
              leftSection={<IconPlus size={16} />}
              onClick={() => create.mutate()}
            >
              {t('common.create')}
            </Button>
          </Group>
        </Stack>
      )}
    </Modal>
  );
}
