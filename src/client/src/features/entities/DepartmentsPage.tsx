import { ActionIcon, Button, Group, Modal, Stack, Switch, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconDeviceFloppy, IconPencil, IconPlus, IconTrash } from '@tabler/icons-react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { DepartmentDto } from '../../api/types';
import { PageHeader } from '../../components/PageHeader';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';

export function DepartmentsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<DepartmentDto | null>(null);
  const [creating, setCreating] = useState(false);

  const departments = useQuery({
    queryKey: keys.departments.list({ includeInactive: true }),
    queryFn: () => api.departments.list({ includeInactive: true }),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: keys.departments.all });

  const deactivate = useMutation({
    mutationFn: (id: string) => api.departments.deactivate(id),
    onSuccess: async () => {
      await invalidate();
      notifySaved();
    },
    onError: notifyError,
  });

  return (
    <>
      <PageHeader
        title={t('departments.title')}
        description={t('departments.hint')}
        actions={
          <Button leftSection={<IconPlus size={16} />} onClick={() => setCreating(true)}>
            {t('departments.new')}
          </Button>
        }
      />

      <DataTable
        highlightOnHover
        withTableBorder
        minHeight={200}
        fetching={departments.isFetching}
        records={departments.data?.items ?? []}
        idAccessor="id"
        noRecordsText={t('departments.empty')}
        columns={[
          { accessor: 'name', title: t('common.name') },
          {
            accessor: 'active',
            title: t('common.status'),
            render: (row) => (row.active ? t('common.active') : t('common.inactive')),
          },
          {
            accessor: 'actions',
            title: '',
            textAlign: 'right',
            render: (row) => (
              <Group gap={4} justify="flex-end">
                <ActionIcon variant="subtle" aria-label={t('common.edit')} onClick={() => setEditing(row)}>
                  <IconPencil size={16} />
                </ActionIcon>
                {row.active && (
                  <ActionIcon
                    variant="subtle"
                    color="red"
                    aria-label={t('common.deactivate')}
                    onClick={() => deactivate.mutate(row.id)}
                  >
                    <IconTrash size={16} />
                  </ActionIcon>
                )}
              </Group>
            ),
          },
        ]}
      />

      <DepartmentModal
        department={editing}
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

function DepartmentModal({
  department,
  opened,
  onClose,
  onSaved,
}: {
  department: DepartmentDto | null;
  opened: boolean;
  onClose: () => void;
  onSaved: () => Promise<unknown>;
}) {
  const { t } = useTranslation();

  const form = useForm({
    initialValues: { name: department?.name ?? '', active: department?.active ?? true },
    validate: { name: (value) => (value.trim().length === 0 ? t('common.required') : null) },
  });

  const save = useMutation({
    mutationFn: (values: typeof form.values) =>
      department
        ? api.departments.update(department.id, { name: values.name.trim(), active: values.active })
        : api.departments.create({ name: values.name.trim() }),
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
      title={department ? t('departments.edit') : t('departments.new')}
      centered
      key={department?.id ?? 'new'}
    >
      <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
        <Stack>
          <TextInput label={t('common.name')} data-autofocus {...form.getInputProps('name')} />
          {department && (
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
