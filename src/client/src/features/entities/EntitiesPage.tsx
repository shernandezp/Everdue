import { ActionIcon, Anchor, Button, Group, Modal, Select, Stack, Switch, Text, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconDeviceFloppy, IconFileImport, IconPencil, IconPlus, IconTrash } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { ENTITY_TYPES, type EntityCustomFieldValue, type EntityDto } from '../../api/types';
import { PageHeader } from '../../components/PageHeader';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { importLink, routes } from '../../lib/routes';
import { useSession } from '../auth/session';
import { keys } from '../../lib/queryKeys';
import { customFieldValues, EntityCustomFieldsSection } from './EntityCustomFieldsSection';

export function EntitiesPage() {
  const { t } = useTranslation();
  const { isAdmin } = useSession();
  const queryClient = useQueryClient();

  const [showInactive, setShowInactive] = useState(false);
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<EntityDto | null>(null);
  const [creating, setCreating] = useState(false);

  const entities = useQuery({
    queryKey: keys.entities.list({ search, showInactive }),
    queryFn: () => api.entities.list({ search: search || undefined, includeInactive: showInactive }),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: keys.entities.all });

  const deactivate = useMutation({
    mutationFn: (id: string) => api.entities.deactivate(id),
    onSuccess: async () => {
      await invalidate();
      notifySaved();
    },
    onError: notifyError,
  });

  return (
    <>
      <PageHeader
        title={t('entities.title')}
        description={t('entities.guardrail')}
        actions={
          isAdmin && (
            <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>
              {t('entities.new')}
            </Button>
          )
        }
      />

      <Group mb="sm" gap="sm">
        <TextInput
          placeholder={t('common.search')}
          value={search}
          onChange={(event) => setSearch(event.currentTarget.value)}
          w={240}
        />
        <Switch
          label={t('entities.showInactive')}
          checked={showInactive}
          onChange={(event) => setShowInactive(event.currentTarget.checked)}
        />
      </Group>

      <DataTable
        highlightOnHover
        withTableBorder
        minHeight={200}
        fetching={entities.isFetching}
        records={entities.data?.items ?? []}
        idAccessor="id"
        /*
         * The empty state is the on-ramp: somebody arriving here for the first time has their client list in a
         * spreadsheet, and the fastest way to a useful Everdue is to point at it.
         */
        emptyState={
          <Stack align="center" gap="xs" py="lg">
            <Text size="sm" c="dimmed">
              {t('entities.empty')}
            </Text>
            {isAdmin && (
              <Button
                component={Link}
                to={importLink('entities')}
                size="xs"
                variant="light"
                leftSection={<IconFileImport size={14} />}
              >
                {t('imports.emptyStateCta')}
              </Button>
            )}
          </Stack>
        }
        columns={[
          {
            accessor: 'name',
            title: t('common.name'),
            render: (entity) => (
              <Anchor component={Link} to={routes.entityTimeline(entity.id)} size="sm">
                {entity.name}
              </Anchor>
            ),
          },
          { accessor: 'type', title: t('common.type'), render: (entity) => t(`entityType.${entity.type}`) },
          {
            accessor: 'active',
            title: t('common.status'),
            render: (entity) => (entity.active ? t('common.active') : t('common.inactive')),
          },
          {
            accessor: 'actions',
            title: '',
            textAlign: 'right',
            render: (entity) =>
              isAdmin && (
                <Group gap={4} justify="flex-end">
                  <ActionIcon variant="subtle" aria-label={t('common.edit')} onClick={() => setEditing(entity)}>
                    <IconPencil size={16} />
                  </ActionIcon>
                  {entity.active && (
                    <ActionIcon
                      variant="subtle"
                      color="red"
                      aria-label={t('common.deactivate')}
                      onClick={() => {
                        if (window.confirm(t('entities.deactivateConfirm'))) {
                          deactivate.mutate(entity.id);
                        }
                      }}
                    >
                      <IconTrash size={16} />
                    </ActionIcon>
                  )}
                </Group>
              ),
          },
        ]}
      />

      <EntityModal
        entity={editing}
        opened={creating || editing !== null}
        onClose={() => {
          setCreating(false);
          setEditing(null);
        }}
        onSaved={invalidate}
      />
    </>
  );
}

function EntityModal({
  entity,
  opened,
  onClose,
  onSaved,
}: {
  entity: EntityDto | null;
  opened: boolean;
  onClose: () => void;
  onSaved: () => Promise<unknown>;
}) {
  const { t } = useTranslation();

  const form = useForm({
    initialValues: { name: entity?.name ?? '', type: entity?.type ?? 'Customer', active: entity?.active ?? true },
    validate: { name: (value) => (value.trim().length === 0 ? t('common.required') : null) },
  });

  // Definitions are per entity type, so switching the type in the form switches which fields apply.
  const definitions = useQuery({
    queryKey: keys.entityFields.all,
    queryFn: () => api.entityFields.list(),
    enabled: opened,
  });

  const [values, setValues] = useState<Record<string, string>>(() => customFieldValues(entity?.customFields));

  const fields: EntityCustomFieldValue[] = (definitions.data ?? [])
    .filter((definition) => definition.entityType === form.values.type)
    .map((definition) => ({
      definitionId: definition.id,
      name: definition.name,
      fieldType: definition.fieldType,
      options: definition.options,
      position: definition.position,
      value: values[definition.id] ?? null,
    }));

  const save = useMutation({
    mutationFn: (submitted: typeof form.values) =>
      entity
        ? api.entities.update(entity.id, {
            name: submitted.name.trim(),
            type: submitted.type,
            active: submitted.active,
            customFields: values,
          })
        : api.entities.create({ name: submitted.name.trim(), type: submitted.type, customFields: values }),
    onSuccess: async () => {
      await onSaved();
      notifySaved();
      onClose();
    },
    onError: notifyError,
  });

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={entity ? t('entities.edit') : t('entities.new')}
      centered
      // Remount on target change so the form always starts from the row being edited.
      key={entity?.id ?? 'new'}
    >
      <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
        <Stack>
          <TextInput label={t('common.name')} data-autofocus {...form.getInputProps('name')} />
          <Select
            label={t('common.type')}
            data={ENTITY_TYPES.map((type) => ({ value: type, label: t(`entityType.${type}`) }))}
            allowDeselect={false}
            {...form.getInputProps('type')}
          />

          <EntityCustomFieldsSection fields={fields} values={values} onChange={setValues} />

          {entity && (
            <Switch
              label={t('common.active')}
              checked={form.values.active}
              onChange={(event) => form.setFieldValue('active', event.currentTarget.checked)}
            />
          )}
          <Group justify="flex-end">
            <Button variant="default" onClick={onClose}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" loading={save.isPending} leftSection={<IconDeviceFloppy size={16} />}>
              {t('common.save')}
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
