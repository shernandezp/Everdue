import { ActionIcon, Alert, Badge, Button, Group, Modal, Select, Stack, Text, Textarea, TextInput } from '@mantine/core';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { IconDeviceFloppy, IconInfoCircle, IconPencil, IconPlus, IconTrash } from '@tabler/icons-react';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ENTITY_FIELD_TYPES, ENTITY_TYPES, type EntityFieldDef, type EntityFieldType } from '../../../api/types';
import { api } from '../../../lib/api';
import { notifyError, notifySaved } from '../../../lib/notify';
import { keys } from '../../../lib/queryKeys';

/**
 * Custom field definitions. A tab of the settings page — the header above it belongs to Settings.
 *
 * The screen states the boundary rather than assuming somebody already knows the rule: these are display-only
 * references, capped per entity type, and nothing in the product filters, sorts or reports on them. The moment a
 * custom field drives behaviour, entities have stopped being thin.
 */
export function EntityFieldDefsPanel() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<EntityFieldDef | 'new' | null>(null);

  const defs = useQuery({
    queryKey: keys.entityFields.includingInactive,
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
      <Group justify="space-between" align="flex-start" wrap="wrap" gap="sm">
        <Text size="sm" c="dimmed" maw={640}>
          {t('entityFields.description')}
        </Text>
        <Button size="xs" leftSection={<IconPlus size={14} />} onClick={() => setEditing('new')}>
          {t('entityFields.add')}
        </Button>
      </Group>

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
              <Group gap={4} justify="flex-end" wrap="nowrap">
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
        {t('entityFields.deleteNote')}
      </Text>

      {editing && (
        <FieldModal field={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={refresh} />
      )}
    </Stack>
  );
}

const MAX_OPTIONS = 20;

// One per line, or tab-separated as pasted from a spreadsheet row. Not commas: entity names contain them.
// Deduplicated case-insensitively, as the server does, so the limit counts what will actually be stored.
const parseOptions = (text: string) => {
  const options = text
    .split(/[\r\n\t]+/)
    .map((option) => option.trim())
    .filter((option) => option.length > 0);

  return options.filter(
    (option, index) => options.findIndex((other) => other.toLowerCase() === option.toLowerCase()) === index,
  );
};

// Mounted only while open, so each opening starts from the stored field rather than a previous draft.
function FieldModal({
  field,
  onClose,
  onSaved,
}: {
  field: EntityFieldDef | null;
  onClose: () => void;
  onSaved: () => Promise<unknown>;
}) {
  const { t } = useTranslation();
  const [entityType, setEntityType] = useState<string>(field?.entityType ?? 'Customer');
  const [name, setName] = useState(field?.name ?? '');
  const [fieldType, setFieldType] = useState<EntityFieldType>(field?.fieldType ?? 'Text');
  const [options, setOptions] = useState(field?.options.join('\n') ?? '');

  const parsed = fieldType === 'Select' ? parseOptions(options) : null;
  const tooManyOptions = (parsed?.length ?? 0) > MAX_OPTIONS;

  const save = useMutation({
    mutationFn: () =>
      field
        ? api.entityFields.update(field.id, {
            name: name.trim(),
            options: parsed,
            position: field.position,
            active: field.active,
          })
        : api.entityFields.create({ entityType, name: name.trim(), fieldType, options: parsed }),
    onSuccess: async () => {
      notifySaved();
      await onSaved();
      onClose();
    },
    onError: notifyError,
  });

  return (
    <Modal opened onClose={onClose} title={t(field ? 'entityFields.edit' : 'entityFields.add')}>
      <Stack>
        <Select
          label={t('entityFields.entityType')}
          data={ENTITY_TYPES.map((type) => ({ value: type, label: t(`entityType.${type}`) }))}
          value={entityType}
          allowDeselect={false}
          disabled={field !== null}
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
          disabled={field !== null}
          onChange={(value) => setFieldType((value ?? 'Text') as EntityFieldType)}
        />

        {fieldType === 'Select' && (
          <Textarea
            label={t('entityFields.options')}
            description={t(field ? 'entityFields.optionsEditHint' : 'entityFields.optionsHint')}
            error={tooManyOptions ? t('entityFields.tooManyOptions', { max: MAX_OPTIONS }) : undefined}
            autosize
            minRows={3}
            value={options}
            onChange={(event) => setOptions(event.currentTarget.value)}
          />
        )}

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button
            loading={save.isPending}
            disabled={name.trim().length === 0 || tooManyOptions}
            leftSection={field ? <IconDeviceFloppy size={16} /> : <IconPlus size={16} />}
            onClick={() => save.mutate()}
          >
            {t(field ? 'common.save' : 'common.create')}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
