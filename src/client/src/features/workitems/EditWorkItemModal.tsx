import { Button, Group, Modal, Stack, Textarea, TextInput } from '@mantine/core';
import { IconDeviceFloppy } from '@tabler/icons-react';
import { useForm } from '@mantine/form';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { WorkItem } from '../../api/types';
import { DepartmentPicker, EntityPicker, UserPicker } from '../../components/pickers';
import { api } from '../../lib/api';
import { notifyError, notifySaved } from '../../lib/notify';
import { keys } from '../../lib/queryKeys';

/**
 * Descriptive fields only — the same five the API accepts. Dates move through /reschedule and
 * status through the transition actions, so there is no way to edit around the rules.
 */
export function EditWorkItemModal({
  item,
  opened,
  onClose,
}: {
  item: WorkItem;
  opened: boolean;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const form = useForm({
    initialValues: {
      title: item.title,
      description: item.description ?? '',
      ownerUserId: item.ownerUserId as string | null,
      entityId: item.entityId as string | null,
      departmentId: item.departmentId as string | null,
    },
    validate: {
      title: (value) => (value.trim().length === 0 ? t('common.required') : null),
      ownerUserId: (value) => (value ? null : t('common.required')),
    },
  });

  const save = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.workItems.update(item.id, {
        title: values.title.trim(),
        description: values.description.trim() === '' ? null : values.description.trim(),
        ownerUserId: values.ownerUserId!,
        entityId: values.entityId,
        departmentId: values.departmentId,
      }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: keys.workItems.all }),
        queryClient.invalidateQueries({ queryKey: keys.workItems.one(item.id) }),
        queryClient.invalidateQueries({ queryKey: keys.reports.all }),
      ]);
      notifySaved();
      onClose();
    },
    onError: notifyError,
  });

  return (
    <Modal opened={opened} onClose={onClose} title={t('workItem.editTitle')} centered size="lg" key={item.id}>
      <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
        <Stack>
          <TextInput label={t('workItem.title')} data-autofocus {...form.getInputProps('title')} />
          <Textarea label={t('workItem.description')} autosize minRows={2} {...form.getInputProps('description')} />

          <UserPicker
            required
            value={form.values.ownerUserId}
            onChange={(value) => form.setFieldValue('ownerUserId', value)}
            error={form.errors.ownerUserId as string | undefined}
          />

          <Group grow align="flex-start">
            <EntityPicker value={form.values.entityId} onChange={(value) => form.setFieldValue('entityId', value)} />
            <DepartmentPicker
              value={form.values.departmentId}
              onChange={(value) => form.setFieldValue('departmentId', value)}
            />
          </Group>

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
