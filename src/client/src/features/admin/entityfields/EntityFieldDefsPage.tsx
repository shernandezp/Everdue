import { ActionIcon, Alert, Badge, Button, Group, Modal, Select, Stack, Text, TextInput } from '@mantine/core';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { IconInfoCircle, IconPlus, IconTrash } from '@tabler/icons-react';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ENTITY_FIELD_TYPES, ENTITY_TYPES, type EntityFieldDef, type EntityFieldType } from '../../../api/types';
import { PageHeader } from '../../../components/PageHeader';
import { api } from '../../../lib/api';
import { notifyError, notifySaved } from '../../../lib/notify';
import { keys } from '../../../lib/queryKeys';

/**
 * Custom field definitions.
 *
 * The page states the boundary rather than assuming somebody read the guardrails: these are display-only
 * references, capped per entity type, and nothing in the product filters, sorts or reports on them. The moment a
 * custom field drives behaviour, entities have stopped being thin.
 */
export function EntityFieldDefsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [creating, setCreating] = useState(false);

  const defs = useQuery({
    queryKey: keys.entityFields.all,
    queryFn: () => api.entityFields.list({ includeInactive: true }),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: keys.entityFields.all });

  const remove = useMutation({
    mutationFn: (id: string) => api.entityFields.remove(id),
    onSuccess: () => {
      notifySaved();
      return refresh();
    },
    onError: notifyError,
  });

  return (
    <Stack>
      <PageHeader
        title={t('entityFields.title')}
        description={t('entityFields.description')}
        actions={
          <Button size="xs" leftSection={<IconPlus size={14} />} onClick={() => setCreating(true)}>
            {t('entityFields.add')}
          </Button>
        }
      />

      <Alert variant="light" color="blue" icon={<IconInfoCircle size={16} />}>
        <Text size="sm">{t('entityFields.guardrail')}</Text>
      </Alert>

      <DataTable
        highlightOnHover
        withTableBorder
        minHeight={200}
        records={defs.data ?? []}
        fetching={defs.isLoading}
        noRecordsText={t('entityFields.none')}
        idAccessor="id"
        columns={[
          {
            accessor: 'entityType',
            title: t('entityFields.entityType'),
            render: (row: EntityFieldDef) => <Badge variant="light">{t(`entityType.${row.entityType}`)}</Badge>,
          },
          { accessor: 'name', title: t('common.name') },
          {
            accessor: 'fieldType',
            title: t('entityFields.fieldType'),
            render: (row: EntityFieldDef) => t(`entityFields.type.${row.fieldType}`),
          },
          {
            accessor: 'options',
            title: t('entityFields.options'),
            render: (row: EntityFieldDef) => (row.options.length > 0 ? row.options.join(', ') : '—'),
          },
          {
            accessor: 'actions',
            title: t('common.actions'),
            textAlign: 'right',
            render: (row: EntityFieldDef) => (
              <ActionIcon
                variant="subtle"
                color="red"
                aria-label={t('common.delete')}
                onClick={() => remove.mutate(row.id)}
              >
                <IconTrash size={16} />
              </ActionIcon>
            ),
          },
        ]}
      />

      <Text size="xs" c="dimmed">
        {t('entityFields.deleteNote')}
      </Text>

      <CreateFieldModal opened={creating} onClose={() => setCreating(false)} onCreated={refresh} />
    </Stack>
  );
}

function CreateFieldModal({
  opened,
  onClose,
  onCreated,
}: {
  opened: boolean;
  onClose: () => void;
  onCreated: () => Promise<unknown>;
}) {
  const { t } = useTranslation();
  const [entityType, setEntityType] = useState<string>('Customer');
  const [name, setName] = useState('');
  const [fieldType, setFieldType] = useState<EntityFieldType>('Text');
  const [options, setOptions] = useState('');

  const create = useMutation({
    mutationFn: () =>
      api.entityFields.create({
        entityType,
        name: name.trim(),
        fieldType,

        // One per line: an option list separated by commas cannot contain a comma, and entity names do.
        options:
          fieldType === 'Select'
            ? options
                .split('\n')
                .map((option) => option.trim())
                .filter((option) => option.length > 0)
            : null,
      }),
    onSuccess: async () => {
      notifySaved();
      setName('');
      setOptions('');
      await onCreated();
      onClose();
    },
    onError: notifyError,
  });

  return (
    <Modal opened={opened} onClose={onClose} title={t('entityFields.add')}>
      <Stack>
        <Select
          label={t('entityFields.entityType')}
          data={ENTITY_TYPES.map((type) => ({ value: type, label: t(`entityType.${type}`) }))}
          value={entityType}
          allowDeselect={false}
          onChange={(value) => setEntityType(value ?? 'Customer')}
        />

        <TextInput
          label={t('common.name')}
          value={name}
          maxLength={50}
          required
          onChange={(event) => setName(event.currentTarget.value)}
        />

        <Select
          label={t('entityFields.fieldType')}
          description={t('entityFields.fieldTypeHint')}
          data={ENTITY_FIELD_TYPES.map((type) => ({ value: type, label: t(`entityFields.type.${type}`) }))}
          value={fieldType}
          allowDeselect={false}
          onChange={(value) => setFieldType((value ?? 'Text') as EntityFieldType)}
        />

        {fieldType === 'Select' && (
          <TextInput
            label={t('entityFields.options')}
            description={t('entityFields.optionsHint')}
            value={options}
            onChange={(event) => setOptions(event.currentTarget.value)}
          />
        )}

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
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
    </Modal>
  );
}
